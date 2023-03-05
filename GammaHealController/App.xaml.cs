using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TINS.Ephys.Native;
using TINS.Native;

namespace GammaHealController
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		static App()
		{
			// provide the necessary DLLs
			NativeWrapper.Provide<DAQmx>("nicaiu");
			NativeWrapper.Provide<FFTWSingle>("libfftw3f-3.dll");
		}

		/// <summary>
		/// Attempt to get a resource by type and name.
		/// </summary>
		/// <typeparam name="T">The type of the resource.</typeparam>
		/// <param name="resourceName">The name of the resource.</param>
		/// <returns>A resource object, if found, null otherwise.</returns>
		public static T GetResource<T>(string resourceName) where T : class
			=> Current.TryFindResource(resourceName) as T;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Application_Startup(object sender, StartupEventArgs e)
		{
			// sanity check
			if (!Environment.Is64BitOperatingSystem)
				throw new PlatformNotSupportedException("Only 64bit OS-es can run this app.");

			// create windows
			var mainWindow	= new MainWindow();
			var stimulation = new StimulationWindow();
			var devTest		= new DeviceTestWindow();
			var closedLoop	= new ClosedLoopWindow();

			// run the welcome dialog
			if (mainWindow.ShowDialog() == true)
			{
				switch (mainWindow.SelectedOption)
				{
					case ProgramType.DeviceTest:
						devTest.ShowDialog();
						break;

					case ProgramType.Stimulation:
						stimulation.ShowDialog();
						break;

					case ProgramType.ClosedLoop:
						closedLoop.ShowDialog(); 
						break;	

					default:
						break;
				}
			}

			stimulation	?.Dispose();
			devTest		?.Dispose();
			closedLoop	?.Dispose();
			Environment.Exit(0);
        }
    }
}
