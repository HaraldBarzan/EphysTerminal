using System;
using System.IO;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using TINS.Conexus.Data;
using TINS.Ephys.Settings;
using TINS.IO;
using TINS.Terminal.Display.Protocol;
using TINS.Terminal.Stimulation;
using TINS.Terminal.UI;
using TINS.Utilities;

namespace TINS.Terminal.Protocols.Genus
{
	/// <summary>
	/// The possible states of the Genus protocol.
	/// </summary>
	public enum HumanGenusState
	{
		Idle,
		Await,
		Mask,
		Prestimulus,
		Stimulus,
		Poststimulus,
		PostMask,
		Intertrial
	}


	/// <summary>
	/// Genus protocol.
	/// </summary>
	public class HumanGenusProtocol
		: StimulationProtocol<HumanGenusConfig, GenusController>
	{
		/// <summary>
		/// Create a new Genus protocol.
		/// </summary>
		/// <param name="stream">The parent ephys stream.</param>
		/// <param name="config">The configuration for this protocol.</param>
		/// <param name="stimulusController">The stimulus controller for the protocol.</param>
		public HumanGenusProtocol(EphysTerminal stream, HumanGenusConfig config, GenusController stimulusController)
			: base(stream, config, stimulusController)
		{
			// load the configuration
			Config		= config;
			TrialLogger = null;

			if (Config.TrialSelfInitiate)
			{
				if (stream.InputStream is not BiosemiTcpStream biosemiStream)
					throw new Exception("Trial self initiation is only compatible with the Biosemi stream.");
				if (!biosemiStream.UseResponseSwitches && !Config.UseProtocolScreen)
					throw new Exception("Trial self initiation requires a Biosemi stream with UseResponseSwitches set to \'true\'.");

				biosemiStream.ResponseSwitchPressed += BiosemiStream_ResponseSwitchPressed;
			}

			// check compatibility
			if (Config.BlockPeriod != SourceStream.Settings.Input.PollingPeriod)
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
			var port = (SourceStream.UI as MainWindow).SettingsDialog.SelectedPortGenus;
			StimulusController.Connect(port);
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
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void SkiaProtocolDisplay_SpaceKeyPressed(object sender, KeyEventArgs e)
			=> ProcessEvent(GenusEvent.InitiateTrial);

		/// <summary>
		/// Assert whether the protocol is running.
		/// </summary>
		public override bool IsRunning => _stateMachine.CurrentState != HumanGenusState.Idle;

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
		public class TrialTemplate
		{
			[INILine(Key = "REPETITIONS")]
			public int Count { get; set; }

			[INILine(Key = "INSTRUCTION_GENERATOR")]
			public string InstructionGenerator { get; set; }

			[INILine(Key = "TRIAL_NAME")]
			public string TrialName { get; set; }

			[INILine(Key = "MASK_TIMEOUT")]
			public int MaskTimeout { get; set; }

			[INILine(Key = "PRESTIMULUS_TIMEOUT")]
			public int PrestimulusTimeout { get; set; }

			[INILine(Key = "POSTSTIMULUS_TIMEOUT")]
			public int PoststimulusTimeout { get; set; }
			
			[INILine(Key = "POSTMASK_TIMEOUT")]
			public int PostMaskTimeout { get; set; }

			public GenusCachedTrial Instructions { get; set; }
		}

		/// <summary>
		/// Genus protocol state machine.
		/// </summary>
		public class StateMachine
			: StateMachine<HumanGenusState, GenusEvent>
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
			public StateMachine(HumanGenusProtocol protocol)
			{
				_p				= protocol;
				_stim			= protocol.StimulusController;
				_ui				= protocol.SourceStream.UI;
				_protoDisplay	= null;

				// just so we have a list at any time
				ResetTrials();
			}

			/// <summary>
			/// Configure the state machine.
			/// </summary>
			protected override void ConfigureStates()
			{
				// IDLE
				AddState(HumanGenusState.Idle,
					eventAction: (e) =>
					{
						if (e is GenusEvent.Start)
						{
							ResetTrials();
							return HumanGenusState.Intertrial;
						}
						return CurrentState;
					},
					enterStateAction: () =>
					{
						_stim.Reset();
						StopProtocolDisplay();
					},
					exitStateAction: StartProtocolDisplay);

				// PRETRIAL
				AddState(HumanGenusState.Await,
					eventAction: (e) => e switch
					{
						GenusEvent.InitiateTrial	=> HumanGenusState.Mask,
						_							=> CurrentState
					},
					enterStateAction: () =>
					{
						_stim.Beep();
						_protoDisplay?.SwitchToTextAsync(Color.FromRgb(0, 0, 0), "Press SPACE or one of the EEG buttons to start a new trial...", null);
					});

				// MASK
				AddState(HumanGenusState.Mask,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock => Elapse(HumanGenusState.Prestimulus),
						GenusEvent.Stop => HumanGenusState.Idle,
						_ => CurrentState
					},
					enterStateAction: () =>
					{
						_stateTimeout = CurrentTrial.MaskTimeout;
						_stim.EmitTrigger(_p.Config.MaskTrigger);
						_protoDisplay?.SwitchToFixationCrossAsync(Color.FromRgb(0, 0, 0), null);
					});

				// PRESTIMULUS
				AddState(HumanGenusState.Prestimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock		=> Elapse(HumanGenusState.Stimulus),
						GenusEvent.Stop			=> HumanGenusState.Idle,
						_						=> CurrentState
					},
					enterStateAction: () =>
					{
						_stateTimeout = _trials[CurrentTrialIndex].PrestimulusTimeout;
						_protoDisplay?.SwitchToFixationCrossAsync(Color.FromRgb(128, 128, 128), null);
						_stim.EmitTrigger(_p.Config.TrialStartTrigger);
					});


				// STIMULUS
				AddState(HumanGenusState.Stimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.StimulationComplete	=> HumanGenusState.Poststimulus,
						GenusEvent.Stop					=> HumanGenusState.Idle,
						_								=> CurrentState
					},
					enterStateAction: () => _stim.SendInstructionList(CurrentTrial.Instructions.Instructions));


				// POSTSTIMULUS
				AddState(HumanGenusState.Poststimulus,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock => Elapse(HumanGenusState.PostMask),
						GenusEvent.Stop		=> HumanGenusState.Idle,
						_					=> CurrentState
					},
					enterStateAction:	() => _stateTimeout = CurrentTrial.PoststimulusTimeout,
					exitStateAction:	() => _stim.EmitTrigger(_p.Config.TrialEndTrigger));

				// POST-MASK
				AddState(HumanGenusState.PostMask,
					eventAction: (e) => e switch
					{
						GenusEvent.NewBlock => Elapse(HumanGenusState.Intertrial),
						GenusEvent.Stop => HumanGenusState.Idle,
						_ => CurrentState
					},
					enterStateAction: () => _stateTimeout = CurrentTrial.PostMaskTimeout,
					exitStateAction: () => 
					{
						_stim.EmitTrigger(_p.Config.PostMaskTrigger);
						_protoDisplay?.SwitchToFixationCrossAsync(Color.FromRgb(0, 0, 0), null); 
					});


				// INTERTRIAL
				AddState(HumanGenusState.Intertrial,
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
									return HumanGenusState.Idle;
								}

								// begin a new trial
								TrialBegin?.Invoke(CurrentTrialIndex);
								if (_p.Config.TrialSelfInitiate)
									return HumanGenusState.Await;
								return HumanGenusState.Mask;
							}
							--_stateTimeout;
						}

						if (e is GenusEvent.Stop)
							return HumanGenusState.Idle;
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
			public TrialTemplate CurrentTrial => _trials.IsEmpty ? null : _trials[CurrentTrialIndex];

			/// <summary>
			/// Reset the trial list and the current index.
			/// </summary>
			protected void ResetTrials()
			{
				// prepare the trial list
				_trials.Clear();
				CurrentTrialIndex = -1;
				foreach (var t in _p.Config.TrialTemplates)
					for (int i = 0; i < t.Count; ++i)
						_trials.PushBack(t);

				// shuffle if necessary
				if (_p.Config.Randomize)
					new RNG().Shuffle(_trials);

				// assign each trial its instruction list
				foreach (var t in _trials)
				{
					var instructions = GenusCachedTrial.Create(_p.Config, t);
					if (instructions is null)
						throw new Exception($"Instruction list \'{t.InstructionGenerator}\' not found for trial.");
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
			protected HumanGenusState Elapse(HumanGenusState onTimeoutReached)
			{
				--_stateTimeout;
				if (_stateTimeout == 0)
					return onTimeoutReached;
				return CurrentState;
			}

			/// <summary>
			/// 
			/// </summary>
			protected void StartProtocolDisplay()
			{
				StopProtocolDisplay();
				if (_p.Config.UseProtocolScreen)
				{
					var thread = new Thread(() => 
					{
						_protoDisplay = new SkiaProtocolDisplay(Monitors.MonitorCount - 1);
						_protoDisplay.KeyPressed += (_, _) => _p.ProcessEvent(GenusEvent.InitiateTrial);
						_protoDisplay.ShowDialog();
					});
					thread.SetApartmentState(ApartmentState.STA);
					thread.Start();
				}
			}

			/// <summary>
			/// 
			/// </summary>
			protected void StopProtocolDisplay()
			{
				if (_protoDisplay is not null)
				{
					_protoDisplay.Dispatcher.BeginInvoke(_protoDisplay.Close);
					_protoDisplay = null;
				}
			}

			protected HumanGenusProtocol	_p;
			protected GenusController		_stim;
			protected SkiaProtocolDisplay	_protoDisplay;
			protected IUserInterface		_ui;
			protected Vector<TrialTemplate>			_trials			= new();
			protected int					_stateTimeout;
		}
	}



	/// <summary>
	/// Genus configuration.
	/// </summary>
	public class HumanGenusConfig
		: ProtocolConfig
	{
		/// <summary>
		/// 
		/// </summary>
		public struct TrialDefinition
		{
			[INILine(Key = "NAME")]
			public string Name { get; set; }

			[INILine(Key = "TYPE")]
			public string TemplateType { get; set; }
		}

		/// <summary>
		/// The duration, in seconds, of a single block of data.
		/// </summary>
		[INILine(Key = "BLOCK_PERIOD")]
		public float BlockPeriod { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "TRIAL_START_TRIGGER")]
		public byte TrialStartTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "MASK_START_TRIGGER")]
		public byte MaskTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "STIMULUS_START_TRIGGER")]
		public byte StimulationStartTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "STIMULUS_END_TRIGGER")]
		public byte StimulationEndTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "POSTSTIMULUS_END_TRIGGER")]
		public byte PostMaskTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "TRIAL_END_TRIGGER")]
		public byte TrialEndTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "FLICKER_TRIGGERS_BINDING", Default = GenusController.FlickerTriggerAttach.None)]
		public GenusController.FlickerTriggerAttach FlickerTriggersBinding { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "FLICKER_TRIGGERS_RISE_TRIGGER")]
		public byte FlickerTriggersRiseTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "FLICKER_TRIGGERS_FALL_TRIGGER")]
		public byte FlickerTriggersFallTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "INTERTRIAL_TIMEOUT")]
		public int IntertrialTimeout { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "RANDOMIZE_TRIALS")]
		public bool Randomize { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "TRIAL_SELF_INITIATE")]
		public bool TrialSelfInitiate { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "FEEDBACK_ON_STIMULUS_END")]
		public bool FeedbackOnStimulusEnd { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "USE_PROTOCOL_SCREEN")]
		public bool UseProtocolScreen { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[INIStructVector(Key = "TRIAL_TEMPLATE_COUNT", ValueMask = "TRIAL_TEMPLATE_*_", StructType = typeof(TrialDefinition))]
		public Vector<TrialDefinition> TrialDefinitions { get; set; } = new();

		/// <summary>
		/// The list of trials.
		/// </summary>
		public Vector<HumanGenusProtocol.TrialTemplate> TrialTemplates { get; set; } = new();

		/// <summary>
		/// Serialization.
		/// </summary>
		/// <param name="ini"></param>
		/// <param name="sectionName"></param>
		/// <param name="direction"></param>
		public override void Serialize(INI ini, string sectionName, SerializationDirection direction)
		{
			base.Serialize(ini, sectionName, direction);

			if (direction is SerializationDirection.In)
			{
				TrialTemplates.Resize(TrialDefinitions.Size);

				for (int i = 0; i < TrialDefinitions.Size; i++)
				{
					TrialTemplates[i] = TrialDefinitions[i].TemplateType switch
					{
						"STATIC"	=> new HumanGenusStaticTrial(),
						"RAMP"		=> new HumanGenusRampTrial(),
						_			=> null
					};

					if (TrialTemplates[i] is not null)
						INISerialization.Serialize(ini[TrialDefinitions[i].Name], TrialTemplates[i]);
				}
			}
			else
			{
				for (int i = 0; i < TrialTemplates.Size; ++i)
					INISerialization.Serialize(TrialTemplates, ini[TrialDefinitions[i].Name]);
			}
		}


		/// <summary>
		/// The list of trials.
		/// </summary>
		public Vector<HumanGenusProtocol.TrialTemplate> Trials { get; set; } = new();
	}

	/// <summary>
	/// Genus static trial.
	/// </summary>
	public class HumanGenusStaticTrial
		: HumanGenusProtocol.TrialTemplate
	{
		[INILine(Key = "FREQUENCY")]
		public float Frequency { get; set; }

		[INILine(Key = "AUDIO_TONE_FREQUENCY")]
		public float AudioToneFrequency { get; set; } = 10000;

		[INILine(Key = "DURATION")]
		public int Duration { get; set; }

		[INILine(Key = "USE_AUDIO_STIMULATION")]
		public bool UseAudioStimulation { get; set; }

		[INILine(Key = "USE_VISUAL_STIMULATION")]
		public bool UseVisualStimulation { get; set; }
	}

	/// <summary>
	/// 
	/// </summary>
	public class HumanGenusRampTrial
		: HumanGenusProtocol.TrialTemplate
	{
		[INILine(Key = "FREQUENCY_START")]
		public float FrequencyStart { get; set; }

		[INILine(Key = "FREQUENCY_END")]
		public float FrequencyEnd { get; set; }

		[INILine(Key = "FREQUENCY_STEPS")]
		public int FrequencySteps { get; set; }

		[INILine(Key = "AUDIO_TONE_FREQUENCY")]
		public float AudioToneFrequency { get; set; } = 10000;

		[INILine(Key = "DURATION")]
		public int Duration { get; set; }

		[INILine(Key = "USE_AUDIO_STIMULATION")]
		public bool UseAudioStimulation { get; set; }

		[INILine(Key = "USE_VISUAL_STIMULATION")]
		public bool UseVisualStimulation { get; set; }
	}
}
