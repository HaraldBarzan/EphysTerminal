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
		/// <param name="pollCapacity"></param>
		/// <param name="detector"></param>
		public SpikeDisplayAccumulator(int pollCapacity, MUASpikeDetector detector)
		{
			if (detector is null || pollCapacity < 1)
				throw new Exception("Invalid arguments.");

			// initialize accumulator
			PollCapacity		= pollCapacity;
			Source				= detector;
			CurrentFillIndex	= 0;
			ChannelCount		= detector.ChannelCount;
			WaveformSize		= detector.WaveformSize;
			BufferSize			= detector.BufferSize;
			
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

			PollCapacity		= 0;
			Source				= null;
			CurrentFillIndex	= 0;
			ChannelCount		= 0;
			WaveformSize		= 0;

			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Reset all accumulated values.
		/// </summary>
		public void Reset()
		{
			CurrentFillIndex = 0;
			foreach (var vTimes in _timings)
				vTimes?.Clear();
			foreach (var vWaves in _waveforms)
				vWaves?.Clear();
		}

		/// <summary>
		/// Accumulate spikes and waveforms from the source buffer.
		/// </summary>
		public void Accumulate()
		{
			if (IsFull)
				Reset();

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
					int offset = BufferSize * CurrentFillIndex;
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

			++CurrentFillIndex;
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
		/// 
		/// </summary>
		public int PollCapacity { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public int BufferSize { get; private set; }
		
		/// <summary>
		/// 
		/// </summary>
		public MUASpikeDetector Source { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public int CurrentFillIndex { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public bool IsFull => CurrentFillIndex == PollCapacity;


		Vector<Vector<float>>	_waveforms	= new();
		Vector<Vector<int>>		_timings	= new();


		/// <summary>
		/// Create a dummy display accumulator with the specified number of channels and spike counts.
		/// </summary>
		/// <param name="spikeCounts">The number of columns.</param>
		/// <param name="waveformSize">The size of each waveform in samples.</param>
		/// <returns>A dummy display accumulator.</returns>
		public static SpikeDisplayAccumulator CreateDummy(Vector<int> spikeCounts, int waveformSize = 58)
		{
			var result = new SpikeDisplayAccumulator()
			{
				PollCapacity		= 1,
				CurrentFillIndex	= 1,
				Source				= null,
				ChannelCount		= spikeCounts.Size,
				WaveformSize		= waveformSize
			};
			result._timings		.Resize(spikeCounts.Size);
			result._waveforms	.Resize(spikeCounts.Size);

			for (int i = 0; i < spikeCounts.Size; ++i)
			{
				result._timings[i]		= new Vector<int>(spikeCounts[i]);
				result._waveforms[i]	= new Vector<float>(spikeCounts[i] * waveformSize);
			}

			return result;
		}

	}
}
