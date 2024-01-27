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
	public class CL2V4 : CL2Algorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		public CL2V4(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }


		public override string CurrentBlockType => "cl4";

		/// <summary>
		/// 
		/// </summary>
		/// <param name="spectrum"></param>
		/// <returns></returns>
		public override int FindPeak(Spectrum1D spectrum)
		{
			// compute necessary parameters
			var freqRange = spectrum.FrequencyRange;
			var powerRange = Numerics.GetRange(spectrum.Span);
			using var minima = Numerics.FindLocalMinima(spectrum);
			using var maxima = Numerics.FindLocalMaxima(spectrum);
			int candidate = -1;
			float candidateProm = 0;

			// we look at maxima, searching for two flanking minima
			if (maxima.Size < 1 || minima.Size < 2)
				return candidate;

			for (int i = 0; i < minima.Size - 1; ++i)
			{
				// look for maximum within 2 minima
				int iMax = maxima.IndexOf(m => m > minima[i] && m < minima[i + 1]);
				if (iMax > -1)
				{
					int iPeak = maxima[iMax];

					// compute prominence and basis
					float prominence = spectrum[iPeak] - Math.Max(spectrum[minima[i]], spectrum[minima[i + 1]]);
					float basis = spectrum[iPeak] - prominence;

					// check prominence criterion
					if (prominence / basis < Protocol.Config.PeakMinPromToBasisRatio)
						continue;

					// get values in plot coordinates
					float left = spectrum.BinToFrequency(minima[i]);
					float right = spectrum.BinToFrequency(minima[i + 1]);

					// restrict width if needed
					if ((left, right).Size() > Protocol.Config.PeakMaxWidth)
					{
						float peak = spectrum.BinToFrequency(iPeak);
						left = Numerics.Clamp(peak - freqRange.Size() * (Protocol.Config.PeakMaxWidth / 2), freqRange);
						right = Numerics.Clamp(peak + freqRange.Size() * (Protocol.Config.PeakMaxWidth / 2), freqRange);
					}

					// check aspect ratio criterion (relative to freq and power ranges)
					float aspectRatio = (prominence / powerRange.Size()) / ((left, right).Size() / freqRange.Size());
					if (aspectRatio < Protocol.Config.PeakMinAspectRatio)
						continue;

					// save the peak if its prominence is bigger
					if (candidate < 0 || candidateProm < prominence)
					{
						candidate = iPeak;
						candidateProm = prominence;
					}
				}
			}

			Console.WriteLine($"peak: {candidate}");

			return candidate;
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
			using var spec = Get1DPowerSpectrum(periods);
			var iPeak = FindPeak(spec);

			if (iPeak >= 0)
			{
				blockResult = "cl4-update";
				return spec.BinToFrequency(iPeak);
			}
			else
			{
				blockResult = "cl4-nopeak";
				return currentFrequency;
			}
		}
	}
}
