using System;
using TINS.Ephys.Analysis.Events;

namespace TINS.Terminal.UI
{
	public sealed class SpikeDisplayAccumulator
		: IDisposable
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		SpikeDisplayAccumulator()
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="capacity"></param>
		/// <param name="detector"></param>
		public SpikeDisplayAccumulator(float capacity, MUASpikeDetector detector)
		{
			if (detector is null)
				throw new Exception("Invalid arguments.");

			// initialize accumulator
			Capacity			= Numerics.Round(capacity * detector.InputBuffer.SamplingRate);
			Source				= detector;
			ChannelCount		= detector.ChannelCount;
			WaveformSize		= detector.WaveformSize;
			
			// resize storage
			_timings	.Resize(ChannelCount);
			_waveforms	.Resize(ChannelCount);
			for (int i = 0; i < ChannelCount; ++i)
			{
				_timings[i]		??= new();
				_waveforms[i]	??= new();
			}
		}

		/// <summary>
		/// Dispose of buffers.
		/// </summary>
		public void Dispose()
		{
			_timings	?.RecursiveDispose();
			_waveforms	?.RecursiveDispose();

			CurrentOffset	= 0;
			Source				= null;
			ChannelCount		= 0;
			WaveformSize		= 0;

			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Reset all accumulated values.
		/// </summary>
		public void Reset()
		{
			CurrentOffset = 0;
			foreach (var vTimes in _timings)
				vTimes?.Clear();
			foreach (var vWaves in _waveforms)
				vWaves?.Clear();
		}

		/// <summary>
		/// Accumulate spikes and waveforms from the source buffer.
		/// </summary>
		public void Accumulate(float updatePeriod = float.PositiveInfinity)
		{
			if (IsFull)
				Reset();

			if (!Source.InputBuffer.GetMostRecentPeriod(updatePeriod, out _, out var sampleCount))
				return;

			lock (Source)
			{
				for (int iCh = 0; iCh < Source.ChannelCount; ++iCh)
				{
					int currentCount = _timings[iCh].Size;

					// do timestamps
					var srcTimes = Source.GetDetectorTimestamps(iCh);
					_timings[iCh].Resize(currentCount + srcTimes.Size);
					var dstTimes = _timings[iCh];

					// add new spikes with offset
					int offset = CurrentOffset;
					for (int i = 0; i < srcTimes.Size; ++i)
						dstTimes[currentCount + i] = srcTimes[i] + offset;

					// do waveforms
					var waveforms = Source.GetDetectorWaveforms(iCh);
					_waveforms[iCh].Resize((currentCount + waveforms.Rows) * Source.WaveformSize);
					for (int i = 0; i < waveforms.Rows; ++i)
					{
						// copy each waveform to its destination
						waveforms.GetBuffer(i).CopyTo(_waveforms[iCh].GetSpan().Slice((currentCount + i) * Source.WaveformSize));
					}
				}
			}

			CurrentOffset += sampleCount;
		}

		/// <summary>
		/// Get the timings of all spikes in a channel.
		/// </summary>
		/// <param name="channelIndex"></param>
		/// <returns></returns>
		public Span<int> GetTimestamps(int channelIndex)
			=> _timings[channelIndex].GetSpan();

		/// <summary>
		/// Get a waveform from the accumulator.
		/// </summary>
		/// <param name="channelIndex"></param>
		/// <param name="waveformIndex"></param>
		/// <returns></returns>
		public Span<float> GetWaveform(int channelIndex, int waveformIndex)
			=> _waveforms[channelIndex].GetSpan().Slice(waveformIndex * WaveformSize, WaveformSize);

		/// <summary>
		/// The number of supported channels.
		/// </summary>
		public int ChannelCount { get; private set; }

		/// <summary>
		/// The size of each waveform.
		/// </summary>
		public int WaveformSize { get; private set; }

		/// <summary>
		/// Detection capacity in samples.
		/// </summary>
		public int Capacity { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public MUASpikeDetector Source { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public int CurrentOffset { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public bool IsFull => CurrentOffset >= Capacity;


		Vector<Vector<float>>	_waveforms	= new();
		Vector<Vector<int>>		_timings	= new();
	}
}
