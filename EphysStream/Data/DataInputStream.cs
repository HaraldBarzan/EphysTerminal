using System;
using TINS.Ephys.Settings;
using TINS.Containers;
using TINS.Utilities;

namespace TINS.Ephys.Data
{
	/// <summary>
	/// The status of a data stream.
	/// </summary>
	public enum DataStreamStatus
	{
		Running,
		Starting,
		TerminationPending,
		Idle,
		Invalid
	}

	/// <summary>
	/// Possible errors that may arise when certain actions are performed on a data stream.
	/// </summary>
	public enum DataStreamError
	{
		None,
		ConnectionFailure,
		InvalidStream,
		Unspecified
	}

	/// <summary>
	/// Base class for data streams.
	/// </summary>
	public abstract class DataInputStream
		: AsynchronousObject
		, IDisposable
	{
		/// <summary>
		/// Raised when acquisition is initiated.
		/// </summary>
		public event Action AcquisitionStarted;
		
		/// <summary>
		/// Raised when acquistion is terminated.
		/// </summary>
		public event Action AcquistionEnded;

		/// <summary>
		/// Raised when new data is available.
		/// </summary>
		public event EventHandler<InputDataFrame> DataAvailable;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="settings"></param>
		/// <param name="ringBufferSize"></param>
		public DataInputStream(EphysSettings settings, int ringBufferSize = 3)
		{
			// check input validity
			if (settings.ChannelCount < 1)		throw new ArgumentException($"{nameof(settings.ChannelCount)} should be a positive, non-zero integer.");
			if (settings.SamplesPerBlock < 1)	throw new ArgumentException($"{nameof(settings.SamplesPerBlock)} should be a positive, non-zero integer.");
			if (ringBufferSize < 1)				throw new ArgumentException($"{nameof(ringBufferSize)} should be a positive, non-zero integer.");

			// set properties
			ChannelCount	= settings.ChannelCount;
			SamplesPerBlock = settings.SamplesPerBlock;
			Status			= DataStreamStatus.Idle;

			// alloc the ring buffer
			_ringBuffer.Resize(ringBufferSize);
			for (int i = 0; i < _ringBuffer.Size; ++i)
			{
				_ringBuffer[i] ??= new InputDataFrame();
				_ringBuffer[i].ResizeFrame(ChannelCount, SamplesPerBlock);
			}
		}

		/// <summary>
		/// Virtual dispose method.
		/// </summary>
		/// <param name="disposing">True if dispose has been called, false if finalizer.</param>
		protected override void Dispose(bool disposing)
		{
			if (_disposed) return;

			StopAcquisition();

			DataAvailable		= null;
			AcquisitionStarted	= null;
			AcquistionEnded		= null;
			
			if (disposing)
			{
				SafeDisposeRingBuffer();
				_ringBuffer?.Dispose();
			}

			Status		= DataStreamStatus.Invalid;
			_ringBuffer = null;

			base.Dispose(disposing);
			_disposed = true;
		}


		/// <summary>
		/// Begin the acquisition of new data.
		/// </summary>
		/// <returns>An error, if applicable.</returns>
		public virtual DataStreamError StartAcquisition()
		{
			if (Status == DataStreamStatus.Invalid)
				return DataStreamError.InvalidStream;
			
			// do nothing if already running
			if (Status == DataStreamStatus.Running)
				return DataStreamError.None;

			Status = DataStreamStatus.Starting;
			return ConnectStream();
		}

		/// <summary>
		/// Stop data acquisition.
		/// </summary>
		/// <returns>An error, if applicable.</returns>
		public virtual DataStreamError StopAcquisition()
		{
			if (Status == DataStreamStatus.Invalid)
				return DataStreamError.InvalidStream;

			// send a signal to the reading thread
			Status = DataStreamStatus.TerminationPending;
			return DataStreamError.None;
		}

		/// <summary>
		/// The number of channels to read.
		/// </summary>
		public int ChannelCount { get; init; }

		/// <summary>
		/// The number of samples to read for each channel in the block.
		/// </summary>
		public int SamplesPerBlock { get; init; }

		/// <summary>
		/// The status of the data stream.
		/// </summary>
		public DataStreamStatus Status
		{
			get => _status;
			set => _status = value;
		}

		/// <summary>
		/// Abstract method to launch the data acquisition thread.
		/// </summary>
		/// <returns>An error if connection fails.</returns>
		protected abstract DataStreamError ConnectStream();

		/// <summary>
		/// Abstract method to stop data acquisition.
		/// </summary>
		protected abstract void DisconnectStream();

		/// <summary>
		/// Safely destroy each ring buffer item.
		/// </summary>
		protected void SafeDisposeRingBuffer()
		{
			foreach (var buffer in _ringBuffer)
			{
				if (buffer is object)
				{
					lock (buffer) 
						buffer.Dispose();
				}
			}
			_ringBuffer.Clear();
		}

		/// <summary>
		/// Raise the DataAvailable event.
		/// </summary>
		protected void RaiseDataAvailable(InputDataFrame e) => DataAvailable?.Invoke(this, e);

		/// <summary>
		/// Raise the AcquisitionStarted event.
		/// </summary>
		protected void RaiseAcqusitionStarted() => AcquisitionStarted?.Invoke();

		/// <summary>
		/// Raise the AcquistionEnded event.
		/// </summary>
		protected void RaiseAcquistionEnded() => AcquistionEnded?.Invoke();


		protected volatile DataStreamStatus	_status		= DataStreamStatus.Invalid;
		protected Ring<InputDataFrame>		_ringBuffer	= new();
		private bool						_disposed	= false;
	}
}
