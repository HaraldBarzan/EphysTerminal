﻿using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TINS;
using TINS.Ephys;
using TINS.Ephys.Data;
using TINS.Ephys.Display;
using TINS.Ephys.Settings;
using TINS.Ephys.Stimulation;
using TINS.Ephys.Stimulation.Genus;
using TINS.Ephys.UI;
using TINS.IO;

namespace CircuitGENUS.Windows
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow 
		: Window, IUserInterface
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();
			titleBar.HookWindow(this);
			drawMua.ThresholdChanged += OnMUAThresholdChange;

			// set up the protocol wizard
			ProtocolWizard = new ProtocolWizard(this);
			ProtocolWizard.SetActions(
				startRecording: (path) =>
				{
					if (IsRecording)
						EphysStream.StopRecording();
					EphysStream.StartStream();
					EphysStream.StartRecording(path);
				},
				startProtocol: (path) =>
				{
					if (EphysStream is not null && TryLoadProtocol(path, out var protocol, out _))
					{
						EphysStream.SetStimulationProtocolAsync(protocol);
						EphysStream.StartProtocol();
					}
				},
				stopProtocol: () => EphysStream?.StopProtocol(),
				stopRecording: () => EphysStream?.StopStream());
		}


		/// <summary>
		/// The ephys stream.
		/// </summary>
		public EphysStream EphysStream { get; protected set; }

		/// <summary>
		/// Protocol wizard.
		/// </summary>
		public ProtocolWizard ProtocolWizard { get; protected init; }

		/// <summary>
		/// Update user interface activity regarding multiunit activity (spikes).
		/// </summary>
		/// <param name="muaAccumulator">Multiunit activity.</param>
		public void UpdateMUA(ContinuousDisplayAccumulator muaAccumulator, SpikeDisplayAccumulator spikeAccumulator = null)
		{
			// switch thread
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<ContinuousDisplayAccumulator, SpikeDisplayAccumulator>(UpdateMUA), muaAccumulator, spikeAccumulator);
			else
				drawMua.Update(muaAccumulator, spikeAccumulator);
		}

		/// <summary>
		/// Update user interface activity regarding local field potentials.
		/// </summary>
		/// <param name="lfpAccumulator">Local field potentials.</param>
		public void UpdateLFP(ContinuousDisplayAccumulator lfpAccumulator)
		{
			// switch thread
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<ContinuousDisplayAccumulator>(UpdateLFP), lfpAccumulator);
			else
				drawLfp.Update(lfpAccumulator);
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

		/// <summary>
		/// Check whether the input stream is on.
		/// </summary>
		protected bool IsStreaming 
			=>	EphysStream is object && 
				EphysStream.IsStreaming;

		/// <summary>
		/// Check whether the recording stream is on.
		/// </summary>
		protected bool IsRecording 
			=>	EphysStream is object && 
				EphysStream.IsRecording;

		/// <summary>
		/// Check whether a protocol is running.
		/// </summary>
		protected bool IsProtocolRunning 
			=>	EphysStream is object && 
				EphysStream.StimulationProtocol is object && 
				EphysStream.StimulationProtocol.IsRunning;

		/// <summary>
		/// Test recorder creation.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnStreamToggle_Click(object sender, RoutedEventArgs e)
		{
			if (EphysStream is null)
			{
				MessageBox.Show("No configuration selected for the recorder.", "Error!", MessageBoxButton.OK);
				return;
			}

			// toggle streaming
			if (EphysStream.IsStreaming)
			{
				// stop the ephys stream
				EphysStream.StopProtocol();
				EphysStream.StopRecording();
				EphysStream.StopStream();
			}
			else
			{
				drawLfp.Clear();
				drawMua.Clear();

				// start the ephys stream
				EphysStream.StartStream();
			}
		}

		

		/// <summary>
		/// Test drawing.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRecordToggle_Click(object sender, RoutedEventArgs e)
		{
			if (EphysStream is null)
			{
				MessageBox.Show("No configuration selected for the recorder.", "Error!", MessageBoxButton.OK);
				return;
			}

			// check recording status
			if (EphysStream.IsRecording)
			{
				EphysStream.StopRecording();
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
						InitialDirectory	= @"C:\_data\ephys"
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
						EphysStream.StartRecording(newPath);
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
				if (EphysStream is not null && EphysStream.IsStreaming)
				{

					if (MessageBox.Show("A configuration already exists. Do you wish to overwrite it?",
						"Caution!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					{
						if (EphysStream.IsRecording)
						{
							if (MessageBox.Show("Current configuration is executing a recording session. Do you wish to close it (saving the data) and set a new configuration?",
								"Caution!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
							{
								EphysStream.StopStream();
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
					InitialDirectory	= @"C:\_code\CircuitGENUS\Settings\",
					Filter				= "Settings files (*.ini) | *.ini",
					Multiselect			= false
				};
				if (ofd.ShowDialog() == true)
				{
					try
					{
						var settings = new EphysSettings();
						settings.Serialize(new INI(ofd.FileName), "CIRCUIT_GENUS", SerializationDirection.In);

						// get local dataset path
						string localDatasetPath = null;
						if (settings.Input.InputDevice is InputDevice.Local)
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

						// stop acquisition and delete the old recorder
						if (EphysStream is not null)
						{
							EphysStream.StateChanged -= UpdateInterfaceControls;
							EphysStream.DisposeAsync();
							EphysStream = null;
						}
						
						// return to the old thread
						Dispatcher.BeginInvoke(new Action<EphysSettings, string>(OnLoadSuccessful), settings, localDatasetPath);
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
			EphysStream		?.Dispose();
			ProtocolWizard	?.Dispose();
		}

		/// <summary>
		/// Called on a successful settings loading operation to create a new recorder object.
		/// </summary>
		/// <param name="settings">Stream configuration.</param>
		/// <param name="localDatasetPath">Path to local dataset (if necessary).</param>
		private void OnLoadSuccessful(EphysSettings settings, string localDatasetPath)
		{
			// create the necessary input stream
			DataInputStream inputStream = null;
			switch (settings.Input.InputDevice)
			{
				case InputDevice.Dummy:
					inputStream = new DummyDataStream(settings);
					break;

				case InputDevice.Local:
					if (string.IsNullOrEmpty(localDatasetPath))
						throw new Exception("The parameter \'localDatasetPath\' must be provided when the input device is set to \'Local\'.");
					inputStream = new LocalDataStream(settings, localDatasetPath);
					break;

				case InputDevice.MEA64USB:
					inputStream = new MCSDataStream(settings);
					break;

				default:
					throw new Exception("Invalid input device specified.");
			}

			// create stream and start it on a new thread
			EphysStream = new EphysStream(
				settings:			settings,		// the settings item
				ui:					this,			// the UI for the streamer
				dataInputStream:	inputStream);   // the input stream (platform specific));
			EphysStream.StateChanged	+= UpdateInterfaceControls;
			EphysStream.InputReceived	+= (_, _) => ProtocolWizard.NotifyNewBlock(); 
			new Thread(() => EphysStream.Start()).Start();

			// create channel mapping matrix
			var mapping		= new Matrix<DataDisplay.Mapping>(settings.UI.DisplayGridRows, settings.UI.DisplayGridColumns);
			var channels	= settings.UI.DisplayChannels;
			for (int i = 0; i < channels.Size; ++i)
			{
				int sourceIndex;
				if (!string.IsNullOrEmpty(channels[i])										&&	// valid label
					(sourceIndex = settings.Input.ChannelLabels.IndexOf(channels[i])) >= 0)		// label found in inputs
				{
					mapping[i] = new() { Label = channels[i], SourceIndex = sourceIndex };
				}
			}

			// setup MUA axes
			drawMua.Setup(
				channelMapping: mapping,
				xRange: (0, settings.Input.PollingPeriod * settings.UI.MUARefreshRate * 1000 /*conv to ms*/),
				yRange: (settings.UI.MUAYRangeMin, settings.UI.MUAYRangeMax));

			// set thresholds and waveform size
			foreach (var set in settings.Analysis.Pipes)
			{
				if (set.Name == settings.UI.MUASpikeDetector && 
					set is SpikeSettings spikeSet)
				{
					drawMua.WaveformXRange = (-spikeSet.PeakOffset, MathF.Round(spikeSet.SpikeCutWidth - spikeSet.PeakOffset, 2));
					using var thr = new Matrix<float>(mapping.Dimensions);
						thr.Fill(spikeSet.Threshold);
					drawMua.SetThresholds(thr);
				}
			}

			// setup LFP axes
			drawLfp.Setup(
				channelMapping: mapping,
				xRange: (0, settings.Input.PollingPeriod * settings.UI.LFPRefreshRate * 1000 /*conv to ms*/),
				yRange: (settings.UI.LFPYRangeMin, settings.UI.LFPYRangeMax));

			// update the controls
			UpdateInterfaceControls();
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
				btnStreamToggle.Content		= App.GetResource<Image>(stream		? "StopIcon" : "PlayIcon");
				btnRecordToggle.Content		= App.GetResource<Image>(record		? "RecordOnIcon" : "RecordOffIcon");
				btnToggleProtocol.Content	= App.GetResource<Image>(proto		? "ProtStopIcon" : "ProtStartIcon"); 

				// update status label
				if (EphysStream is null)
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnMUAThresholdChange(object sender, ThresholdChangedEventArgs e)
		{
			if (sender == drawMua && EphysStream is object)
			{
				EphysStream.ChangeDetectorThresholdAsync(e.Mapping.SourceIndex, e.NewValue);
			}
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

			if (EphysStream is null)
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
					InitialDirectory	= @"C:\_code\CircuitGENUS\Settings\",
					Filter				= "Configuration files (*.json) | *.json",
					Multiselect			= false
				};

				// validate input path
				if (ofd.ShowDialog() == true && File.Exists(ofd.FileName))
				{
					if (TryLoadProtocol(ofd.FileName, out var protocol, out var excMessage))
					{
						// register the protocol
						EphysStream.SetStimulationProtocolAsync(protocol);
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

			if (EphysStream is null)
			{
				MessageBox.Show(this, "No stream configuration loaded. Load a configuration and try again.", "Error!");
				return;
			}

			if (EphysStream.StimulationProtocol is null)
			{
				App.MessageBoxAsync("No stimulation protocol loaded.", "Error");
				return;
			}

			if (EphysStream.StimulationProtocol.IsRunning)
				EphysStream.StopProtocol();
			else
				EphysStream.StartProtocol();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="path"></param>
		private bool TryLoadProtocol(string path, out IStimulationProtocol protocol, out string excMessage)
		{
			protocol	= null;
			excMessage	= null;

			try
			{
				var fileData = File.ReadAllText(path);

				// preload config
				var config = JsonSerializer.Deserialize<ProtocolConfig>(fileData);

				// check protocol name
				switch (config.ProtocolType)
				{
					case "genus":
						// start protocol stream
						var genusProtocol = new GenusProtocol(
							parent: EphysStream,
							config: JsonSerializer.Deserialize<GenusConfig>(fileData),
							stimulusController: new ArduinoStimulusController());
						genusProtocol.UpdateProgress	+= UpdateTrialIndicator;
						genusProtocol.ProtocolEnded		+= () => 
						{
							UpdateTrialIndicator(genusProtocol.TrialCount, genusProtocol.TrialCount);
							ProtocolWizard.NotifyProtocolEnded();
							if (!ProtocolWizard.IsExecuting) 
								App.MessageBoxAsync("Protocol run finished.", "Finish!");
						};
						protocol = genusProtocol;
						break;

					default:
						throw new Exception($"Protocol name \'{config.ProtocolType}\' not recognized.");
				}

				return true;
			}
			catch (Exception e)
			{
				excMessage = e.Message;
				return false;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnProtoWizard_Click(object sender, RoutedEventArgs e)
		{
			ProtocolWizard.Show();
		}
	}
}