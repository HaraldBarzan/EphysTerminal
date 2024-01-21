using Mcs.Usb;
using System;
using TINS.Ephys.Data;
using TINS.Ephys.Settings;
using TINS.MatrixImpl;

namespace TINS.Terminal.Data
{
	/// <summary>
	/// Streams data from the MCS MEA device.
	/// </summary>
	public class MCSDataStream
		: DataInputStream
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="settings"></param>
		/// <param name="ringBufferSize"></param>
		/// <param name="useDigitalChannel"></param>
		/// <param name="useChecksumChannels"></param>
		/// <exception cref="Exception"></exception>
		public MCSDataStream(
			InputSettings				settings, 
			int ringBufferSize			= 3,
			int usbDeviceIndex			= 0,
			bool useDigitalChannel		= true, 
			bool useChecksumChannels	= false)
			: base(settings, ringBufferSize)
		{
			// number of polls per second
			int pollingRate = Numerics.Round(1 / settings.PollingPeriod);

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
			_selectedChannels = Numerics.Clamp(settings.ReadChannelCount, (0, supportedAnalogChannels));
			_device.SetNumberOfChannels(_selectedChannels);

			// set digital input
			_useDigitalInput = useDigitalChannel;
			_device.EnableDigitalIn(_useDigitalInput, 0);

			// set checksum channels
			_useChecksumChannels = useChecksumChannels;
			_device.EnableChecksum(_useChecksumChannels, 0);

			// set sampling rate
			_samplingRate = Numerics.Round(settings.SamplingRate);
			_device.SetSamplerate(_samplingRate, 1, 0);

			// set the voltage range
			_voltageRange = _device.GetVoltageRangeInMilliVolt();
			Gain = _device.GetGain();

			// get the layout and confirm data selection
			_device.GetChannelLayout(out int analogChannels, out int digitalChannels, out _, out _, out int blockSize, 0);
			_channelBlockSize = Numerics.Floor(_samplingRate * settings.PollingPeriod);
			_selectedChannels = blockSize;
			_channelReadBuffer.Resize((_selectedChannels + (_useDigitalInput ? 1 : 0)) * _channelBlockSize);

			// TODO: play around so that we don't even read excluded channels
			_device.SetSelectedData(_selectedChannels, _samplingRate, _channelBlockSize, SampleSizeNet.SampleSize16Unsigned, blockSize); 
			//_device.ChannelBlock.SetSelectedData(_selectedChannels, _samplingRate, _channelBlockSize, SampleSizeNet.SampleSize16Unsigned, SampleDstSizeNet.SampleDstSize16, blockSize);

			// map the input channels channels
			for (int iCh = 0; iCh < settings.ChannelLabels.Size; ++iCh)
			{
				if (settings.ExcludedChannels.Contains(settings.ChannelLabels[iCh]))
					continue;
				_channelMap.PushBack(iCh);
			}

			// since we handle connection in constructor, set stream status to idle
			Status = DataStreamStatus.Idle;
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
		/// 
		/// </summary>
		/// <returns></returns>
		public override DataStreamError StartAcquisition()
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
		/// <param name="awaitTermination"></param>
		/// <returns></returns>
		public override DataStreamError StopAcquisition(bool awaitTermination = false)
		{
			if (_device is object && _device.IsConnected())
			{
				_device.StopDacq();
				Status = DataStreamStatus.Idle;

				// raise end event
				RaiseAcquistionEnded();
			}

			return DataStreamError.None;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected override DataStreamError ConnectStream()
		{
			return DataStreamError.None;
		}

		/// <summary>
		/// 
		/// </summary>
		protected override void DisconnectStream()
		{
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
			//var data = _device.ChannelBlock.ReadFramesUI16(0, 0, _channelBlockSize, out int frameCount);


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
			float voltageScale	= 2f * _voltageRange / ushort.MaxValue;

			// determine the number of actual channels
			int nSourceAnalogCh	= TotalChannelCount;
			int nSourceTotalCh	= nSourceAnalogCh + (_useDigitalInput ? 1 : 0);

			lock (dataInput)
			{
				// get the analog input stream
				for (int iCh = 0; iCh < EffectiveChannelCount; ++iCh)
				{
					int sourceChIndex = _channelMap[iCh];
					for (int iFrame = 0; iFrame < frameCount; ++iFrame)
					{
						analogData[iCh, iFrame] = data[iFrame * nSourceTotalCh + sourceChIndex] * voltageScale - _voltageRange;
					}
				}

				// get the digital input stream
				if (_useDigitalInput)
				{
					for (int iFrame = 0; iFrame < frameCount; ++iFrame)
					{
						digitalInput[iFrame] = data[iFrame * nSourceTotalCh + nSourceAnalogCh];
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
		/// Read loop.
		/// </summary>
		/// <returns></returns>
		protected override DataStreamError ReadLoop() => DataStreamError.None;

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
		protected Vector<int>			_channelMap				= new();

		protected int					_samplingRate			= 0;
		protected int					_voltageRange			= 0;

		private bool					_disposed				= false;
	}
}