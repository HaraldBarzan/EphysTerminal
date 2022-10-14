using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TINS.Terminal.Settings.UI;
using TINS.Terminal.UI;
using TINS.Visual.Axes;

namespace TINS.Terminal.Display
{
	/// <summary>
	/// Interaction logic for EEGDisplay.xaml
	/// </summary>
	public partial class EEGDisplay 
		: UserControl
		, IChannelDisplay
	{
		/// <summary>
		/// Offset of tick labels from their respective axes.
		/// </summary>
		public static float TickLabelOffset { get; set; } = 2;

		/// <summary>
		/// Describes a mapping of channels to a source buffer.
		/// </summary>
		public struct Mapping
		{
			/// <summary>
			/// 
			/// </summary>
			public int SourceIndex { get; init; }

			/// <summary>
			/// 
			/// </summary>
			public string Label { get; init; }

			/// <summary>
			/// 
			/// </summary>
			public bool IsInvalid => SourceIndex < 0 || string.IsNullOrEmpty(Label);

			/// <summary>
			/// 
			/// </summary>
			public static Mapping Invalid { get; } = new() { SourceIndex = -1, Label = string.Empty };

			/// <summary>
			/// 
			/// </summary>
			/// <returns></returns>
			public override string ToString() => IsInvalid ? "Invalid" : $"{Label} (idx: {SourceIndex})";
		}

		/// <summary>
		/// Display type.
		/// </summary>
		public DisplayType DisplayType => DisplayType.EEG;

		/// <summary>
		/// Default constructor.
		/// </summary>
		public EEGDisplay()
		{
			InitializeComponent();

			_graphPaints.PushBack(new SKPaint() { Color = new(0xff3986d3u), Style = SKPaintStyle.Stroke });
			_graphPaints.PushBack(new SKPaint() { Color = new(0xff2fa3aau), Style = SKPaintStyle.Stroke });
			_graphPaints.PushBack(new SKPaint() { Color = new(0xffbf90c9u), Style = SKPaintStyle.Stroke });
		}


		/// <summary>
		/// Destructor.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{

			}
			_disposed = true;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="yRange"></param>
		public virtual void SetYAxisRange((float Lower, float Upper) yRange)
			=> _yRange = yRange;

		/// <summary>
		/// Update the data that is to be rendered.
		/// </summary>
		/// <param name="eegAcc">The EEG data accumulator.</param>
		public void Update(ContinuousDisplayAccumulator eegAcc)
		{
			if (eegAcc is null || eegAcc.BufferSize < 2)
				return;

			lock (eegAcc)
			{
				for (int iCh = 0; iCh < _channelMapping.Size; ++iCh)
				{
					if (_channelMapping[iCh].IsInvalid)
						continue;

					int sourceChIndex = _channelMapping[iCh].SourceIndex;

					// create graph
					_graphs[iCh]	??= new SKPath();
					var lineGraph	= _graphs[iCh];
					var lineData	= eegAcc.GetBuffer(sourceChIndex);

					// rewind the graph and start from scratch
					lineGraph.Rewind();

					// push data to the graph
					lineGraph.MoveTo(0, -lineData[0]);
					for (int i = 1; i < lineData.Length; ++i)
						lineGraph.LineTo(i, -lineData[i]);
				}
			}

			data.InvalidateVisual();
		}

		/// <summary>
		/// Initialize the display.
		/// </summary>
		/// <param name="terminal">The terminal.</param>
		public void InitializeChannelDisplay(EphysTerminal terminal)
		{
			// set the new terminal
			EphysTerminal = terminal;
			var settings = EphysTerminal.TerminalSettings.UI as UISettingsEEG;
			if (settings is null)
				throw new Exception("");

			// create channel mapping vector
			var sourceChannels	= settings.DisplayChannels;
			_channelMapping	.Resize(settings.DisplayChannels.Size);
			_yPositions		.Resize(settings.DisplayChannels.Size);
			_graphs			.Resize(settings.DisplayChannels.Size);
			for (int i = 0; i < sourceChannels.Size; ++i)
			{
				int sourceIndex;
				if (!string.IsNullOrEmpty(sourceChannels[i]) &&
					(sourceIndex = terminal.Settings.Input.ChannelLabels.IndexOf(sourceChannels[i])) >= 0)
				{
					_channelMapping[i] = new() { Label = sourceChannels[i], SourceIndex = sourceIndex };
				}
			}

			_xRange = (-settings.EEGUpdatePeriod, 0);
			_yRange = (-5, 5);

			// setup axes
			//cmbEEGYRange.Text = settings.EEGYRange.ToString();
			UpdateDisplay();
		}

		/// <summary>
		/// Clear any data from the display.
		/// </summary>
		public void ClearDisplay()
		{
			ResetData();
			UpdateDisplay();
		}

		/// <summary>
		/// The source stream.
		/// </summary>
		public EphysTerminal EphysTerminal { get; private set; }

		/// <summary>
		/// Forcibly redraw the visuals.
		/// </summary>
		public virtual void UpdateDisplay()
		{
			grid.InvalidateVisual();
			data.InvalidateVisual();
			axes.InvalidateVisual();
		}

		/// <summary>
		/// 
		/// </summary>
		protected virtual void ResetData()
		{
			foreach (var g in _graphs)
				g?.Rewind();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		protected virtual void RenderData(SKCanvas c)
		{
			if (!_canDrawData) return;
			
			float yScale = _channelHeight / _yRange.Size();
			
			// reset the affine transformation matrix and save its data
			int outerState = c.Save();
			c.Clear();
			c.ClipRect(_axisRect);
			c.Translate(_axisRect.Left, _axisRect.Top);
			
			for (int iCh = 0; iCh < _graphs.Size; ++iCh)
			{
				if (_graphs[iCh] is null || _graphs[iCh].PointCount < 2)
					continue;
			
				float xScale = _axisRect.Width / _graphs[iCh].PointCount;
			
				// map to channel coordinates
				var state = c.Save();
				c.Translate(0, _yPositions[iCh]);
				c.Scale(xScale, yScale);
			
				// draw the graph
				c.DrawPath(_graphs[iCh], _graphPaints[iCh % _graphPaints.Size]);
			
				// restore state
				c.RestoreToCount(state);
			}
			
			// restore state
			c.RestoreToCount(outerState);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		protected virtual void RenderAxes(SKCanvas c)
		{
			_canDrawData = false;

			// check xy ranges
			if (!_xRange.IsValid() || !_yRange.IsValid())
			{
				RenderText(c, "Invalid X or Y range.");
				return;
			}

			// fonts and paints
			using var tickFont	= new SKFont()			{ Size = 12, Typeface = SKTypeface.FromFamilyName("Arial") };
			using var tickPaint = new SKPaint(tickFont) { Color = new(214, 214, 214), Style = SKPaintStyle.Fill };
			using var linePaint = new SKPaint()			{ Color = new(214, 214, 214), Style = SKPaintStyle.Stroke };

			// measure tick widths
			using var yTickLabelWidths = new Vector<float>(_channelMapping.Size);
			for (int i = 0; i < _channelMapping.Size; ++i)
				yTickLabelWidths[i] = tickPaint.MeasureText(_channelMapping[i].Label);
			float maxYTickLabelWidth = yTickLabelWidths.Max();

			// precompute sizes
			var availableArea	= c.DeviceClipBounds;
			float tickOffset	= TickLabelOffset;
			_axisRect = SKRect.Create(	tickOffset + maxYTickLabelWidth + _margin, _margin,
										availableArea.Width - tickOffset - 2 * _margin - maxYTickLabelWidth,
										availableArea.Height - tickOffset - 2 * _margin - tickFont.Spacing);

			// compute Y-axis positions
			_channelHeight = _axisRect.Height / _channelMapping.Size;
			for (int i = 0; i < _channelMapping.Size; ++i)
				_yPositions[i] = (i + 0.5f) * _channelHeight;

			// begin draw
			c.Clear();

			// draw tick labels
			int maxDrawableTicks = Numerics.Floor(_axisRect.Height / tickFont.Spacing);
			if (maxDrawableTicks < _channelMapping.Size)
			{
				float tickStep = _channelMapping.Size / (float)maxDrawableTicks;
			
				// draw with skips
				for (int i = 0; i < maxDrawableTicks; ++i)
				{
					int iCh = Numerics.Round(i * tickStep);
			
					if (_channelMapping[iCh].IsInvalid)
						continue;
			
					float xTickPos = _axisRect.Left - TickLabelOffset - yTickLabelWidths[iCh];
					float yTickPos = _yPositions[iCh] + tickFont.Spacing / 2;
			
					c.DrawText(_channelMapping[iCh].Label, xTickPos, yTickPos, tickPaint);
				}
			}
			else
			{
				// draw without skips
				for (int iCh = 0; iCh < _channelMapping.Size; ++iCh)
				{
					if (_channelMapping[iCh].IsInvalid)
						continue;

					float xTickPos = _axisRect.Left - TickLabelOffset - yTickLabelWidths[iCh];
					//float yTickPos = _yPositions[iCh] + (tickFont.Metrics.Bottom - tickFont.Metrics.Top) / 2;
					float yTickPos = _yPositions[iCh] + tickFont.Spacing / 2;

					c.DrawText(_channelMapping[iCh].Label, xTickPos, yTickPos, tickPaint);
				}
			}
			
			// draw the main rectangle
			c.DrawRect(_axisRect, linePaint);

			_canDrawData = true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		protected virtual void RenderGridlines(SKCanvas c)
		{
			var state = c.Save();
			//c.ClipRect(_axisRect);
			c.Translate(_axisRect.Left, _axisRect.Top);

			using var gridLinePaint = new SKPaint()
			{
				Color = new(0xff303030),
				Style = SKPaintStyle.Stroke
			};

			c.Clear();
			foreach (var pos in _yPositions)
				c.DrawLine(0, pos, _axisRect.Width, pos, gridLinePaint);

			c.RestoreToCount(state);
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="text"></param>
		protected virtual void RenderText(SKCanvas c, string text)
		{
			text ??= "<no text>";

			using var paint = new SKPaint()
			{
				Color		= new SKColor(255, 255, 255),
				Style		= SKPaintStyle.Fill,
				TextAlign	= SKTextAlign.Center
			};
			using var font = new SKFont()
			{
				Size = 28,
				Typeface = SKTypeface.FromFamilyName("Arial")
			};

			c.Clear();
			c.DrawText(text, (float)Width / 2, (float)Height / 2, font, paint);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void grid_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
			=> RenderGridlines(e.Surface.Canvas);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void axes_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
			=> RenderAxes(e.Surface.Canvas);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void data_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
			=> RenderData(e.Surface.Canvas);







		protected (float Lower, float Upper)	_xRange			= default;
		protected (float Lower, float Upper)	_yRange			= default;
		protected float							_channelHeight	= default;
		protected float							_margin			= 5;
		protected bool							_canDrawData	= false;
		protected SKRect						_axisRect		= default;
		protected Vector<SKPath>				_graphs			= new();
		protected Vector<Mapping>				_channelMapping	= new();
		protected Vector<float>					_yPositions		= new();
		protected Vector<SKPaint>				_graphPaints	= new();
		private bool							_disposed		= false;


		/// <summary>
		/// A list of supported y ranges.
		/// </summary>
		protected static Vector<float> SupportedYRanges = new()
		{
			0.1f,   0.2f,   0.5f,
			1,      2,      5,
			10,     20,     50,
			100,    200,    500,
			1000,   2000,   5000
		};
	}
}
