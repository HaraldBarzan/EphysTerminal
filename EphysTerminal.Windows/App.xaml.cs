using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TeensyNet;
using TINS.Ephys.Native;
using TINS.Native;
using TINS.Terminal.Display.Protocol;
using TINS.Terminal.Protocols.Genus;
using TINS.Terminal.Stimulation;
using TINS.Utilities;

namespace TINS.Terminal
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		/// <summary>
		/// 
		/// </summary>
		public App()
		{
			Instance = this;
		}

		/// <summary>
		/// Teensy factory (for the application).
		/// </summary>
		public static TeensyFactory TeensyFactory { get; } = new();

		/// <summary>
		/// Attempt to get a resource by type and name.
		/// </summary>
		/// <typeparam name="T">The type of the resource.</typeparam>
		/// <param name="resourceName">The name of the resource.</param>
		/// <returns>A resource object, if found, null otherwise.</returns>
		public static T GetResource<T>(string resourceName) where T : class
			=> Current.TryFindResource(resourceName) as T;

		/// <summary>
		/// On app startup.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnStartup(StartupEventArgs e)
		{
			// check if system is 64 bit
			if (!Environment.Is64BitOperatingSystem)
				throw new PlatformNotSupportedException("Only 64bit OS-es can run this app.");
		
			// load native libraries
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
					NativeWrapper.Provide<FFTWSingle>("libfftw3f-3.dll");
					NativeWrapper.Provide<DAQmx>("nicaiu.dll");
					break;
		
				default:
					throw new PlatformNotSupportedException($"Platform {Environment.OSVersion.Platform} is not supported.");
			}
			
			// register the available protocols
			ProtocolFactory.RegisterProtocol(typeof(GenusProtocol),				"genus");
			ProtocolFactory.RegisterProtocol(typeof(HumanGenusProtocol),		"genus-human");
			ProtocolFactory.RegisterProtocol(typeof(GenusClosedLoopProtocol),	"genus-closedloop");
			ProtocolFactory.RegisterProtocol(typeof(GenusCL2),					"genus-closedloop2");

			// load persistent storage
			LoadPersistentStorageFile();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			SavePersistentStorageFile();
			base.OnExit(e);
		}

		/// <summary>
		/// Get the running App instance.
		/// </summary>
		public static App Instance { get; protected set; }

		/// <summary>
		/// Properties.
		/// </summary>
		public static HybridDictionary Persistent
			=> Instance is not null
				? Instance.Properties as HybridDictionary
				: null;

		/// <summary>
		/// Get the value for a given key, or a default value if the key is missing.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="defaultValue">A default value if the key is missing.</param>
		/// <returns>The value for the key, or a default value if the key is missing.</returns>
		public static string GetPropOrDefault(string key, string defaultValue = null)
		{
			if (Persistent is null)
				return defaultValue;
			if (Persistent.Contains(key))
				return Persistent[key] as string;
			return defaultValue;
		}

		/// <summary>
		/// Set the value for a given key. The key is created if missing.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The new value for the key.</param>
		public static void SetProp(string key, string value)
		{
			if (Persistent is null)
				return;
			Persistent[key] = value;
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
			MessageBoxImage		image	= MessageBoxImage.Information)
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
		/// <returns></returns>
		public static Vector<Teensy> GetConnectedTeensys()
		{
			var result = new Vector<Teensy>();
			TeensyFactory.EnumTeensies((e) =>
			{
				result.PushBack(e);
				return true;
			});
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public static string GetFirstTeensyPort()
		{
			using var teensys = GetConnectedTeensys();
			return teensys.IsEmpty ? null : teensys.Front.PortName;
		}

		/// <summary>
		/// 
		/// </summary>
		static void TestSkiaProtocolDisplay()
		{
			SkiaProtocolDisplay sk = default;
			var t = new Thread(() =>
			{
				sk = new();
				sk.ShowDialog();
			});
			t.SetApartmentState(ApartmentState.STA);
			t.Start();

			Thread.Sleep(1000);
			var c = new Vector<string>(128, "Ch??");
			sk.SwitchToChannelSelectAsync(null, c, 0, "MATA", (0, 10));

			Thread.Sleep(100000);
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
			Random.Shared.Shuffle(trials);

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

				Thread.Sleep(Random.Shared.Next(1000, 3000));
			}

			logger.EndFile();
			Environment.Exit(0);
		}

		/// <summary>
		/// 
		/// </summary>
		protected void LoadPersistentStorageFile()
		{
			var storage = IsolatedStorageFile.GetUserStoreForDomain();
			try
			{
				var prop			= Persistent;
				using var stream	= new IsolatedStorageFileStream("persistent.txt", FileMode.Open, storage);
				using var reader	= new StreamReader(stream);

				while (!reader.EndOfStream)
				{
					var kvp = reader.ReadLine().Split(',');
					prop[kvp[0]] = kvp[1];
				}
			}
			catch
			{

			}
		}

		/// <summary>
		/// 
		/// </summary>
		protected void SavePersistentStorageFile()
		{
			var storage			= IsolatedStorageFile.GetUserStoreForDomain();
			var prop			= Persistent;
			using var stream	= new IsolatedStorageFileStream("persistent.txt", FileMode.OpenOrCreate, storage);
			using var writer	= new StreamWriter(stream);

			foreach (var key in prop.Keys)
				writer.WriteLine($"{key},{prop[key]}");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string BinStr(byte value)
		{
			Span<char> chars = stackalloc char[8];
			for (int i = 0; i < chars.Length; ++i)
				chars[7 - i] = (value & (1 << i)) != 0 ? '1' : '0';
			return new string(chars);
		}
	}
}
