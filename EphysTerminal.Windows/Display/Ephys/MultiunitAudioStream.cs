using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using TINS.Containers;
using TINS.Ephys.Processing;
using TINS.Terminal.Settings;
using TINS.Terminal.Settings.UI;

namespace TINS.Terminal.Display.Ephys
{
	/// <summary>
	/// An audio stream for multiunit activity.
	/// </summary>
	public class MultiunitAudioStream
		: ISampleProvider
		, IDisposable
	{
		const float DataScale = 0.01f;

		/// <summary>
		/// Create a new audio stream.
		/// </summary>
		/// <param name="stream">The parent ephys stream.</param>
		public MultiunitAudioStream(EphysTerminal stream)
		{
			EphysStream	= stream;
			var settings = stream.TerminalSettings.UI as UISettingsEphys;
			if (settings is null)
				throw new Exception();

			if (!EphysStream.ProcessingPipeline.TryGetBuffer(settings.AudioSourceBuffer, out _targetBuffer) ||
				!_targetBuffer.ChannelLabels.Contains(settings.DefaultAudioChannel))
			{
				throw new Exception("Invalid buffer or channel specified!");
			}
			
			// initialize the audio stream
			WaveFormat		= WaveFormat.CreateIeeeFloatWaveFormat(Numerics.Floor(_targetBuffer.SamplingRate), 1);
			_audioOutput	= new WasapiOut(AudioClientShareMode.Shared, true, Numerics.Round(stream.Settings.Input.PollingPeriod * 1000));
			_audioOutput	.Init(this);
			_streamBuffer	.Resize(Numerics.Floor(_targetBuffer.SamplingRate * 2));
			_targetChannel	= settings.DefaultAudioChannel;
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
			_writePos = EphysStream.Settings.SamplesPerFrame * 4;
		}

		/// <summary>
		/// Stop playing audio.
		/// </summary>
		public void Stop()
		{
			if (_disposed) 
				return;
			_audioOutput.Stop();
		}

		/// <summary>
		/// Change the source channel.
		/// </summary>
		/// <param name="newChannelLabel">The label of the source channel.</param>
		public void ChangeSourceChannel(string newChannelLabel)
		{
			if (_targetBuffer.ChannelLabels.Contains(newChannelLabel))
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
			if (buffer is null || _disposed)
				return 0;

			// process request for delay
			lock (_streamBuffer)
			{
				var writeBuffer = new Span<float>(buffer).Slice(offset, count);
				_streamBuffer.CopyTo(writeBuffer);

				int rot = Math.Min(_writePos, count);
				_streamBuffer.RotateLeft(rot);
				_writePos -= rot;

				// rescale samples to an acceptable domain
				for (int i = 0; i < writeBuffer.Length; ++i)
					writeBuffer[i] *= DataScale;
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

			var buffer	= _targetBuffer.GetChannelBuffer(_targetChannel);
			count		= Math.Min(count, buffer.Length);
			buffer		= buffer.Slice(buffer.Length - count, count);

			lock (_streamBuffer)
			{
				_streamBuffer.CopyFrom(buffer, _writePos);
				_writePos += count;
			}

			return count;
		}

		/// <summary>
		/// The parent ephys stream.
		/// </summary>
		public EphysTerminal EphysStream { get; init; }



		protected Ring<float>			_streamBuffer	= new();
		protected int					_writePos		= 0;
		protected MultichannelBuffer	_targetBuffer	= null;
		protected string				_targetChannel	= string.Empty;
		protected WasapiOut				_audioOutput	= null;

		private bool					_disposed		= false;
	}
}
