using System;
using System.Text.Json.Serialization;

namespace TINS.Ephys.Stimulation
{
	/// <summary>
	/// Interface for stimulation protocols.
	/// </summary>
	public interface IStimulationProtocol
		: IDisposable
	{
		/// <summary>
		/// Raised when the protocol is started.
		/// </summary>
		public event Action ProtocolStarted;

		/// <summary>
		/// Raised when the protocol has ended.
		/// </summary>
		public event Action ProtocolEnded;

		/// <summary>
		/// Update the progress of the protocol (current index, max).
		/// </summary>
		public event Action<int, int> UpdateProgress;

		/// <summary>
		/// Run the protocol.
		/// </summary>
		public void Start();

		/// <summary>
		/// New block event.
		/// </summary>
		public void ProcessBlock();

		/// <summary>
		/// Stop the protocol if running.
		/// </summary>
		public void Stop();

		/// <summary>
		/// Set the stimulus controller.
		/// </summary>
		/// <param name="stimulusController">The stimulus controller.</param>
		public void SetStimulusController(StimulusController stimulusController);

		/// <summary>
		/// Assert whether the protocol is running.
		/// </summary>
		public bool IsRunning { get; }
	}



	/// <summary>
	/// Base class for stimulation configurations.
	/// </summary>
	public class ProtocolConfig
	{
		/// <summary>
		/// The registered type of the protocol.
		/// </summary>
		[JsonPropertyName("protocolType")]
		public string ProtocolType { get; set; }

		/// <summary>
		/// Output EPD file will only contain the listed triggers.
		/// </summary>
		[JsonPropertyName("supportedTriggers")]
		public Vector<int> SupportedTriggers { get; set; } = new();
	}



	/// <summary>
	/// Base class for stimulation protocols.
	/// </summary>
	public abstract class StimulationProtocol<TConfig, TStim>
		: IStimulationProtocol
		where TConfig	: ProtocolConfig
		where TStim		: StimulusController
	{
		/// <summary>
		/// Raised when the protocol is started.
		/// </summary>
		public event Action ProtocolStarted;

		/// <summary>
		/// Raised when the protocol has ended.
		/// </summary>
		public event Action ProtocolEnded;

		/// <summary>
		/// Update the progress of the protocol (current index, max).
		/// </summary>
		public event Action<int, int> UpdateProgress;

		/// <summary>
		/// Create a new stimulation protocol.
		/// </summary>
		/// <param name="parent">The parent stream on which the protocol is ran.</param>
		/// <param name="config">The configuration of the protocol.</param>
		/// <param name="stimulusController">The stimulus controller used by the protocol.</param>
		public StimulationProtocol(EphysStream parent, TConfig config, TStim stimulusController = null)
		{
			ParentStream		= parent;
			Config				= config;
			StimulusController	= stimulusController;
		}

		/// <summary>
		/// Dispose method.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			ProtocolStarted = null;
			ProtocolEnded	= null;
			UpdateProgress	= null;
		}

		/// <summary>
		/// Run the protocol.
		/// </summary>
		public abstract void Start();

		/// <summary>
		/// New block event.
		/// </summary>
		public abstract void ProcessBlock();

		/// <summary>
		/// Stop the protocol if running.
		/// </summary>
		public abstract void Stop();

		/// <summary>
		/// Set the stimulus controller.
		/// </summary>
		/// <param name="controller">The stimulus controller.</param>
		public virtual void SetStimulusController(StimulusController controller)
		{
			if (controller is null)
				throw new NullReferenceException("Controller is null.");

			if (controller is not TStim c)
				throw new Exception($"Invalid controller type ({typeof(TStim).Name} expected).");

			SetStimulusController(c);
		}

		/// <summary>
		/// Set the stimulus controller.
		/// </summary>
		/// <param name="controller">The stimulus controller.</param>
		public virtual void SetStimulusController(TStim controller)
		{
			StimulusController = controller;
		}

		/// <summary>
		/// Assert whether the protocol is running.
		/// </summary>
		public abstract bool IsRunning { get; }

		/// <summary>
		/// Stimulus controller.
		/// </summary>
		public TStim StimulusController { get; protected set; }

		/// <summary>
		/// Configuration.
		/// </summary>
		public TConfig Config { get; init; }

		/// <summary>
		/// The parent stream.
		/// </summary>
		public EphysStream ParentStream { get; init; }

		/// <summary>
		/// Raise protocol started.
		/// </summary>
		protected void RaiseProtocolStarted() => ProtocolStarted?.Invoke();

		/// <summary>
		/// Raise protocol ended.
		/// </summary>
		protected void RaiseProtocolEnded() => ProtocolEnded?.Invoke();

		/// <summary>
		/// Raise update progress.
		/// </summary>
		protected void RaiseUpdateProgress(int progress, int total) => UpdateProgress?.Invoke(progress, total);
	}
}
