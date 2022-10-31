using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace TINS.Terminal.Display.Protocol
{
	/// <summary>
	/// 
	/// </summary>
	static class Monitors
	{
		static Monitors()
		{
			// do a first pass through the monitor list
			if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, 0))
				throw new Exception("Monitor list could not be retrieved.");
			MonitorList.Reverse();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="window"></param>
		public static void SetMonitorHooks(Window window)
		{
			// hook to the target window and install a monitor list updater
			if (PresentationSource.FromVisual(window) is HwndSource src)
				src.AddHook(WindowProc);
		}

		/// <summary>
		/// 
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct ScreenRect
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
			public Rect ToRect() => new(new Point(left, top), new Point(right, bottom));
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="hdc"></param>
		/// <param name="lpRect"></param>
		/// <param name="callback"></param>
		/// <param name="dwData"></param>
		/// <returns></returns>
		[DllImport("user32")]
		private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lpRect, MonitorEnumProc callback, int dwData);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="hDesktop"></param>
		/// <param name="hdc"></param>
		/// <param name="rect"></param>
		/// <param name="dwData"></param>
		/// <returns></returns>
		private delegate bool MonitorEnumProc(IntPtr hDesktop, IntPtr hdc, ref ScreenRect rect, int dwData);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="hwnd"></param>
		/// <param name="msg"></param>
		/// <param name="wParam"></param>
		/// <param name="lParam"></param>
		/// <param name="handled"></param>
		/// <returns></returns>
		static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			const int WM_DISPLAYCHANGE = 0x007e;

			if (msg == WM_DISPLAYCHANGE)
			{
				if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, 0))
					throw new Exception("Monitor list could not be retrieved.");
				MonitorList.Reverse();
			}

			return IntPtr.Zero;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="hDesktop"></param>
		/// <param name="hdc"></param>
		/// <param name="rect"></param>
		/// <param name="dwData"></param>
		/// <returns></returns>
		static bool EnumProc(IntPtr hDesktop, IntPtr hdc, ref ScreenRect rect, int dwData)
		{
			MonitorList.PushBack(rect.ToRect());
			return MonitorCount > 0;
		}

		/// <summary>
		/// Get the list of monitors.
		/// </summary>
		public static Vector<Rect> MonitorList { get; } = new();

		/// <summary>
		/// Get number of connected monitors.
		/// </summary>
		public static int MonitorCount => MonitorList.Size;
	}
}
