using System;

namespace TINS.Terminal.UI
{
	/// <summary>
	/// Provides methods for the GENUS to interact with user interfaces.
	/// </summary>
	public interface IUserInterface
	{
		/// <summary>
		/// Update the data on the screen.
		/// </summary>
		/// <param name="displayData">Display data item.</param>
		public void UpdateData(AbstractDisplayData displayData);

		/// <summary>
		/// Update user interface activity regarding new events.
		/// </summary>
		/// <param name="events">A list of new events.</param>
		public void UpdateEvents(Vector<int> events);

		/// <summary>
		/// Update the trial display of the user interface.
		/// </summary>
		/// <param name="currentTrialIndex">The zero-based index of the current trial.</param>
		/// <param name="totalTrialCount">The total number of trials.</param>
		public void UpdateTrialIndicator(int currentTrialIndex, int totalTrialCount);
	}


	/// <summary>
	/// 
	/// </summary>
	public abstract class AbstractDisplayData
		: IDisposable
	{
		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
		}

		/// <summary>
		/// Trigger an accumulation round.
		/// </summary>
		/// <param name="updatePeriod">The time frame, in seconds, at the end of the buffer to accumulate.</param>
		/// <returns>True if the buffer has reached maximum capacity.</returns>
		public abstract bool Accumulate(float updatePeriod = float.PositiveInfinity);
	}
}
