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
        [INILine(Key = "SHOW_MUA", Default = true)]
        public bool ShowMUA { get; set; }

        /// <summary>
        /// Display local field potentials.
        /// </summary>
        [INILine(Key = "SHOW_LFP", Default = true)]
        public bool ShowLFP { get; set; }

        /// <summary>
        /// The period of the MUA update, in seconds.
        /// </summary>
        [INILine(Key = "MUA_UPDATE_PERIOD", Default = 1)]
        public float MUAUpdatePeriod { get; set; }

        /// <summary>
        /// The period of the LFP update, in seconds.
        /// </summary>
        [INILine(Key = "LFP_UPDATE_PERIOD", Default = 2)]
        public int LFPUpdatePeriod { get; set; }

        /// <summary>
        /// The minimum y-range value for the MUA display.
        /// </summary>
        [INILine(Key = "MUA_YRANGE", Default = 50f)]
        public float MUAYRange { get; set; }

        /// <summary>
        /// The minimum y-range value for the LFP display.
        /// </summary>
        [INILine(Key = "LFP_YRANGE", Default = 50f)]
        public float LFPYRange { get; set; }

        /// <summary>
        /// The refresh rate of MUA.
        /// </summary>
        [INILine(Key = "MUA_DISPLAY_BUFFER")]
        public string MUAInputBuffer { get; set; }

        /// <summary>
        /// The refresh rate of LFP.
        /// </summary>
        [INILine(Key = "LFP_DISPLAY_BUFFER")]
        public string LFPInputBuffer { get; set; }

        /// <summary>
        /// The name of the input spike detector.
        /// </summary>
        [INILine(Key = "MUA_SPIKE_DETECTOR")]
        public string MUASpikeDetector { get; set; }

        /// <summary>
        /// The name of the source buffer.
        /// </summary>
        [INILine(Key = "AUDIO_SOURCE_BUFFER", Default = "MUA")]
        public string AudioSourceBuffer { get; set; }

        /// <summary>
        /// The default audio channel to stream.
        /// </summary>
        [INILine(Key = "AUDIO_DEFAULT_CHANNEL", Default = "El_01")]
        public string DefaultAudioChannel { get; set; }

        /// <summary>
        /// The number of cell rows for MUA and LFP displays.
        /// </summary>
        [INILine(Key = "DISPLAY_GRID_ROWS", Default = 8)]
        public int DisplayGridRows { get; set; }

        /// <summary>
        /// The number of cell columns for MUA and LFP displays.
        /// </summary>
        [INILine(Key = "DISPLAY_GRID_COLUMNS", Default = 4)]
        public int DisplayGridColumns { get; set; }

        /// <summary>
        /// The channels to display on each cell, in row-major order.
        /// </summary>
        [INIVector(Key = "DISPLAY_CHANNELS_COUNT", ValueMask = "DISPLAY_CHANNEL_*_NAME")]
        public Vector<string> DisplayChannels { get; set; } = new();
	}
}
