using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TINS;
using TINS.Terminal.Protocols.Genus;
using TINS.Terminal.Stimulation;
using TINS.Native;
using TINS.Utilities;
using TINS.Terminal.Display.Protocol;
using System.Windows.Media;
using System.Diagnostics.CodeAnalysis;

namespace TINS.Terminal
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		/// <summary>
		/// Attempt to get a resource by type and name.
		/// </summary>
		/// <typeparam name="T">The type of the resource.</typeparam>
		/// <param name="resourceName">The name of the resource.</param>
		/// <returns>A resource object, if found, null otherwise.</returns>
		public static T GetResource<T>(string resourceName) where T : class
			=> Current.TryFindResource(resourceName) as T;

		/// <summary>
		/// Startup routine.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnStartup(StartupEventArgs e)
		{
			//SkiaProtocolDisplay sk = default;
			//var t = new Thread(() =>
			//{
			//	sk = new SkiaProtocolDisplay(1);
			//	sk.ShowDialog();
			//});
			//t.SetApartmentState(ApartmentState.STA);
			//t.Start();
			//
			//Thread.Sleep(1000);
			//sk.SwitchToChannelSelectAsync(Color.FromRgb(0, 0, 0), new Vector<string>(128, "Ch ??"), 5, "Select source channel and press SPACE or one of the EEG buttons to initiate next trial...", (5, 10));
			//
			//Thread.Sleep(5000);
			//sk.SwitchToFixationCrossAsync(null, null);

			// check if system is 64 bit
			if (!Environment.Is64BitOperatingSystem)
				throw new PlatformNotSupportedException("Only 64bit OS-es can run this app.");
		
			// load native libraries
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
					NativeWrapper.Provide<FFTWSingle>(@"C:\_code\_binaries\x64\libfftw3f-3.dll");
					break;
		
				default:
					throw new PlatformNotSupportedException($"Platform {Environment.OSVersion.Platform} is not supported.");
			}
			
			// register the available protocols
			ProtocolFactory.RegisterProtocol(typeof(GenusProtocol),				"genus");
			ProtocolFactory.RegisterProtocol(typeof(HumanGenusProtocol),		"genus-human");
			ProtocolFactory.RegisterProtocol(typeof(GenusClosedLoopProtocol),	"genus-closedloop");
		}

		/// <summary>
		/// Show a non-blocking message box.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="title"></param>
		/// <param name="buttons"></param>
		/// <param name="image"></param>
		public static Task<MessageBoxResult> MessageBoxAsync(
			string				text,
			string				title,
			MessageBoxButton	buttons = MessageBoxButton.OK,
			MessageBoxImage		image = MessageBoxImage.Information)
		{
			// define the result
			var result = new Task<MessageBoxResult>(() => MessageBox.Show(text, title, buttons, image));
			
			// run the thread
			var t = new Thread(() => result.RunSynchronously());
			t.SetApartmentState(ApartmentState.STA);
			t.Start();

			// return the await handle
			return result;
		}


		



		/// <summary>
		/// 
		/// </summary>
		static void ConnectionlessProtocol()
		{
			var trials = new Vector<GenusCachedTrial>();
			trials.PushBack(new Vector<GenusCachedTrial>(10, fill: GenusCachedTrial.Get("static-v-6000ms-07Hz")));
			trials.PushBack(new Vector<GenusCachedTrial>(10, fill: GenusCachedTrial.Get("static-v-6000ms-10Hz")));
			trials.PushBack(new Vector<GenusCachedTrial>(10, fill: GenusCachedTrial.Get("static-v-6000ms-20Hz")));
			trials.PushBack(new Vector<GenusCachedTrial>(10, fill: GenusCachedTrial.Get("static-v-6000ms-30Hz")));
			trials.PushBack(new Vector<GenusCachedTrial>(10, fill: GenusCachedTrial.Get("static-v-6000ms-40Hz")));
			trials.PushBack(new Vector<GenusCachedTrial>(10, fill: GenusCachedTrial.Get("static-v-6000ms-50Hz")));
			trials.PushBack(new Vector<GenusCachedTrial>(10, fill: GenusCachedTrial.Get("static-v-6000ms-60Hz")));
			new RNG().Shuffle(trials);

			// create the text writer
			var logger = new TrialInfoLogger(@"C:\_data\ephys\mouse\genus\raw\m079\m079_static_av_0016.eti", 
				header: new() 
				{
					"Trial",
					"TrialName",
					"TrialType",
					"Audio",
					"Visual",
					"StimulationRuntime",
					"StepCount",
					"FlickerFrequency",
					"AudioToneFrequency",
					"UseFlickerTriggers",
					"UseTransitionTriggers",
				});

			string Binarize(bool p) => p ? "1" : "0";
			string Frequency((float, float) f) => f.Size() > 0 ? $"{f.Item1}:{f.Item2}" : f.Item1.ToString();
			
			// controller
			var stc = new GenusController();
			stc.Connect();
			var evt = new AutoResetEvent(false);
			stc.FeedbackReceived += (_, _) => evt.Set();

			// loop through trials
			for (int i = 0; i < trials.Size; ++i)
			{
				stc.EmitTrigger(128);
				Thread.Sleep(1000);
				stc.SendInstructionList(trials[i].Instructions);
				evt.WaitOne();
				Thread.Sleep(1000);
				stc.EmitTrigger(192);

				var il = trials[i];

				logger.LogTrial(
					i + 1, 
					il.Name, 
					il.Type,
					Binarize(il.Audio), 
					Binarize(il.Visual),
					il.StimulationRuntime, 
					il.StepCount,
					Frequency(il.FlickerFrequency),
					il.ToneFrequency,
					Binarize(il.UseFlickerTriggers),
					Binarize(il.UseTransitionTriggers));

				Thread.Sleep(new RNG().NextInt(1000, 3000));
			}

			logger.EndFile();
			Environment.Exit(0);
		}
	}
}
