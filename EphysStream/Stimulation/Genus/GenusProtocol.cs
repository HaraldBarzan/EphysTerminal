using System;
using System.IO;

namespace TINS.Ephys.Stimulation.Genus
{
	/// <summary>
	/// The possible states of the Genus protocol.
	/// </summary>
	public enum GenusState
	{
		Idle,
		Prestimulus,
		Stimulus,
		Poststimulus,
		Intertrial
	}

	/// <summary>
	/// The possible events registered by the Genus protocol.
	/// </summary>
	public enum GenusEvent
	{
		Start,
		NewBlock,
		Stop
	}

	/// <summary>
	/// Genus protocol.
	/// </summary>
	public class GenusProtocol
		: StimulationProtocol<GenusConfig, StimulusController>
	{
		/// <summary>
		/// Create a new Genus protocol.
		/// </summary>
		/// <param name="parent">The parent ephys stream.</param>
		/// <param name="config">The configuration for this protocol.</param>
		/// <param name="stimulusController">The stimulus controller for the protocol.</param>
		public GenusProtocol(EphysStream parent, GenusConfig config, StimulusController stimulusController)
			: base(parent, config, stimulusController)
		{
			// load the configuration
			Config		= config;
			TextOutput	= null;

			// check compatibility
			if (Config.SupportedSamplingPeriod != ParentStream.Settings.Input.PollingPeriod)
				throw new Exception("The protocol's polling period does not match the current settings.");

			stimulusController.ConnectToDevice();
			stimulusController.ResetParameters();

			// initialize the state machine
			_stateMachine = new(this);
			_stateMachine.TrialBegin	+= (iTrial) => RaiseUpdateProgress(iTrial, TrialCount);
			_stateMachine.RunCompleted	+= Stop;
		}

		/// <summary>
		/// Start the protocol.
		/// </summary>
		public override void Start()
		{
			_stateMachine.ProcessEvent(GenusEvent.Start);

			// create a text writer 
			if (ParentStream.OutputStream is object)
			{
				// obtain path from output 
				ParentStream.OutputStream.GetPath(out var dir, out var dsName);
				var outputPath = Path.Combine(dir, dsName + ".eti");
				
				// create the text writer
				TextOutput = new StreamWriter(outputPath);
				TextOutput.WriteLine($"Trials,{_stateMachine.TrialCount}\nFields,2\n\nTrial,StimFreq");
			}

			RaiseProtocolStarted();
		}

		/// <summary>
		/// Stop running the protocol.
		/// </summary>
		public override void Stop()
		{
			if (IsRunning)
			{
				_stateMachine.ProcessEvent(GenusEvent.Stop);

				TextOutput?.Dispose();
				TextOutput = null;

				RaiseProtocolEnded();
			}
		}

		/// <summary>
		/// Signal new input data to the protocol.
		/// </summary>
		public override void ProcessBlock()
		{
			_stateMachine.ProcessEvent(GenusEvent.NewBlock);
		}

		/// <summary>
		/// Assert whether the protocol is running.
		/// </summary>
		public override bool IsRunning => _stateMachine.CurrentState != GenusState.Idle;

		/// <summary>
		/// Text output for this protocol. Is active only when recording.
		/// </summary>
		public StreamWriter TextOutput { get; protected set; }

		/// <summary>
		/// Number of trials.
		/// </summary>
		public int TrialCount => _stateMachine.TrialCount;

		protected GenusStateMachine	_stateMachine;
	}
	
}
