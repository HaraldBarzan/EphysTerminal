using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TINS.IO;

namespace TINS.Ephys.Settings
{
	public class EphysTerminalSettings
		: StreamSettings
	{
		/// <summary>
		/// Ephys terminal header section.
		/// </summary>
		public override string HeaderSection => "ELECTROPHYSIOLOGY_CONFIGURATION";

		/// <summary>
		/// The section name for the stimulation section.
		/// </summary>
		[INILine(Key = "STIMULATION_SECTION", Default = "STIMULATION")]
		public string StimulationSection { get; set; }

		/// <summary>
		/// The section name for the graphical user interface (GUI) section.
		/// </summary>
		[INILine(Key = "UI_SECTION", Default = "UI")]
		public string UISection { get; set; }

		/// <summary>
		/// The analysis pipeline settings.
		/// </summary>
		public UISettings UI { get; } = new();

		/// <summary>
		/// Get the input device.
		/// </summary>
		public TerminalInputDevice InputDevice => (TerminalInputDevice)Input.GetInputDeviceID();

		/// <summary>
		/// Serialize a <c>EphysTerminalSettings</c> item from an INI file.
		/// </summary>
		/// <param name="ini">The INI file.</param>
		/// <param name="sectionName">The section to serialize from.</param>
		/// <param name="direction">The direction of serialization.</param>
		public override void Serialize(INI ini, string sectionName, SerializationDirection direction)
		{
			base.Serialize(ini, sectionName, direction);
			UI.Serialize(ini, UISection, direction);
		}
	}
}
