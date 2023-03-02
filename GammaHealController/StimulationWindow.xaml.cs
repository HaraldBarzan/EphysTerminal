using System;
using System.Windows;
using TeensyNet;
using TINS;
using TINS.Terminal.Protocols.Genus;

namespace GammaHealController
{
	using Instr = GenusController.Instruction;

	/// <summary>
	/// Interaction logic for StimulationWindow.xaml
	/// </summary>
	public partial class StimulationWindow 
		: Window, IDisposable
	{
		/// <summary>
		/// 
		/// </summary>
		public StimulationWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			_controller?.Dispose();
			_controller = null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			_controller?.Dispose();
			_controller = null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void _this_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue is true)
				RefreshController();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRefresh_Click(object sender, RoutedEventArgs e)
		{
			RefreshController();
		}

		/// <summary>
		/// 
		/// </summary>
		private void RefreshController()
		{
			if (_controller is not null)
			{
				_controller.SendInstruction(Instr.Reset());
				_controller.Dispose();
				_controller = null;
			}

			if (_controller is null)
			{
				var teensyPort = string.Empty;
				var teensyName = string.Empty;

				using var factory = new TeensyFactory();
				factory.EnumTeensies((t) =>
				{
					teensyName = t.Name;
					teensyPort = t.PortName;
					return true;
				});

				if (string.IsNullOrEmpty(teensyPort))
				{
					lblStatus.Content = "No devices connected.";
					return;
				}

				_controller = new GenusController();
				_controller.FeedbackReceived += _controller_FeedbackReceived;
				_controller.Connect(teensyPort);
				_controller.SendInstruction(Instr.SetTriggerPin(true));
				lblStatus.Content = $"{teensyName} on port {teensyPort}.";
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void _controller_FeedbackReceived(object sender, GenusController.Feedback e)
		{
			if (e is GenusController.Feedback.TriggerPinRise)
			{
				if (float.TryParse(ntbFrequency.Text, out var frequency))
				{
					frequency = Numerics.Clamp(frequency, GenusController.FlickerFrequencyRange);
					_controller.SendInstruction(Instr.StartFlicker(frequency));
				}
			}
			else if (e is GenusController.Feedback.TriggerPinFall) 
			{
				_controller.SendInstruction(Instr.StopFlicker());
			}
		}


		protected GenusController _controller;
	}
}
