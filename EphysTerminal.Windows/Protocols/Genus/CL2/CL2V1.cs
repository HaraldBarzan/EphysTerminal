using TINS.Containers;
using TINS.Ephys.Analysis;

namespace TINS.Terminal.Protocols.Genus.CL2
{
	using TFSpectrumAnalyzer = IAnalyzerOutput<Ring<ChannelTimeResult<Spectrum2D>>>;


	/// <summary>
	/// Closed loop algorithm variant I:
	/// always return the frequency at max power (old version)
	/// </summary>
	public class CL2V1 : CL2Algorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		/// <param name="delta"></param>
		public CL2V1(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }

		/// <summary>
		/// CL1 block type.
		/// </summary>
		public override string CurrentBlockType => "cl1";

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentFrequency"></param>
		/// <returns></returns>
		public override float ComputeNextStimulusFrequency(float currentFrequency, int periods, out string blockResult)
		{
			base.ComputeNextStimulusFrequency(currentFrequency, periods, out blockResult);

			// get frequency at peak
			using var spec = Get1DPowerSpectrum(periods);
			int iPeak = spec.ArgMax();
			blockResult = "cl1-update";

			return Numerics.Clamp(spec.BinToFrequency(iPeak), Protocol.Config.StimulationFrequencyRange);
		}
	}
}
