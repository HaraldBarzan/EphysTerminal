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
	/// A dummy stimulus controller that does absolutely nothing.
	/// </summary>
	public class DummyStimulusController : StimulusController
	{
		public override void ChangeParameters(float? brightness, float? frequency, byte? trigger) { }
		public override void ConnectToDevice(string port = null) { }
		public override void Disconnect() { }
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
			_isRunning = true;
			RaiseProtocolStarted();
			RaiseUpdateProgress(0, 1);
		}

		/// <summary>
		/// New block event.
		/// </summary>
		public override void ProcessBlock()
		{
			if (_isRunning && _timeout == 0)
				Stop();
			else
				--_timeout;
		}

		/// <summary>
		/// Stop the protocol if running.
		/// </summary>
		public override void Stop()
		{
			if (_isRunning)
			{
				_isRunning	= false;
				_timeout	= 0;
				RaiseProtocolEnded();
			}
		}

		/// <summary>
		/// Assert whether the protocol is running.
		/// </summary>
		public override bool IsRunning => _isRunning;

		protected int	_timeout;
		protected bool	_isRunning;
	}
}
