using TINS.Containers;
using TINS.Ephys.Analysis;


namespace TINS.Terminal.Protocols.Genus.CL2
{
	using TFSpectrumAnalyzer = IAnalyzerOutput<Ring<ChannelTimeResult<Spectrum2D>>>;

	/// <summary>
	/// Closed loop algorithm variant III:
	/// add two fixed exploration blocks at +- delta around current stimulus frequency, continue with best one
	/// </summary>
	public class CL2V3 : CL2Algorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		public CL2V3(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }

		/// <summary>
		/// 
		/// </summary>
		public override string CurrentBlockType
		{
			get
			{
				if (!_inExplorationBlocks)
					return "cl3";
				else
				{
					if (float.IsNaN(_lowerPeakPower))
						return "cl3-lower";
					else
						return "cl3-upper";
				}
			}
		}

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

			if (!_inExplorationBlocks)
			{
				// do not change stimulus frequency if no peak was found
				// and do not create exploration blocks if we're nearing the end
				if (iPeak < 0 || 
					_blockCounter + 2 >= Protocol.Config.BlocksPerTrial)
					return currentFrequency;

				// prepare exploration blocks
				_lowerPeakPower = float.NaN;
				_upperPeakPower = float.NaN;

				// determine exploration block frequencies
				_lowerFrequency = currentFrequency - Protocol.Config.CL3Delta;
				_upperFrequency = currentFrequency + Protocol.Config.CL3Delta;

				// start lower frequency block
				_inExplorationBlocks = true;
				return _lowerFrequency;
			}
			else
			{
				if (float.IsNaN(_lowerPeakPower))
				{
					_lowerPeakPower = iPeak >= 0 ? spec[iPeak] : 0;

					// go to upper frequency block
					return _upperFrequency;
				}
				else if (float.IsNaN(_upperPeakPower))
				{
					_upperPeakPower = iPeak >= 0 ? spec[iPeak] : 0;

					// set the stimulus frequency to the bigger power response
					_inExplorationBlocks = false;
					return _upperPeakPower > _lowerPeakPower ? _upperFrequency : _lowerFrequency;
				}

				return currentFrequency; // we should never get here though
			}
		}

		protected bool	_inExplorationBlocks	= false;
		protected float	_lowerFrequency			= 0;
		protected float	_upperFrequency			= 0;
		protected float _lowerPeakPower			= float.NaN;
		protected float _upperPeakPower			= float.NaN;
	}
}
