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
		[INILine("STIMULATION_SECTION", "STIMULATION",
			"The section name for the stimulation section.")]
		public string StimulationSection { get; set; }
		
		/// <summary>
		/// The type of UI to use. Choice between EPHYS (intracortical-type recordings) and EEG.
		/// </summary>
		[INILine("UI_TYPE", "EPHYS",
			"The type of UI to use. Choice between EPHYS (intracortical-type recordings) and EEG.")]
		public string UIType { get; protected set; }

		/// <summary>
		/// The section name for the graphical user interface (GUI) section.
		/// </summary>
		[INILine("UI_SECTION", "UI", 
			"The section name for the graphical user interface (GUI) section.")]
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
		/// 
		/// </summary>
		/// <returns></returns>
		public override InputSettings CreateInputSettingsStub()
			=> InputType.ToLower() switch
			{
				"usb-me64"	=> new InputSettings(),
				_			=> base.CreateInputSettingsStub()
			};

	}
}
