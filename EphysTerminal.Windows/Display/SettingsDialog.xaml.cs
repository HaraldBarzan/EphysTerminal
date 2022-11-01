using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TINS.Terminal.Protocols.Genus;

namespace TINS.Terminal.Display
{
	/// <summary>
	/// Interaction logic for SettingsDialog.xaml
	/// </summary>
	public partial class SettingsDialog : Window
	{
		/// <summary>
		/// 
		/// </summary>
		public SettingsDialog(MainWindow mainWindow)
		{
			InitializeComponent();
			ParentWindow = mainWindow;
		}

		public MainWindow ParentWindow { get; init; }

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

				// create, connect to and change the parameters of the Genus device
				var ctl = new GenusController();
				try
				{
					ctl.Connect();
					ctl.ChangeParameters(freq, freq, freq, null, trig);
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
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = true;
			Visibility = Visibility.Hidden;
		}
	}
}
