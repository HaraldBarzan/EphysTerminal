namespace TINS.Ephys.UI
{
	/// <summary>
	/// Provides methods for the GENUS to interact with user interfaces.
	/// </summary>
	public interface IUserInterface
	{
		/// <summary>
		/// Update user interface activity regarding multiunit activity (spikes).
		/// </summary>
		/// <param name="muaAccumulator">Multiunit activity.</param>
		public void UpdateMUA(ContinuousDisplayAccumulator muaAccumulator, SpikeDisplayAccumulator spikeAccumulator);

		/// <summary>
		/// Update user interface activity regarding local field potentials.
		/// </summary>
		/// <param name="lfpAccumulator">Local field potentials.</param>
		public void UpdateLFP(ContinuousDisplayAccumulator lfpAccumulator);

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
}
