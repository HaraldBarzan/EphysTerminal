using System;
using System.Threading.Tasks;
using TINS.Ephys.Settings;
using TINS.Filtering;
using TINS.IO;

namespace TINS.Ephys.Processing
{
	/// <summary>
	/// Processing pipe implementing a parallel filter bank.
	/// </summary>
	public class FilterBank
		: ProcessingPipe
	{
		/// <summary>
		/// Processing pipe constructor.
		/// </summary>
		/// <param name="settings">The settings for the filter bank.</param>
		/// <param name="inputBuffer">The input buffer.</param>
		/// <param name="outputBuffer">The output buffer.</param>
		public FilterBank(FilterBankSettings settings, MultichannelBuffer inputBuffer, MultichannelBuffer outputBuffer)
			: base(inputBuffer, outputBuffer, settings.Name)
		{
			Output			.Configure(Input.Dimensions, Input.SamplingRate, Input.Labels);
			_filters		.Resize(Math.Min(inputBuffer.Rows, outputBuffer.Rows));
			_parallelOpts	.MaxDegreeOfParallelism = settings.ThreadCount;
			
			for (int i = 0; i < _filters.Size; ++i)
			{
				_filters[i] = new IIRFilter(
					filterType:			settings.FilterType,
					filterPassType:		settings.FilterPassType,
					order:				settings.Order,
					samplingRate:		inputBuffer.SamplingRate,
					cutoffFrequencies:	(settings.Cutoff1, settings.Cutoff2));
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				_filters?.Dispose();
		}

		/// <summary>
		/// Reset all filter states.
		/// </summary>
		public void Reset() => _filters.ForEach(f => f.ResetState());


		/// <summary>
		/// Run the filter pipe.
		/// </summary>
		public override void Run() => Parallel.For(0, _filters.Size, _parallelOpts, i => 
		{
			var inputBuffer		= Input.GetBuffer(i);
			var outputBuffer	= Output.GetBuffer(i);
			_filters[i]			.ForwardFilter(inputBuffer, outputBuffer);
		});


		protected ParallelOptions	_parallelOpts	= new();
		protected Vector<IIRFilter> _filters		= new();
	}



	/// <summary>
	/// Settings for an IIR filter.
	/// </summary>
	public class FilterBankSettings 
		: ProcessingPipeSettings
	{
		/// <summary>
		/// The family of the IIR filter.
		/// </summary>
		public IIRFilterType FilterType
		{
			get => (IIRFilterType)FilterTypeInt;
			set => FilterTypeInt = (int)value;
		}

		/// <summary>
		/// The pass characteristic of the IIR filter.
		/// </summary>
		public FilterPass FilterPassType
		{
			get => (FilterPass)FilterPassTypeInt;
			set => FilterPassTypeInt = (int)value;
		}

		/// <summary>
		/// The order of the IIR filter.
		/// </summary>
		[INILine(Key = "ORDER", Default = 0)]
		public int Order { get; set; }

		/// <summary>
		/// The first cutoff frequency.
		/// </summary>
		[INILine(Key = "CUTOFF_1")]
		public double Cutoff1 { get; set; }

		/// <summary>
		/// The second cutoff frequency (unused by low- and highpass filters).
		/// </summary>
		[INILine(Key = "CUTOFF_2")]
		public double Cutoff2 { get; set; }

		/// <summary>
		/// The family of the IIR filter.
		/// </summary>
		[INILine(Key = "FILTER_TYPE", Default = 1)]
		protected int FilterTypeInt { get; set; }
		
		/// <summary>
		/// The pass characteristic of the IIR filter.
		/// </summary>
		[INILine(Key = "FILTER_PASS_TYPE", Default = 2)]
		protected int FilterPassTypeInt { get; set; }

		/// <summary>
		/// The designated type name for IIR filter settings.
		/// </summary>
		public override string TypeName => "IIR";
	}
}
