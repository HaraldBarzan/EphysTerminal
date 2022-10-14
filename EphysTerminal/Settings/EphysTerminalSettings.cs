using System;
using TINS.Ephys.Settings;
using TINS.IO;
using TINS.Terminal.Settings.UI;

namespace TINS.Terminal.Settings
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
		/// Get or set the type of UI.
		/// </summary>
		[INILine(Key = "UI_TYPE", Default = "EPHYS")]
		public string UIType { get; protected set; }

		/// <summary>
		/// The section name for the graphical user interface (GUI) section.
		/// </summary>
		[INILine(Key = "UI_SECTION", Default = "UI")]
		public string UISection { get; set; }

		/// <summary>
		/// The analysis pipeline settings.
		/// </summary>
		public UISettings UI { get; set; }

		/// <summary>
		/// Serialize a <c>EphysTerminalSettings</c> item from an INI file.
		/// </summary>
		/// <param name="ini">The INI file.</param>
		/// <param name="sectionName">The section to serialize from.</param>
		/// <param name="direction">The direction of serialization.</param>
		public override void Serialize(INI ini, string sectionName, SerializationDirection direction)
		{
			base.Serialize(ini, sectionName, direction);

			if (direction is SerializationDirection.In)
			{
				switch (UIType)
				{
					case "EPHYS":
						UI = new UISettingsEphys();
						break;

					case "EEG":
						UI = new UISettingsEEG();
						break;

					default:
						throw new Exception($"UI type \'{UIType}\' not recognized.");
				}
			}

			UI.Serialize(ini, UISection, direction);
		}

		/// <summary>
		/// Attempt to detect and instantiate the correct input settings.
		/// </summary>
		/// <returns>True if the name is recognized, false otherwise.</returns>
		public override bool DetectInputSettings()
		{
			if (!base.DetectInputSettings())
			{
				switch (InputType)
				{
					case "LOCAL":
						Input = new InputSettings();
						return true;

					default:
						return false;
				}

			}
			return true;
		}
	}
}
