using System;

namespace TINS.Ephys.Data
{
	/// <summary>
	/// Input data record.
	/// </summary>
	public class InputDataFrame
		: IDisposable
	{
		/// <summary>
		/// Analog input received from the amplifier.
		/// </summary>
		public Matrix<float> AnalogInput { get; } = new();

		/// <summary>
		/// Digital input received from the amplifier.
		/// </summary>
		public Vector<int> DigitalInput { get; } = new();

		/// <summary>
		/// Dispose method.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Overridable dispose method.
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				AnalogInput?.Dispose();
				DigitalInput?.Dispose();
			}
			_disposed = true;
		}

		/// <summary>
		/// Resize the storage.
		/// </summary>
		/// <param name="analogChannelCount">The number of analog channels (only one digital channel permitted).</param>
		/// <param name="samplesPerBlock">Number of samples per one block.</param>
		public void ResizeFrame(int analogChannelCount, int samplesPerBlock)
		{
			AnalogInput.Resize(analogChannelCount, samplesPerBlock);
			DigitalInput.Resize(samplesPerBlock);
		}


		private bool _disposed = false;
	}
}
