using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TINS;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace StimulationScreen
{
	internal class StimulationScreen
		: IDisposable
	{



		public StimulationScreen()
		{

		}

		public void Dispose()
		{
		}


		/// <summary>
		/// Check that a monitor index refers to a valid, connected monitor.
		/// </summary>
		/// <param name="monitorIndex">The index of the monitor to check for.</param>
		/// <param name="detectedMonitors">The number of detected monitors.</param>
		/// <returns>True if the index is within <c>[0, detectedMonitors)</c>.</returns>
		public static bool CheckValidMonitorIndex(int monitorIndex, out int detectedMonitors)
		{
			// monitor counter
			detectedMonitors = 0;
		
			// trigger the DXGI 
			if (!DXGI.CreateDXGIFactory1<IDXGIFactory1>(out var dxgiFactory).Failure)
			{
				// exit condition
				if (dxgiFactory is null)
					return false;
		
				// create Direct3D device
				if (!D3D11.D3D11CreateDevice(
					IntPtr.Zero,
					DriverType.Hardware,
					DeviceCreationFlags.BgraSupport,
					new FeatureLevel[] { FeatureLevel.Level_11_0 },
					out var d3dDevice,
					out _,
					out var deviceContext).Failure)
				{
					// get the adapter for the device
					using (var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>())
					{
						if (!dxgiDevice.GetAdapter(out var dxgiAdapter).Failure)
						{
							while (!dxgiAdapter.EnumOutputs(detectedMonitors, out var output).Failure)
							{
								detectedMonitors++;
								output.Dispose();
							}
							dxgiAdapter.Dispose();
						}
					}
					d3dDevice.Dispose();
					deviceContext.Dispose();
				}
				dxgiFactory.Dispose();
			}
		
			// check if index is within the range of detected monitors
			return monitorIndex < detectedMonitors;
		}
	}
}
