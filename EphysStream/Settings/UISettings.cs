using TINS.IO;

namespace TINS.Ephys.Settings
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
		[INILine(Key = "MUA_YRANGE_MIN", Default = -50f)]
		public float MUAYRangeMin { get; set; }

		/// <summary>
		/// The maximum y-range value for the MUA display.
		/// </summary>
		[INILine(Key = "MUA_YRANGE_MAX", Default = 50f)]
		public float MUAYRangeMax { get; set; }

		/// <summary>
		/// The minimum y-range value for the LFP display.
		/// </summary>
		[INILine(Key = "LFP_YRANGE_MIN", Default = -50f)]
		public float LFPYRangeMin { get; set; }

		/// <summary>
		/// The maximum y-range value for the LFP display.
		/// </summary>
		[INILine(Key = "LFP_YRANGE_MAX", Default = 50f)]
		public float LFPYRangeMax { get; set; }

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
