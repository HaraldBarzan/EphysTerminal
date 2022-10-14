using System;
using TINS.Ephys.Processing;

namespace TINS.Terminal.UI
{
	/// <summary>
	/// An accumulator class used to display stuff to the GUI.
	/// </summary>
	public sealed class ContinuousDisplayAccumulator
		: MultichannelBuffer
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		ContinuousDisplayAccumulator()
			: base(nameof(ContinuousDisplayAccumulator))
		{
		}

		/// <summary>
		/// Create a continuous data display accumulator.
		/// </summary>
		/// <param name="capacity">Number of seconds of buffering.</param>
		/// <param name="sourceBuffer">The source buffer.</param>
		public ContinuousDisplayAccumulator(float capacity, MultichannelBuffer sourceBuffer)
			: this()
		{
			if (sourceBuffer is null || capacity < 1)
				throw new Exception("Invalid arguments.");

			// initialize accumulator
			Source				= sourceBuffer;
			Capacity			= Numerics.Round(capacity * sourceBuffer.SamplingRate);
			CurrentFillIndex	= 0;
			Configure(Name, sourceBuffer.ChannelLabels, Capacity, sourceBuffer.SamplingRate);
		}

		/// <summary>
		/// Reset accumulator data.
		/// </summary>
		public void Reset()
		{
			CurrentFillIndex	= 0;
			_previousLeftover	= 0;
			Fill(0);
		}

		/// <summary>
		/// Accumulate the data currently present in the source buffer.
		/// </summary>
		/// <param name="updatePeriod">The period, in seconds, at the end of the 
		/// source buffer to accumulate.</param>
		/// <returns>True if the buffer has been filled.</returns>
		public bool Accumulate(float updatePeriod = float.PositiveInfinity, bool countPreviousLeftover = false)
		{
			if (countPreviousLeftover) 
				updatePeriod += _previousLeftover;
			if (!Source.GetMostRecentPeriod(updatePeriod, out MatrixSpan<float> buffers))
				return IsFull;

			if (IsFull)
				Reset();

			// perform copy
			int copyCount = Math.Min(buffers.Cols, Capacity - CurrentFillIndex);
			for (int i = 0; i < buffers.Rows; ++i)
				buffers.GetRowSpan(i).Slice(0, copyCount).CopyTo(GetBuffer(i).Slice(CurrentFillIndex));

			CurrentFillIndex += copyCount;
			return IsFull;
		}

		/// <summary>
		/// Check whether the accumulator is full.
		/// </summary>
		public bool IsFull => CurrentFillIndex == Capacity;

		/// <summary>
		/// The maximum number of pooled buffers.
		/// </summary>
		public int Capacity { get; private set; }

		/// <summary>
		/// The current fill index.
		/// </summary>
		public int CurrentFillIndex { get; private set; }

		/// <summary>
		/// The source buffer.
		/// </summary>
		public MultichannelBuffer Source { get; private set; }

		private float _previousLeftover = 0;
	}
}
