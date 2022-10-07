using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Windows;
using System.Windows.Controls;

namespace TINS.Terminal.Display.Ephys
{
	/// <summary>
	/// Interaction logic for DataDisplay.xaml
	/// </summary>
	public partial class DataDisplay 
		: UserControl
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
		/// Default constructor.
		/// </summary>
		public DataDisplay()
		{
			InitializeComponent();

			using var lbl = new Matrix<Mapping>(8, 4);
		
			for (int i = 0; i < lbl.Size; ++i)
				lbl[i] = new() { SourceIndex = i, Label = "El_" + (i < 10 ? $"0{i}" : i.ToString()) };

			Setup(lbl, (0, 1000), (-1, 1));
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="channelMapping"></param>
		/// <param name="xRange"></param>
		/// <param name="yRange"></param>
		/// <param name="margin"></param>
		public void Setup(
			Matrix<Mapping>				channelMapping, 
			(float Lower, float Upper)	xRange, 
			(float Lower, float Upper)	yRange, 
			float						margin	= 5)
		{
			SetChannelConfiguration(channelMapping);
			SetAxisRanges(xRange, yRange, margin);
			UpdateDisplay();
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="channelMapping">Describes how the channels are mapped to the source buffer.</param>
		public virtual void SetChannelConfiguration(Matrix<Mapping> channelMapping)
		{
			if (channelMapping is null)
				throw new Exception("Invalid channel configuration.");

			_channelMapping.Assign(channelMapping);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xRange"></param>
		/// <param name="yRange"></param>
		/// <param name="margin"></param>
		public virtual void SetAxisRanges((float Lower, float Upper) xRange, (float Lower, float Upper) yRange, float margin = 5)
		{
			_xRange = xRange;
			_yRange = yRange;
			_margin = margin;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xRange"></param>
		/// <param name="yRange"></param>
		/// <param name="margin"></param>
		public virtual void SetYAxisRange((float Lower, float Upper) yRange)
			=> _yRange = yRange;

		/// <summary>
		/// Forcibly redraw the visuals.
		/// </summary>
		public virtual void UpdateDisplay()
		{
			over.InvalidateVisual();
			data.InvalidateVisual();
			axes.InvalidateVisual();
		}

		/// <summary>
		/// 
		/// </summary>
		public void Clear()
		{
			ResetData();
			UpdateDisplay();
		}

		/// <summary>
		/// Reset the displayed data.
		/// </summary>
		protected virtual void ResetData()
		{
		}

		/// <summary>
		/// Render data to the screen.
		/// </summary>
		/// <param name="c"></param>
		protected virtual void RenderData(SKCanvas c)
		{
		}

		/// <summary>
		/// Render data to the screen.
		/// </summary>
		/// <param name="c"></param>
		protected virtual void RenderOverlay(SKCanvas c)
		{
		}

		/// <summary>
		/// Render the axes to the screen.
		/// </summary>
		protected virtual void RenderAxes(SKCanvas c)
		{
			_canDrawData = false;

			// check config
			if (_channelMapping.Size == 0)
			{
				RenderText(c, "Invalid config matrix.");
				return;
			}

			// check xy ranges
			if (!_xRange.IsValid() || !_yRange.IsValid())
			{
				RenderText(c, "Invalid X or Y range.");
				return;
			}


			// fonts and paints
			using var tickFont	= new SKFont()			{ Size = 10, Typeface = SKTypeface.FromFamilyName("Arial") };
			using var tickPaint = new SKPaint(tickFont) { Color = new(214, 214, 214), Style = SKPaintStyle.Fill };
			using var linePaint = new SKPaint()			{ Color = new(214, 214, 214), Style = SKPaintStyle.Stroke };
			
			// text and text size
			var strXRange		= (Lower: _xRange.Lower.ToString(), Upper: _xRange.Upper.ToString());
			var strYRange		= (Lower: _yRange.Lower.ToString(), Upper: _yRange.Upper.ToString());
			var xTickWidths		= (Lower: tickPaint.MeasureText(strXRange.Lower), Upper: tickPaint.MeasureText(strXRange.Upper));
			var yTickWidths		= (Lower: tickPaint.MeasureText(strYRange.Lower), Upper: tickPaint.MeasureText(strYRange.Upper));
			float maxYTickWidth = Math.Max(yTickWidths.Lower, yTickWidths.Upper);

			// precompute sizes
			var availableArea	= c.DeviceClipBounds;
			float tickOffset	= TickLabelOffset;
			_panelRect			= SKRect.Create(0, 0, availableArea.Width / _channelMapping.Cols, availableArea.Height / _channelMapping.Rows);
			_axisRect			= SKRect.Create(tickOffset + maxYTickWidth + _margin, _margin, 
												_panelRect.Width - tickOffset - 2 * _margin - maxYTickWidth,
												_panelRect.Height - tickOffset - 2 * _margin - tickFont.Size);

			// begin draw
			c.Clear();
			for (int iRow = 0; iRow < _channelMapping.Rows; ++iRow)
			{
				for (int iCol = 0; iCol < _channelMapping.Cols; ++iCol)
				{
					if (_channelMapping[iRow, iCol].IsInvalid)
						continue;

					// go to the current panel
					c.ResetMatrix();
					c.Translate(iCol * (float)_panelRect.Width, iRow * (float)_panelRect.Height);

					// draw labels
					c.DrawText(strYRange.Upper, _margin, _axisRect.Top + tickFont.Size, tickPaint);
					c.DrawText(strYRange.Lower, _margin, _axisRect.Bottom, tickPaint);
					c.DrawText(strXRange.Lower, _axisRect.Left, _axisRect.Bottom + tickOffset + tickFont.Size, tickPaint);
					c.DrawText(strXRange.Upper, _axisRect.Right - xTickWidths.Upper, _axisRect.Bottom + tickOffset + tickFont.Size, tickPaint);
					c.DrawText(_channelMapping[iRow, iCol].Label, _axisRect.Right - tickPaint.MeasureText(_channelMapping[iRow, iCol].Label), _axisRect.Top + tickFont.Size, tickPaint);

					// draw axes
					c.DrawRect(_axisRect, linePaint);
				}
			}

			_canDrawData = true;
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="text"></param>
		protected void RenderText(SKCanvas c, string text)
		{
			text ??= "<no text>";

			using var paint = new SKPaint()
			{
				Color = new SKColor(255, 0, 0),
				Style = SKPaintStyle.Fill
			};
			using var font = new SKFont()
			{
				Size = 28,
				Typeface = SKTypeface.FromFamilyName("Arial")
			};

			c.Clear();
			c.DrawText(text, 0, 0, font, paint);
		}

		/// <summary>
		/// Attempt to retrieve the channel at the selected location.
		/// </summary>
		/// <param name="location">The selected location.</param>
		/// <param name="channel">The channel mapping (source channel index and label).</param>
		/// <param name="row">The row within the channel grid.</param>
		/// <param name="col">The column within the channel grid.</param>
		/// <returns>True if a retrieval is successful, false otherwise.</returns>
		protected bool TryGetChannelAt(Point location, out Mapping channel, out int row, out int col)
		{
			row = col = -1;
			channel = Mapping.Invalid;

			if (_channelMapping.Size == 0)
				return false;

			for (int iRow = 0; iRow < _channelMapping.Size; ++iRow)
			{
				for (int iCol = 0; iCol < _channelMapping.Size; ++iCol)
				{
					// check if the rectangle contains the point
					if (new Rect(	iCol * _panelRect.Width + _axisRect.Left, 
									iRow * _panelRect.Height + _axisRect.Top, 
									_axisRect.Width, _axisRect.Height).Contains(location))
					{
						row		= iRow;
						col		= iCol;
						channel = _channelMapping[iRow, iCol];
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected virtual void over_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected virtual void over_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected virtual void over_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
		}

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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void over_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
			=> RenderOverlay(e.Surface.Canvas);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mitSetRange_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// bring up a menu which allows for range selection
			

		}

		private void mitResetRange_Click(object sender, RoutedEventArgs e)
		{

		}


		protected Matrix<Mapping>				_channelMapping	= new();
		protected (float Lower, float Upper)	_xRange			= default;
		protected (float Lower, float Upper)	_yRange			= default;
		protected float							_margin			= 5;
		protected bool							_canDrawData	= false;
		protected SKRect						_axisRect		= default;
		protected SKRect						_panelRect		= default;


	}
}
