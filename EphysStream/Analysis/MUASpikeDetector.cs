using System;
using System.Threading.Tasks;
using TINS.Ephys.Processing;
using TINS.Ephys.Settings;
using TINS.Data.Spikes;
using TINS.Spikes;

namespace TINS.Ephys.Analysis
{
	/// <summary>
	/// 
	/// </summary>
	public class MUASpikeDetector
		: AnalysisPipe
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="settings"></param>
		/// <param name="inputBuffer"></param>
		public MUASpikeDetector(SpikeSettings settings, MultichannelBuffer inputBuffer)
			: base(settings.Name, inputBuffer)
		{
			int channelCount	= inputBuffer.Rows;
			float samplingRate	= inputBuffer.SamplingRate;

			var detectorSettings = CreateSettings(settings, samplingRate);
			WaveformSize	= detectorSettings.WaveformSize;
			BufferSize		= inputBuffer.Cols;

			for (int i = 0; i < channelCount; ++i)
				_detectors.PushBack(new SpikeDetector(detectorSettings));
			_parallelOpts.MaxDegreeOfParallelism = settings.ThreadCount;
		}

		/// <summary>
		/// 
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			foreach (var detector in _detectors)
				detector?.Dispose();
			_detectors?.Dispose();
		}

		/// <summary>
		/// Change the threshold of all channel spike detectors.
		/// </summary>
		/// <param name="newThreshold">A new threshold for all channels.</param>
		public void ChangeThresholds(float newThreshold)
		{
			foreach (var detector in _detectors)
				detector?.ChangeThreshold((newThreshold, newThreshold));
		}

		/// <summary>
		/// Change the threshold of all channel spike detectors.
		/// </summary>
		/// <param name="thresholds">A list of individual channel thresholds.</param>
		public void ChangeThresholds(Vector<(int SourceIndex, float Threshold)> thresholds)
		{
			foreach (var thr in thresholds)
			{
				if (Numerics.IndexInRange(thr.SourceIndex, _detectors))
					_detectors[thr.SourceIndex].ChangeThreshold((thr.Threshold, thr.Threshold));
			}
		}

		/// <summary>
		/// Change the threshold of a single channel spike detector.
		/// </summary>
		/// <param name="channelIndex">The index of the channel.</param>
		/// <param name="newThreshold">The new threshold.</param>
		public void ChangeThreshold(int channelIndex, float newThreshold)
		{
			if (channelIndex >= 0 && channelIndex < _detectors.Size)
				_detectors[channelIndex].ChangeThreshold((newThreshold, newThreshold));
		}

		/// <summary>
		/// Run the spike detector.
		/// </summary>
		public override void Run()
		{
			if (Input.Rows != _detectors.Size)
				throw new Exception("Buffer count mismatch.");

			lock (Input)
			{
				Parallel.For(0, _detectors.Size, _parallelOpts, 
					i => _detectors[i].Parse(Input.GetBuffer(i)));
			}
		}

		/// <summary>
		/// Compute the threshold for each channel.
		/// </summary>
		/// <param name="thresholds">The list of thresholds.</param>
		/// <param name="sdFactor">Multiple of the standard deviation.</param>
		/// <param name="apply">Auto-apply the resulting thresholds to the detectors.</param>
		public virtual void ComputeAutoThresholdSD(out Vector<(int SourceIndex, float Threshold)> thresholds, float sdFactor, bool apply = false)
		{
			thresholds = new Vector<(int SourceIndex, float Threshold)>(_detectors.Size);

			lock (Input)
			{
				for (int iCh = 0; iCh < _detectors.Size; iCh++)
				{
					var sd			= Statistics.StandardDeviation(Input.GetBuffer(iCh), out var mean);
					float threshold = mean - sd * sdFactor;
					thresholds[iCh] = (iCh, threshold);

					if (apply)
					{
						_detectors[iCh].ChangeThreshold((threshold, threshold));
					}
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="channelIndex"></param>
		/// <returns></returns>
		public Vector<int> GetDetectorTimestamps(int channelIndex)
		{
			if (channelIndex >= 0 && channelIndex < _detectors.Size)
				return _detectors[channelIndex].DetectedTimestamps;
			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="channelIndex"></param>
		/// <returns></returns>
		public Matrix<float> GetDetectorWaveforms(int channelIndex)
		{
			if (channelIndex >= 0 && channelIndex < _detectors.Size)
				return _detectors[channelIndex].Waveforms;
			return null;
		}

		/// <summary>
		/// The number of supported channels.
		/// </summary>
		public int ChannelCount => _detectors.Size;

		/// <summary>
		/// Get the size of a waveform.
		/// </summary>
		public int WaveformSize { get; protected set; }

		/// <summary>
		/// The size of the buffer for the previous run.
		/// </summary>
		public int BufferSize { get; protected set; }

		/// <summary>
		/// Create settings item for the spike detector.
		/// </summary>
		/// <param name="settings"></param>
		/// <param name="inputSamplingRate"></param>
		/// <returns></returns>
		protected static SpikeDetectorSettings CreateSettings(SpikeSettings settings, float inputSamplingRate)
			=> new()
			{
				WaveformSize			= Numerics.Round(settings.SpikeCutWidth / 1000 * inputSamplingRate),
				WaveformPeakIndex		= Numerics.Round(settings.PeakOffset / 1000 * inputSamplingRate),
				Threshold				= (settings.Threshold, settings.Threshold),
				ThresholdType			= SpikeThresholdType.Negative,
				Refractoriness			= Numerics.Round(settings.Refractoriness / 1000 * inputSamplingRate),
				MaxAmplitude			= 1000,
				MinimumPNSpikeDistance	= Numerics.Round(settings.SpikeCutWidth / 1000 * inputSamplingRate)
			};



		protected Vector<SpikeDetector> _detectors		= new();
		protected ParallelOptions		_parallelOpts	= new();
	}


}
