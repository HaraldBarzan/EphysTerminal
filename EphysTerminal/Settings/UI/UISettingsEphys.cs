using TINS.IO;

namespace TINS.Terminal.Settings.UI
{
    /// <summary>
    /// Settings for the electrophysiology UI display.
    /// </summary>
	public class UISettingsEphys
		: UISettings
	{
        /// <summary>
        /// Type name for ephys UI settings.
        /// </summary>
        public override string TypeName => "EPHYS";

        /// <summary>
        /// Display multiunit activity.
        /// </summary>
        [INILine("SHOW_MUA", true, 
            "Display multiunit activity.")]
        public bool ShowMUA { get; set; }

        /// <summary>
        /// Display local field potentials.
        /// </summary>
        [INILine("SHOW_LFP", true,
			"Display local field potentials.")]
        public bool ShowLFP { get; set; }

        /// <summary>
        /// The period of the MUA update, in seconds.
        /// </summary>
        [INILine("MUA_UPDATE_PERIOD", 1,
			" The period of the MUA update, in seconds.")]
        public float MUAUpdatePeriod { get; set; }

        /// <summary>
        /// The period of the LFP update, in seconds.
        /// </summary>
        [INILine("LFP_UPDATE_PERIOD", 2,
			"The period of the LFP update, in seconds.")]
        public int LFPUpdatePeriod { get; set; }

        /// <summary>
        /// The minimum y-range value for the MUA display.
        /// </summary>
        [INILine("MUA_YRANGE", 50f,
			"The minimum y-range value for the MUA display.")]
        public float MUAYRange { get; set; }

        /// <summary>
        /// The minimum y-range value for the LFP display.
        /// </summary>
        [INILine("LFP_YRANGE", 50f,
			"The minimum y-range value for the LFP display.")]
        public float LFPYRange { get; set; }

        /// <summary>
        /// The refresh rate of MUA.
        /// </summary>
        [INILine("MUA_DISPLAY_BUFFER",
            Comment = "Name of the source buffer (as defined in the PROCESSING settings) for the MUA display.")]
        public string MUAInputBuffer { get; set; }

        /// <summary>
        /// The refresh rate of LFP.
        /// </summary>
        [INILine("LFP_DISPLAY_BUFFER",
			Comment = "Name of the source buffer (as defined in the PROCESSING settings) for the MUA display.")]
        public string LFPInputBuffer { get; set; }

        /// <summary>
        /// The name of the input spike detector.
        /// </summary>
        [INILine("MUA_SPIKE_DETECTOR",
            Comment = "Name of the source spike detector (as defined in the ANALYSIS settings).")]
        public string MUASpikeDetector { get; set; }

        /// <summary>
        /// The name of the source buffer.
        /// </summary>
        [INILine("AUDIO_SOURCE_BUFFER", "MUA",
			"Name of the source buffer (as defined in the PROCESSING settings) for the MUA audio stream.")]
        public string AudioSourceBuffer { get; set; }

        /// <summary>
        /// The default audio channel to stream.
        /// </summary>
        [INILine("AUDIO_DEFAULT_CHANNEL", "El_01",
            "Name of the default channel to be used as the audio source.")]
        public string DefaultAudioChannel { get; set; }

        /// <summary>
        /// The number of cell rows for MUA and LFP displays.
        /// </summary>
        [INILine("DISPLAY_GRID_ROWS", 8,
			"The number of cell rows for MUA and LFP displays.")]
        public int DisplayGridRows { get; set; }

        /// <summary>
        /// The number of cell columns for MUA and LFP displays.
        /// </summary>
        [INILine("DISPLAY_GRID_COLUMNS", 4,
			"The number of cell columns for MUA and LFP displays.")]
        public int DisplayGridColumns { get; set; }

        /// <summary>
        /// The channels to display on each cell, in row-major order.
        /// </summary>
        [INIVector("DISPLAY_CHANNELS_COUNT", "DISPLAY_CHANNEL_*_NAME",
			"The channels to display on each cell, in row-major order.")]
        public Vector<string> DisplayChannels { get; set; } = new();
	}
}
