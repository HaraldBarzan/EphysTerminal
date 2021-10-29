using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TINS.Ephys.Stimulation.Genus
{
	/// <summary>
	/// Encapsulates a genus trial type.
	/// </summary>
	public class GenusTrial
	{
		[JsonPropertyName("count")]
		public int Count { get; set; }

		[JsonPropertyName("preTimeout")]
		public int PrestimulusTimeout { get; set; }

		[JsonPropertyName("stimTimeout")]
		public int StimulusTimeout { get; set; }

		[JsonPropertyName("postTimeout")]
		public int PoststimulusTimeout { get; set; }

		[JsonPropertyName("stimFrequency")]
		public float StimulationFrequency { get; set; }
	}

	/// <summary>
	/// Genus configuration.
	/// </summary>
	public class GenusConfig
		: ProtocolConfig
	{
		[JsonPropertyName("supportedSamplingPeriod")]
		public float SupportedSamplingPeriod { get; set; }

		[JsonPropertyName("prestimTrigger")]
		public byte PrestimulusTrigger { get; set; }

		[JsonPropertyName("stimTrigger")]
		public byte StimulusTrigger { get; set; }

		[JsonPropertyName("poststimTrigger")]
		public byte PoststimulusTrigger { get; set; }

		[JsonPropertyName("trialEndTrigger")]
		public byte TrialEndTrigger { get; set; }

		[JsonPropertyName("intertrialTimeout")]
		public int IntertrialTimeout { get; set; }

		[JsonPropertyName("randomize")]
		public bool Randomize { get; set; }

		[JsonPropertyName("trials")]
		public Vector<GenusTrial> Trials { get; set; } = new();
	}
}
