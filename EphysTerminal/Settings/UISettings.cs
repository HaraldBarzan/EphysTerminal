using TINS.IO;
using TINS.Ephys.Settings;

namespace TINS.Terminal.Settings
{
	/// <summary>
	/// Settings to display a graphical user interface (GUI).
	/// </summary>
	public class UISettings 
		: SerializableSettingsItem
	{
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
		/// The refresh rate of MUA.
		/// </summary>
		[INILine(Key = "MUA_REFRESH_RATE", Default = 4)]
		public int MUARefreshRate { get; set; }

		/// <summary>
		/// The refresh rate of LFP.
		/// </summary>
		[INILine(Key = "LFP_REFRESH_RATE", Default = 8)]
		public int LFPRefreshRate { get; set; }

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
