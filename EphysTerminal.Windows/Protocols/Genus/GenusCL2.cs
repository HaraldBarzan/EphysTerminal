using System;
using System.IO;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using TINS.Conexus.Data;
using TINS.Containers;
using TINS.Ephys.Analysis;
using TINS.Ephys.Analysis.Events;
using TINS.IO;
using TINS.Terminal.Display.Protocol;
using TINS.Terminal.Stimulation;
using TINS.Utilities;

namespace TINS.Terminal.Protocols.Genus
{
	using TFSpectrumAnalyzer = IAnalyzerOutput<Ring<ChannelTimeResult<Spectrum2D>>>;


	/// <summary>
	/// 
	/// </summary>
	public class GenusCL2
		: StimulationProtocol<GenusCL2Config, GenusController>
	{
		/// <summary>
		/// Raised when a trial is completed.
		/// </summary>
		public event Action<int> TrialBegin;

		/// <summary>
		/// Raised when a trial run is completed.
		/// </summary>
		public event Action RunCompleted;

		public GenusCL2(EphysTerminal stream, GenusCL2Config config, GenusController controller)
			: base(stream, config, controller)
		{
			// load the configuration
			Config			= config;
			UpdateLogger	= null;
			TrialLogger		= null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			//if (SourceStream.UI is not null  && SourceStream.UI is MainWindow mw)
			//	mw.
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
			StateMachine.ProcessEvent(GenusEvent.Start);

			// create a text writer 
			if (SourceStream.OutputStream is object)
			{
				// obtain path from output 
				SourceStream.OutputStream.GetPath(out var dir, out var dsName);

				// create the text writer
				UpdateLogger = new TrialInfoLogger(Path.Combine(dir, dsName + "-updates.eti"),
					header: new()
					{
						"Trial",
						"UpdateIndex",
						"SourceChannel",
						"OldFrequency",
						"NewFrequency"
					});
				TrialLogger = new TrialInfoLogger(Path.Combine(dir, dsName + ".eti"),
					header: new()
					{
						"Trial",
						"SourceChannel",
					});

				Console.WriteLine("Created trial logger");
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
				StateMachine.ProcessEvent(GenusEvent.Stop);

				UpdateLogger?.Dispose();
				UpdateLogger = null;
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
			=> StateMachine.ProcessEvent(GenusEvent.NewBlock);

		/// <summary>
		/// Signal an arbitrary event to the protocol.
		/// </summary>
		/// <param name="genusEvent">The event.</param>
		public virtual void ProcessEvent(GenusEvent genusEvent)
			=> StateMachine.ProcessEvent(genusEvent);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="trialIndex"></param>
		public void NotifyTrialBegin(int trialIndex)
			=> RaiseUpdateProgress(trialIndex, StateMachine.TotalTrialCount);

		/// <summary>
		/// 
		/// </summary>
		public void NotifyRunComplete()
		{
			Stop();
			RaiseUpdateProgress(StateMachine.TotalTrialCount, StateMachine.TotalTrialCount);
		}

		public CL2StateMachine StateMachine { get; protected init; }

		/// <summary>
		/// Assert whether the protocol is running.
		/// </summary>
		public override bool IsRunning => StateMachine.CurrentState != GenusClosedLoopState.Idle;

		/// <summary>
		/// Text output for this protocol. Tracks frequency updates. Is active only when recording.
		/// </summary>
		public TrialInfoLogger UpdateLogger { get; protected set; }

		/// <summary>
		/// Text output for this protocol. Tracks actual trials. Is active only when recording.
		/// </summary>
		public TrialInfoLogger TrialLogger { get; protected set; }

		/// <summary>
		/// Number of trials.
		/// </summary>
		public int TrialCount => StateMachine.TotalTrialCount;

		/// <summary>
		/// Raised when the device signals 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="feedback"></param>
		protected virtual void OnDeviceFeedback(object sender, GenusController.Feedback feedback)
		{
			if (feedback is GenusController.Feedback.StimulationComplete)
				StateMachine.ProcessEvent(GenusEvent.StimulationComplete);
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
	}


	/// <summary>
	/// State machine for closed loop stimulation.
	/// </summary>
	public class CL2StateMachine
		: StateMachine<GenusClosedLoopState, GenusEvent>
	{

		/// <summary>
		/// Create a state machine for a Genus protocol.
		/// </summary>
		/// <param name="protocol">The parent Genus protocol.</param>
		public CL2StateMachine(GenusCL2 protocol)
		{
			_p					= protocol;
			_gc					= protocol.StimulusController;
			_stateTimeout		= 0;
			_stimUpdateTimeout	= 0;
			TotalTrialCount		= _p.Config.TrialCount;
			CurrentTrialIndex	= 0;

			// init spectrum primitives
		}

		/// <summary>
		/// 
		/// </summary>
		public int TotalTrialCount { get; protected set; }
		
		/// <summary>
		/// 
		/// </summary>
		public int CurrentTrialIndex { get; protected set; }

		/// <summary>
		/// 
		/// </summary>
		protected override void ConfigureStates()
		{
			// IDLE
			AddState(GenusClosedLoopState.Idle,
				eventAction: (e) => e is GenusEvent.Start
					? GenusClosedLoopState.Intertrial
					: CurrentState,
				enterStateAction: () =>
				{
					CurrentTrialIndex = 0;
					_gc.Reset();
					StopProtocolDisplay();
				},
				exitStateAction: StartProtocolDisplay);

			// INTERTRIAL
			AddState(GenusClosedLoopState.Intertrial,
				eventAction: (e) =>
				{
					if (e is GenusEvent.NewBlock || e is GenusEvent.InitiateTrial)
					{
						if (CurrentTrialIndex < TotalTrialCount)
						{
							switch (e)
							{
								case GenusEvent.NewBlock:
									return Elapse(_p.Config.TrialSelfInitiate ? GenusClosedLoopState.Await : GenusClosedLoopState.Mask);
								default:
									return CurrentState;
							}
						}
						else
						{
							_p.NotifyRunComplete();
							return GenusClosedLoopState.Idle;
						}
					}

					if (e is GenusEvent.Stop)
						return GenusClosedLoopState.Idle;
					return CurrentState;
				},
				enterStateAction: () =>
				{
					_stateTimeout = _p.Config.IntertrialTimeout;
					_pd?.ClearScreenAsync(Color.FromRgb(0, 0, 0));
				});

			// AWAIT
			AddState(GenusClosedLoopState.Await,
				eventAction: (e) => e switch
				{
					GenusEvent.InitiateTrial => GenusClosedLoopState.Mask,
					GenusEvent.Stop => GenusClosedLoopState.Idle,
					_ => CurrentState
				},
				enterStateAction: () =>
				{
					_pd?.SwitchToChannelSelectAsync(Color.FromRgb(0, 0, 0),
						_input.ChannelLabels,
						_input.ChannelLabels.IndexOf(_sourceCh),
						"Select source channel and press SPACE or one of the EEG buttons to initiate next trial...",
						(CurrentTrialIndex, TotalTrialCount));
				},
				exitStateAction: () =>
				{
					//_channelSelector.

					if (_pd is not null)
						_sourceCh = _input.ChannelLabels[_pd.SelectedChannelIndex];
				});

			// MASK
			AddState(GenusClosedLoopState.Mask,
				eventAction: (e) => Elapse(GenusClosedLoopState.Prestimulus, e),
				enterStateAction: () =>
				{
					_p.NotifyTrialBegin(CurrentTrialIndex);
					_stateTimeout = _p.Config.MaskTimeout;
					_gc.EmitTrigger(_p.Config.TrialStartTrigger);
					_pd?.SwitchToFixationCrossAsync(Color.FromRgb(0, 0, 0), null);
				});
			
			// PRESTIMULUS
			AddState(GenusClosedLoopState.Prestimulus,
				eventAction: (e) => Elapse(GenusClosedLoopState.Stimulation, e),
				enterStateAction: () =>
				{
					_stateTimeout = _p.Config.PrestimulusTimeout;
					_gc.EmitTrigger(_p.Config.PrestimulusStartTrigger);
					_pd?.SwitchToFixationCrossAsync(Color.FromRgb(128, 128, 128), null);
				});

			// STIMULATION
			AddState(GenusClosedLoopState.Stimulation, 
				eventAction: ClosedLoopProc,
				enterStateAction: () =>
				{
					_stateTimeout		= _p.Config.StimulationTimeout;
					_stimUpdateTimeout	= _p.Config.UpdateTimeout;
					_updateIndex		= 0;

					_stimFreq = _p.Config.StartingFlickerFrequency;
					_gc.ChangeParameters(_stimFreq, null, _stimFreq, 10000, _p.Config.StimulationStartTrigger);

					_p.UpdateLogger?.LogTrial(
						CurrentTrialIndex + 1,
						_updateIndex,
						_sourceCh,
						0,
						_stimFreq);
				},
				exitStateAction: () =>
				{
					_gc.ChangeParameters(0, null, 0, null, _p.Config.StimulationEndTrigger);
				});

			// POSTSTIMULUS
			AddState(GenusClosedLoopState.Poststimulus,
				eventAction:		(e) => Elapse(GenusClosedLoopState.PostMask, e),
				enterStateAction:	() => _stateTimeout = _p.Config.PoststimulusTimeout,
				exitStateAction:	() => _gc.EmitTrigger(_p.Config.PoststimulusEndTrigger));

			// POST-MASK
			AddState(GenusClosedLoopState.PostMask,
				eventAction:		(e) => Elapse(GenusClosedLoopState.Intertrial, e),
				enterStateAction:	() =>
				{
					_stateTimeout = _p.Config.PostMaskTimeout;
					_pd?.SwitchToFixationCrossAsync(Color.FromRgb(0, 0, 0), null);
				},
				exitStateAction:	() =>
				{
					_p.TrialLogger?.LogTrial(CurrentTrialIndex + 1, _sourceCh);
					_gc.EmitTrigger(_p.Config.TrialEndTrigger);
					CurrentTrialIndex++;
				});
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		/// <returns></returns>
		protected GenusClosedLoopState ClosedLoopProc(GenusEvent e)
		{
			// check exit condition
			if (e is GenusEvent.Stop)
				return GenusClosedLoopState.Idle;

			if (e is GenusEvent.NewBlock)
			{
				// elapse
				--_stateTimeout;
				if (_stateTimeout == 0)
					return GenusClosedLoopState.Poststimulus;

				// update stim parameters if necessary
				--_stimUpdateTimeout;
				if (_stimUpdateTimeout == 0)
				{
					_stimUpdateTimeout = _p.Config.UpdateTimeout;

					float oldFreq	= _stimFreq;
					_stimFreq		= SpectrumMax();
					_updateIndex++;

					// emit eti line
					_p.UpdateLogger?.LogTrial(
						CurrentTrialIndex + 1,
						_updateIndex,
						_sourceCh,
						oldFreq,
						_stimFreq);

					// update trigger
					//_gc.ChangeParameters(_stimFreq, null, _stimFreq, 10000, _p.Config.StimUpdateTrigger);
					if (_p.Config.UseAudioStimulation && _p.Config.UseVisualStimulation)
						_gc.ChangeParameters(_stimFreq, _p.Config.AudioToneFrequency, _p.Config.StimUpdateTrigger);
					else
					{
						_gc.ChangeParameters(
							frequencyL:		_p.Config.UseVisualStimulation ? _stimFreq : null,
							frequencyR:		null,
							frequencyAudio: _p.Config.UseAudioStimulation ? _stimFreq : null,
							frequencyTone:	_p.Config.AudioToneFrequency,
							trigger:		_p.Config.StimUpdateTrigger);
					}

					Thread.Sleep(20);
					_gc.EmitTrigger(0);
					//_gc.EmitTrigger(_p.Config.StimUpdateTrigger);
				}
			}

			return CurrentState;
		}

		/// <summary>
		/// Transition to a new state.
		/// </summary>
		/// <param name="newState"></param>
		/// <returns></returns>
		protected override bool TransitionTo(GenusClosedLoopState newState)
		{
			if (newState != CurrentState)
				Console.WriteLine($"State transition to {newState} at {DateTime.Now}.");
			return base.TransitionTo(newState);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="onTimeoutReached"></param>
		/// <param name="withEvent"></param>
		/// <returns></returns>
		protected GenusClosedLoopState Elapse(GenusClosedLoopState onTimeoutReached, GenusEvent withEvent)
		{
			switch (withEvent)
			{
				case GenusEvent.NewBlock:
					return Elapse(onTimeoutReached);
				case GenusEvent.Stop:
					_p.NotifyRunComplete();
					return GenusClosedLoopState.Idle;
				default:
					return CurrentState;
			}
		}

		/// <summary>
		/// If timeout is reached, go to another state, otherwise decrement the timeout counter.
		/// </summary>
		/// <param name="onTimeoutReached">The state to go to on timeout zero.</param>
		/// <returns>A state.</returns>
		protected GenusClosedLoopState Elapse(GenusClosedLoopState onTimeoutReached)
		{
			--_stateTimeout;
			if (_stateTimeout <= 0)
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
					_pd = new SkiaProtocolDisplay();
					//_pd = new SkiaProtocolDisplay(Monitors.MonitorCount - 1);
					_pd.KeyPressed += (_, _) => _p.ProcessEvent(GenusEvent.InitiateTrial);
					_pd.ShowDialog();
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
			if (_pd is not null)
			{
				_pd.Dispatcher.BeginInvoke(_pd.Close);
				_pd = null;
			}
		}

		GenusCL2				_p;
		GenusController			_gc;
		SkiaProtocolDisplay		_pd;
		int						_stateTimeout;
		int						_stimUpdateTimeout;
		int						_updateIndex;
		float					_stimFreq;

		// source analyzer and rejector
		TFSpectrumAnalyzer		_analyzer;
		ArtifactDetectorSD		_detectorSD;

	}


	/// <summary>
	/// 
	/// </summary>
	public abstract class ClosedLoopAlgorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		public ClosedLoopAlgorithm(GenusCL2 protocol)
		{
			Protocol = protocol;
		}

		/// <summary>
		/// Parent protocol.
		/// </summary>
		public GenusCL2 Protocol { get; protected set; }

		/// <summary>
		/// Get the 1D power spectrum.
		/// </summary>
		/// <returns></returns>
		public virtual Spectrum1D Get1DPowerSpectrum()
		{

		}

		/// <summary>
		/// Find a peak inside the given spectrum.
		/// </summary>
		/// <param name="spectrum"></param>
		/// <returns></returns>
		public virtual float FindPeak(Spectrum1D spectrum)
		{

		}

		/// <summary>
		/// Compute the next stimulus frequency based on the input power spectrum 
		/// and the current stimulus frequency.
		/// </summary>
		/// <returns></returns>
		public abstract float ComputeNextStimulusFrequency(Spectrum1D spectrum, float currentFrequency);
	}

	/// <summary>
	/// 
	/// </summary>
	public class CL2AV1 : ClosedLoopAlgorithm
	{
		public CL2AV1(GenusCL2 protocol)
			: base(protocol)
		{
		}

		public override float ComputeNextStimulusFrequency(Spectrum1D spectrum, float currentFrequency)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public class CL2AV2 : ClosedLoopAlgorithm
	{
		public CL2AV2(GenusCL2 protocol)
			: base(protocol)
		{
		}

		public override float ComputeNextStimulusFrequency(Spectrum1D spectrum, float currentFrequency)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public class CL2AV3 : ClosedLoopAlgorithm
	{
		public CL2AV3(GenusCL2 protocol)
			: base(protocol)
		{
		}

		public override float ComputeNextStimulusFrequency(Spectrum1D spectrum, float currentFrequency)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Closed loop algorithm type.
	/// </summary>
	public enum ClosedLoopAlgorithmVersion
	{
		V1,
		V2, 
		V3
	}


	/// <summary>
	/// 
	/// </summary>
	public class GenusCL2Config
		: ProtocolConfig
	{
		/// <summary>
		/// The duration (sec) of a block. Must match the stream config file.
		/// </summary>
		[INILine(Key = "BLOCK_PERIOD")]
		public float BlockPeriod { get; set; }

		/// <summary>
		/// True if user input is required to start a trial.
		/// </summary>
		[INILine(Key = "TRIAL_SELF_INITIATE", Default = false)]
		public bool TrialSelfInitiate { get; set; }

		/// <summary>
		/// If true, a presenter will appear during protocol runtime.
		/// </summary>
		[INILine(Key = "USE_PROTOCOL_SCREEN", Default = false)]
		public bool UseProtocolScreen { get; set; }

		/// <summary>
		/// Number of trials in the protocol.
		/// </summary>
		[INILine(Key = "TRIAL_COUNT", 
			Comment = "Total number of trials.")]
		public int TrialCount { get; set; }

		/// <summary>
		/// The frequency analyzer used to determine stimulation frequency.
		/// </summary>
		[INILine(Key = "FREQUENCY_ANALYZER")]
		public string FrequencyAnalyzer { get; set; }

		/// <summary>
		/// The name of the artifact detector.
		/// </summary>
		[INILine(Key = "ARTIFACT_DETECTOR")]
		public string ArtifactDetector { get; set; }

		/// <summary>
		/// The initial stimulation frequency for each trial.
		/// </summary>
		[INILine(Key = "INITIAL_FREQUENCY")]
		public float StartingFlickerFrequency { get; set; }

		/// <summary>
		/// The limits of the stimulation frequency.
		/// </summary>
		public (float Lower, float Upper) StimulationFrequencyRange
		{
			get => (StimulationFrequencyLower, StimulationFrequencyUpper);
			set 
			{
				StimulationFrequencyLower = value.Lower;
				StimulationFrequencyUpper = value.Upper;
			}
		}

		/// <summary>
		/// The lower range of the stimulation frequency.
		/// </summary>
		[INILine(Key = "FREQUENCY_RANGE_LOWER", Default = 20f)]
		public float StimulationFrequencyLower { get; set; }

		/// <summary>
		/// The upper limit of the stimulation frequency.
		/// </summary>
		[INILine(Key = "FREQUENCY_RANGE_UPPER", Default = 60f)]
		public float StimulationFrequencyUpper { get; set; }

		/// <summary>
		/// True if audio stimulation should be used.
		/// </summary>
		[INILine(Key = "USE_AUDIO_STIMULATION", Default = true)]
		public bool UseAudioStimulation { get; set; }

		/// <summary>
		/// The frequency of the audio tone.
		/// </summary>
		[INILine(Key = "AUDIO_TONE_FREQUENCY", Default = 10000f)]
		public float AudioToneFrequency { get; set; }

		/// <summary>
		/// True if visual stimulation should be used.
		/// </summary>
		[INILine(Key = "USE_VISUAL_STIMULATION", Default = true)]
		public bool UseVisualStimulation { get; set; }

		/// <summary>
		/// If true, Log10 will be applied to the spectrum to determine result.
		/// </summary>
		[INILine(Key = "USE_LOG10", Default = true)]
		public bool UseLog10 { get; set; }

		/// <summary>
		/// Get the version of the algorithm to use.
		/// </summary>
		[INILine(Key = "CLOSED_LOOP_ALGORITHM", Default = ClosedLoopAlgorithmVersion.V1)]
		public ClosedLoopAlgorithmVersion CLAlgVersion { get; set; }

		/// <summary>
		/// The intertrial timeout in blocks.
		/// </summary>
		[INILine(Key = "INTERTRIAL_TIMEOUT")]
		public int IntertrialTimeout { get; set; }

		/// <summary>
		/// The stimulation timeout in blocks.
		/// </summary>
		[INILine(Key = "STIMULATION_TIMEOUT")]
		public int StimulationTimeout { get; set; }

		/// <summary>
		/// Time (in blocks) until the next update.
		/// </summary>
		[INILine(Key = "UPDATE_TIMEOUT")]
		public int UpdateTimeout { get; set; }

		/// <summary>
		/// Get the duration of a block in seconds.
		/// </summary>
		public float UpdateBlockDuration => UpdateTimeout * BlockPeriod;

		/// <summary>
		/// The post-mask timeout in blocks.
		/// </summary>
		[INILine(Key = "MASK_TIMEOUT")]
		public int MaskTimeout { get; set; }

		/// <summary>
		///  The prestimulus timeout in blocks.
		/// </summary>
		[INILine(Key = "PRESTIMULUS_TIMEOUT")]
		public byte PrestimulusTimeout { get; set; }

		/// <summary>
		///  The poststimulus timeout in blocks.
		/// </summary>
		[INILine(Key = "POSTSTIMULUS_TIMEOUT")]
		public byte PoststimulusTimeout { get; set; }

		/// <summary>
		/// The mask timeout in blocks.
		/// </summary>
		[INILine(Key = "POSTMASK_TIMEOUT")]
		public int PostMaskTimeout { get; set; }

		/// <summary>
		/// The trial start trigger.
		/// </summary>
		[INILine(Key = "TRIAL_START_TRIGGER")]
		public byte TrialStartTrigger { get; set; }

		/// <summary>
		/// Mask start trigger.
		/// </summary>
		[INILine(Key = "MASK_START_TRIGGER")]
		public byte PrestimulusStartTrigger { get; set; }

		/// <summary>
		/// Stimulation start trigger.
		/// </summary>
		[INILine(Key = "STIMULUS_START_TRIGGER")]
		public byte StimulationStartTrigger { get; set; }

		/// <summary>
		/// Stimulation end trigger.
		/// </summary>
		[INILine(Key = "STIMULUS_END_TRIGGER")]
		public byte StimulationEndTrigger { get; set; }

		/// <summary>
		/// Poststimulus end trigger.
		/// </summary>
		[INILine(Key = "POSTSTIMULUS_END_TRIGGER")]
		public byte PoststimulusEndTrigger { get; set; }

		/// <summary>
		/// The trial end trigger.
		/// </summary>
		[INILine(Key = "TRIAL_END_TRIGGER")]
		public byte TrialEndTrigger { get; set; }

		/// <summary>
		/// Stimulation parameter update trigger.
		/// </summary>
		[INILine(Key = "FREQUENCY_UPDATE_TRIGGER")]
		public byte StimUpdateTrigger { get; set; }
	}
}
