using EphysStream.Windows;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TINS.Ephys.Data;
using TINS.Utilities;

namespace TINS.Ephys
{
	/// <summary>
	/// Interaction logic for ProtocolWizard.xaml
	/// </summary>
	public partial class ProtocolWizard 
		: Window, IDisposable
    {
		/// <summary>
		/// Create a window.
		/// </summary>
		/// <param name="mainWindow"></param>
        public ProtocolWizard(MainWindow mainWindow)
        {
            InitializeComponent();
			MainWindow		= mainWindow;
			_stateMachine	= new ProtocolWizardStateMachine(this);

			_stateMachine.SetActions(
				startRecording: (e) =>
				{
					Directory.CreateDirectory(e.DatasetDirectory);
					MainWindow?.EphysStream?.StartRecording(this, e);
					MainWindow?.EphysStream?.StartStream();
					MainWindow?.AudioStream?.Start();
				},
				startProtocol: (path) =>
				{
					if (MainWindow is object && MainWindow.EphysStream is object && 
						MainWindow.TryLoadProtocol(path, out var protocol, out _))
					{
						MainWindow?.EphysStream?.SetStimulationProtocolAsync(protocol);
						MainWindow?.EphysStream?.StartProtocol();
					}
				},
				stopProtocol:	() => MainWindow?.EphysStream?.StopProtocol(),
				stopRecording:	() => 
				{
					MainWindow?.AudioStream?.Stop();
					MainWindow?.EphysStream?.StopStream();
					MainWindow?.EphysStream?.StopRecording();
				}); 
		}

		/// <summary>
		/// Dispose method.
		/// </summary>
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			Close();
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocolPath"></param>
		/// <param name="outputPath"></param>
		/// <param name="durations"></param>
		public void GetParameters(out string protocolPath, out string outputPath, out string datasetName, out (int Pre, int Post) durations)
		{
			if (!Dispatcher.CheckAccess())
			{
				string pp		= null;
				string op		= null;
				string dn		= null;
				(int, int) dur	= default;

				Dispatcher.BeginInvoke(new Action(() => 
				{
					pp	= txbProtoPath.Text;
					op	= txbOutputPath.Text;
					dn	= GetDatasetName();
					dur = (ntxbPrerunDuration.IntegerValue, ntxbPostrunDuration.IntegerValue);
				})).Wait();

				protocolPath	= pp;
				outputPath		= op;
				datasetName		= dn;
				durations		= dur;
			}
			else
			{
				protocolPath	= txbProtoPath.Text;
				outputPath		= txbOutputPath.Text;
				datasetName		= GetDatasetName();
				durations		= (ntxbPrerunDuration.IntegerValue, ntxbPostrunDuration.IntegerValue);
			}

			outputPath = Path.Combine(outputPath, datasetName);
		}

		/// <summary>
		/// Notify the wizard of the occurence of a new block.
		/// </summary>
		public void NotifyNewBlock()
		{
			if (_stateMachine.CurrentState == WizardState.Idle)
				return;

			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action(NotifyNewBlock));
			else
				_stateMachine.ProcessEvent(WizardEvent.NewBlock);
		}

		/// <summary>
		/// Notify the wizard of the occurence of a new block.
		/// </summary>
		public void NotifyProtocolEnded()
		{
			if (_stateMachine.CurrentState == WizardState.Idle)
				return;

			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action(NotifyProtocolEnded));
			else
				_stateMachine.ProcessEvent(WizardEvent.ProtocolEnded);
		}

		/// <summary>
		/// Parent main window.
		/// </summary>
		public MainWindow MainWindow { get; protected init; }

		/// <summary>
		/// 
		/// </summary>
		public bool ShowOnProtocolFinish
		{
			get => ckbHide.IsChecked == true;
			set => ckbHide.IsChecked = value;
		}

		/// <summary>
		/// Check whether the protocol wizard is executing.
		/// </summary>
		public bool IsExecuting => _stateMachine.CurrentState != WizardState.Idle;

		/// <summary>
		/// Protocol wizard state.
		/// </summary>
		public enum WizardState
		{
			Idle,
			Prerun,
			Running,
			Postrun
		}

		/// <summary>
		/// Protocol wizard events.
		/// </summary>
		public enum WizardEvent
		{
			Start,
			NewBlock,
			ProtocolEnded,
			Cancel
		}

		/// <summary>
		/// State machine for the protocol wizard.
		/// </summary>
		public class ProtocolWizardStateMachine
			: StateMachine<WizardState, WizardEvent>
		{
			/// <summary>
			/// Create a protocol wizard state machine.
			/// </summary>
			/// <param name="parent">The parent wizard.</param>
			public ProtocolWizardStateMachine(ProtocolWizard parent)
			{
				_p = parent;
				_s = parent?.MainWindow?.EphysStream;
			}

			/// <summary>
			/// Set the actions of the state machine.
			/// </summary>
			/// <param name="startRecording"></param>
			/// <param name="startProtocol"></param>
			/// <param name="stopProtocol"></param>
			/// <param name="stopRecording"></param>
			public void SetActions(
				Action<DataOutputEventArgs> startRecording  = null,
				Action<string>				startProtocol   = null,
				Action						stopProtocol    = null,
				Action						stopRecording   = null)
			{
				_startRecording = startRecording;
				_startProtocol  = startProtocol;
				_stopProtocol   = stopProtocol;
				_stopRecording  = stopRecording;
			}

			/// <summary>
			/// Configure the state machine.
			/// </summary>
			protected override void ConfigureStates()
			{
				// IDLE
				AddState(WizardState.Idle,
					eventAction: (e) =>
					{
						if (e is WizardEvent.Start)
							return WizardState.Prerun;
						return CurrentState;
					});

				// PRERUN
				AddState(WizardState.Prerun,
					eventAction: (e) => e switch
					{
						WizardEvent.NewBlock    => Elapse(WizardState.Running),
						WizardEvent.Cancel      => Stop(),
						_                       => CurrentState
					},
					enterStateAction: () =>
					{
						// load parameters and start recording
						_p.GetParameters(out _protocolPath, out var datasetDirectory, out var datasetName, out _timeouts);
						_currentStateTimeout = _timeouts.Pre;

						_startRecording?.Invoke(new() 
						{
							DatasetDirectory	= datasetDirectory,
							DatasetName			= datasetName
						});
					});

				// RUNNING
				AddState(WizardState.Running,
					eventAction: (e) => e switch
					{
						WizardEvent.ProtocolEnded	=> WizardState.Postrun,
						WizardEvent.Cancel			=> Stop(),
						_							=> CurrentState
					},
					enterStateAction:	() => _startProtocol?.Invoke(_protocolPath),
					exitStateAction:	() => _stopProtocol?.Invoke());

				// POSTRUN
				AddState(WizardState.Postrun,
					eventAction: (e) => e switch
					{
						WizardEvent.NewBlock    => Elapse(WizardState.Idle),
						WizardEvent.Cancel      => WizardState.Idle,
						_						=> CurrentState
					},
					enterStateAction:	() => _currentStateTimeout = _timeouts.Post,
					exitStateAction:	() => Stop());
			}

			/// <summary>
			/// 
			/// </summary>
			/// <param name="onTimeoutElapsed"></param>
			/// <returns></returns>
			protected WizardState Elapse(WizardState onTimeoutElapsed)
			{
				if (_currentStateTimeout == 0)
					return onTimeoutElapsed;
				--_currentStateTimeout;
				return CurrentState;
			}

			/// <summary>
			/// 
			/// </summary>
			protected WizardState Stop()
			{
				_stopRecording?.Invoke();
				if (_p.ShowOnProtocolFinish)
					_p.Visibility = Visibility.Visible;
				App.MessageBoxAsync("Protocol run complete!", "Complete!");
				return WizardState.Idle;
			}

			protected ProtocolWizard				_p;
			protected EphysTerminal					_s;
			protected int							_currentStateTimeout;

			protected (int Pre, int Post)			_timeouts;
			protected string						_protocolPath;

			protected Action<DataOutputEventArgs>	_startRecording;
			protected Action<string>				_startProtocol;
			protected Action						_stopProtocol;
			protected Action						_stopRecording;
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnProtoBrowse_Click(object sender, RoutedEventArgs e)
		{
			_ = sender;
			_ = e;

			// open a file dialog
			var ofd = new OpenFileDialog()
			{
				InitialDirectory	= @"C:\_code\ephysstream\settings\protocols",
				Filter				= "Protocol files (*.json) | *.json",
				Multiselect			= false
			};

			// validate input path
			if (ofd.ShowDialog() == true && File.Exists(ofd.FileName))
			{
				txbProtoPath.Text		= ofd.FileName;
				txbProtoName.Text		= Path.GetFileNameWithoutExtension(ofd.FileName);
				lblPrerunSec.Content	= $"x {MainWindow?.EphysStream?.Settings.Input.PollingPeriod} seconds";
				lblPostrunSec.Content	= $"x {MainWindow?.EphysStream?.Settings.Input.PollingPeriod} seconds";
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnOutputBrowse_Click(object sender, RoutedEventArgs e)
		{
			_ = sender;
			_ = e;

			// open a file dialog
			var sfd = new SaveFileDialog()
			{
				InitialDirectory	= @"C:\_code\ephysstream\settings",
				FileName			= "Select the output folder."
			};

			// validate input path
			if (sfd.ShowDialog() == true)
			{
				var outputDir = Path.GetDirectoryName(sfd.FileName);
				txbOutputPath.Text = outputDir;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRun_Click(object sender, RoutedEventArgs e)
		{
			// hide interface if requested
			if (ShowOnProtocolFinish)
				Visibility = Visibility.Hidden;

			_stateMachine.ProcessEvent(WizardEvent.Start);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel	= !_disposed;
			Visibility	= Visibility.Hidden;
		}

		/// <summary>
		/// Create the name of the output dataset.
		/// </summary>
		/// <returns>The name of a dataset.</returns>
		protected string GetDatasetName()
			=> $"{txbAnimalName.Text}_{txbProtoName.Text}_{ntxbDatasetID.IntegerValue}";

		/// <summary>
		/// 
		/// </summary>
		protected bool ProtocolSet 
			=> !string.IsNullOrWhiteSpace(txbProtoName.Text);
		
		/// <summary>
		/// 
		/// </summary>
		protected bool OutputSet 
			=> !string.IsNullOrWhiteSpace(txbOutputPath.Text);
		
		/// <summary>
		/// 
		/// </summary>
		protected bool DatasetNameSet 
			=> !(	string.IsNullOrWhiteSpace(txbAnimalName.Text)	|| 
					string.IsNullOrWhiteSpace(txbProtoName.Text)	|| 
					string.IsNullOrWhiteSpace(ntxbDatasetID.Text));


		protected ProtocolWizardStateMachine	_stateMachine;
		private bool							_disposed		= false;
	}
}
