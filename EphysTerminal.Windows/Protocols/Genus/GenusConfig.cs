using System.Text.Json.Serialization;
using TINS.Terminal.Stimulation;

namespace TINS.Terminal.Protocols.Genus
{
	/// <summary>
	/// Genus configuration.
	/// </summary>
	public class GenusConfig
		: ProtocolConfig
	{
		[JsonPropertyName("supportedSamplingPeriod")]
		public float SupportedSamplingPeriod { get; set; }

		[JsonPropertyName("trialStartTrigger")]
		public byte TrialStartTrigger { get; set; }

		[JsonPropertyName("trialEndTrigger")]
		public byte TrialEndTrigger { get; set; }

		[JsonPropertyName("intertrialTimeout")]
		public int IntertrialTimeout { get; set; }

		[JsonPropertyName("randomize")]
		public bool Randomize { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("trials")]
		public Vector<GenusProtocol.Trial> Trials { get; set; } = new();
	}
}
