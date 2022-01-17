using System;
using System.IO;
using System.Text.Json.Serialization;
using TINS.Ephys.ModuleImplementations;
using TINS.Ephys.UI;
using TINS.Utilities;

namespace TINS.Ephys.Stimulation.Genus
{
	/// <summary>
	/// Genus protocol.
	/// </summary>
	public class GenusMk2Static
		: StimulationProtocol<GenusMk2StaticConfig, GenusMk2Controller>
	{
		/// <summary>
		/// Create a new Genus protocol.
		/// </summary>
		/// <param name="parent">The parent ephys stream.</param>
		/// <param name="config">The configuration for this protocol.</param>
		/// <param name="stimulusController">The stimulus controller for the protocol.</param>
		public GenusMk2Static(EphysStream parent, GenusMk2StaticConfig config, GenusMk2Controller stimulusController)
			: base(parent, config, stimulusController)
		{
			// load the configuration
			Config		= config;
			TextOutput	= null;

			// check compatibility
			if (Config.SupportedSamplingPeriod != ParentStream.Settings.Input.PollingPeriod)
				throw new Exception("The protocol's polling period does not match the current settings.");

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
		/// Start the protocol.
		/// </summary>
		public override void Start()
		{
			StimulusController.Connect();
			StimulusController.Reset();


			_stateMachine.ProcessEvent(GenusEvent.Start);

			// create a text writer 
			if (ParentStream.OutputStream is object)
			{
				// obtain path from output 
				ParentStream.OutputStream.GetPath(out var dir, out var dsName);
				var outputPath = Path.Combine(dir, dsName + ".eti");
				
				// create the text writer
				TextOutput = new StreamWriter(outputPath);
				TextOutput.WriteLine($"Trials,{_stateMachine.TrialCount}\nFields,2\n\nTrial,FlickerFreqL,FlickerFreqR,AudioFreq");
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

				StimulusController?.Disconnect();

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

		protected StateMachine _stateMachine;

		/// <summary>
		/// Encapsulates a genus trial type.
		/// </summary>
		public class Trial
		{
			[JsonPropertyName("count")]
			public int Count { get; set; }

			[JsonPropertyName("preTimeout")]
			public int PrestimulusTimeout { get; set; }

			[JsonPropertyName("stimTimeout")]
			public int StimulusTimeout { get; set; }

			[JsonPropertyName("postTimeout")]
			public int PoststimulusTimeout { get; set; }

			[JsonPropertyName("flickerFrequencyLeft")]
			public float FlickerFrequencyLeft { get; set; }

			[JsonPropertyName("flickerFrequencyRight")]
			public float FlickerFrequencyRight { get; set; }

			[JsonPropertyName("audioFrequency")]
			public float AudioFlickerFrequency { get; set; }

			[JsonPropertyName("toneFrequency")]
			public float ToneFrequency { get; set; }
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
			public StateMachine(GenusMk2Static protocol)
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
			protected override void ConfigureStateMachine()
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
						_stim.EmitTrigger(_p.Config.PrestimulusTrigger);
					});


				// STIMULUS
				AddState(GenusState.Stimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock => Elapse(GenusState.Poststimulus),
						GenusEvent.Stop		=> GenusState.Idle,
						_					=> CurrentState
					},
					enterStateAction: () =>
					{
						_stateTimeout = _trials[CurrentTrialIndex].StimulusTimeout;
						_stim.ChangeParameters(
							_trials[CurrentTrialIndex].FlickerFrequencyLeft,
							_trials[CurrentTrialIndex].FlickerFrequencyRight,
							_trials[CurrentTrialIndex].AudioFlickerFrequency,
							_trials[CurrentTrialIndex].ToneFrequency,
							_p.Config.StimulusTrigger);
					},
					exitStateAction: () => _stim.ChangeParameters(0, 0, 0, 0, null));


				// POSTSTIMULUS
				AddState(GenusState.Poststimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock => Elapse(GenusState.Intertrial),
						GenusEvent.Stop		=> GenusState.Idle,
						_					=> CurrentState
					},
					enterStateAction: () =>
					{
						_stateTimeout = _trials[CurrentTrialIndex].PoststimulusTimeout;
						_stim.EmitTrigger(_p.Config.PoststimulusTrigger);
					},
					exitStateAction: () => _stim.EmitTrigger(_p.Config.TrialEndTrigger));


				// INTERTRIAL
				AddState(GenusState.Intertrial,
					eventAction: (e) => 
					{
						if (e is GenusEvent.NewBlock)
						{
							if (_stateTimeout == 0)
							{
								// emit line
								if (Numerics.IsClamped(CurrentTrialIndex, (0, TrialCount - 1)) && _p.TextOutput is not null)
									_p.TextOutput.WriteLine($"{CurrentTrialIndex + 1},{CurrentTrial.FlickerFrequencyLeft},{CurrentTrial.FlickerFrequencyRight},{CurrentTrial.ToneFrequency}");

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



			protected GenusMk2Static		_p;
			protected GenusMk2Controller	_stim;
			protected IUserInterface		_ui;

			protected Vector<Trial>			_trials	= new();
			protected int					_stateTimeout;
		}
	}




	/// <summary>
	/// Genus configuration.
	/// </summary>
	public class GenusMk2StaticConfig
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
		public Vector<GenusMk2Static.Trial> Trials { get; set; } = new();
	}
}
