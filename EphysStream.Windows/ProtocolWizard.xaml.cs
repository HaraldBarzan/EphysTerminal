using CircuitGENUS.Windows;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
			MainWindow = mainWindow;
			_stateMachine = new ProtocolWizardStateMachine(this);
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
		public void GetParameters(out string protocolPath, out string outputPath, out (int Pre, int Post) durations)
		{
			if (!Dispatcher.CheckAccess())
			{
				string pp		= null;
				string op		= null;
				(int, int) dur	= default;
				Dispatcher.BeginInvoke(new Action(() => 
				{
					pp	= txbProtoPath.Text;
					op	= Path.Combine(txbOutputPath.Text, GetDatasetName() + ".epd");
					dur = (GetInt(txbPrerunDuration), GetInt(txbPostrunDuration));
				})).Wait();
				protocolPath	= pp;
				outputPath		= op;
				durations		= dur;
			}
			else
			{
				protocolPath	= txbProtoPath.Text;
				outputPath		= Path.Combine(txbOutputPath.Text, GetDatasetName() + ".epd");
				durations		= (GetInt(txbPrerunDuration), GetInt(txbPostrunDuration));
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="startRecording"></param>
		/// <param name="startProtocol"></param>
		/// <param name="stopProtocol"></param>
		/// <param name="stopRecording"></param>
		public void SetActions(
			Action<string>  startRecording   = null,
			Action<string>	startProtocol    = null,
			Action          stopProtocol     = null,
			Action          stopRecording    = null)
		{
			_stateMachine.SetActions(startRecording, startProtocol, stopProtocol, stopRecording);
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
				InitialDirectory	= @"C:\_code\CircuitGENUS\Settings",
				Filter				= "Protocol files (*.json) | *.json",
				Multiselect			= false
			};

			// validate input path
			if (ofd.ShowDialog() == true && File.Exists(ofd.FileName))
			{
				txbProtoPath.Text = ofd.FileName;
				txbProtoName.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
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
				InitialDirectory	= @"C:\_code\CircuitGENUS\Settings",
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
		/// Use to filter for numerical values.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void FilterNumeric(object sender, KeyEventArgs e)
		{
			_ = sender;

			int key = (int)e.Key;
			e.Handled = !(	Numerics.IsClamped(key, ((int)Key.D0,		(int)Key.D9))		||	// key is on alphanumeric keyboard
							Numerics.IsClamped(key, ((int)Key.NumPad9,	(int)Key.NumPad9))	||	// key is on num pad
							key == (int)Key.Back);												// key is backspace
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
			=> $"{txbAnimalName.Text}_{txbProtoName.Text}_{txbDatasetID.Text}";

		/// <summary>
		/// Get an integer from a textbox.
		/// </summary>
		/// <param name="txb">The textbox.</param>
		/// <returns>An integer.</returns>
		protected static int GetInt(TextBox txb) => int.TryParse(txb.Text, out int i) ? i : default; 

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
					string.IsNullOrWhiteSpace(txbDatasetID.Text));


		protected ProtocolWizardStateMachine _stateMachine;
		private bool _disposed = false;
	}
}
