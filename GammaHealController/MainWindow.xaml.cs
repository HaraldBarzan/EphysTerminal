using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Media;
using TINS;
using TINS.Terminal.Protocols.Genus;

namespace GammaHealController
{
	using Instr = GenusController.Instruction;

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		/// <summary>
		/// 
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Reset all icons to default.
		/// </summary>
		private void ResetAll()
		{
			_leftLedOn			= false;
			_rightLedOn			= false;
			_audioOn			= false;
			btnLeftLED.Source	= App.GetResource<ImageSource>("PlayIcon");
			btnRightLED.Source	= App.GetResource<ImageSource>("PlayIcon");
			btnAudio.Source		= App.GetResource<ImageSource>("PlayIcon");
			_controller			?.SendInstruction(Instr.Reset());
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		private bool QueryController()
		{
			if (_controller is null)
			{
				if (SerialPort.GetPortNames().Length == 0)
					return false;

				_controller = new GenusController();
				_controller.Connect();
				return true;
			}
			else
				return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (QueryController())
				_controller.Reset();
			Environment.Exit(0);
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnLeftLED_Click(object sender, RoutedEventArgs e)
		{
			if (!QueryController())
			{
				ResetAll();
				return;
			}


			if (_leftLedOn)
			{
				_controller.SendInstruction(Instr.StopLedFlickerLeft());
				_leftLedOn = false;
				btnLeftLED.Source = App.GetResource<ImageSource>("PlayIcon");
			}
			else
			{
				if (float.TryParse(ntbLeftLED.Text, out var freq) &&
					Numerics.IsClamped(freq, (0, 100)))
				{
					_controller.SendInstruction(Instr.StartLedFlickerLeft(freq));
					_leftLedOn = true;
					btnLeftLED.Source = App.GetResource<ImageSource>("StopIcon");
				}
				else
					MessageBox.Show("Please provide a numerical value between 0 and 100.");
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRightLED_Click(object sender, RoutedEventArgs e)
		{
			if (!QueryController())
			{
				ResetAll();
				return;
			}

			if (_rightLedOn)
			{
				_controller.SendInstruction(Instr.StopLedFlickerRight());
				_rightLedOn = false;
				btnRightLED.Source = App.GetResource<ImageSource>("PlayIcon");
			}
			else
			{
				if (float.TryParse(ntbRightLED.Text, out var freq) &&
					Numerics.IsClamped(freq, (0, 100)))
				{
					_controller.SendInstruction(Instr.StartLedFlickerRight(freq));
					_rightLedOn = true;
					btnRightLED.Source = App.GetResource<ImageSource>("StopIcon");
				}
				else
					MessageBox.Show("Please provide a numerical value between 0 and 100.");
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnAudio_Click(object sender, RoutedEventArgs e)
		{
			if (!QueryController())
			{
				ResetAll();
				return;
			}

			if (_audioOn)
			{
				_controller.SendInstruction(Instr.StopAudioFlicker());
				_audioOn = false;
				btnAudio.Source = App.GetResource<ImageSource>("PlayIcon");
			}
			else
			{
				if (float.TryParse(ntbAudio.Text, out var freq) &&
					Numerics.IsClamped(freq, (0, 100)))
				{
					_controller.SendInstruction(Instr.StartAudioFlicker(freq));
					_audioOn = true;
					btnAudio.Source = App.GetResource<ImageSource>("StopIcon");
				}
				else
					MessageBox.Show("Please provide a numerical value between 0 and 100.");
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnTriggers_Click(object sender, RoutedEventArgs e)
		{
			if (!QueryController())
			{
				ResetAll();
				return;
			}

			if (int.TryParse(ntbTriggers.Text, out var triggerValue) &&
				Numerics.IsClamped(triggerValue, (0, 63)))
			{
				_controller.EmitTrigger((byte)triggerValue);
			}
			else
				MessageBox.Show("Please provide an integer numerical value between 0 and 63.");
		}



		protected GenusController	_controller		= null;
		protected bool				_leftLedOn		= false;
		protected bool				_rightLedOn		= false;
		protected bool				_audioOn		= false;
	}
}
