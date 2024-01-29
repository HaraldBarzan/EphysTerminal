using System;
using TINS.Containers;
using TINS.Ephys.Analysis;


namespace TINS.Terminal.Protocols.Genus.CL2
{
	using TFSpectrumAnalyzer = IAnalyzerOutput<Ring<ChannelTimeResult<Spectrum2D>>>;

	/// <summary>
	/// Closed loop algorithm variant IV:
	/// set the stimulus frequency to whatever peak is the most prominent
	/// </summary>
	public class CL2Washout : CL2Algorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		public CL2Washout(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }

		/// <summary>
		/// 
		/// </summary>
		public override string CurrentBlockType => "cl4";

		/// <summary>
		/// Get the number of consecutive peakless blocks.
		/// </summary>
		public int ConsecutivePeaklessBlocks { get; protected set; }

		/// <summary>
		/// The timeout to wait until completing the washout period.
		/// </summary>
		public int WashoutTimeout { get; protected set; }

		/// <summary>
		/// Check if we are in a washout period.
		/// </summary>
		public bool InWashout { get; protected set; }

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
			float frequency = Protocol.Config.StartingFlickerFrequency;

			if (iPeak >= 0)
			{
				// clamp in +-delta from currentfrequency and stimulation frequency range
				frequency = MathF.Round(spec.BinToFrequency(iPeak));
				frequency = Numerics.Clamp(frequency, (currentFrequency - Protocol.Config.PeakFollowerDelta, currentFrequency + Protocol.Config.PeakFollowerDelta));
				frequency = Numerics.Clamp(frequency, Protocol.Config.StimulationFrequencyRange);
			}

			// check if we are in the washout period or not
			if (InWashout)
			{
				WashoutTimeout--;
				if (WashoutTimeout <= 0)
				{
					EndWashout();
					if (iPeak < 0)
					{
						// end of washout, but no peak
						ConsecutivePeaklessBlocks++;
						blockResult = "cl4-nopeak";
						//return currentFrequency;
						return Protocol.Config.StartingFlickerFrequency;
					}
					else
					{
						// end of washout, we found a peak (go to it)
						blockResult = "cl4-update";
						return frequency;
					}
				}
				else
				{
					// still in washout (return washout frequency)
					blockResult = "cl4-washout";
					return Protocol.Config.WashoutFrequency;
				}
			}
			else
			{
				if (iPeak < 0)
				{
					// not in washout, no peak. increment counter and check for washout trigger
					ConsecutivePeaklessBlocks++;
					if (ConsecutivePeaklessBlocks >= Protocol.Config.WashoutTriggerBlocks)
					{
						// starting washout
						BeginWashout();
						blockResult = "cl4-washout";
						return Protocol.Config.WashoutFrequency;
					}
					else
					{
						// no peak, but no washout trigger either. just try again at the current frequency
						blockResult = "cl4-nopeak";
						return currentFrequency;
					}
				}
				else
				{
					// found peak, we update the stimulation frequency
					ConsecutivePeaklessBlocks = 0;
					blockResult = "cl4-update";
					return frequency;
				}
			}
		}


		/// <summary>
		/// 
		/// </summary>
		protected void BeginWashout()
		{
			InWashout = true;
			WashoutTimeout = Protocol.Config.WashoutTimeoutBlocks;
			ConsecutivePeaklessBlocks = 0;
		}

		/// <summary>
		/// 
		/// </summary>
		protected void EndWashout()
		{
			InWashout = false;
			WashoutTimeout = 0;
			ConsecutivePeaklessBlocks = 0;
		}
	}
}
