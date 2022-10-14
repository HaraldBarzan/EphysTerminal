using System;
using System.IO;
using System.Text.Json.Serialization;
using TINS.Conexus.Data;
using TINS.Terminal.Stimulation;
using TINS.Terminal.UI;
using TINS.Utilities;

namespace TINS.Terminal.Protocols.Genus
{
	/// <summary>
	/// Genus protocol.
	/// </summary>
	public class GenusProtocol
		: StimulationProtocol<GenusConfig, GenusController>
	{
		/// <summary>
		/// Create a new Genus protocol.
		/// </summary>
		/// <param name="stream">The parent ephys stream.</param>
		/// <param name="config">The configuration for this protocol.</param>
		/// <param name="stimulusController">The stimulus controller for the protocol.</param>
		public GenusProtocol(EphysTerminal stream, GenusConfig config, GenusController stimulusController)
			: base(stream, config, stimulusController)
		{
			// load the configuration
			Config		= config;
			TrialLogger = null;

			if (Config.TrialSelfInitiate)
			{
				if (stream.InputStream is not BiosemiTcpStream biosemiStream)
					throw new Exception("Trial self initiation is only compatible with the Biosemi stream.");
				if (!biosemiStream.UseResponseSwitches)
					throw new Exception("Trial self initiation requires a Biosemi stream with UseResponseSwitches set to \'true\'.");

				biosemiStream.ResponseSwitchPressed += BiosemiStream_ResponseSwitchPressed;
			}

			// check compatibility
			if (Config.SupportedSamplingPeriod != SourceStream.Settings.Input.PollingPeriod)
				throw new Exception("The protocol's polling period does not match the current settings.");

			// attach feedback detector
			stimulusController.FeedbackReceived += OnDeviceFeedback;

			// initialize the state machine
			_stateMachine = new(this);
			_stateMachine.TrialBegin += (iTrial) => RaiseUpdateProgress(iTrial, TrialCount);
			_stateMachine.RunCompleted += () =>
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
			if (SourceStream.InputStream is BiosemiTcpStream biosemiTcpStream)
				biosemiTcpStream.ResponseSwitchPressed -= BiosemiStream_ResponseSwitchPressed;
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
			if (SourceStream.OutputStream is object)
			{
				// obtain path from output 
				SourceStream.OutputStream.GetPath(out var dir, out var dsName);
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
		/// Signal an arbitrary event to the protocol.
		/// </summary>
		/// <param name="genusEvent">The event.</param>
		public virtual void ProcessEvent(GenusEvent genusEvent)
		{
			_stateMachine.ProcessEvent(genusEvent);
		}

		/// <summary>
		/// Raised when the device signals 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="feedback"></param>
		protected virtual void OnDeviceFeedback(object sender, GenusController.Feedback feedback)
		{
			if (feedback is GenusController.Feedback.StimulationComplete)
				_stateMachine.ProcessEvent(GenusEvent.StimulationComplete);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BiosemiStream_ResponseSwitchPressed(object sender, Ephys.Settings.BiosemiResponseSwitch e)
			=> ProcessEvent(GenusEvent.InitiateTrial);

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

			public GenusCachedTrial Instructions { get; set; }
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
			public StateMachine(GenusProtocol protocol)
			{
				_p = protocol;
				_stim = protocol.StimulusController;
				_ui = protocol.SourceStream.UI;

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

				// PRETRIAL
				AddState(GenusState.Await,
					eventAction: (e) => e switch
					{
						GenusEvent.InitiateTrial	=> GenusState.Prestimulus,
						_							=> CurrentState
					},
					enterStateAction: () => _stim.Beep());


				// PRESTIMULUS
				AddState(GenusState.Prestimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock		=> Elapse(GenusState.Stimulus),
						GenusEvent.Stop			=> GenusState.Idle,
						_						=> CurrentState
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
					enterStateAction: () => _stim.SendInstructionList(CurrentTrial.Instructions.Instructions));


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
						if (e is GenusEvent.NewBlock || e is GenusEvent.InitiateTrial)
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
								if (_p.Config.TrialSelfInitiate)
									return GenusState.Await;
								return GenusState.Prestimulus;
							}
							--_stateTimeout;
						}

						if (e is GenusEvent.Stop)
							return GenusState.Idle;
						return CurrentState;
					},
					enterStateAction: () =>
					{
						if (!_p.Config.TrialSelfInitiate)
							_stateTimeout = _p.Config.IntertrialTimeout;
					});
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
					var instructions = GenusCachedTrial.Get(t.InstructionList);
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



			protected GenusProtocol		_p;
			protected GenusController	_stim;
			protected IUserInterface	_ui;
			protected Vector<Trial>		_trials			= new();
			protected int				_stateTimeout;
		}
	}



	/// <summary>
	/// Genus configuration.
	/// </summary>
	public class GenusConfig
		: ProtocolConfig
	{
		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("supportedSamplingPeriod")]
		public float SupportedSamplingPeriod { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("trialStartTrigger")]
		public byte TrialStartTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("trialEndTrigger")]
		public byte TrialEndTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("intertrialTimeout")]
		public int IntertrialTimeout { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("randomize")]
		public bool Randomize { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("trialSelfInitiate")]
		public bool TrialSelfInitiate { get; set; }

		/// <summary>
		/// The list of trials.
		/// </summary>
		[JsonPropertyName("trials")]
		public Vector<GenusProtocol.Trial> Trials { get; set; } = new();
	}



	///// <summary>
	///// Genus protocol state machine.
	///// </summary>
	//public class StateMachine
	//	: StateMachine<GenusState, GenusEvent>
	//{
	//	/// <summary>
	//	/// Raised when a trial is completed.
	//	/// </summary>
	//	public event Action<int> TrialBegin;
	//
	//	/// <summary>
	//	/// Raised when a trial run is completed.
	//	/// </summary>
	//	public event Action RunCompleted;
	//
	//	/// <summary>
	//	/// Create a state machine for a Genus protocol.
	//	/// </summary>
	//	/// <param name="protocol">The parent Genus protocol.</param>
	//	public StateMachine(GenusProtocol protocol)
	//	{
	//		_p = protocol;
	//		_stim = protocol.StimulusController;
	//		_ui = protocol.SourceStream.UI;
	//
	//		// just so we have a list at any time
	//		ResetTrials();
	//	}
	//
	//	/// <summary>
	//	/// Configure the state machine.
	//	/// </summary>
	//	protected override void ConfigureStates()
	//	{
	//		// IDLE
	//		AddState(GenusState.Idle,
	//			eventAction: (e) =>
	//			{
	//				if (e is GenusEvent.Start)
	//				{
	//					ResetTrials();
	//					return GenusState.Intertrial;
	//				}
	//				return CurrentState;
	//			},
	//			enterStateAction: () => _stim.Reset());
	//
	//
	//		// PRESTIMULUS
	//		AddState(GenusState.Prestimulus,
	//			eventAction: (e) => e switch
	//			{
	//				GenusEvent.NewBlock		=> Elapse(GenusState.Stimulus),
	//				GenusEvent.Stop			=> GenusState.Idle,
	//				_						=> CurrentState
	//			},
	//			enterStateAction: () =>
	//			{
	//				_stateTimeout = _trials[CurrentTrialIndex].PrestimulusTimeout;
	//				_stim.EmitTrigger(_p.Config.TrialStartTrigger);
	//			});
	//
	//
	//		// STIMULUS
	//		AddState(GenusState.Stimulus,
	//			eventAction: (e) => e switch
	//			{
	//				GenusEvent.StimulationComplete	=> GenusState.Poststimulus,
	//				GenusEvent.Stop					=> GenusState.Idle,
	//				_								=> CurrentState
	//			},
	//			enterStateAction: () => _stim.SendInstructionList(CurrentTrial.Instructions.Instructions));
	//
	//
	//		// POSTSTIMULUS
	//		AddState(GenusState.Poststimulus,
	//			eventAction: (e) => e switch
	//			{
	//				GenusEvent.NewBlock => Elapse(GenusState.Intertrial),
	//				GenusEvent.Stop		=> GenusState.Idle,
	//				_					=> CurrentState
	//			},
	//			enterStateAction:	() => _stateTimeout = CurrentTrial.PoststimulusTimeout,
	//			exitStateAction:	() => _stim.EmitTrigger(_p.Config.TrialEndTrigger));
	//
	//
	//		// INTERTRIAL
	//		AddState(GenusState.Intertrial,
	//			eventAction: (e) =>
	//			{
	//				if (e is GenusEvent.NewBlock || e is GenusEvent.InitiateTrial)
	//				{
	//					if (_stateTimeout == 0)
	//					{
	//						// emit line
	//						EmitEtiLine();
	//						CurrentTrialIndex++;
	//
	//						// check stopping condition
	//						if (CurrentTrialIndex == TrialCount)
	//						{
	//							RunCompleted?.Invoke();
	//							return GenusState.Idle;
	//						}
	//
	//						// begin a new trial
	//						TrialBegin?.Invoke(CurrentTrialIndex);
	//						return GenusState.Prestimulus;
	//					}
	//					--_stateTimeout;
	//				}
	//
	//				if (e is GenusEvent.Stop)
	//					return GenusState.Idle;
	//				return CurrentState;
	//			},
	//			enterStateAction: () =>
	//			{
	//				if (!_p.Config.TrialSelfInitiate)
	//					_stateTimeout = _p.Config.IntertrialTimeout;
	//			},
	//			exitStateAction: () =>
	//			{
	//
	//			});
	//	}
	//
	//	/// <summary>
	//	/// Total number of trials.
	//	/// </summary>
	//	public int TrialCount => _trials.Size;
	//
	//	/// <summary>
	//	/// Index of current trial.
	//	/// </summary>
	//	public int CurrentTrialIndex { get; protected set; }
	//
	//	/// <summary>
	//	/// Current trial.
	//	/// </summary>
	//	public Trial CurrentTrial => _trials.IsEmpty ? null : _trials[CurrentTrialIndex];
	//
	//	/// <summary>
	//	/// Reset the trial list and the current index.
	//	/// </summary>
	//	protected void ResetTrials()
	//	{
	//		// prepare the trial list
	//		_trials.Clear();
	//		CurrentTrialIndex = -1;
	//		foreach (var t in _p.Config.Trials)
	//			for (int i = 0; i < t.Count; ++i)
	//				_trials.PushBack(t);
	//
	//		// shuffle if necessary
	//		if (_p.Config.Randomize)
	//			new RNG().Shuffle(_trials);
	//
	//		// assign each trial its instruction list
	//		foreach (var t in _trials)
	//		{
	//			var instructions = GenusCachedTrial.Get(t.InstructionList);
	//			if (instructions is null)
	//				throw new Exception($"Instruction list \'{t.InstructionList}\' not found for trial.");
	//			t.Instructions = instructions;
	//		}
	//	}
	//
	//	/// <summary>
	//	/// Emit a trial line in the eti.
	//	/// </summary>
	//	protected void EmitEtiLine()
	//	{
	//		if (Numerics.IsClamped(CurrentTrialIndex, (0, TrialCount - 1)) && _p.TrialLogger is not null)
	//		{
	//			var il = CurrentTrial.Instructions;
	//			string Binarize(bool p) => p ? "1" : "0";
	//			string Frequency((float, float) f) => f.Size() > 0 ? $"{f.Item1}:{f.Item2}" : f.Item1.ToString();
	//
	//			_p.TrialLogger.LogTrial(
	//				CurrentTrialIndex + 1,
	//				il.Name,
	//				il.Type,
	//				Binarize(il.Audio),
	//				Binarize(il.Visual),
	//				il.StimulationRuntime,
	//				il.StepCount,
	//				Frequency(il.FlickerFrequency),
	//				il.ToneFrequency,
	//				Binarize(il.UseFlickerTriggers),
	//				Binarize(il.UseTransitionTriggers));
	//		}
	//	}
	//
	//	/// <summary>
	//	/// If timeout is reached, go to another state, otherwise decrement the timeout counter.
	//	/// </summary>
	//	/// <param name="onTimeoutReached">The state to go to on timeout zero.</param>
	//	/// <returns>A state.</returns>
	//	protected GenusState Elapse(GenusState onTimeoutReached)
	//	{
	//		if (_stateTimeout == 0)
	//			return onTimeoutReached;
	//		--_stateTimeout;
	//		return CurrentState;
	//	}
	//
	//
	//
	//	protected GenusProtocol _p;
	//	protected GenusController _stim;
	//	protected IUserInterface _ui;
	//
	//	protected Vector<Trial> _trials = new();
	//	protected int _stateTimeout;
	//}
}
