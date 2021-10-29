using System;
using System.Threading.Tasks;
using TINS.Ephys.Processing;
using TINS.Ephys.Settings;
using TINS.Filtering;
using TINS.IO;

namespace TINS.Ephys.Analysis
{
	/// <summary>
	/// The method used by a spectrum analyzer.
	/// </summary>
	public enum SpectrumMethod
	{
		Fourier,
		Superlet
	}

	/// <summary>
	/// Spectrum analyzer for continuous data.
	/// </summary>
	public class SpectrumAnalyzer
		: AnalysisPipe
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="settings"></param>
		public SpectrumAnalyzer(SpectrumSettings settings, MultichannelBuffer inputBuffer)
			: base(settings.Name, inputBuffer)
		{
			// set multithread options
			int realThreadCount = Math.Max(Input.BufferSize, settings.ThreadCount);
			_parallelOpts.MaxDegreeOfParallelism = realThreadCount;

			// configure the result
			// Result.Configure(...);

		}

		/// <summary>
		/// Run the spectrum analyzer.
		/// </summary>
		public override void Run()
		{
			if (_units.IsEmpty)
				return;

			if (_units.Size == 1)
			{
				for (int i = 0; i < Input.BufferCount; ++i)
					_units.Front.RunUnit2D(Input.GetBuffer(i), Result.GetChannelMatrix(i));
			}
			else
			{
				// execute the operation in parallel
				Parallel.For(0, Input.BufferCount, _parallelOpts, (i) =>
				{
					// perform analysis (lock the unit)
					var unit = _units[i % _units.Size];
					lock (unit)
					{
						unit.RunUnit2D(Input.GetBuffer(i), Result.GetChannelMatrix(i));
					}
				});
			}
		}

		/// <summary>
		/// The result item for this analyzer.
		/// </summary>
		public AnalysisResultBuffer Result { get; } = new();

		protected Vector<ISpectrumAnalyzerUnit> _units			= new();
		protected ParallelOptions				_parallelOpts	= new();
	}

	/// <summary>
	/// The settings for the spectrum analyzer.
	/// </summary>
	public class SpectrumSettings
		: AnalysisPipeSettings
	{
		/// <summary>
		/// The method to use.
		/// </summary>
		public SpectrumMethod Method
		{
			get => (SpectrumMethod)MethodInt;
			set => MethodInt = (int)value;
		}

		[INILine(Key = "METHOD", Default = (int)SpectrumMethod.Fourier)]
		protected int MethodInt { get; set; }

		/// <summary>
		/// The lower frequency boundary.
		/// </summary>
		[INILine(Key = "FREQUENCY_MIN", Default = 30f)]
		public float FrequencyLow { get; set; }

		/// <summary>
		/// The upper frequency boundary.
		/// </summary>
		[INILine(Key = "FREQUENCY_MAX", Default = 80f)]
		public float FrequencyHigh { get; set; }

		/// <summary>
		/// The size of the analysis window in samples.
		/// </summary>
		[INILine(Key = "FOURIER_WINDOW_SIZE", Default = 0.15f)]
		public float FourierWindowSize { get; set; }

		/// <summary>
		/// The size of the DFT window in samples.
		/// </summary>
		[INILine(Key = "DFT_SIZE", Default = 512)]
		public int FourierDFTSize { get; set; }

		/// <summary>
		/// The step size of the DFT in seconds.
		/// </summary>
		[INILine(Key = "FOURIER_WINDOW_STEP", Default = 0.01f)]
		public float FourierStepSize { get; set; }

		/// <summary>
		/// The type of the Fourier window.
		/// </summary>
		public WindowType FourierWindowType
		{
			get => (WindowType)FourierWindowTypeInt;
			set => FourierWindowTypeInt = (int)value;
		}

		[INILine(Key = "FOURIER_WINDOW_TYPE", Default = (int)WindowType.Blackman)]
		protected int FourierWindowTypeInt { get; set; }

		/// <summary>
		/// The number of wavelets or superlets for the defined frequency range.
		/// </summary>
		[INILine(Key = "SUPERLET_BIN_COUNT", Default = 26)]
		public int SuperletBinCount { get; set; }

		/// <summary>
		/// The base wavelet number of cycles.
		/// </summary>
		[INILine(Key = "SUPERLET_BASE_CYCLES", Default = 2.5f)]
		public float SuperletBaseCycles { get; set; }
		
		/// <summary>
		/// The order of a superlet.
		/// </summary>
		[INILine(Key = "SUPERLET_ORDER", Default = 3f)]
		public float SuperletOrder { get; set; }

		/// <summary>
		/// The maximum order of a superlet (may be different from the minimum).
		/// </summary>
		[INILine(Key = "SUPERLET_ORDER_MAX", Default = 3f)]
		public float SuperletOrderMax { get; set; }


	}
}
