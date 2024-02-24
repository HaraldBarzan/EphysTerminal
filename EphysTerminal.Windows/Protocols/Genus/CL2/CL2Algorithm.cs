using System;
using System.Threading;
using TINS.Containers;
using TINS.Ephys.Analysis;
using TINS.Terminal.Display;

namespace TINS.Terminal.Protocols.Genus.CL2
{
	using TFSpectrumAnalyzer = IAnalyzerOutput<Ring<ChannelTimeResult<Spectrum2D>>>;


	/// <summary>
	/// Closed loop algorithm type.
	/// </summary>
	public enum CL2AlgorithmVersion
	{
		ArgMaxFollower,
		PeakFollowerDelta,
		DichotomicExplorator,
		Washout,
		Static
	}

	/// <summary>
	/// 
	/// </summary>
	public abstract class CL2Algorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		public CL2Algorithm(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
		{
			Protocol = protocol;
			SpectrumAnalyzer = analyzer;
		}

		/// <summary>
		/// Parent protocol.
		/// </summary>
		public GenusCL2 Protocol { get; protected set; }

		/// <summary>
		/// Spectrum analyzer.
		/// </summary>
		public TFSpectrumAnalyzer SpectrumAnalyzer { get; protected set; }

		/// <summary>
		/// Reset the block counter.
		/// </summary>
		public void ResetBlockCounter() => _blockCounter = 0;

		/// <summary>
		/// Get the type of the current block.
		/// </summary>
		public abstract string CurrentBlockType { get; }

		/// <summary>
		/// 
		/// </summary>
		public RealTimeSpectrumDisplay Viewer { get; protected set; }

		/// <summary>
		/// Open a spectrum viewer window.
		/// </summary>
		public void OpenSpectrumViewer()
		{
			var t = new Thread(() =>
			{
				Viewer = new RealTimeSpectrumDisplay();
				Viewer.TopMost = true;
				Viewer.FormClosing += (_, _) => Viewer = null;
				Viewer.ShowDialog();
			});
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="blocks"></param>
		public void AccumulateBaseline(int blocks)
		{
			if (SpectrumAnalyzer.GetOutput(out var resultRing))
			{
				// only compute up to update block duration (if possible)
				int resultCount = Math.Min(blocks, resultRing.Size);

				// create baseline object
				_baseline = new ZScoreAccumulator(resultRing[0].Results[0].Rows);
				if (Protocol.Config.UseLog10)
				{
					using var buffer = new Vector<float>(_baseline.Size);
					for (int iResult = 0; iResult < resultCount; ++iResult)
					{
						foreach (var item in resultRing[iResult].Results)
						{
							for (int j = 0; j < item.Cols; ++j)
							{
								for (int i = 0; i < item.Rows; ++i)
									buffer[i] = item[i, j];
								_baseline.Push(buffer);
							}
						}
					}
				}
				else
				{
					for (int iResult = 0; iResult < resultCount; ++iResult)
						foreach (var item in resultRing[iResult].Results)
							_baseline.PushColumns(item);
				}

				_baseline.Update();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void ResetBaseline()
		{
			_baseline?.Dispose();
			_baseline = null;
		}

		/// <summary>
		/// Get the 1D power spectrum.
		/// </summary>
		/// <returns></returns>
		public virtual Spectrum1D Get1DPowerSpectrum(int blocks)
		{
			if (SpectrumAnalyzer.GetOutput(out var resultRing))
			{
				// only compute up to update block duration (if possible)
				int resultCount = Math.Min(blocks, resultRing.Size);

				// frequency spectrum
				var spectrum1d	= new Spectrum1D(resultRing[0].Results[0].Rows, resultRing[0].Results[0].FrequencyRange);
				int pushedCount = 0;
				if (Protocol.Config.UseLog10)
				{
					for (int iResult = 0; iResult < resultCount; ++iResult)
					{
						foreach (var item in resultRing[iResult].Results)
						{
							for (int i = 0; i < item.Rows; ++i)
								for (int j = 0; j < item.Cols; ++j)
									spectrum1d[i] += MathF.Log10(item[i, j] + 1e-6f);
							pushedCount += item.Cols;
						}
					}
				}
				else
				{
					for (int iResult = 0; iResult < resultCount; ++iResult)
					{
						foreach (var item in resultRing[iResult].Results)
						{
							for (int i = 0; i < item.Rows; ++i)
								for (int j = 0; j < item.Cols; ++j)
									spectrum1d[i] += item[i, j];
							pushedCount += item.Cols;
						}
					}
				}

				// scale by how many columns have been pushed
				if (pushedCount > 0)
					spectrum1d.Scale(1f / pushedCount);

				if (_baseline is not null && _baseline.LinesAccumulated > 0)
					_baseline.ZScore(spectrum1d);

				// plot to viewer (clone to avoid disposal)
				Viewer?.Plot(spectrum1d.Clone() as Spectrum1D);
				Thread.Sleep(100);

				return spectrum1d;
			}

			throw new Exception("Could not fetch result ring.");
		}

		/// <summary>
		/// Find a peak inside the given spectrum.
		/// </summary>
		/// <param name="spectrum"></param>
		/// <returns></returns>
		public virtual int FindPeak(Spectrum1D spectrum)
		{
			// compute necessary parameters
			var freqRange			= spectrum.FrequencyRange;
			var powerRange			= Numerics.GetRange(spectrum.Span);
			using var minima		= Numerics.FindLocalMinima(spectrum);
			using var maxima		= Numerics.FindLocalMaxima(spectrum);
			int candidate			= -1;

			// we look at maxima, searching for two flanking minima
			if (maxima.Size < 1 || minima.Size < 2)
				return candidate;

			for (int i = 0; i < minima.Size - 1; ++i)
			{
				// look for maximum within 2 minima
				int iMax = maxima.IndexOf(m => m > minima[i] && m < minima[i + 1]);
				if (iMax > -1)
				{
					int iPeak = maxima[iMax]; // get spectum index of maximum

					// compute prominence and basis
					float prominence	= Math.Abs(spectrum[iPeak] - Math.Max(spectrum[minima[i]], spectrum[minima[i + 1]]));
					float basis			= Math.Abs(spectrum[iPeak] - prominence - powerRange.Lower);

					// check prominence criterion
					if (prominence / basis < Protocol.Config.PeakMinPromToBasisRatio)
						continue;

					// get values in plot coordinates
					float left	= spectrum.BinToFrequency(minima[i]);
					float right = spectrum.BinToFrequency(minima[i + 1]);

					// restrict width if needed
					if ((left, right).Size() > Protocol.Config.PeakMaxWidth)
					{
						float peak	= spectrum.BinToFrequency(iPeak);
						left		= Numerics.Clamp(peak - freqRange.Size() * (Protocol.Config.PeakMaxWidth / 2), freqRange);
						right		= Numerics.Clamp(peak + freqRange.Size() * (Protocol.Config.PeakMaxWidth / 2), freqRange);
					}

					// check aspect ratio criterion (relative to freq and power ranges)
					float aspectRatio = (prominence / powerRange.Size()) / ((left, right).Size() / freqRange.Size());
					if (aspectRatio < Protocol.Config.PeakMinAspectRatio)
						continue;

					// save the peak if it is bigger
					if (candidate < 0 || spectrum[candidate] < spectrum[iPeak])
						candidate = iPeak;
				}
			}

			return candidate;
		}

		/// <summary>
		/// Compute the next stimulus frequency based on the input power spectrum 
		/// and the current stimulus frequency.
		/// </summary>
		/// <returns></returns>
		public virtual float ComputeNextStimulusFrequency(float currentFrequency, int periods, out string blockResult)
		{
			blockResult = string.Empty;
			_blockCounter++;
			return currentFrequency;
		}


		protected int _blockCounter = 0;
		protected ZScoreAccumulator _baseline;
	}
}
