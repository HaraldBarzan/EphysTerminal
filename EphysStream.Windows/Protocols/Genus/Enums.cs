using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TINS.Ephys.Protocols.Genus
{
	/// <summary>
	/// The possible states of the Genus protocol.
	/// </summary>
	public enum GenusState
	{
		Idle,
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
		StimulationComplete,
		Stop
	}
}
