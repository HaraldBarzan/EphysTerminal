namespace TINS.Terminal.Protocols.Genus
{
	/// <summary>
	/// The possible states of the Genus protocol.
	/// </summary>
	public enum GenusState
	{
		Idle,
		Await,
		Prestimulus,
		Stimulus,
		Poststimulus,
		Intertrial
	}

	/// <summary>
	/// The possible events registered by the Genus protocol.
	/// </summary>
	public enum GenusEvent
	{
		Start,
		NewBlock,
		InitiateTrial,
		StimulationComplete,
		Stop
	}
}
