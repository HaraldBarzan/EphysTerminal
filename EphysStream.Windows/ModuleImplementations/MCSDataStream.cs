using Mcs.Usb;
using System;
using TINS.Ephys.Settings;
using TINS.MatrixImpl;


namespace TINS.Ephys.Data
{
	/// <summary>
	/// Streams data from the MCS MEA device.
	/// </summary>
	public class MCSDataStream
		: DataInputStream
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		public MCSDataStream(EphysSettings settings, int ringBufferSize = 3)
			: base(settings, ringBufferSize)
		{
			// number of polls per second
			int pollingRate = Numerics.Round(1 / settings.Input.PollingPeriod);

			// attempt to connect to the device
			Connect(settings.ChannelCount, Numerics.Round(settings.SamplingRate), pollingRate, useDigitalChannel: true);
		}

		/// <summary>
		/// 
		/// </summary>
		~MCSDataStream() => Dispose(false);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (_disposed) return;

			DestroyDevice();
			if (disposing)
			{
				_channelReadBuffer?.Dispose();
			}

			_disposed = true;
		}

		/// <summary>
		/// Attempt to connect to a USB-ME64 device.
		/// </summary>
		/// <param name="channelCount"></param>
		/// <param name="samplingRate"></param>
		/// <param name="pollingRate"></param>
		/// <param name="usbDeviceIndex"></param>
		/// <param name="useDigitalChannel"></param>
		/// <param name="useChecksumChannels"></param>
		/// <returns></returns>
		public void Connect(
			int		channelCount,
			int		samplingRate,
			int		pollingRate,
			int		usbDeviceIndex		= 0,
			bool	useDigitalChannel	= false,
			bool	useChecksumChannels = false)
		{
			// disconnect currently attached device if present
			if (IsConnected)
				DestroyDevice();

			// check entry list
			UpdateMCSDeviceList();
			if (MCSMEADeviceList.Count == usbDeviceIndex)
				throw new Exception("Device list is empty.");

			// create the new device (with channel data methods)
			_device = new CMeaDeviceNet(MCSMEADeviceList.GetUsbListEntry((uint)usbDeviceIndex).DeviceId.BusType, OnChannelData, OnError);

			// attempt connection
			if (_device.Connect() != 0)
				throw new Exception("Could not connect to device.");

			// sanity check
			_device.SendStopDacq(); 

			// configure the channels
			_device.HWInfo().GetNumberOfHWADCChannels(out int supportedAnalogChannels);
			if (supportedAnalogChannels == 0) supportedAnalogChannels = 64;
			_selectedChannels = Numerics.Clamp(channelCount, (0, supportedAnalogChannels));
			_device.SetNumberOfChannels(_selectedChannels);

			// set digital input
			_useDigitalInput = useDigitalChannel;
			_device.EnableDigitalIn(_useDigitalInput, 0);

			// set checksum channels
			_useChecksumChannels = useChecksumChannels;
			_device.EnableChecksum(_useChecksumChannels, 0);

			// set sampling rate
			_samplingRate = samplingRate;
			_device.SetSamplerate(_samplingRate, 1, 0);

			// set the voltage range
			_voltageRange = _device.GetVoltageRangeInMilliVolt();
			//Gain = _device.GetGain() / 1000;
			Gain = _device.GetGain();

			// get the layout and confirm data selection
			_device.GetChannelLayout(out int analogChannels, out int digitalChannels, out _, out _, out int blockSize, 0);
			_channelBlockSize = _samplingRate / pollingRate;
			_selectedChannels = blockSize;
			_channelReadBuffer.Resize((_selectedChannels + (_useDigitalInput ? 1 : 0)) * _channelBlockSize);
			_device.SetSelectedData(_selectedChannels, _samplingRate, _channelBlockSize, SampleSizeNet.SampleSize16Unsigned, blockSize);

			// suggest a size to the data inputs in the ring
			foreach (var input in _ringBuffer)
				input.ResizeFrame(ChannelCount, SamplesPerBlock);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected override DataStreamError ConnectStream()
		{
			if (_device is null || !_device.IsConnected())
				return DataStreamError.ConnectionFailure;

			// start digital acquisition
			_device.StartDacq();
			Status = DataStreamStatus.Running;

			// raise start event
			RaiseAcqusitionStarted();

			return DataStreamError.None;
		}

		/// <summary>
		/// 
		/// </summary>
		protected override void DisconnectStream()
		{
			if (_device is object && _device.IsConnected())
			{
				_device.StopDacq();
				Status = DataStreamStatus.Idle;

				// raise end event
				RaiseAcquistionEnded();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public bool IsConnected { get; }

		/// <summary>
		/// 
		/// </summary>
		public bool IsStreaming { get; }

		/// <summary>
		/// 
		/// </summary>
		public int Gain { get; protected set; }

		/// <summary>
		/// 
		/// </summary>
		protected void DestroyDevice()
		{
			if (_device is object)
			{
				_device.StopDacq();
				_device.Disconnect();
				_device.Dispose();
				_device = null;
			}
		}

		/// <summary>
		/// Channel data method for the MEA device.
		/// </summary>
		/// <param name="dacq">Digital acquisition device object.</param>
		/// <param name="channelBlockHandle">The handle of the current channel (not needed).</param>
		/// <param name="sampleCount">The number of samples the method has been invoked with.</param>
		protected void OnChannelData(CMcsUsbDacqNet dacq, int channelBlockHandle, int sampleCount)
		{
			// ignore all these parameters (we already have them)
			_ = dacq;
			_ = channelBlockHandle;
			_ = sampleCount;

			// read the newly acquired frames
			var data = MatrixImplProvider<ushort>.Get().GetMatrixArray(_channelReadBuffer);
			_device.ChannelBlock_ReadFramesUI16(0, data, 0, _channelBlockSize, out int frameCount);

			// check termination criteria
			if (Status == DataStreamStatus.TerminationPending)
			{
				Status = DataStreamStatus.Idle;
				DisconnectStream();
				return;
			}

			// get the data input
			var dataInput		= _ringBuffer.Current;
			var analogData		= dataInput.AnalogInput;
			var digitalInput	= dataInput.DigitalInput;
			_ringBuffer.RotateRight(1);

			// compute scaling coefficients
			float voltageOffset = -_voltageRange;
			float voltageScale	= 2f * _voltageRange / ushort.MaxValue;

			// determine the number of actual channels
			int analogChannels	= ChannelCount;
			int totalChannels	= ChannelCount + (_useDigitalInput ? 1 : 0);

			lock (dataInput)
			{
				// resize the newly acquired data frame
				dataInput.ResizeFrame(analogChannels, frameCount);

				// get the analog input stream
				for (int iCh = 0; iCh < analogChannels; ++iCh)
				{
					for (int iFrame = 0; iFrame < frameCount; ++iFrame)
					{
						analogData[iCh, iFrame] = data[iFrame * totalChannels + iCh] * voltageScale - _voltageRange;
					}
				}

				// get the digital input stream
				if (_useDigitalInput)
				{
					for (int iFrame = 0; iFrame < frameCount; ++iFrame)
					{
						digitalInput[iFrame] = data[iFrame * totalChannels + analogChannels];
					}
				}
			}

			// raise new data
			RaiseDataAvailable(dataInput);
		}

		/// <summary>
		/// Raised when the digital acquisition device encounters an error.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="info">Error information.</param>
		protected void OnError(string message, int info)
			=> throw new Exception($"USB-ME64 device has thrown an exception with the following message: {message} (info: {info}).");

		/// <summary>
		/// Static constructor.
		/// </summary>
		static MCSDataStream() => UpdateMCSDeviceList();

		/// <summary>
		/// Update the connected device list.
		/// </summary>
		public static void UpdateMCSDeviceList()
		{
			MCSMEADeviceList?.Dispose();
			MCSMEADeviceList = new CMcsUsbListNet(DeviceEnumNet.MCS_MEA_DEVICE);
		}

		/// <summary>
		/// Get a list of names for the connected MCS devices and their serial numbers.
		/// </summary>
		/// <returns>A list of devices and serial numbers.</returns>
		public static Vector<(string DeviceName, string SerialNo)> GetMCSDeviceListDescription()
		{
			var result = new Vector<(string DeviceName, string SerialNo)>();
			
			for (uint i = 0; i < MCSMEADeviceList.Count; ++i)
			{
				result.PushBack((
					DeviceName: MCSMEADeviceList.GetUsbListEntry(i).DeviceName,
					SerialNo:	MCSMEADeviceList.GetUsbListEntry(i).SerialNumber));
			}

			return result;
		}

		/// <summary>
		/// The connected MCS-MEA device list.
		/// </summary>
		public static CMcsUsbListNet	 MCSMEADeviceList { get; protected set; } = null;

		protected CMeaDeviceNet			_device					= null;
		protected int					_channelBlockSize		= 0;
		protected int					_selectedChannels		= 0;
		protected bool					_useDigitalInput		= false;
		protected bool					_useChecksumChannels	= false;
		protected Vector<ushort>		_channelReadBuffer		= new();

		protected int					_samplingRate			= 0;
		protected int					_voltageRange			= 0;

		private bool					_disposed				= false;
	}
}