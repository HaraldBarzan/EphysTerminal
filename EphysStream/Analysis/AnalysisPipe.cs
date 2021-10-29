using System;
using TINS.Ephys.Processing;

namespace TINS.Ephys.Analysis
{
	/// <summary>
	/// 
	/// </summary>
	public abstract class AnalysisPipe
		: IDisposable
	{
		/// <summary>
		/// Create an analysis pipe.
		/// </summary>
		/// <param name="name">The name of the pipe.</param>
		/// <param name="inputBuffer">The input processing buffer.</param>
		public AnalysisPipe(string name, MultichannelBuffer inputBuffer)
		{
			if (inputBuffer is null) throw new Exception("This pipe must have a valid input buffer.");

			Input	= inputBuffer;
			Name	= name ?? Guid.NewGuid().ToString();
		}

		/// <summary>
		/// Dispose of the pipe.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Dispose of the pipe.
		/// </summary>
		/// <param name="disposing">True if called via dispose, false if via finalizer.</param>
		protected virtual void Dispose(bool disposing)
		{
		}

		/// <summary>
		/// Run the pipe.
		/// </summary>
		public abstract void Run();

		/// <summary>
		/// The name of this pipe.
		/// </summary>
		public string Name { get; protected set; }

		/// <summary>
		/// The input buffer of this pipe.
		/// </summary>
		public MultichannelBuffer Input { get; protected set; }
	}
}
