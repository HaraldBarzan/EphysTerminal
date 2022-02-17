using System;
using System.IO;
using System.Text.Json.Serialization;
using TINS.Ephys.ModuleImplementations;
using TINS.Ephys.UI;
using TINS.Utilities;

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
		StimulationComplete,
		Stop
	}

	/// <summary>
	/// Genus protocol.
	/// </summary>
	public class GenusMk2Cached
		: StimulationProtocol<GenusMk2CachedConfig, GenusMk2Controller>
	{
		/// <summary>
		/// Create a new Genus protocol.
		/// </summary>
		/// <param name="parent">The parent ephys stream.</param>
		/// <param name="config">The configuration for this protocol.</param>
		/// <param name="stimulusController">The stimulus controller for the protocol.</param>
		public GenusMk2Cached(EphysStream parent, GenusMk2CachedConfig config, GenusMk2Controller stimulusController)
			: base(parent, config, stimulusController)
		{
			// load the configuration
			Config		= config;
			TrialLogger	= null;

			// check compatibility
			if (Config.SupportedSamplingPeriod != ParentStream.Settings.Input.PollingPeriod)
				throw new Exception("The protocol's polling period does not match the current settings.");

			// attach feedback detector
			stimulusController.FeedbackReceived += OnDeviceFeedback;

			// initialize the state machine
			_stateMachine = new(this);
			_stateMachine.TrialBegin	+= (iTrial) => RaiseUpdateProgress(iTrial, TrialCount);
			_stateMachine.RunCompleted	+= () =>
			{
				Stop();
				RaiseUpdateProgress(TrialCount, TrialCount);
			};
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (StimulusController is not null)
				StimulusController.FeedbackReceived -= OnDeviceFeedback;
			base.Dispose(disposing);
		}

		/// <summary>
		/// Start the protocol.
		/// </summary>
		public override void Start()
		{
			// connect to stimulus controller
			StimulusController.Connect();
			StimulusController.Reset();

			// start state machine
			_stateMachine.ProcessEvent(GenusEvent.Start);

			// create a text writer 
			if (ParentStream.OutputStream is object)
			{
				// obtain path from output 
				ParentStream.OutputStream.GetPath(out var dir, out var dsName);
				var outputPath = Path.Combine(dir, dsName + ".eti");
				
				// create the text writer
				TrialLogger = new TrialInfoLogger(outputPath, 
					header: new() 
					{
						"Trial",
						"TrialName",
						"TrialType",
						"Audio",
						"Visual",
						"StimulationRuntime",
						"StepCount",
						"FlickerFrequency",
						"AudioToneFrequency",
						"UseFlickerTriggers",
						"UseTransitionTriggers",
					});
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

				TrialLogger?.Dispose();
				TrialLogger = null;

				StimulusController?.Disconnect();

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
		/// Raised when the device signals 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="feedback"></param>
		protected virtual void OnDeviceFeedback(object sender, GenusMk2Controller.Feedback feedback)
		{
			if (feedback is GenusMk2Controller.Feedback.StimulationComplete)
				_stateMachine.ProcessEvent(GenusEvent.StimulationComplete);
		}

		/// <summary>
		/// Assert whether the protocol is running.
		/// </summary>
		public override bool IsRunning => _stateMachine.CurrentState != GenusState.Idle;

		/// <summary>
		/// Text output for this protocol. Is active only when recording.
		/// </summary>
		public TrialInfoLogger TrialLogger { get; protected set; }

		/// <summary>
		/// Number of trials.
		/// </summary>
		public int TrialCount => _stateMachine.TrialCount;

		protected StateMachine _stateMachine;

		/// <summary>
		/// Encapsulates a genus trial type.
		/// </summary>
		public class Trial
		{
			[JsonPropertyName("count")]
			public int Count { get; set; }

			[JsonPropertyName("instructionList")]
			public string InstructionList { get; set; }

			[JsonPropertyName("preTimeout")]
			public int PrestimulusTimeout { get; set; }

			[JsonPropertyName("postTimeout")]
			public int PoststimulusTimeout { get; set; }

			public GenusMk2CachedTrial Instructions { get; set; }
		}

		/// <summary>
		/// Genus protocol state machine.
		/// </summary>
		public class StateMachine
			: StateMachine<GenusState, GenusEvent>
		{
			/// <summary>
			/// Raised when a trial is completed.
			/// </summary>
			public event Action<int> TrialBegin;

			/// <summary>
			/// Raised when a trial run is completed.
			/// </summary>
			public event Action RunCompleted;

			/// <summary>
			/// Create a state machine for a Genus protocol.
			/// </summary>
			/// <param name="protocol">The parent Genus protocol.</param>
			public StateMachine(GenusMk2Cached protocol)
			{
				_p				= protocol;
				_stim			= protocol.StimulusController;
				_ui				= protocol.ParentStream.UI;

				// just so we have a list at any time
				ResetTrials();
			}

			/// <summary>
			/// Configure the state machine.
			/// </summary>
			protected override void ConfigureStates()
			{
				// IDLE
				AddState(GenusState.Idle,
					eventAction: (e) =>
					{
						if (e is GenusEvent.Start)
						{
							ResetTrials();
							return GenusState.Intertrial;
						}

						return CurrentState;
					},
					enterStateAction: () => _stim.Reset());


				// PRESTIMULUS
				AddState(GenusState.Prestimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock => Elapse(GenusState.Stimulus),
						GenusEvent.Stop		=> GenusState.Idle,
						_					=> CurrentState
					},
					enterStateAction: () =>
					{
						_stateTimeout = _trials[CurrentTrialIndex].PrestimulusTimeout;
						_stim.EmitTrigger(_p.Config.TrialStartTrigger);
					});


				// STIMULUS
				AddState(GenusState.Stimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.StimulationComplete	=> GenusState.Poststimulus,
						GenusEvent.Stop					=> GenusState.Idle,
						_								=> CurrentState
					},
					enterStateAction: () =>
					{
						_stim.SendInstructionList(CurrentTrial.Instructions.Instructions);
					});


				// POSTSTIMULUS
				AddState(GenusState.Poststimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock => Elapse(GenusState.Intertrial),
						GenusEvent.Stop		=> GenusState.Idle,
						_					=> CurrentState
					},
					enterStateAction:	() => _stateTimeout = CurrentTrial.PoststimulusTimeout,
					exitStateAction:	() => _stim.EmitTrigger(_p.Config.TrialEndTrigger));


				// INTERTRIAL
				AddState(GenusState.Intertrial,
					eventAction: (e) => 
					{
						if (e is GenusEvent.NewBlock)
						{
							if (_stateTimeout == 0)
							{
								// emit line
								EmitEtiLine();
								CurrentTrialIndex++;
								
								// check stopping condition
								if (CurrentTrialIndex == TrialCount)
								{
									RunCompleted?.Invoke();
									return GenusState.Idle;
								}

								// begin a new trial
								TrialBegin?.Invoke(CurrentTrialIndex);
								return GenusState.Prestimulus;
							}
							--_stateTimeout;
						}

						if (e is GenusEvent.Stop)
							return GenusState.Idle;
						return CurrentState;
					},
					enterStateAction: () => _stateTimeout = _p.Config.IntertrialTimeout);
			}

			/// <summary>
			/// Total number of trials.
			/// </summary>
			public int TrialCount => _trials.Size;

			/// <summary>
			/// Index of current trial.
			/// </summary>
			public int CurrentTrialIndex { get; protected set; }

			/// <summary>
			/// Current trial.
			/// </summary>
			public Trial CurrentTrial => _trials.IsEmpty ? null : _trials[CurrentTrialIndex];

			/// <summary>
			/// Reset the trial list and the current index.
			/// </summary>
			protected void ResetTrials()
			{
				// prepare the trial list
				_trials.Clear();
				CurrentTrialIndex = -1;
				foreach (var t in _p.Config.Trials)
					for (int i = 0; i < t.Count; ++i)
						_trials.PushBack(t);
				
				// shuffle if necessary
				if (_p.Config.Randomize)
					new RNG().Shuffle(_trials);

				// assign each trial its instruction list
				foreach (var t in _trials)
				{
					var instructions = GenusMk2CachedTrial.Get(t.InstructionList);
					if (instructions is null)
						throw new Exception($"Instruction list \'{t.InstructionList}\' not found for trial.");
					t.Instructions = instructions;
				}
			}

			/// <summary>
			/// Emit a trial line in the eti.
			/// </summary>
			protected void EmitEtiLine()
			{
				if (Numerics.IsClamped(CurrentTrialIndex, (0, TrialCount - 1)) && _p.TrialLogger is not null)
				{
					var il = CurrentTrial.Instructions;
					string Binarize(bool p) => p ? "1" : "0";
					string Frequency((float, float) f) => f.Size() > 0 ? $"{f.Item1}:{f.Item2}" : f.Item1.ToString();

					_p.TrialLogger.LogTrial(
						CurrentTrialIndex + 1, 
						il.Name, 
						il.Type,
						Binarize(il.Audio), 
						Binarize(il.Visual),
						il.StimulationRuntime, 
						il.StepCount,
						Frequency(il.FlickerFrequency),
						il.ToneFrequency,
						Binarize(il.UseFlickerTriggers),
						Binarize(il.UseTransitionTriggers));
				}
			}

			/// <summary>
			/// If timeout is reached, go to another state, otherwise decrement the timeout counter.
			/// </summary>
			/// <param name="onTimeoutReached">The state to go to on timeout zero.</param>
			/// <returns>A state.</returns>
			protected GenusState Elapse(GenusState onTimeoutReached)
			{
				if (_stateTimeout == 0)
					return onTimeoutReached;
				--_stateTimeout;
				return CurrentState;
			}



			protected GenusMk2Cached		_p;
			protected GenusMk2Controller	_stim;
			protected IUserInterface		_ui;

			protected Vector<Trial>			_trials	= new();
			protected int					_stateTimeout;
		}
	}

	/// <summary>
	/// Genus configuration.
	/// </summary>
	public class GenusMk2CachedConfig
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

		[JsonPropertyName("trials")]
		public Vector<GenusMk2Cached.Trial> Trials { get; set; } = new();
	}
}
