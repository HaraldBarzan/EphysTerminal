using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using TINS.Ephys.Analysis.TimeFrequency;
using TINS.Visual;
using TINS.Visual.Axes;
using TINS.Visual.Drawing;
using TINS.Visual.Layout;
using TINS.Visual.Plottables;

namespace TINS.Terminal.Display.Ephys
{
	/// <summary>
	/// Interaction logic for RealTimeTFRDisplay.xaml
	/// </summary>
	public partial class RealTimeTFRDisplay : Window
	{
		/// <summary>
		/// 
		/// </summary>
		public RealTimeTFRDisplay(SuperletAnalyzer analyzer, string initialSource)
		{
			InitializeComponent();

			Chart = new Chart
			{
				//Anchor			= AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
				Interactions	= Interaction.None,
				SelectionMode	= Visual.SelectionMode.None,
				Name			= "chart",
				AutoLegend		= false,
				BackgroundBrush = new SolidBrush(System.Drawing.Color.FromArgb(0x2d2d30))
			};
			Chart.Size		= new System.Drawing.Size((int)chartHost.Width, (int)chartHost.Height);
			Chart.Viewport	= new System.Drawing.Rectangle(0, 0, Chart.Width, Chart.Height);
			chartHost.Child = Chart;

			_analyzer	= analyzer;
			_source		= initialSource;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="updatePeriod"></param>
		public void UpdateData()
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(UpdateData);
			else
			{
				// don't do anything if the viewer is not visible
				if (Visibility != Visibility.Visible)
					return;

				var data = _analyzer.ResultRing[0];
				lock (data)
				{
					if (data.Results.Size < 1)
						return;

					// get spectrum
					if (_source == "*")
					{
						// get average spectrum
						using var spectrum = data.Results[0].Clone() as Spectrum2D;
						for (int i = 1; i < data.Results.Size; ++i)
							spectrum.Accumulate(data.Results[i]);
						spectrum.Scale(1f / data.Results.Size);

						UpdateChartData(spectrum);
					}
					else
					{
						for (int i = 0; i < data.ChannelLabels.Size; ++i)
						{
							if (data.ChannelLabels[i] == _source)
							{
								UpdateChartData(data.Results[i]);
								break;
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Initialize the chart for a superlet analyzer.
		/// </summary>
		/// <param name="chart">The chart to initialize.</param>
		/// <param name="analyzer">The superlet analyzer.</param>
		/// <param name="source">The TFR source.</param>
		protected void UpdateChartData(Spectrum2D spectrum)
		{
			if (Chart.PlottableCount > 0 &&
				Chart.GetPlottable() is ColorMap colorMap)
			{
				// fill in the color map data
				colorMap.Data.Set(
					data:		spectrum,
					keyRange:	spectrum.TimeRange,
					valueRange: spectrum.FrequencyRange);
				colorMap.RescaleDataRange(true);
				colorMap.ColorBar?.RescaleDataRange();

				//if (powerRange.HasValue && powerRange.Value.Size() > 0)
				//	colorMap.ColorBar.DataRange = powerRange.Value;

				Chart.RescaleAxes();
				if (Visibility == Visibility.Visible)
					Chart.Replot();
			}
			else
			{
				Chart.ClearPlottables();

				// create a colorbar
				var colorBar = new ColorBar(Chart)
				{
					BarWidth = 15,
					AxisType = AxisType.Right,
					Gradient = new ColorGradient(GradientPreset.Jet, 350)
				};

				// create the color map
				colorMap = new ColorMap(Chart.DefaultXAxis, Chart.DefaultYAxis);
				colorMap.ColorBar = colorBar;

				// color bar (if needed) - create a margin group and add the color bar to it
				var marginGroup = new MarginGroup(Chart);
				var axisRect = colorMap.AxisRect;
				axisRect.SetupFullAxesBox(true);
				Chart.MainLayout.AddElement(0, 1, colorBar);
				marginGroup.Include(MarginSide.Vertical, colorBar);
				marginGroup.Include(MarginSide.Vertical, colorMap.AxisRect);
				
				colorBar.Label = "Power";

				// set labels
				Chart.DefaultXAxis.LabelColor		= System.Drawing.Color.White;
				Chart.DefaultXAxis.TickLabelColor	= System.Drawing.Color.White;
				Chart.DefaultXAxis.TickPen			= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultXAxis.SubtickPen		= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultXAxis.BasePen			= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultXAxis.Label			= "Time [s]";

				Chart.DefaultYAxis.LabelColor		= System.Drawing.Color.White;
				Chart.DefaultYAxis.TickLabelColor	= System.Drawing.Color.White;
				Chart.DefaultYAxis.TickPen			= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultYAxis.SubtickPen		= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultYAxis.BasePen			= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultYAxis.Label			= "Frequency [Hz]";

				// set labels
				Chart.DefaultX2Axis.LabelColor		= System.Drawing.Color.White;
				Chart.DefaultX2Axis.TickLabelColor	= System.Drawing.Color.White;
				Chart.DefaultX2Axis.TickPen			= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultX2Axis.SubtickPen		= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultX2Axis.BasePen			= new System.Drawing.Pen(System.Drawing.Color.White);

				Chart.DefaultY2Axis.LabelColor		= System.Drawing.Color.White;
				Chart.DefaultY2Axis.TickLabelColor	= System.Drawing.Color.White;
				Chart.DefaultY2Axis.TickPen			= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultY2Axis.SubtickPen		= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultY2Axis.BasePen			= new System.Drawing.Pen(System.Drawing.Color.White);
				Chart.DefaultY2Axis.Label			= "Frequency [Hz]";

				Chart.SetAxisLabelFonts(new Font("Calibri", 12));
				Chart.SetAxisTickLabelFonts(new Font("Calibri", 10));
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public Chart Chart { get; protected set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
			=> Visibility = Visibility.Hidden;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void chartHost_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (ReferenceEquals(sender, chartHost))
				Chart.Size = new System.Drawing.Size((int)e.NewSize.Width, (int)e.NewSize.Height);
		}


		protected SuperletAnalyzer	_analyzer;
		protected string			_source;
    }
}
