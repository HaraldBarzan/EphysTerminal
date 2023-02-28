using SkiaSharp;
using SkiaSharp.Views.WPF;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TINS.Terminal.Display.Protocol
{
	/// <summary>
	/// 
	/// </summary>
	public enum ProtocolDisplayState
	{
		BlankScreen,
		Text,
		TextTrialIndicator,
		ChannelSelect,
		FixationCross
	}


	/// <summary>
	/// Interaction logic for SkiaProtocolDisplay.xaml
	/// </summary>
	public partial class SkiaProtocolDisplay : Window
	{
		/// <summary>
		/// 
		/// </summary>
		public event KeyEventHandler KeyPressed;

		/// <summary>
		/// 
		/// </summary>
		public SkiaProtocolDisplay(int monitorIndex = 0)
		{
			InitializeComponent();
			MonitorIndex = monitorIndex;

			_fixCrossPaint			= new()				{ Color = new(0xffffffff), Style = SKPaintStyle.Stroke, StrokeWidth = 5 };
			_textFont				= new()				{ Size = 32 };
			_textPaint				= new(_textFont)	{ Color = new(0xffffffff), Style = SKPaintStyle.StrokeAndFill };
			_trialIndicatorOutline	= new()				{ Color = new(0xffffffff), Style = SKPaintStyle.Stroke };
			_trialIndicatorBar		= new()				{ Color = new(0xff00ff00), Style = SKPaintStyle.Fill };

			_chSelectBoxLinePaint	= new()				{ Color = new(0xffffffff), Style = SKPaintStyle.Stroke };
			_chSelectBoxTextFont	= new()				{ Size = 14 };
			_chSelectBoxTextPaint	= new(_chSelectBoxTextFont)	{ Color = new(0xffffffff), Style = SKPaintStyle.Fill };
		}

		
		/// <summary>
		/// 
		/// </summary>
		public ProtocolDisplayState State { get; set; } = ProtocolDisplayState.BlankScreen;
		
		/// <summary>
		/// 
		/// </summary>
		public Color BackgroundColor { get; set; } = Color.FromArgb(255, 0, 0, 0);
		
		/// <summary>
		/// 
		/// </summary>
		public Color FixationCrossColor { get; set; } = Color.FromArgb(255, 255, 255, 255);
		
		/// <summary>
		/// 
		/// </summary>
		public Color TextColor { get; set; } = Color.FromArgb(255, 255, 255, 255);
		
		/// <summary>
		/// 
		/// </summary>
		public string Text { get; set; } = string.Empty;

		/// <summary>
		/// 
		/// </summary>
		public (int Current, int Total) TrialIndicator { get; set; } = (0, 0);

		/// <summary>
		/// 
		/// </summary>
		public Vector<string> ChannelLabels { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public int SelectedChannelIndex { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public int MonitorIndex { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public void ClearScreenAsync(Color? color)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<Color?>(ClearScreenAsync), color);
			else
			{
				if (color.HasValue)
					BackgroundColor = color.Value;
				State = ProtocolDisplayState.BlankScreen;
				Cursor = Cursors.None;
				skc.InvalidateVisual();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="backgroundColor"></param>
		/// <param name="text"></param>
		public void SwitchToTextAsync(Color? backgroundColor, string text, Color? textColor)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<Color?, string, Color?>(SwitchToTextAsync), backgroundColor, text, textColor);
			else
			{
				if (text is not null)
					Text = text;
				if (backgroundColor.HasValue)
					BackgroundColor = backgroundColor.Value;
				if (textColor.HasValue)
					BackgroundColor = textColor.Value;
				State = ProtocolDisplayState.Text;
				Cursor = Cursors.None;
				skc.InvalidateVisual();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="backgroundColor"></param>
		/// <param name="text"></param>
		/// <param name="trialIndicator"></param>
		public void SwitchToTrialIndicatorAsync(Color? backgroundColor, string text, (int Current, int Total)? trialIndicator)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<Color?, string, (int, int)?>(SwitchToTrialIndicatorAsync), backgroundColor, text, trialIndicator);
			else
			{
				if (text is not null)
					Text = text;
				if (backgroundColor.HasValue)
					BackgroundColor = backgroundColor.Value;
				if (trialIndicator.HasValue)
					TrialIndicator = trialIndicator.Value;
				State = ProtocolDisplayState.TextTrialIndicator;
				Cursor = Cursors.None;
				skc.InvalidateVisual();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="backgroundColor"></param>
		/// <param name="fixationCrossColor"></param>
		public void SwitchToFixationCrossAsync(Color? backgroundColor, Color? fixationCrossColor)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<Color?, Color?>(SwitchToFixationCrossAsync), backgroundColor, fixationCrossColor);
			else
			{
				if (fixationCrossColor is not null)
					FixationCrossColor = fixationCrossColor.Value;
				if (backgroundColor.HasValue)
					BackgroundColor = backgroundColor.Value;
				State = ProtocolDisplayState.FixationCross;
				Cursor = Cursors.None;
				skc.InvalidateVisual();

			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="backgroundColor"></param>
		/// <param name="channelLabels"></param>
		/// <param name="selectedChannelIndex"></param>
		public void SwitchToChannelSelectAsync(
			Color?			backgroundColor,
			Vector<string>	channelLabels,
			int?			selectedChannelIndex,
			string			optionalText,
			(int C, int T)? trialIndicator)
		{
			if (!Dispatcher.CheckAccess())
			{
				Dispatcher.BeginInvoke(new Action<Color?, Vector<string>, int?, string, (int, int)?>(SwitchToChannelSelectAsync),
					backgroundColor, channelLabels, selectedChannelIndex, optionalText, trialIndicator);
			}
			else
			{
				BackgroundColor			= backgroundColor ?? BackgroundColor;
				ChannelLabels			= channelLabels;
				SelectedChannelIndex	= selectedChannelIndex ?? SelectedChannelIndex;
				Text					= optionalText;
				TrialIndicator			= trialIndicator ?? default;
				State					= ProtocolDisplayState.ChannelSelect;

				if (ChannelLabels is not null)
				{
					//_chSelectBoxRows = Numerics.Ceiling(channelLabels.Size / (float)_chSelectBoxCols);
					Cursor = Cursors.Arrow;
				}

				skc.InvalidateVisual();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue is true)
			{
				Top			= Monitors.MonitorList[MonitorIndex].Top;
				Left		= Monitors.MonitorList[MonitorIndex].Left;
				Width		= Monitors.MonitorList[MonitorIndex].Width;
				Height		= Monitors.MonitorList[MonitorIndex].Height;
				WindowState = WindowState.Maximized;
			}
		}

		/// <summary>
		/// Paint the surface.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void skc_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
		{
			// get the drawing primitive
			var hDC = e.Surface.Canvas;

			hDC.Clear(GetColor(BackgroundColor));
			var center = new SKPoint(
				x: (hDC.DeviceClipBounds.Right - hDC.DeviceClipBounds.Left) / 2,
				y: (hDC.DeviceClipBounds.Bottom - hDC.DeviceClipBounds.Top) / 2);
			switch (State)
			{
				case ProtocolDisplayState.Text:
					float textWidth = _textPaint.MeasureText(Text);
					hDC.DrawText(Text, center.X - textWidth / 2, center.Y + _textFont.Size / 2, _textPaint);
					break;

				case ProtocolDisplayState.FixationCross:
					float fixCrossHSize = 25;
					hDC.DrawLine(center.X - fixCrossHSize, center.Y, center.X + fixCrossHSize, center.Y, _fixCrossPaint);
					hDC.DrawLine(center.X, center.Y - fixCrossHSize, center.X, center.Y + fixCrossHSize, _fixCrossPaint);
					break;

				case ProtocolDisplayState.TextTrialIndicator:
					// text
					textWidth = _textPaint.MeasureText(Text);
					hDC.DrawText(Text, center.X - textWidth / 2, center.Y + _textFont.Size / 2, _textPaint);

					// trial indicator
					string trialIndicator	= $"Trial {TrialIndicator.Current} out of {TrialIndicator.Total}.";
					float vspace			= hDC.DeviceClipBounds.Height * 0.1f;
					float hspace			= hDC.DeviceClipBounds.Width * 0.02f;
					var barSize				= new SKSize(250, 15);
					var barRect				= SKRect.Create(center.X - barSize.Width - hspace, center.Y - barSize.Height / 2, barSize.Width, barSize.Height);

					hDC.DrawRect(barRect, _trialIndicatorBar);
					hDC.DrawRect(barRect, _trialIndicatorOutline);
					hDC.DrawText(trialIndicator, center.X + hspace, center.Y - vspace + _chSelectBoxTextFont.Size / 2, _chSelectBoxTextPaint);
					break;

				case ProtocolDisplayState.ChannelSelect:
					// text
					if (!string.IsNullOrEmpty(Text))
					{
						textWidth = _textPaint.MeasureText(Text);
						hDC.DrawText(Text, center.X - textWidth / 2, center.Y + _textFont.Size / 2, _textPaint);
					}

					// trial indicator
					trialIndicator	= $"Trial {TrialIndicator.Current} out of {TrialIndicator.Total}.";
					vspace			= hDC.DeviceClipBounds.Height * 0.1f;
					hspace			= hDC.DeviceClipBounds.Width * 0.005f;
					barSize			= new SKSize(250, 20);
					barRect			= SKRect.Create(center.X - barSize.Width - hspace, center.Y - vspace - barSize.Height / 2, barSize.Width, barSize.Height);

					float progress = TrialIndicator.Total == 0 ? 0 : TrialIndicator.Current / (float)TrialIndicator.Total;
					var progBar = SKRect.Create(barRect.Left, barRect.Top, barRect.Width * progress, barRect.Height);
					hDC.DrawRect(progBar, _trialIndicatorBar);
					hDC.DrawRect(barRect, _trialIndicatorOutline);
					hDC.DrawText(trialIndicator, center.X + hspace, center.Y - vspace + _chSelectBoxTextFont.Size / 2, _chSelectBoxTextPaint);

					if (ChannelLabels is not null && Numerics.IsClamped(SelectedChannelIndex, (0, ChannelLabels.Size)))
					{
						var width		= hDC.DeviceClipBounds.Width * 0.60f;
						var height		= hDC.DeviceClipBounds.Height * 0.25f;
						var chBoxRect	= SKRect.Create((hDC.DeviceClipBounds.Width - width) / 2, center.Y + vspace, width, height);
						_chSelectBox	= new Rect(chBoxRect.Left, chBoxRect.Top, width, height);

						int oldState = hDC.Save();
						hDC.DrawRect(chBoxRect, _trialIndicatorOutline);
						hDC.ClipRect(chBoxRect);
						hDC.Translate(chBoxRect.Location);
						RenderChannelBox(hDC);
						hDC.RestoreToCount(oldState);
					}
					break;

				default:
					break;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void skc_MouseUp(object sender, MouseButtonEventArgs e)
		{
			var pos = e.GetPosition(this);
			pos = PresentationSource.FromVisual(skc).CompositionTarget.TransformToDevice.Transform(pos);

			if (State			is ProtocolDisplayState.ChannelSelect && 
				e.LeftButton	is MouseButtonState.Released && 
				ChannelLabels	is not null &&
				_chSelectBox	.Contains(pos))
			{
				// pressed channel label
				int xIndex = Numerics.Floor((float)((pos.X - _chSelectBox.X) * (_chSelectBoxCols / _chSelectBox.Width)));
				int yIndex = Numerics.Floor((float)((pos.Y - _chSelectBox.Y) * (_chSelectBoxRows / _chSelectBox.Height)));
				int channelIndex = yIndex * _chSelectBoxCols + xIndex;

				if (xIndex < 0			|| xIndex >= _chSelectBoxCols ||
					yIndex < 0			|| yIndex >= _chSelectBoxRows ||
					channelIndex < 0	|| channelIndex >= ChannelLabels.Size)
				{
					return;
				}

				SelectedChannelIndex = channelIndex;
				skc.InvalidateVisual();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="hDC"></param>
		private void RenderChannelBox(SKCanvas hDC)
		{
			if (ChannelLabels is null)
				return;

			// channel label rect
			var labelRect = SKRect.Create((float)hDC.DeviceClipBounds.Width / _chSelectBoxCols, (float)hDC.DeviceClipBounds.Height / _chSelectBoxRows);
			
			for (int i = 0; i < ChannelLabels.Size; ++i)
			{
				// compute index
				int xIndex = i % _chSelectBoxCols;
				int yIndex = i / _chSelectBoxCols;
				var currentRect = labelRect;
				currentRect.Offset(xIndex * labelRect.Width, yIndex * labelRect.Height);

				// draw the rect
				int oldState = hDC.Save();
				//hDC.ClipRect(currentRect);
				hDC.Translate(currentRect.Location);

				float labelWidth = _chSelectBoxTextPaint.MeasureText(ChannelLabels[i]);
				if (SelectedChannelIndex == i)
					hDC.DrawRect(labelRect, _trialIndicatorBar);
				hDC.DrawRect(labelRect, _chSelectBoxLinePaint);
				hDC.DrawText(ChannelLabels[i], (labelRect.Width - labelWidth) / 2, (labelRect.Height + _chSelectBoxTextFont.Size) / 2, _chSelectBoxTextPaint);
				hDC.RestoreToCount(oldState);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key is Key.Space)
				KeyPressed?.Invoke(this, e);
		}

		// channel select box
		Rect	_chSelectBox		= default;
		int		_chSelectBoxRows	= 8;
		int		_chSelectBoxCols	= 16;

		SKPaint _fixCrossPaint			= default;
		SKPaint _textPaint				= default;
		SKFont	_textFont				= default;
		SKPaint _trialIndicatorOutline	= default;
		SKPaint _trialIndicatorBar		= default;
		SKPaint _chSelectBoxLinePaint	= default;
		SKFont	_chSelectBoxTextFont	= default;
		SKPaint _chSelectBoxTextPaint	= default;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
		private static SKColor GetColor(Color c)
			=> new SKColor(c.R, c.G, c.B, c.A);


	}
}
