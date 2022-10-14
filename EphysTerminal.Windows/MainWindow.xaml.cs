using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using TINS.Conexus.Data;
using TINS.Ephys.Data;
using TINS.Ephys.Settings;
using TINS.IO;
using TINS.Terminal.Data;
using TINS.Terminal.Display;
using TINS.Terminal.Settings;
using TINS.Terminal.Stimulation;
using TINS.Terminal.UI;

namespace TINS.Terminal
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow 
		: Window
		, IUserInterface
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();

			// set up the protocol wizard
			ProtocolWizard = new ProtocolWizard(this);

			// have something on the screen
			var ed = new EphysDisplay();
			ChannelDisplay = ed;
			displayPanel.Children.Add(ed);
		}

		#region IUserInterface
		public void UpdateData(AbstractDisplayData displayData)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<AbstractDisplayData>(UpdateData), displayData);
			else
			{
				// update MUA
				if (displayData		is EphysDisplayData ephysData &&
					ChannelDisplay	is EphysDisplay		ephysDisplay)
				{
					if (ephysData.MUA is not null && ephysData.MUA.IsFull)
						ephysDisplay.UpdateMUA(ephysData.MUA, ephysData.Spikes);
					if (ephysData.LFP is not null && ephysData.LFP.IsFull)
						ephysDisplay.UpdateLFP(ephysData.LFP);
				}
				else if (displayData	is EEGDisplayData	eegData		&&
					ChannelDisplay		is EEGDisplay		eegDisplay	&&
					eegData.EEG			is not null						&& 
					eegData.EEG.IsFull)
				{
					eegDisplay.Update(eegData.EEG);
				}
			}
		}

		/// <summary>
		/// Update user interface activity regarding new events.
		/// </summary>
		/// <param name="events">A list of new events.</param>
		public void UpdateEvents(Vector<int> events)
		{
			if (events is null || events.IsEmpty)
				return;

			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<Vector<int>>(UpdateEvents), events);
			else
				lblTrigger.Content = events.Back.ToString();
		}

		/// <summary>
		/// Update the trial display of the user interface.
		/// </summary>
		/// <param name="currentTrialIndex">The zero-based index of the current trial.</param>
		/// <param name="totalTrialCount">The total number of trials.</param>
		public void UpdateTrialIndicator(int currentTrialIndex, int totalTrialCount)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<int, int>(UpdateTrialIndicator), currentTrialIndex, totalTrialCount);
			else
			{
				totalTrialCount			= Math.Max(totalTrialCount, 0);
				currentTrialIndex		= Numerics.Clamp(currentTrialIndex, (0, totalTrialCount));
				pgbTrials.Minimum		= 0;
				pgbTrials.Maximum		= totalTrialCount;
				pgbTrials.Value			= currentTrialIndex;
				lblTrialStatus.Content	= currentTrialIndex < totalTrialCount 
					? $"Trial {currentTrialIndex + 1} out of {totalTrialCount}."
					: "Protocol run completed.";
			}
		}
		#endregion


		/// <summary>
		/// The ephys stream.
		/// </summary>
		public EphysTerminal EphysTerminal { get; protected set; }

		/// <summary>
		/// Protocol wizard.
		/// </summary>
		public ProtocolWizard ProtocolWizard { get; protected init; }

		/// <summary>
		/// The channel display.
		/// </summary>
		public IChannelDisplay ChannelDisplay { get; protected set; }

		/// <summary>
		/// Check whether the input stream is on.
		/// </summary>
		public bool IsStreaming 
			=> EphysTerminal is object
			&& EphysTerminal.IsStreaming;

		/// <summary>
		/// Check whether the recording stream is on.
		/// </summary>
		public bool IsRecording 
			=> EphysTerminal is object
			&& EphysTerminal.IsRecording;

		/// <summary>
		/// Check whether a protocol is running.
		/// </summary>
		public bool IsProtocolRunning 
			=>EphysTerminal is object
			&& EphysTerminal.IsRunningProtocol;


		#region Processing events
		/// <summary>
		/// Called on a successful settings loading operation to create a new recorder object.
		/// </summary>
		/// <param name="settings">Stream configuration.</param>
		/// <param name="localDatasetPath">Path to local dataset (if necessary).</param>
		private void OnLoadSuccessful(EphysTerminalSettings settings, string localDatasetPath)
		{
			// close the current ephys stream
			if (EphysTerminal is object)
			{
				EphysTerminal.Dispose();
				EphysTerminal = null;
			}

			// determine UI type
			var displayType	= settings.UIType switch
			{
				"EPHYS" => DisplayType.Electrophysiology,
				"EEG"	=> DisplayType.EEG,
				_		=> throw new Exception()
			};

			// create the necessary input stream
			DataInputStream inputStream	= null;
			switch (settings.InputType)
			{
				case "DUMMY":
					inputStream = new DummyDataStream(settings.Input);
					break;

				case "LOCAL":
					if (string.IsNullOrEmpty(localDatasetPath))
						throw new Exception("The parameter \'localDatasetPath\' must be provided when the input device is set to \'Local\'.");
					inputStream = new LocalDataStream(settings.Input, localDatasetPath);
					break;

				case "USB-ME64":
					inputStream = new MCSDataStream(settings.Input);
					break;

				case "BIOSEMI-TCP":
					inputStream = new BiosemiTcpStream(settings.Input as BiosemiTcpInputSettings);
					break;

				default:
					throw new Exception("Invalid input device specified.");
			}
			new Thread(() => inputStream.StartThread()).Start();

			// create stream and start it on a new thread
			EphysTerminal = new EphysTerminal(
				settings:			settings,		// the settings item
				ui:					this,			// the UI for the streamer
				dataInputStream:	inputStream);	// the input stream (platform specific));
			EphysTerminal.StateChanged	+= UpdateInterfaceControls;
			EphysTerminal.InputReceived	+= (_, _) => ProtocolWizard.NotifyNewBlock();
			new Thread(() => EphysTerminal.StartThread()).Start();
			EphysTerminal.ProcessingComplete += EphysStream_ProcessingComplete;

			UpdateInterfaceControls();
			SwitchDisplay(displayType);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="displayType"></param>
		protected void SwitchDisplay(DisplayType displayType)
		{
			if (ChannelDisplay is null)
			{
				ChannelDisplay = displayType switch
				{
					DisplayType.Electrophysiology	=> new EphysDisplay(),
					DisplayType.EEG					=> new EEGDisplay(),
					_								=> throw new NotImplementedException()
				};

				displayPanel.Children.Add(ChannelDisplay as UIElement);
			}
			
			if (ChannelDisplay.DisplayType == displayType)
				ChannelDisplay.InitializeChannelDisplay(EphysTerminal);
			else
			{
				// recreate display
				displayPanel.Children.Clear();
				ChannelDisplay = null;
				SwitchDisplay(displayType);
			}
		}
		#endregion



		#region Window events (clicks, etc)
		/// <summary>
		/// Test recorder creation.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnStreamToggle_Click(object sender, RoutedEventArgs e)
		{
			if (EphysTerminal is null)
			{
				MessageBox.Show("No configuration selected for the recorder.", "Error!", MessageBoxButton.OK);
				return;
			}

			// toggle streaming
			if (EphysTerminal.IsStreaming)
			{
				// stop the ephys stream
				if (ChannelDisplay is EphysDisplay disp)
					disp.AudioStream.Stop();
				EphysTerminal.StopProtocol();
				EphysTerminal.StopRecording();
				EphysTerminal.StopStream();
			}
			else
			{
				ChannelDisplay?.ClearDisplay();

				if (ChannelDisplay is EphysDisplay disp)
					disp.AudioStream.Start();
				EphysTerminal.StartStream();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="frameDuration"></param>
		private void EphysStream_ProcessingComplete(object sender, float frameDuration)
		{
			if (sender == EphysTerminal)
			{
				if (ChannelDisplay is EphysDisplay ephysDisplay && ephysDisplay.AudioStream is not null)
				{
					int sampleCount = Numerics.Round(frameDuration * EphysTerminal.Settings.SamplingRate);
					ephysDisplay.AudioStream.WriteFromSourceBuffer(sampleCount);
				}
			}
		}

		/// <summary>
		/// Test drawing.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRecordToggle_Click(object sender, RoutedEventArgs e)
		{
			if (EphysTerminal is null)
			{
				MessageBox.Show("No configuration selected for the recorder.", "Error!", MessageBoxButton.OK);
				return;
			}

			// check recording status
			if (EphysTerminal.IsRecording)
			{
				EphysTerminal.StopRecording();
			}
			else
			{
				// execute save browsing on a separate thread
				var t = new Thread(() =>
				{
					var sfd = new SaveFileDialog()
					{
						Title				= "Save EEG Processor Dataset",
						Filter				= "EEG Processor Dataset (*.epd) | *.epd",
						InitialDirectory	= @"C:\_data\ephys\mouse"
					};

					// open the file dialog
					if (sfd.ShowDialog() == true)
					{
						// check for other epd files in the same directory and ask to create a subdirectory
						var dir		= Directory.GetParent(sfd.FileName);
						var name	= Path.GetFileNameWithoutExtension(sfd.FileName);

						if (dir.GetFiles("*.epd", SearchOption.TopDirectoryOnly).Length > 0 &&
							!File.Exists(sfd.FileName))
						{
							// show a yes/no/cancel dialog 
							switch (MessageBox.Show("Selected directory already contains an .epd file. Do you wish to create a subdirectory for your new recording?",
								"Information", MessageBoxButton.YesNoCancel, MessageBoxImage.Information))
							{
								case MessageBoxResult.Yes:
									dir = dir.CreateSubdirectory(name);
									break;

								case MessageBoxResult.Cancel:
									return;

								default:
									break;
							}
						}

						var newPath = Path.Combine(dir.FullName, name + ".epd");
						EphysTerminal.StartRecording(this, new()
						{
							DatasetDirectory	= dir.ToString(),
							DatasetName			= name
						});
					}
				});
				t.SetApartmentState(ApartmentState.STA);
				t.Start();
			}
		}

		/// <summary>
		/// Called when clicking on the load settings button.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnLoadSettings_Click(object sender, RoutedEventArgs e)
		{
			var t = new Thread(() =>
			{
				if (EphysTerminal is not null && EphysTerminal.IsStreaming)
				{

					if (MessageBox.Show("A configuration already exists. Do you wish to overwrite it?",
						"Caution!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					{
						if (EphysTerminal.IsRecording)
						{
							if (MessageBox.Show("Current configuration is executing a recording session. Do you wish to close it (saving the data) and set a new configuration?",
								"Caution!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
							{
								EphysTerminal.StopStream();
							}
							else return;
						}
					}
					else return;
				}

				// open a file dialog on a background thread
				var ofd = new OpenFileDialog()
				{
					Title				= "Open configuration file",
					InitialDirectory	= @"C:\_code\ephysterminal\settings\configurations",
					Filter				= "Settings files (*.ini) | *.ini",
					Multiselect			= false
				};
				if (ofd.ShowDialog() == true)
				{
					try
					{
						var settings = new EphysTerminalSettings();
						settings.Serialize(new INI(ofd.FileName), settings.HeaderSection, SerializationDirection.In);

						// get local dataset path
						string localDatasetPath = null;
						if (settings.InputType == "LOCAL")
						{
							ofd = new OpenFileDialog()
							{
								InitialDirectory	= @"C:\_werk\CircuitGENUS\testLocalInput\",
								Filter				= "EEG Processor Dataset files (*.epd) | *.epd",
								Multiselect			= false
							};
							if (ofd.ShowDialog() == true)
								localDatasetPath = ofd.FileName;
							else
								MessageBox.Show("A local dataset must be selected for input device \'Local\'.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
						}

						// close the ephys stream
						CloseEphysStream();
						
						// return to the old thread
						Dispatcher.BeginInvoke(new Action<EphysTerminalSettings, string>(OnLoadSuccessful), settings, localDatasetPath);
					}
					catch (Exception e)
					{
						MessageBox.Show($"An error has occurred while trying to change configuration:\n\n{e.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
					}
				}
			});
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnSettings_Click(object sender, RoutedEventArgs e)
		{

		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			EphysTerminal		?.Dispose();
			ProtocolWizard	?.Dispose();
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnLoadProtocol_Click(object sender, RoutedEventArgs e)
		{
			_ = sender;
			_ = e;

			if (EphysTerminal is null)
			{
				MessageBox.Show(this, "No stream configuration loaded. Load a configuration and try again.", "Error!");
				return;
			}

			if (IsProtocolRunning)
			{
				App.MessageBoxAsync("A stimulation protocol is already loaded and running. Stop it and load again.", "Error");
				return;
			}

			// do the loading on a background thread
			var t = new Thread(() =>
			{
				// open a file dialog
				var ofd = new OpenFileDialog()
				{
					Title				= "Open protocol configuration file",
					InitialDirectory	= @"C:\_code\ephysstream\settings\protocols",
					Filter				= "Configuration files (*.json) | *.json",
					Multiselect			= false
				};

				// validate input path
				if (ofd.ShowDialog() == true && File.Exists(ofd.FileName))
				{
					if (TryLoadProtocol(ofd.FileName, out var protocol, out var excMessage))
					{
						// register the protocol
						EphysTerminal.SetStimulationProtocolAsync(protocol);
					}
					else 
					{
						Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(this,
							$"Error thrown while attempting to load protocol:\n{excMessage}",
							"Error", MessageBoxButton.OK, MessageBoxImage.Error)));
					}
				}
			});
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnToggleProtocol_Click(object sender, RoutedEventArgs e)
		{
			_ = sender;
			_ = e;

			if (EphysTerminal is null)
			{
				MessageBox.Show(this, "No stream configuration loaded. Load a configuration and try again.", "Error!");
				return;
			}

			if (EphysTerminal.StimulationProtocol is null)
			{
				App.MessageBoxAsync("No stimulation protocol loaded.", "Error");
				return;
			}

			if (EphysTerminal.StimulationProtocol.IsRunning)
				EphysTerminal.StopProtocol();
			else
				EphysTerminal.StartProtocol();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnProtoWizard_Click(object sender, RoutedEventArgs e)
			=> ProtocolWizard.Show();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnFieldAnalyzer_Click(object sender, RoutedEventArgs e)
		{
			var t = new Thread(() => new FieldAnalyzer().ShowDialog());
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
		}
		#endregion


		#region Protected members

		/// <summary>
		/// 
		/// </summary>
		/// <param name="path"></param>
		public bool TryLoadProtocol(string path, out IStimulationProtocol protocol, out Exception loadException)
		{
			protocol		= null;
			loadException	= null;

			try
			{
				protocol = ProtocolFactory.LoadProtocol(EphysTerminal, path);
				protocol.UpdateProgress += UpdateTrialIndicator;
				protocol.ProtocolEnded	+= () => ProtocolWizard.NotifyProtocolEnded();
				return true;
			}
			catch (Exception e)
			{
				loadException = e;
				return false;
			}
		}

		/// <summary>
		/// Close the ephys stream.
		/// </summary>
		private void CloseEphysStream()
		{
			EphysTerminal?.DisposeAsync();
			EphysTerminal = null;
		}

		/// <summary>
		/// Update the button icons and status text.
		/// </summary>
		private void UpdateInterfaceControls()
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action(UpdateInterfaceControls));
			else
			{
				bool stream = IsStreaming;
				bool record = IsRecording;
				bool proto	= IsProtocolRunning;

				// update buttons
				btnStreamToggle.Source		= App.GetResource<ImageSource>(stream		? "StopIcon" : "PlayIcon");
				btnRecordToggle.Source		= App.GetResource<ImageSource>(record		? "RecordOnIcon" : "RecordOffIcon");
				btnToggleProtocol.Source	= App.GetResource<ImageSource>(proto		? "ProtStopIcon" : "ProtStartIcon"); 

				// update status label
				if (EphysTerminal is null)
					lblStatus.Content = "No configuration loaded";
				else if (!stream && !record && !proto)
					lblStatus.Content = "Idle";
				else
				{
					var str = string.Empty;
					if (stream)	str += "Streaming";
					if (record)	str += stream ? ", recording" : "Recording";
					if (proto)	str += stream || record ? ", running protocol" : "Running protocol";
					lblStatus.Content = str;
				}
			}
		}

		#endregion

		


	}
}
