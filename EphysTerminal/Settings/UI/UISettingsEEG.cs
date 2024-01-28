using TINS.IO;

namespace TINS.Terminal.Settings.UI
{
	/// <summary>
	/// Settings for the EEG UI display.
	/// </summary>
	public class UISettingsEEG
        : UISettings
    {
		/// <summary>
		/// Type name for EEG UI settings.
		/// </summary>
		public override string TypeName => "EEG";

		/// <summary>
		/// Display electroencephalography data.
		/// </summary>
		[INILine("SHOW_EEG", true, 
			"Display electroencephalography data.")]
		public bool ShowEEG { get; set; }

		/// <summary>
		/// The period of the EEG update, in seconds.
		/// </summary>
		[INILine("EEG_UPDATE_PERIOD", 1, 
			"The period of the EEG update, in seconds.")]
		public float EEGUpdatePeriod { get; set; }

		/// <summary>
		/// EEG display buffer name.
		/// </summary>
		[INILine("EEG_DISPLAY_BUFFER",
			Comment = "Name of the source buffer (defined in the PROCESSING settings) for the EEG display.")]
		public string EEGInputBuffer { get; set; }

		/// <summary>
		/// The channels to display, in top-to-bottom order.
		/// </summary>
		[INIVector("DISPLAY_CHANNELS_COUNT", "DISPLAY_CHANNEL_*_NAME", 
			"The channels to display, in top-to-bottom order.")]
		public Vector<string> DisplayChannels { get; set; } = new();
	}
}
