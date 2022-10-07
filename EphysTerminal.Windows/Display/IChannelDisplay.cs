namespace TINS.Terminal.Display
{
	/// <summary>
	/// Types of displays.
	/// </summary>
	public enum DisplayType
	{
		Electrophysiology,
		EEG
	}


	/// <summary>
	/// Channel display interface. 
	/// </summary>
	public interface IChannelDisplay
	{
		/// <summary>
		/// Initialize the display.
		/// </summary>
		/// <param name="terminal">The source terminal.</param>
		public void InitializeChannelDisplay(EphysTerminal terminal);

		/// <summary>
		/// Clear any data from the display.
		/// </summary>
		public void ClearDisplay();

		/// <summary>
		/// Get the display type.
		/// </summary>
		public DisplayType DisplayType { get; }
	}
}
