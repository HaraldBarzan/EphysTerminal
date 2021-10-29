using System;
using System.Diagnostics;
using System.Threading;
using TINS.Ephys.Settings;
using TINS.Data;
using TINS.Data.EPD;
using TINS.IO;

namespace TINS.Ephys.Data
{
	/// <summary>
	/// Local data input stream that reads a local EPD file. Usually used for testing purposes.
	/// </summary>
	public class LocalDataStream
		: DataInputStream
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="settings"></param>
		/// <param name="ringBufferSize"></param>
		public LocalDataStream(EphysSettings settings, string epdPath, int ringBufferSize = 3)
			: base(settings, ringBufferSize)
		{
			var ds = new Dataset(epdPath);

			// check dataset sampling rate
			if (ds.SamplingRate != settings.SamplingRate)
				throw new Exception("Provided dataset does not the input sampling rate.");

			// set polling period (in milliseconds)
			PollingPeriod = Numerics.RoundL(settings.Input.PollingPeriod * 1000);

			// open channel streams
			foreach (var ch in ds.Channels)
				_streams.PushBack(new IOStream(ds.GetChannelPath(ch)));
			_events = ds.GetEvents();
		}

		/// <summary>
		/// The polling period.
		/// </summary>
		public long PollingPeriod { get; init; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			foreach (var stream in _streams)
				stream?.Dispose();
			_streams?.Dispose();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected override DataStreamError ConnectStream()
		{
			BeginInvoke(new Action(ReadLoop));
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
		protected void ReadLoop()
		{
			Status	= DataStreamStatus.Running;
			var t	= new Stopwatch();

			// raise start event
			RaiseAcqusitionStarted();

			// loop
			do
			{
				t.Restart();

				// check termination criteria
				if (Status == DataStreamStatus.TerminationPending)
				{
					Status = DataStreamStatus.Idle;
					break;
				}

				// check invalidation criteria
				if (_ringBuffer is null || _ringBuffer.IsEmpty)
				{
					Status = DataStreamStatus.Invalid;
					break;
				}

				// request new buffer
				var dataInput	= _ringBuffer.Current;
				var analogData	= dataInput.AnalogInput;
				var digitalData = dataInput.DigitalInput;
				_ringBuffer.RotateRight(1);

				lock (dataInput)
				{
					// reset the streams if needed
					for (int i = 0; i < _streams.Size; ++i)
					{
						if (_streams[i].Position + analogData.Cols > _streams[i].Length)
							ResetStreamPosition();
					}

					// lock matrix and write stream data
					for (int iCh = 0; iCh < _streams.Size; ++iCh)
						_streams[iCh].Read(analogData.GetBuffer(iCh));
					
					// advance the current position
					_currentPos += analogData.Cols;
				}

				// signal data availability
				RaiseDataAvailable(dataInput);

				// put the thread to sleep for the remainder of the tick
				Thread.Sleep((int)Math.Max(0, PollingPeriod - t.ElapsedMilliseconds));
			}
			while (Status != DataStreamStatus.Idle && !_disposed);

			DisconnectStream();

			// raise end event
			RaiseAcquistionEnded();
		}

		/// <summary>
		/// 
		/// </summary>
		protected void ResetStreamPosition()
		{
			for (int i = 0; i < _streams.Size; ++i)
				_streams[i].Seek(0);
		}


		protected Vector<IOStream>		_streams	= new();
		protected Vector<EventMarker>	_events		= new();
		protected int					_currentPos = 0;
		private bool					_disposed	= false;
	}
}
