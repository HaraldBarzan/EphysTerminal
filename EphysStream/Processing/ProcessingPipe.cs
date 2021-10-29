using System;

namespace TINS.Ephys.Processing
{
	/// <summary>
	/// Abstract base class for processing pipes.
	/// </summary>
	public abstract class ProcessingPipe
		: IDisposable
	{
		/// <summary>
		/// Processing pipe constructor.
		/// </summary>
		/// <param name="inputBuffer">The input buffer.</param>
		/// <param name="outputBuffer">The output buffer.</param>
		/// <param name="name">The name of the processing pipe.</param>
		public ProcessingPipe(MultichannelBuffer inputBuffer, MultichannelBuffer outputBuffer, string name)
		{
			if (inputBuffer is null)	throw new NullReferenceException($"Missing input buffer.");
			if (outputBuffer is null)	throw new NullReferenceException($"Missing output buffer.");

			Name				= name ?? Guid.NewGuid().ToString();
			Input				= inputBuffer;
			Output				= outputBuffer;
			Output.SamplingRate = Input.SamplingRate;
		}

		/// <summary>
		/// Dispose of the processing pipe.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Dispose of the processing pipe.
		/// </summary>
		/// <param name="disposing">False if dispose was called via the finalizer.</param>
		protected virtual void Dispose(bool disposing)
		{
			_ = disposing;
		}

		/// <summary>
		/// Run the pipe for the configured buffers.
		/// </summary>
		public abstract void Run();

		/// <summary>
		/// The name of the pipe.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The input buffer.
		/// </summary>
		public MultichannelBuffer Input { get; protected set; }
		
		/// <summary>
		/// The output buffer.
		/// </summary>
		public MultichannelBuffer Output { get; protected set; }
	}
}
