using TINS.Containers;
using TINS.Ephys.Analysis;

namespace TINS.Terminal.Protocols.Genus.CL2
{
    using TFSpectrumAnalyzer = IAnalyzerOutput<Ring<ChannelTimeResult<Spectrum2D>>>;

    /// <summary>
    /// 
    /// </summary>
    public class CL2Static
        : CL2Algorithm
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="protocol"></param>
        /// <param name="analyzer"></param>
        public CL2Static(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
            : base(protocol, analyzer)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        public override string CurrentBlockType => "cl5";


        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentFrequency"></param>
        /// <param name="periods"></param>
        /// <param name="blockResult"></param>
        /// <returns></returns>
        public override float ComputeNextStimulusFrequency(float currentFrequency, int periods, out string blockResult)
        {
            base.ComputeNextStimulusFrequency(currentFrequency, periods, out _);
            blockResult = "static";
            return currentFrequency;
        }
    }
}
