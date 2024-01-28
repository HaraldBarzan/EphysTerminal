using TINS.IO;
using TINS.Terminal.Stimulation;

namespace TINS.Terminal.Protocols.Genus.CL2
{
	/// <summary>
	/// 
	/// </summary>
	public class GenusCL2Config
		: ProtocolConfig
	{
		/// <summary>
		/// The duration (sec) of a block. Must match the stream config file.
		/// </summary>
		[INILine("BLOCK_PERIOD",
			Comment = "The duration (sec) of a block. Must match the stream config file.")]
		public float BlockPeriod { get; set; }

		/// <summary>
		/// Debug option for running without controller.
		/// </summary>
		[INILine("USE_GENUS_CONTROLLER", true,
			"Debug option for running without controller.")]
		public bool UseGenusController { get; set; }

		/// <summary>
		/// True if user input is required to start a trial.
		/// </summary>
		[INILine("TRIAL_SELF_INITIATE", false,
			"True if user input is required to start a trial (e.g. when using Biosemi EEG).")]
		public bool TrialSelfInitiate { get; set; }

		/// <summary>
		/// If true, a presenter will appear during protocol runtime.
		/// </summary>
		[INILine("USE_PROTOCOL_SCREEN", false,
			"If true, a presenter will appear during protocol runtime.")]
		public bool UseProtocolScreen { get; set; }

		/// <summary>
		/// Number of trials in the protocol.
		/// </summary>
		[INILine("TRIAL_COUNT",
			Comment = "Number of trials in the protocol.")]
		public int TrialCount { get; set; }

		/// <summary>
		/// The frequency analyzer used to determine stimulation frequency.
		/// </summary>
		[INILine("FREQUENCY_ANALYZER", "SPECTRUM",
			"The frequency analyzer used to determine stimulation frequency. Must match the name in the stream configuration file.")]
		public string FrequencyAnalyzer { get; set; }

		/// <summary>
		/// The name of the artifact detector.
		/// </summary>
		[INILine("ARTIFACT_DETECTOR", "ARTIFACT_DETECTOR",
			"The name of the artifact detector. Must match the name in the stream configuration file.")]
		public string ArtifactDetector { get; set; }

		/// <summary>
		/// The initial stimulation frequency for each trial.
		/// </summary>
		[INILine("STARTING_FREQUENCY", 40f,
			"The initial stimulation frequency for each trial.")]
		public float StartingFlickerFrequency { get; set; }

		/// <summary>
		/// If set, the starting frequency will ramp linearly inside the frequency range with each trial.
		/// </summary>
		[INILine("RAMP_STARTING_FREQUENCY", false,
			"If set, the starting frequency will ramp linearly inside the frequency range with each trial (STARTING_FREQUENCY is ignored).")]
		public bool RampStartingFrequency { get; set; }

		/// <summary>
		/// The limits of the stimulation frequency.
		/// </summary>
		public (float Lower, float Upper) StimulationFrequencyRange
		{
			get => (StimulationFrequencyLower, StimulationFrequencyUpper);
			set 
			{
				StimulationFrequencyLower = value.Lower;
				StimulationFrequencyUpper = value.Upper;
			}
		}

		/// <summary>
		/// The lower range of the stimulation frequency.
		/// </summary>
		[INILine("FREQUENCY_RANGE_LOWER", 20f,
			"The lower range of the stimulation frequency.")]
		public float StimulationFrequencyLower { get; set; }

		/// <summary>
		/// The upper limit of the stimulation frequency.
		/// </summary>
		[INILine("FREQUENCY_RANGE_UPPER", 60f, 
			"The upper limit of the stimulation frequency.")]
		public float StimulationFrequencyUpper { get; set; }

		/// <summary>
		/// True if audio stimulation should be used.
		/// </summary>
		[INILine("USE_AUDIO_STIMULATION", true, "True if audio stimulation should be used.")]
		public bool UseAudioStimulation { get; set; }

		/// <summary>
		/// The frequency of the audio tone.
		/// </summary>
		[INILine("AUDIO_TONE_FREQUENCY", 10000f,
			"The frequency of the audio tone.")]
		public float AudioToneFrequency { get; set; }

		/// <summary>
		/// True if visual stimulation should be used.
		/// </summary>
		[INILine("USE_VISUAL_STIMULATION", true, 
			"True if visual stimulation should be used.")]
		public bool UseVisualStimulation { get; set; }

		/// <summary>
		/// If set, a prestimulus baseline will be computed for each trial.
		/// </summary>
		[INILine("USE_PRESTIMULUS_BASELINE", false, 
			"If set, a prestimulus baseline will be computed for each trial.")]
		public bool UsePrestimulusBaseline { get; set; }

		/// <summary>
		/// If true, Log10 will be applied to the spectrum to determine result.
		/// </summary>
		[INILine("USE_LOG10", false, 
			"If true, Log10 will be applied to the spectrum to determine result.")]
		public bool UseLog10 { get; set; }

		/// <summary>
		/// Minimum prominence to basis ratio for a peak to be considered.
		/// </summary>
		[INILine("PEAK_MIN_PROM_TO_BASIS_RATIO", 1f,
			"Minimum prominence to basis ratio for a peak to be considered.")]
		public float PeakMinPromToBasisRatio { get; set; }

		/// <summary>
		/// Maximum width of the peak in Hz.
		/// </summary>
		[INILine("PEAK_MAX_WIDTH", 50f,
			"Maximum width of the peak in Hz.")]
		public float PeakMaxWidth { get; set; }

		/// <summary>
		/// Minimum aspect ratio (prominence/width).
		/// </summary>
		[INILine("PEAK_MIN_ASPECT_RATIO", 1f, 
			"Minimum aspect ratio (prominence/width).")]
		public float PeakMinAspectRatio { get; set; }

		/// <summary>
		/// Get the version of the algorithm to use.
		/// </summary>
		[INILine("CLOSED_LOOP_ALGORITHM", CL2AlgorithmVersion.ArgMaxFollower, 
			"Get the version of the algorithm to use.")]
		public CL2AlgorithmVersion CLAlgVersion { get; set; }

		/// <summary>
		/// Delta parameter for closed loop algorithm variant 1.
		/// </summary>
		[INILine("PEAK_FOLLOWER_DELTA", 5f, 
			"Delta parameter for the peak follower algorithm.")]
		public float CL2Delta { get; set; }

		/// <summary>
		/// Delta parameter for closed loop algorithm variant 2.
		/// </summary>
		[INILine("EXPLORATION_DELTA", 5f,
			"Delta parameter for the exporation algorithm.")]
		public float ExplorationDelta { get; set; }

		/// <summary>
		/// Range around the exploration delta center frequencies in which peaks are considered.
		/// </summary>
		[INILine("EXPLORATION_SIGMA", 5f,
			"Range around the exploration delta center frequencies in which peaks are considered.")]
		public float ExplorationSigma { get; protected set; }

		/// <summary>
		/// Frequency for washout. 0 = shut down the panel completely.
		/// </summary>
		[INILine("WASHOUT_FREQUENCY", 7f, 
			"Frequency for washout. 0 = shut down the panel completely.")]
		public float WashoutFrequency { get; set; }

		/// <summary>
		/// Number of consecutive nopeak update blocks to initiate washout.
		/// </summary>
		[INILine("WASHOUT_PEAKLESS_UPDATES_TRIGGER", 5,
			"Number of consecutive nopeak update blocks to initiate washout.")]
		public int WashoutTriggerBlocks { get; set; }

		/// <summary>
		/// The duration of the washout in terms of update blocks.
		/// </summary>
		[INILine("WASHOUT_TIMEOUT_UPDATES", 3,
			"The duration of the washout in terms of update blocks.")]
		public int WashoutTimeoutBlocks { get; set; }

		/// <summary>
		/// The intertrial timeout in blocks.
		/// </summary>
		[INILine("INTERTRIAL_TIMEOUT", Comment = "The intertrial timeout in blocks.")]
		public int IntertrialTimeout { get; set; }

		/// <summary>
		/// The stimulation timeout in blocks.
		/// </summary>
		[INILine("STIMULATION_TIMEOUT", Comment = "The stimulation timeout in blocks.")]
		public int StimulationTimeout { get; set; }

		/// <summary>
		/// Time (in blocks) until the next update.
		/// </summary>
		[INILine("UPDATE_TIMEOUT", Comment = "Time (in blocks) until the next update.")]
		public int UpdateTimeout { get; set; }

		/// <summary>
		/// Get the duration of a block in seconds.
		/// </summary>
		public float UpdateBlockDuration => UpdateTimeout * BlockPeriod;

		/// <summary>
		/// Number of blocks per trial.
		/// </summary>
		public int BlocksPerTrial => StimulationTimeout / UpdateTimeout;

		/// <summary>
		/// The post-mask timeout in blocks.
		/// </summary>
		[INILine("MASK_TIMEOUT", Comment = "The post-mask timeout in blocks.")]
		public int MaskTimeout { get; set; }

		/// <summary>
		/// The prestimulus timeout in blocks.
		/// </summary>
		[INILine("PRESTIMULUS_TIMEOUT")]
		public byte PrestimulusTimeout { get; set; }

		/// <summary>
		/// The poststimulus timeout in blocks.
		/// </summary>
		[INILine("POSTSTIMULUS_TIMEOUT", Comment = "The prestimulus timeout in blocks.")]
		public byte PoststimulusTimeout { get; set; }

		/// <summary>
		/// The mask timeout in blocks.
		/// </summary>
		[INILine("POSTMASK_TIMEOUT", Comment = "The mask timeout in blocks.")]
		public int PostMaskTimeout { get; set; }

		/// <summary>
		/// The trial start trigger.
		/// </summary>
		[INILine("TRIAL_START_TRIGGER", Comment = "The trial start trigger.")]
		public byte TrialStartTrigger { get; set; }

		/// <summary>
		/// Mask start trigger.
		/// </summary>
		[INILine("MASK_START_TRIGGER", Comment = "Mask start trigger.")]
		public byte PrestimulusStartTrigger { get; set; }

		/// <summary>
		/// Stimulation start trigger.
		/// </summary>
		[INILine("STIMULUS_START_TRIGGER", Comment = "Stimulation start trigger.")]
		public byte StimulationStartTrigger { get; set; }

		/// <summary>
		/// Stimulation end trigger.
		/// </summary>
		[INILine("STIMULUS_END_TRIGGER", Comment = "Stimulation end trigger.")]
		public byte StimulationEndTrigger { get; set; }

		/// <summary>
		/// Poststimulus end trigger.
		/// </summary>
		[INILine("POSTSTIMULUS_END_TRIGGER", Comment = "Poststimulus end trigger.")]
		public byte PoststimulusEndTrigger { get; set; }

		/// <summary>
		/// The trial end trigger.
		/// </summary>
		[INILine("TRIAL_END_TRIGGER", Comment = "The trial end trigger.")]
		public byte TrialEndTrigger { get; set; }

		/// <summary>
		/// Stimulation parameter update trigger. Raised once each closed-loop block irrespective of the decision.
		/// </summary>
		[INILine("FREQUENCY_UPDATE_TRIGGER", Comment = "Stimulation parameter update trigger. Raised once each closed-loop block irrespective of the decision.")]
		public byte StimUpdateTrigger { get; set; }
	}
}
