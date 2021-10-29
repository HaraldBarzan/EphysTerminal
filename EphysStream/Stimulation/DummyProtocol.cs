using System.Text.Json.Serialization;

namespace TINS.Ephys.Stimulation
{
	/// <summary>
	/// Configuration for dummy protocol.
	/// </summary>
	public class DummyProtocolConfig : ProtocolConfig
	{
		[JsonPropertyName("experimentTimeout")]
		public int ExperimentTimeout { get; set; }
	}

	/// <summary>
	/// Dummy protocol.
	/// </summary>
	public class DummyProtocol 
		: StimulationProtocol<DummyProtocolConfig, StimulusController>
	{
		/// <summary>
		/// Create a dummy protocol.
		/// </summary>
		/// <param name="parent">Parent ephys stream.</param>
		/// <param name="config">Configuration.</param>
		public DummyProtocol(EphysStream parent, DummyProtocolConfig config)
			: base(parent, config)
		{
		}

		/// <summary>
		/// Run the protocol.
		/// </summary>
		public override void Start()
		{
			_timeout = Config.ExperimentTimeout;
			RaiseProtocolStarted();
			RaiseUpdateProgress(0, 1);
		}

		/// <summary>
		/// New block event.
		/// </summary>
		public override void ProcessBlock()
		{
			if (_timeout == 0)
				Stop();
			--_timeout;
		}

		/// <summary>
		/// Stop the protocol if running.
		/// </summary>
		public override void Stop()
		{
			RaiseProtocolEnded();
		}

		/// <summary>
		/// Assert whether the protocol is running.
		/// </summary>
		public override bool IsRunning => _timeout > 0;

		protected int _timeout;
	}
}
