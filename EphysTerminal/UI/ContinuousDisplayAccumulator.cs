using System;
using TINS.Ephys.Processing;

namespace TINS.Terminal.UI
{
	/// <summary>
	/// An accumulator class used to display stuff to the GUI.
	/// </summary>
	public sealed class ContinuousDisplayAccumulator
		: IDisposable
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		ContinuousDisplayAccumulator()
		{
		}

		/// <summary>
		/// Create a continuous data display accumulator.
		/// </summary>
		/// <param name="pollCapacity">The maximum number of source accumulations until full.</param>
		/// <param name="sourceBuffer">The source buffer.</param>
		public ContinuousDisplayAccumulator(int pollCapacity, MultichannelBuffer sourceBuffer)
		{
			if (sourceBuffer is null || pollCapacity < 1)
				throw new Exception("Invalid arguments.");

			// initialize accumulator
			PollCapacity		= pollCapacity;
			Source				= sourceBuffer;
			CurrentFillIndex	= 0;
			Data				.Resize(Source.Rows, PollCapacity * Source.Cols);
		}

		/// <summary>
		/// Destroy the accumulator.
		/// </summary>
		public void Dispose()
		{
			Data?.Dispose();
			CurrentFillIndex	= 0;
			PollCapacity		= 0;
			Source				= null;
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Reset accumulator data.
		/// </summary>
		public void Reset()
		{
			CurrentFillIndex = 0;
			Data.Fill(0);
		}

		/// <summary>
		/// Accumulate the data currently present in the source buffer.
		/// </summary>
		public void Accumulate()
		{
			if (IsFull) 
				Reset();

			int pos = CurrentFillIndex * Source.Cols;
			lock (Source)
				Data.Sub(0, pos, Source.Rows, Source.Cols).Assign(Source);

			++CurrentFillIndex;
		}

		/// <summary>
		/// Get a buffer from the accumulator.
		/// </summary>
		public Span<float> GetBuffer(int index) => Data.GetBuffer(index);

		/// <summary>
		/// Check whether the accumulator is full.
		/// </summary>
		public bool IsFull => CurrentFillIndex == PollCapacity;

		/// <summary>
		/// The total size of each buffer.
		/// </summary>
		public int BufferSize => Data.Cols;

		/// <summary>
		/// The number of supported channels.
		/// </summary>
		public int ChannelCount => Data.Rows;

		/// <summary>
		/// The maximum number of pooled buffers.
		/// </summary>
		public int PollCapacity { get; private set; }

		/// <summary>
		/// The current fill index.
		/// </summary>
		public int CurrentFillIndex { get; private set; }

		/// <summary>
		/// The source buffer.
		/// </summary>
		public MultichannelBuffer Source { get; private set; }

		/// <summary>
		/// The data buffer.
		/// </summary>
		public Matrix<float> Data { get; } = new();

		/// <summary>
		/// Create a dummy display accumulator with the specified number of rows and columns.
		/// </summary>
		/// <param name="rows">The number of rows.</param>
		/// <param name="cols">The number of columns.</param>
		/// <returns>A dummy display accumulator.</returns>
		public static ContinuousDisplayAccumulator CreateDummy(int rows, int cols)
		{
			var result = new ContinuousDisplayAccumulator()
			{
				PollCapacity		= 1,
				CurrentFillIndex	= 1,
				Source				= null
			};
			result.Data.Resize(rows, cols);
			return result;
		}
	}
}
