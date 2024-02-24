using System;
using TINS.Containers;
using TINS.Ephys.Analysis;

namespace TINS.Terminal.Protocols.Genus.CL2
{
	using TFSpectrumAnalyzer = IAnalyzerOutput<Ring<ChannelTimeResult<Spectrum2D>>>;

	/// <summary>
	/// Closed loop algorithm variant II:
	/// use peak to explore in range +- delta around the current stimulus frequency.
	/// </summary>
	public class CL2PeakFollowerDelta : CL2Algorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		public CL2PeakFollowerDelta(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }

		/// <summary>
		/// 
		/// </summary>
		public override string CurrentBlockType => "cl2";

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentFrequency"></param>
		/// <returns></returns>
		public override float ComputeNextStimulusFrequency(float currentFrequency, int periods, out string blockResult)
		{
			base.ComputeNextStimulusFrequency(currentFrequency, periods, out blockResult);

			// get frequency at peak
			using var spec	= Get1DPowerSpectrum(periods);
			var iPeak		= FindPeak(spec);

			// do not change frequency if no peak is detected
			if (iPeak < 0)
			{
				blockResult = "nopeak";
				return currentFrequency;
			}

			// clamp in +-delta from currentfrequency and stimulation frequency range
			float freq = MathF.Round(spec.BinToFrequency(iPeak));
			freq = Numerics.Clamp(freq, (currentFrequency - Protocol.Config.PeakFollowerDelta, currentFrequency + Protocol.Config.PeakFollowerDelta));
			freq = Numerics.Clamp(freq, Protocol.Config.StimulationFrequencyRange);

			blockResult = freq == currentFrequency ? "noupdate" : "update";
			return freq;
		}
	}
}
