using System;
using System.Drawing;
using System.Media;
using System.Windows;
using System.Windows.Forms;
using TINS.Analysis;
using TINS.Terminal.UI;
using TINS.Filtering;
using TINS.Visual;
using TINS.Visual.Plottables;

namespace TINS.Terminal.Display
{
	/// <summary>
	/// Visualization for real time channel spectra.
	/// </summary>
	public partial class RealTimeSpectrumDisplay : Form
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		public RealTimeSpectrumDisplay()
		{
			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);

			// create chart
			Chart = new()
			{
				Anchor			= AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
				Size			= new System.Drawing.Size(465, 220),
				//Viewport		= new Rectangle(0, 0, 520, 300),
				Location		= new System.Drawing.Point(10, 10),
				AutoLegend		= false,
				Interactions	= Interaction.RangeDrag | Interaction.RangeZoom,
				Name			= "chart",
				BackgroundBrush = SystemBrushes.ControlDarkDark,
				AxisPen			= Pens.White
			};
			Chart.SetAxisLabelFonts(new Font("Calibri", 12), Color.White);
			Chart.SetAxisTickLabelFonts(new Font("Calibri", 10), Color.White);
			Chart.DefaultXAxis.Label = "Frequency [Hz]";
			Chart.DefaultYAxis.Label = "Power (\u03bcV\u00b2/Hz)";

			// create graph
			Graph = new Graph(Chart.DefaultXAxis, Chart.DefaultYAxis)
			{
				Pen = new Pen(Color.Gold, 2)
			};
			
			Controls.Add(Chart);
			InitializeComponent();
		}

		/// <summary>
		/// Configure the real time analysis window.
		/// </summary>
		/// <param name="bufferSize"></param>
		/// <param name="dftSize"></param>
		public void Initialize(int sourceIndex, int dftSize, float samplingRate)
		{
			if (InvokeRequired)
			{
				BeginInvoke(new Action<int, int, float>(Initialize), sourceIndex, dftSize, samplingRate);
				return;
			}

			// set the source index
			SourceChannelIndex = Math.Abs(sourceIndex);

			// init the dft
			_ft ??= new FourierSpectrumAnalyzer();
			_ft.Initialize(new(
				samplingRate:		samplingRate, 
				windowDuration:		0.25f,
				dftSize:			2048,
				timeStep:			0.01f,
				windowType:			WindowType.Blackman));

			// init the graph plot
			_spectrum2d.FrequencyRange = (0, samplingRate / 2);
			using var xAxis = Numerics.Linspace(_spectrum2d.FrequencyRange, _ft.Settings.SpectrumSize);
			using var yAxis = new Vector<float>(_ft.Settings.SpectrumSize);
			Graph.SetData(xAxis, yAxis, true);
		}

		/// <summary>
		/// Provide new data to the analyzer.
		/// </summary>
		/// <param name="lfp">LFP display accumulator.</param>
		public void Update(ContinuousDisplayAccumulator lfp)
		{
			if (_ft is null) return;
			if (lfp is null || lfp.ChannelCount == 0 ||
				!Numerics.IsClamped(SourceChannelIndex, (0, lfp.ChannelCount - 1)))
				return;

			if (!_haveData)
			{
				// get the data and enqueue processing (to avoid blocking the data threads)
				_data.Resize(lfp.BufferSize);
				lfp.GetBuffer(SourceChannelIndex).CopyTo(_data.GetSpan());
				_haveData = true;
			}

			if (InvokeRequired)
			{
				BeginInvoke(new Action<ContinuousDisplayAccumulator>(Update), lfp);
				return;
			}

			lock (_ft)
			{
				_spectrum2d.Reset();
				_ft.Analyze(_data, _spectrum2d);
				_spectrum1d.Assign(_spectrum2d.RowWise().Mean());

				var gData = Graph.Data;
				if (ckbUseLog.Checked)
				{
					for (int i = 0; i < _ft.Settings.SpectrumSize; ++i)
						gData[i].value = MathF.Log10(_spectrum1d[i] + 0.0001f);
				}
				else
				{
					for (int i = 0; i < _ft.Settings.SpectrumSize; ++i)
						gData[i].value = _spectrum1d[i];
				}
			
				_haveData = false;
				Chart.Replot();
			}

		}

		/// <summary>
		/// The chart element.
		/// </summary>
		public Chart Chart { get; init; }

		/// <summary>
		/// The spectrum graph.
		/// </summary>
		public Graph Graph { get; init; }

		/// <summary>
		/// Get or set the source channel index.
		/// </summary>
		public int SourceChannelIndex { get; set; }

		protected FourierSpectrumAnalyzer	_ft;
		protected Vector<float>				_data		= new();
		protected Spectrum2D				_spectrum2d	= new();
		protected Vector<float>				_spectrum1d	= new(orientation: VectorOrientation.Column);
		protected bool						_haveData	= false;

	}
}
