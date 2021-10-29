using System;
using TINS.Analysis;

namespace TINS.Ephys.Analysis
{
	/// <summary>
	/// Interface to be implemented by threading units.
	/// </summary>
	public interface ISpectrumAnalyzerUnit
		: IDisposable
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="inputData"></param>
		/// <param name="result"></param>
		public void RunUnit2D(Span<float> inputData, Matrix<float> result);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="inputData"></param>
		/// <param name="result"></param>
		//public void RunUnit1D(Span<float> inputData, Span<float> result);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rows"></param>
		/// <param name="cols"></param>
		public void GetOutputDimensions2D(int inputSize, out int rows, out int cols);
	}

	/// <summary>
	/// A thread unit for the channel spectrum analyzer.
	/// </summary>
	class WaveletSpectrumAnalyzerUnit 
		: WaveletSpectrumAnalyzer
		, ISpectrumAnalyzerUnit
	{
		/// <summary>
		/// Create a Wavelet analyzer unit.
		/// </summary>
		/// <param name="settings">The settings for the analyzer.</param>
		public WaveletSpectrumAnalyzerUnit(WaveletAnalyzerSettings settings)
			: base(settings)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="inputData"></param>
		/// <param name="result"></param>
		public void RunUnit2D(Span<float> inputData, Matrix<float> result)
			=> TimeResolvedWaveletSpectrum(inputData, result);
		 
		/// <summary>
		/// 
		/// </summary>
		/// <param name="rows"></param>
		/// <param name="cols"></param>
		public void GetOutputDimensions2D(int inputSize, out int rows, out int cols)
		{
			_ = inputSize;
			rows = Settings.FrequencyBinCount;
			cols = Settings.InputSize;
		}
	}

	/// <summary>
	/// A thread unit for the channel spectrum analyzer.
	/// </summary>
	class FourierSpectrumAnalyzerUnit 
		: FourierSpectrumAnalyzer
		, ISpectrumAnalyzerUnit
	{
		/// <summary>
		/// Create a Fourier analysis unit.
		/// </summary>
		/// <param name="settings">The settings for the Fourier analyzer.</param>
		/// <param name="inputSize">The input size (to be locked).</param>
		/// <param name="freqRange">The output frequency range.</param>
		public FourierSpectrumAnalyzerUnit(FourierAnalyzerSettings settings, int inputSize, (float Lower, float Upper) freqRange)
			: base(settings)
		{
			_windowCount = settings.CountWindows(inputSize);
			_temp.Initialize(_outputSize, _windowCount, (0, settings.SamplingRate / 2), settings.SamplingRate);
			_freqBegin = _temp.FrequencyToBin(freqRange.Lower);
			_freqCount = _temp.FrequencyToBin(freqRange.Upper) - _freqBegin + 1;
		}

		/// <summary>
		/// Dispose to remove the temporaries.
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				_temp?.Dispose();
			base.Dispose(disposing);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="inputData"></param>
		/// <param name="result"></param>
		public void RunUnit2D(Span<float> inputData, Matrix<float> result)
		{
			int windowCount = Settings.CountWindows(inputData.Length);
			if (_windowCount != windowCount)
				throw new Exception("Input size mismatch.");
			
			// perform STFT
			TimeResolvedFourierSpectrum(inputData, _temp, windowCount);

			// truncate the spectrum
			result.Assign(_temp.Sub(_freqBegin, 0, _freqCount, _temp.Cols));
		}
		 
		/// <summary>
		/// 
		/// </summary>
		/// <param name="rows"></param>
		/// <param name="cols"></param>
		public void GetOutputDimensions2D(int inputSize, out int rows, out int cols)
		{
			rows = _freqCount;
			cols = _windowCount;
		}

		private int			_freqBegin;
		private int			_freqCount;
		private int			_windowCount;
		private Spectrum2D	_temp			= new();
	}


}
