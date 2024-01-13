using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using TeensyNet;
using TINS;
using TINS.Analysis.Transforms;
using TINS.Ephys.Analysis.Events;
using TINS.Ephys.Data;
using TINS.Ephys.Settings;
using TINS.Filtering;
using TINS.Terminal.Protocols.Genus;

namespace GammaHealController
{
	using Instr = GenusController.Instruction;

	/// <summary>
	/// Interaction logic for ClosedLoopWindow.xaml
	/// </summary>
	public partial class ClosedLoopWindow 
		: Window, IDisposable
	{
		const float SamplingFrequency	= 32000;
		const float PollingPeriod		= 1;
		const int DecimationFactor		= 32;


		/// <summary>
		/// 
		/// </summary>
		public ClosedLoopWindow()
		{
			InitializeComponent();

			_lowPass		= new IIRFilter(IIRFilterType.Butterworth, FilterPass.Lowpass, 3, SamplingFrequency, 300);
			_highPass		= new IIRFilter(IIRFilterType.Butterworth, FilterPass.Highpass, 3, SamplingFrequency / DecimationFactor, 0.1);
			_downsampled	= new Vector<float>(Numerics.Floor(SamplingFrequency * PollingPeriod / DecimationFactor));
			_superlet		= new SuperletTransform((5, 100), 96, SamplingFrequency / DecimationFactor, 3, (2, 10), _downsampled.Size, 2);
			_eventFinder	= new LineEventFinder();
		}

		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			_controller	?.SendInstruction(Instr.Reset());
			_controller	?.Dispose();
			_daq		?.Dispose();
			_superlet	?.Dispose();
			_downsampled?.Dispose();
			_recorder	?.Dispose();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void _this_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue is true)
			{
				RefreshController();
				RefreshDaq();

				if (_controller is not null && _daq is not null)
					lblStatus.Content = "Ready!";
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRefreshCtl_Click(object sender, RoutedEventArgs e)
		{
			RefreshController();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRefreshDaq_Click(object sender, RoutedEventArgs e)
		{
			RefreshDaq();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnStream_Click(object sender, RoutedEventArgs e)
		{
			if (_daq is null)
			{
				MessageBox.Show("No NI-DAQmx device present. Press refresh and try again.");
				return;
			}

			if (_daq.Status is DataStreamStatus.Running)
			{
				_daq.StopAcquisition();
				btnStream.Source	= App.GetResource<ImageSource>("PlayIcon");
				btnStream.Text		= "Start streaming";
			}
			else if (_daq.Status is DataStreamStatus.Idle)
			{
				_daq.StartAcquisition();
				btnStream.Source	= App.GetResource<ImageSource>("StopIcon");
				btnStream.Text		= "Stop streaming";
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRecord_Click(object sender, RoutedEventArgs e)
		{
			if (_recorder is null)
			{
				var ofd = new SaveFileDialog()
				{
					Filter = "EEG Processor Dataset (*.epd)|*.epd",
				};
				if (ofd.ShowDialog() == true)
				{
					_recorder = new DataOutputStream(
						outputFolder:	Path.GetDirectoryName(ofd.FileName),
						datasetName:	Path.GetFileNameWithoutExtension(ofd.FileName),
						samplingRate:	SamplingFrequency,
						channelLabels:	new Vector<string> { "AnalogData" });

					_recorder.StartThreadAsync();
					btnRecord.Source	= App.GetResource<ImageSource>("RecordOnIcon");
					btnRecord.Text		= "Stop recording";
				}
			}
			else
			{
				_recorder.Dispose();
				_recorder = null;
				btnRecord.Source	= App.GetResource<ImageSource>("RecordOffIcon");
				btnRecord.Text		= "Start recording";
			}
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
					lblStatusCtl.Content = "No devices connected.";
					return;
				}

				try
				{
					_controller = new GenusController();
					_controller.FeedbackReceived += _controller_FeedbackReceived;
					_controller.Connect(teensyPort);
					_controller.SendInstruction(Instr.SetTriggerPin(true));
					lblStatusCtl.Content = $"{teensyName} on port {teensyPort}.";
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message);
					lblStatusCtl.Content = "No devices connected.";
					_controller?.Dispose();
					_controller = null;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		private void RefreshDaq()
		{
			if (_daq is not null)
			{
				_daq.Dispose();
				_daq = null;
			}

			if (_daq is null)
			{
				var daqSet = new DAQmxInputSettings()
				{
					DeviceName				= txbDaqName.Text.Trim(),
					DigitalPortName			= txbDaqDigitalPort.Text.Trim(),
					UseDigitalInput			= (bool)ckbUseDigitalPort.IsChecked,
					ChannelLabels			= new() { "A1" },
					EffectiveChannelLabels	= new() { "A1" },
					SamplingRate			= SamplingFrequency,
					PollingPeriod			= PollingPeriod
				};

				try
				{
					_daq = new DAQmxStream(daqSet);
					_daq.DataAvailable += _daq_DataAvailable;
					_daq.StartThreadAsync();

					lblStatusDaq.Content = $"{daqSet.DeviceName} connected!";
				}
				catch (Exception ex) 
				{
					MessageBox.Show(ex.Message);
					lblStatusDaq.Content = $"No NI DAQmx device with name \'{daqSet.DeviceName}\' connected.";
					_daq?.Dispose();
					_daq = null;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void _daq_DataAvailable(object sender, ReadingFrame e)
		{
			// save data if necessary
			if (_recorder is not null)
			{
				_eventFinder.FindEvents(e.DigitalInput);
				if (_eventFinder.FoundEventCount > 0)
					_recorder.WriteEvents(_eventFinder.FoundEvents, MarkerAlignment.CurrentSweep);
				_recorder.WriteData(e.AnalogInput);
			}

			// do processing if needed
			if (_closedLoopControl && _controller is not null)
			{
				// perform decimation and filtering
				_lowPass.ForwardFilter(e.AnalogInput.GetSpan(), e.AnalogInput.GetSpan());
				
				Algorithms.Decimate(e.AnalogInput, 1, 32, result: _downsampled);
				_highPass.ForwardFilter(_downsampled, _downsampled);

				// perform superlet transform and find frequency with highest power
				_superlet.Analyze(_downsampled);
				float stimFreq = PickBestFrequency(_superlet.Output);

				// send the power back to the stimulation device
				if (_closedLoopControl)
				{
					lock (_controller)
						_controller.SendInstruction(Instr.StartFlicker(stimFreq));
					Dispatcher.BeginInvoke(() =>
					{
						lblStatus.Content = $"New data frame received at {DateTime.Now} - adjusting frequency to {stimFreq}.";
					});
				}
			}
			else
			{
				Dispatcher.BeginInvoke(() =>
				{
					lblStatus.Content = $"New data frame received at {DateTime.Now}.";
				});
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		/// <exception cref="NotImplementedException"></exception>
		private void _controller_FeedbackReceived(object sender, GenusController.Feedback e)
		{
			using var instructions = new Vector<Instr>();

			Dispatcher.BeginInvoke(() =>
			{
				if (e is GenusController.Feedback.TriggerPinRise)
				{
					bool start = false;
					if (float.TryParse(ntbInitialFreq.Text, out var initialFreq) &&
						Numerics.IsClamped(initialFreq, (5, 100)))
					{
						instructions.PushBack(Instr.StartFlicker(initialFreq));
						start = true;
					}

					if (byte.TryParse(ntbStartTrigger.Text, out var startTrigger) &&
						Numerics.IsClamped(startTrigger, (1, 63)))
					{
						instructions.PushBack(Instr.EmitTrigger(startTrigger));
					}

					if (start)
					{
						_closedLoopControl = true;
						_controller.SendInstructionList(instructions);
					}
				}
				else if (e is GenusController.Feedback.TriggerPinFall)
				{
					_closedLoopControl = false;

					instructions.PushBack(Instr.StopFlicker());
					if (byte.TryParse(ntbEndTrigger.Text, out var endTrigger) &&
						Numerics.IsClamped(endTrigger, (1, 63)))
					{
						instructions.PushBack(Instr.EmitTrigger(endTrigger));
					}

					lock (_controller)
					{
						_controller.SendInstructionList(instructions);
					}
				}
			});
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="spectrum"></param>
		/// <returns></returns>
		private float PickBestFrequency(Spectrum2D spectrum)
		{
			int maxPowRow	= 0;
			float maxPowSum = 0;
			for (int i = 0; i < spectrum.Rows; ++i)
			{
				float powSum = 0;
				for (int j = 0; j < spectrum.Cols; ++j)
					powSum += _superlet.Output[i, j];
				if (powSum > maxPowSum)
				{
					maxPowSum = powSum;
					maxPowRow = i;
				}
			}

			return spectrum.BinToFrequency(maxPowRow);
		}


		protected bool				_closedLoopControl;
		protected DAQmxStream		_daq;
		protected GenusController	_controller;
		protected IIRFilter			_lowPass;
		protected IIRFilter			_highPass;
		protected SuperletTransform _superlet;
		protected Vector<float>		_downsampled;
		protected DataOutputStream	_recorder;
		protected LineEventFinder	_eventFinder;
	}
}
