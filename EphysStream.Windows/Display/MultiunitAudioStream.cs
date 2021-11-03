using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Threading;
using TINS.Containers;
using TINS.Ephys.Processing;

namespace TINS.Ephys.Display
{
	/// <summary>
	/// An audio stream for multiunit activity.
	/// </summary>
	public class MultiunitAudioStream
		: ISampleProvider
		, IDisposable
	{
		/// <summary>
		/// Create a new audio stream.
		/// </summary>
		/// <param name="stream">The parent ephys stream.</param>
		public MultiunitAudioStream(EphysStream stream)
		{
			EphysStream	= stream;

			if (!EphysStream.ProcessingPipeline.TryGetBuffer(stream.Settings.UI.AudioSourceBuffer, out _targetBuffer) ||
				!_targetBuffer.Labels.Contains(stream.Settings.UI.DefaultAudioChannel))
			{
				throw new Exception("Invalid buffer or channel specified!");
			}
			
			// initialize the audio stream
			WaveFormat		= WaveFormat.CreateIeeeFloatWaveFormat(Numerics.Floor(_targetBuffer.SamplingRate), 1);
			_audioOutput	= new WasapiOut(AudioClientShareMode.Shared, Numerics.Round(1000 * stream.Settings.Input.PollingPeriod));
			_audioOutput	.Init(this);
			_streamBuffer	.Resize(Numerics.Floor(_targetBuffer.SamplingRate));
			_targetChannel	= stream.Settings.UI.DefaultAudioChannel;
			_resetEvent		.Reset();
		}

		/// <summary>
		/// Dispose of this object.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed) return;

			Stop();
			
			if (disposing)
			{
				_streamBuffer	?.Dispose();
				_audioOutput	?.Dispose();
				_resetEvent		?.Dispose();
			}

			_targetBuffer	= null;
			_targetChannel	= string.Empty;
			_disposed		= true;
		}

		/// <summary>
		/// The waveformat for this audio stream.
		/// </summary>
		public WaveFormat WaveFormat { get; init; }

		/// <summary>
		/// Check whether the stream is playing audio.
		/// </summary>
		public bool IsPlaying
			=> _audioOutput is object
			&& _audioOutput.PlaybackState == PlaybackState.Playing;

		/// <summary>
		/// Start playing audio.
		/// </summary>
		public void Start()
		{
			if (_disposed) 
				return;

			lock (_streamBuffer)
			{
				_streamBuffer.Fill(0);
				_writePos = 0;
			}

			_audioOutput.Play();
		}

		/// <summary>
		/// Stop playing audio.
		/// </summary>
		public void Stop()
		{
			if (_disposed) 
				return;
			_audioOutput.Stop();
			_resetEvent	.Set();
		}

		/// <summary>
		/// Change the source channel.
		/// </summary>
		/// <param name="newChannelLabel">The label of the source channel.</param>
		public void ChangeSourceChannel(string newChannelLabel)
		{
			if (_targetBuffer.Labels.Contains(newChannelLabel))
				_targetChannel = newChannelLabel;
		}

		/// <summary>
		/// Read a number <paramref name="count"/> of data samples into <paramref name="buffer"/>.
		/// </summary>
		/// <param name="buffer">The buffer to read into.</param>
		/// <param name="offset">The offset within <paramref name="buffer"/> to start reading.</param>
		/// <param name="count">The number of samples to write into the buffer.</param>
		/// <returns>The actual number of samples read (may differ from <paramref name="count"/>).</returns>
		public virtual int Read(float[] buffer, int offset, int count)
		{
			if (buffer is null)
				return 0;

			// wait until we have enough samples
			while (_writePos < count)
			{
				if (_disposed)
					return 0;
				_resetEvent.WaitOne();
			}

			lock (_streamBuffer)
			{
				_streamBuffer.CopyTo(new Span<float>(buffer).Slice(offset, count));
				_streamBuffer.RotateLeft(count);
				_writePos -= count;
			}

			return count;
		}

		/// <summary>
		/// Write data from the source buffer in the stream.
		/// </summary>
		/// <param name="count">Number of samples to write.</param>
		public int WriteFromSourceBuffer(int count)
		{
			if (_disposed || _targetBuffer is null)
				return 0;

			var buffer	= _targetBuffer.GetBuffer(_targetChannel);
			count		= Math.Min(count, buffer.Length);
			buffer		= buffer.Slice(buffer.Length - count, count);

			lock (_streamBuffer)
			{
				_streamBuffer.CopyFrom(buffer, _writePos);
				_writePos += count;
				_resetEvent?.Set();
			}

			return count;
		}

		/// <summary>
		/// The parent ephys stream.
		/// </summary>
		public EphysStream EphysStream { get; init; }



		protected AutoResetEvent		_resetEvent		= new(false);
		protected Ring<float>			_streamBuffer	= new();
		protected int					_writePos		= 0;
		protected MultichannelBuffer	_targetBuffer	= null;
		protected string				_targetChannel	= string.Empty;
		protected WasapiOut				_audioOutput	= null;

		private bool					_disposed		= false;
	}
}
