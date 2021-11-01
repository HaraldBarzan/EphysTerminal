using TINS.IO;

namespace TINS.Ephys.Settings
{
	/// <summary>
	/// The configuration for the recorder.
	/// </summary>
	public class EphysSettings
		: SerializableSettingsItem
	{
		/// <summary>
		/// The root section of the ini file.
		/// </summary>
		public const string HeaderSection = "ELECTROPHYSIOLOGY_CONFIGURATION";

		/// <summary>
		/// The section name for the input settings.
		/// </summary>
		[INILine(Key = "INPUT_SECTION", Default = "INPUT")]
		public string InputSection { get; set; }

		/// <summary>
		/// The section name for the stimulation section.
		/// </summary>
		[INILine(Key = "PROCESSING_SECTION", Default = "PROCESSING")]
		public string ProcessingSection { get; set; }

		/// <summary>
		/// The section name for analysis section.
		/// </summary>
		[INILine(Key = "ANALYSIS_SECTION", Default = "ANALYSIS")]
		public string AnalysisSection { get; set; }

		/// <summary>
		/// The section name for the stimulation section.
		/// </summary>
		[INILine(Key = "STIMULATION_SECTION", Default = "STIMULATION")]
		public string StimulationSection { get; set; }

		/// <summary>
		/// The section name for the graphical user interface (GUI) section.
		/// </summary>
		[INILine(Key = "UI_SECTION", Default = "UI")]
		public string UISection { get; set; }

		/// <summary>
		/// The number of channels supported by the recorder.
		/// </summary>
		public int ChannelCount => Input.ChannelLabels.Size;
		
		/// <summary>
		/// The number of samples per block supported by the recorder.
		/// </summary>
		public int SamplesPerBlock => Numerics.Round(Input.PollingPeriod * Input.SamplingRate);

		/// <summary>
		/// The sampling rate used by the device.
		/// </summary>
		public float SamplingRate => Input.SamplingRate;

		/// <summary>
		/// The input settings.
		/// </summary>
		public InputSettings Input { get; } = new();

		/// <summary>
		/// The processing pipeline settings.
		/// </summary>
		public ProcessingPipelineSettings Processing { get; } = new();

		/// <summary>
		/// The analysis pipeline settings.
		/// </summary>
		public AnalysisPipelineSettings Analysis { get; } = new();

		/// <summary>
		/// The analysis pipeline settings.
		/// </summary>
		public UISettings UI { get; } = new();

		/// <summary>
		/// Serialize a <c>EphysSettings</c> item from an INI file.
		/// </summary>
		/// <param name="ini">The INI file.</param>
		/// <param name="sectionName">The section to serialize from.</param>
		/// <param name="direction">The direction of serialization.</param>
		public override void Serialize(INI ini, string sectionName, SerializationDirection direction)
		{
			base.Serialize(ini, sectionName, direction);

			Input		.Serialize(ini, InputSection, direction);
			Processing	.Serialize(ini, ProcessingSection, direction);
			Analysis	.Serialize(ini, AnalysisSection, direction);
			UI			.Serialize(ini, UISection, direction);
		}
	}
}
