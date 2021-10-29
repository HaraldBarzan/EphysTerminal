using System;
using TINS.Ephys.Settings;
using TINS.IO;

namespace TINS.Ephys.Processing
{
	/// <summary>
	/// The operating mode for the decimator.
	/// </summary>
	public enum DecimatorMode
	{
		Step,
		Average
	}

	/// <summary>
	/// A pipe that implements decimation (integer order downsampling).
	/// </summary>
	public class Decimator 
		: ProcessingPipe
	{
		/// <summary>
		/// Processing pipe constructor.
		/// </summary>
		/// <param name="settings">The settings for the decimator.</param>
		/// <param name="inputBuffer">The input buffer.</param>
		/// <param name="outputBuffer">The output buffer. If its size is not a good fit, it will be automatically resized.</param>
		public Decimator(
			DecimatorSettings	settings,
			MultichannelBuffer	inputBuffer,
			MultichannelBuffer	outputBuffer)
			: base(inputBuffer, outputBuffer, settings.Name)
		{
			if (ReferenceEquals(Input, Output))
				throw new Exception("Cannot decimate in-place. Input and output buffers must be different.");

			// configure the output buffer
			Output.Configure(Input.Rows, Input.Cols / settings.DecimationOrder, Input.SamplingRate / _order, Input.Labels);

			_order				= settings.DecimationOrder;
			_mode				= settings.DecimatorMode;
		}

		/// <summary>
		/// Process the input buffer.
		/// </summary>
		public override void Run()
		{
			// do decimation
			int count = Input.Cols / _order;
			switch (_mode)
			{
				case DecimatorMode.Step:	DecimateStep(_order, count);	break;
				case DecimatorMode.Average: DecimateAverage(_order, count);	break;
				default:													break;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="order"></param>
		/// <param name="count"></param>
		protected void DecimateStep(int order, int count)
		{
			for (int j = 0; j < count; ++j)
			{
				int pos = j * order;
				for (int i = 0; i < Input.Rows; ++i)
					Output[i, j] = Input[i, pos];
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="order"></param>
		/// <param name="count"></param>
		protected void DecimateAverage(int order, int count)
		{
			for (int j = 0; j < count; ++j)
			{
				int pos = j * order;
				for (int i = 0; i < Input.Rows; ++i)
				{
					float avg = 0;
					for (int k = 0; k < count; ++k)
						avg += Input[i, pos + k];
					Output[i, j] = avg / count;
				}
			}
		}


		protected int			_order;
		protected DecimatorMode _mode;
	}




	/// <summary>
	/// Settings for a decimator.
	/// </summary>
	public class DecimatorSettings : ProcessingPipeSettings
	{
		/// <summary>
		/// The decimation order.
		/// </summary>
		[INILine(Key = "ORDER", Default = 1)]
		public int DecimationOrder { get; set; }

		/// <summary>
		/// The operating mode of the decimator.
		/// </summary>
		public DecimatorMode DecimatorMode
		{
			get => (DecimatorMode)DecimatorModeInt;
			set => DecimatorModeInt = (int)value;
		}

		/// <summary>
		/// The operating mode of the decimator (integer).
		/// </summary>
		[INILine(Key = "MODE", Default = 0)]
		protected int DecimatorModeInt { get; set; }

		/// <summary>
		/// The designated type name for decimator settings.
		/// </summary>
		public override string TypeName => "DECIMATOR";
	}
}
