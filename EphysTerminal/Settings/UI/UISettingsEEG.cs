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
		[INILine(Key = "SHOW_EEG", Default = true)]
		public bool ShowEEG { get; set; }

		/// <summary>
		/// The period of the EEG update, in seconds.
		/// </summary>
		[INILine(Key = "EEG_UPDATE_PERIOD", Default = 1)]
		public int EEGUpdatePeriod { get; set; }

		/// <summary>
		/// EEG display buffer name.
		/// </summary>
		[INILine(Key = "EEG_DISPLAY_BUFFER")]
		public string EEGInputBuffer { get; set; }

		/// <summary>
		/// The channels to display, in top-to-bottom order.
		/// </summary>
		[INIVector(Key = "DISPLAY_CHANNELS_COUNT", ValueMask = "DISPLAY_CHANNEL_*_NAME")]
		public Vector<string> DisplayChannels { get; set; } = new();
	}
}
