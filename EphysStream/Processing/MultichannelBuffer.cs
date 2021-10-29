using System;

namespace TINS.Ephys.Processing
{
	/// <summary>
	/// A matrix-like channel buffer.
	/// </summary>
	public class MultichannelBuffer : Matrix<float>
	{
		/// <summary>
		/// Create a named buffer.
		/// </summary>
		/// <param name="name">The name of the buffer.</param>
		public MultichannelBuffer(string name)
			: base()
		{
			if (string.IsNullOrEmpty(name))
				throw new Exception("The buffer must have a valid name.");
			Name = name;
		}

		/// <summary>
		/// Create a named buffer.
		/// </summary>
		/// <param name="name">The name of the buffer.</param>
		/// <param name="rows">The number of rows.</param>
		/// <param name="cols">The number of columns.</param>
		/// <param name="samplingRate">The sampling rate.</param>
		/// <param name="labels">The list of labels for each channel.</param>
		public MultichannelBuffer(string name, int rows, int cols, float? samplingRate = null, Vector<string> labels = null)
			: this(name)
		{
			// resize the buffer
			Resize(rows, cols);

			if (samplingRate.HasValue)
				SamplingRate = samplingRate.Value;

			if (labels is null)
				AutoPopulateLabelList(rows);
			else
			{
				if (labels.Size < rows)
				{
					AutoPopulateLabelList(rows);
					Labels.Sub(0, labels.Size).Assign(labels);
				}
				else
				{
					Labels.Resize(Math.Min(rows, labels.Size));
					Labels.Assign(labels.Sub(0, rows));
				}
			}
		}

		/// <summary>
		/// Create a named buffer.
		/// </summary>
		/// <param name="rows">The new dimensions.</param>
		/// <param name="samplingRate">The new desired sampling rate (if it needs to change).</param>
		/// <param name="labels">The new list of labels for each channel (if it needs to change).</param>
		public void Configure((int Rows, int Cols) dimensions, float? samplingRate = null, Vector<string> labels = null)
			=> Configure(dimensions.Rows, dimensions.Cols, samplingRate, labels);

		/// <summary>
		/// Create a named buffer.
		/// </summary>
		/// <param name="rows">The new number of rows.</param>
		/// <param name="cols">The new number of columns.</param>
		/// <param name="samplingRate">The new desired sampling rate (if it needs to change).</param>
		/// <param name="labels">The new list of labels for each channel (if it needs to change).</param>
		public void Configure(int rows, int cols, float? samplingRate = null, Vector<string> labels = null)
		{

			// resize the buffer
			int oldChannelCount = Rows;
			base.Resize(rows, cols);

			if (samplingRate.HasValue)
				SamplingRate = samplingRate.Value;

			if (labels is null && oldChannelCount != rows)
				AutoPopulateLabelList(rows);
			else
			{
				if (labels.Size < rows)
				{
					AutoPopulateLabelList(rows);
					Labels.Sub(0, labels.Size).Assign(labels);
				}
				else
				{
					Labels.Resize(Math.Min(rows, labels.Size));
					Labels.Assign(labels.Sub(0, rows));
				}
			}
		}

		/// <summary>
		/// Override the resize to throw an error.
		/// </summary>
		/// <param name="rows"></param>
		/// <param name="cols"></param>
		public override void Resize(int rows, int cols)
		{
			if ((rows, cols) != Dimensions)
				throw new Exception("This buffer must be resized via Configure.");
		}

		/// <summary>
		/// Get a buffer.
		/// </summary>
		/// <param name="channelLabel">The label of the channel.</param>
		/// <returns>A span for the result buffer.</returns>
		public Span<float> GetBuffer(string channelLabel)
		{
			int channelIndex = Labels.IndexOf(channelLabel);
			if (channelIndex == -1)
				throw new Exception("Label not found.");

			return GetBuffer(channelIndex);
		}

		/// <summary>
		/// The name of the buffer.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The sampling rate of the contained signals.
		/// </summary>
		public float SamplingRate { get; set; }

		/// <summary>
		/// The list of labels.
		/// </summary>
		public Vector<string> Labels { get; } = new();

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override string ToString()
			=> $"\'{Name}\' ({Rows}x{Cols}, sampling rate: {SamplingRate} Hz)";

		/// <summary>
		/// Creates an artificial label list.
		/// </summary>
		protected void AutoPopulateLabelList(int channelCount)
		{
			Labels.Resize(channelCount);

			// count the number of digits
			int nDigits	= 0;
			while (channelCount > 0)
			{
				++nDigits;
				channelCount /= 10;
			}
			string format = "D" + nDigits;

			// create format list
			for (int i = 0; i < Labels.Size; ++i)
				Labels[i] = $"El_{(i + 1).ToString(format)}";
		}
	}
}
