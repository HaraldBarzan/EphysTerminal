using TINS.IO;

namespace TINS.Ephys.Settings
{
	/// <summary>
	/// 
	/// </summary>
	public enum DataInputDevice
	{
		Dummy,
		Local,
		MEA64USB
	}

	/// <summary>
	/// The settings for the input.
	/// </summary>
	public class InputSettings 
		: SerializableSettingsItem
	{
		/// <summary>
		/// The input device.
		/// </summary>
		public DataInputDevice InputDevice 
		{
			get => (DataInputDevice)InputDeviceInt;
			set => InputDeviceInt = (int)value;
		}

		/// <summary>
		/// The ID of the input device.
		/// </summary>
		[INILine(Key = "INPUT_DEVICE", Default = 2)]
		protected int InputDeviceInt { get; set; }

		/// <summary>
		/// The desired sampling rate in Hz.
		/// </summary>
		[INILine(Key = "SAMPLING_RATE")]
		public float SamplingRate { get; set; }

		/// <summary>
		/// The time interval, in seconds, between two consecutive polls.
		/// </summary>
		[INILine(Key = "POLLING_PERIOD")]
		public float PollingPeriod { get; set; }

		/// <summary>
		/// The buffer where raw input should be stored.
		/// </summary>
		[INILine(Key = "TARGET_BUFFER", Default = "RAW")]
		public string TargetBuffer { get; set; }

		/// <summary>
		/// The channel labels (each label's position is the position of the channel in the recording stream).
		/// </summary>
		[INIVector(Key = "CHANNEL_COUNT", ValueMask = "CHANNEL_*_NAME")]
		public Vector<string> ChannelLabels { get; set; } = new();

		/// <summary>
		/// Artefact triggers will be omitted.
		/// </summary>
		[INILine(Key = "CORRECT_ARTEFACT_EVENTS", Default = true)]
		public bool CorrectArtefactEvents { get; set; }
	}
}
