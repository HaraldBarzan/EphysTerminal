using Microsoft.Win32;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TINS.Data.EPD;
using TINS.Filtering;
using TINS.IO;
using TINS.Terminal.Display.Protocol;
using TINS.Terminal.Protocols.Genus;

namespace TINS.Terminal.Display
{
	/// <summary>
	/// Interaction logic for SettingsDialog.xaml
	/// </summary>
	public partial class SettingsDialog : System.Windows.Window
	{
		/// <summary>
		/// 
		/// </summary>
		public SettingsDialog(MainWindow mainWindow)
		{
			InitializeComponent();
			ParentWindow = mainWindow;

			// setup genus
			GenusRefreshPorts();
			if (cmbPort.Items.Count > 0)
				cmbPort.SelectedIndex = 0;
		}

		/// <summary>
		/// 
		/// </summary>
		public MainWindow ParentWindow { get; init; }

		/// <summary>
		/// 
		/// </summary>
		public string SelectedPortGenus { get; protected set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnGenusTest_Click(object sender, RoutedEventArgs e)
		{
			if (ParentWindow.EphysTerminal is null)
			{
				MessageBox.Show("No configuration has been loaded.");
				return;
			}

			// parse the data
			if (float.TryParse(txbGenusFreq.Text, out var freq) &&
				byte.TryParse(txbGenusTrig.Text, out var trig))
			{
				if (!Numerics.IsClamped(freq, (0, 100)))
				{
					MessageBox.Show("Could not parse Genus parameters.");
					return;
				}

				// refresh the ports
				GenusRefreshPorts();
				if (cmbPort.Items.Count == 0)
				{
					MessageBox.Show("No devices connected.");
					return;
				}


				// create, connect to and change the parameters of the Genus device
				GenusController ctl = null;
				try
				{
					ctl = new();
					ctl.Connect(SelectedPortGenus);
					ctl.ChangeParameters(freq, null, freq, 10000, trig);
				}
				catch (Exception exc)
				{
					MessageBox.Show($"Could not connect to Genus device: \"{exc.Message}\".");
				}
				finally
				{
					ctl?.Dispose();
				}
			}
			else
				MessageBox.Show("Could not parse Genus parameters.");
		}

		/// <summary>
		//
		/// </summary>
		private void GenusRefreshPorts()
		{
			int oldSelectedIndex = cmbPort.SelectedIndex;

			cmbPort.Items.Clear();
			foreach (var p in SerialPort.GetPortNames())
				cmbPort.Items.Add(p);

			if (oldSelectedIndex > -1)
				cmbPort.SelectedIndex = oldSelectedIndex;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = true;
			Visibility = Visibility.Hidden;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void cmbPort_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			SelectedPortGenus = cmbPort.SelectedIndex > -1 ? cmbPort.SelectedValue as string : null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnGFP_Click(object sender, RoutedEventArgs e)
		{
			var ofd = new OpenFileDialog()
			{
				Filter		= "EEG Processor files (*.epd) | *.epd",
				Multiselect = false
			};

			if (ofd.ShowDialog() == true)
			{
				try
				{
					// open dataset
					var name	= System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
					var ds		= new Dataset(ofd.FileName);

					// load everything into memory (will except if OOM)
					using var allData = new Matrix<float>(ds.ChannelCount, ds.RecordingLength);
					Parallel.For(0, allData.Rows, (i) =>
					{
						var chBuffer = allData.GetBuffer(i);
						using (var io = new IOStream(ds.GetChannelPath(ds.Channels[i])))
							io.Read(chBuffer);

						var filt1 = new IIRFilter(IIRFilterType.Butterworth, FilterPass.Bandpass, 3, ds.SamplingRate, (0.1, 150));
						var filt2 = new IIRFilter(IIRFilterType.Butterworth, FilterPass.Bandstop, 3, ds.SamplingRate, (49.5, 50.5));
						filt1.BidirectionalFilter(chBuffer, chBuffer);
						filt2.BidirectionalFilter(chBuffer, chBuffer);
					});

					// compute GFP
					using var gfp	= new Vector<float>(allData.ColWise().StandardDeviation());
					using var io	= new IOStream(System.IO.Path.Combine(ds.Directory, name + "-GFP.bin"), System.IO.FileAccess.Write);
					io.Write(gfp);
				}
				catch (Exception exc)
				{
					MessageBox.Show($"Could not perform operation: \"{exc.Message}\".");
				}
			}
		}

		
	}
}
