using System;

namespace TINS.Ephys.Analysis
{
	/// <summary>
	/// Abstract analysis result buffer.
	/// </summary>
	public class AnalysisResultBuffer
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		public AnalysisResultBuffer()
		{
		}

		/// <summary>
		/// Create an analysis result buffer.
		/// </summary>
		/// <param name="channelCount"></param>
		/// <param name="bufferSize"></param>
		/// <param name="bufferCounts"></param>
		/// <param name="channelLabels"></param>
		public AnalysisResultBuffer(int channelCount, int bufferSize, int bufferCounts, Vector<string> channelLabels = null)
			: this(new(channelCount, bufferCounts), bufferSize, channelLabels)
		{
		}

		/// <summary>
		/// Create an analysis result buffer.
		/// </summary>
		/// <param name="channelBufferCounts"></param>
		/// <param name="channelLabels"></param>
		/// <param name="channelBufferCounts"></param>
		public AnalysisResultBuffer(Vector<int> channelBufferCounts, int bufferSize, Vector<string> channelLabels = null)
		{
			_channelResults.Resize(channelBufferCounts.Size);
			for (int i = 0; i < _channelResults.Size; ++i)
				_channelResults[i] = new Matrix<float>(channelBufferCounts[i], bufferSize);

			if (channelLabels is object)
			{
				_channelLabels.Resize(_channelResults.Size);
			}
		}

		/// <summary>
		/// Get a result buffer from a channel.
		/// </summary>
		/// <param name="channelIndex">The index of the channel.</param>
		/// <param name="resultIndex">The index result buffer.</param>
		/// <returns>A span for the result buffer.</returns>
		public virtual Span<float> GetChannelResult(int channelIndex, int resultIndex = 0)
			=> _channelResults[channelIndex].GetBuffer(resultIndex);
		
		/// <summary>
		/// Get a result buffer from a channel.
		/// </summary>
		/// <param name="channelLabel">The label of the channel.</param>
		/// <param name="resultIndex">The index result buffer.</param>
		/// <returns>A span for the result buffer.</returns>
		public virtual Span<float> GetChannelResult(string channelLabel, int resultIndex = 0)
		{
			if (_channelLabels is null)
				throw new Exception("Labels not defined for this result item.");

			int channelIndex = _channelLabels.IndexOf(channelLabel);
			if (channelIndex == -1)
				throw new Exception("Label not found.");

			return GetChannelResult(channelIndex, resultIndex);
		}

		/// <summary>
		/// Get a result buffer from a channel.
		/// </summary>
		/// <param name="channelIndex">The index of the channel.</param>
		/// <param name="resultIndex">The index result buffer.</param>
		/// <returns>A span for the result buffer.</returns>
		public virtual Matrix<float> GetChannelMatrix(int channelIndex)
			=> _channelResults[channelIndex];

		/// <summary>
		/// Get a result buffer from a channel.
		/// </summary>
		/// <param name="channelLabel">The label of the channel.</param>
		/// <param name="resultIndex">The index result buffer.</param>
		/// <returns>A span for the result buffer.</returns>
		public virtual Matrix<float> GetChannelMatrix(string channelLabel)
		{
			if (_channelLabels is null)
				throw new Exception("Labels not defined for this result item.");

			int channelIndex = _channelLabels.IndexOf(channelLabel);
			if (channelIndex == -1)
				throw new Exception("Label not found.");

			return _channelResults[channelIndex];
		}

		/// <summary>
		/// Get the number of channels.
		/// </summary>
		public virtual int ChannelCount => _channelResults.Size;


		protected Vector<Matrix<float>> _channelResults = new();
		protected Vector<string>		_channelLabels	= null;
	}
}
