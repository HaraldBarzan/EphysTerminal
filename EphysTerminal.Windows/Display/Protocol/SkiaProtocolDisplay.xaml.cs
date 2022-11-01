using SkiaSharp;
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

			_fixCrossPaint	= new()				{ Color = new(0xFFFFFFFF), Style = SKPaintStyle.Stroke, StrokeWidth = 5 };
			_textFont		= new()				{ Size = 32 };
			_textPaint		= new(_textFont)	{ Color = new(0xFFFFFFFF), Style = SKPaintStyle.StrokeAndFill };
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
		public int MonitorIndex { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public void BlankScreenAsync(Color? color)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<Color?>(BlankScreenAsync), color);
			else
			{
				if (color.HasValue)
					BackgroundColor = color.Value;
				State = ProtocolDisplayState.BlankScreen;
				skc.InvalidateVisual();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="backgroundColor"></param>
		/// <param name="text"></param>
		public void ScreenWithTextAsync(Color? backgroundColor, string text, Color? textColor)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<Color?, string, Color?>(ScreenWithTextAsync), backgroundColor, text, textColor);
			else
			{
				if (text is not null)
					Text = text;
				if (backgroundColor.HasValue)
					BackgroundColor = backgroundColor.Value;
				if (textColor.HasValue)
					BackgroundColor = textColor.Value;
				State = ProtocolDisplayState.Text;
				skc.InvalidateVisual();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="backgroundColor"></param>
		/// <param name="fixationCrossColor"></param>
		public void ScreenWithFixationCrossAsync(Color? backgroundColor, Color? fixationCrossColor)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<Color?, Color?>(ScreenWithFixationCrossAsync), backgroundColor, fixationCrossColor);
			else
			{
				if (fixationCrossColor is not null)
					FixationCrossColor = fixationCrossColor.Value;
				if (backgroundColor.HasValue)
					BackgroundColor = backgroundColor.Value;
				State = ProtocolDisplayState.FixationCross;
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
					int fixCrossHSize = 25;
					hDC.DrawLine(center.X - fixCrossHSize, center.Y, center.X + fixCrossHSize, center.Y, _fixCrossPaint);
					hDC.DrawLine(center.X, center.Y - fixCrossHSize, center.X, center.Y + fixCrossHSize, _fixCrossPaint);
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
		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key is Key.Space)
				KeyPressed?.Invoke(this, e);
		}


		SKPaint _fixCrossPaint	= default;
		SKPaint _textPaint		= default;
		SKFont	_textFont		= default;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
		private static SKColor GetColor(Color c)
			=> new SKColor(c.R, c.G, c.B, c.A);

	}
}
