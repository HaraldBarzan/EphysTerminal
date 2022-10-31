using TINS;

namespace StimulationScreen
{
	/// <summary>
	/// 
	/// </summary>
	public partial class SkiaScreen 
		: Form
	{
		/// <summary>
		/// Create a screen.
		/// </summary>
		public SkiaScreen()
		{
			InitializeComponent();
			sk.PaintSurface += Sk_PaintSurface;
			sk.MouseEnter	+= (_, _) => Cursor.Hide();
			sk.MouseLeave	+= (_, _) => Cursor.Show();
		}

		/// <summary>
		/// Set the window to cover the whole screen.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);

			Location	= new(0, 0);
			Size		= _screen.Bounds.Size;
		}

		/// <summary>
		/// Check that a monitor index refers to a valid, connected monitor.
		/// </summary>
		/// <param name="monitorIndex">The index of the monitor to check for.</param>
		/// <param name="detectedMonitors">The number of detected monitors.</param>
		/// <returns>True if the index is within <c>[0, detectedMonitors)</c>.</returns>
		public static bool CheckValidMonitorIndex(int monitorIndex, out int detectedMonitors)
		{
			detectedMonitors = Screen.AllScreens.Length;
			return Numerics.IsClamped(monitorIndex, (0, detectedMonitors));
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Sk_PaintSurface(object? sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
		{
			var c = e.Surface.Canvas;

			//c.Clear(new(0xFF000000));
		}





		Screen _screen = Screen.PrimaryScreen;
	}
}
