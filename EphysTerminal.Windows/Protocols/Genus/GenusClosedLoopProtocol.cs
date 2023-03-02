using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using TINS.Analysis;
using TINS.Conexus.Data;
using TINS.Ephys.Processing;
using TINS.Terminal.Display.Protocol;
using TINS.Terminal.Stimulation;
using TINS.Utilities;

namespace TINS.Terminal.Protocols.Genus
{
	/// <summary>
	/// 
	/// </summary>
	public enum GenusClosedLoopState
	{
		Idle,
		Await,
		Mask,
		Prestimulus,
		Stimulation,
		Poststimulus,
		PostMask,
		Intertrial
	}


	/// <summary>
	/// 
	/// </summary>
	public class GenusClosedLoopProtocol
		: StimulationProtocol<GenusClosedLoopConfig, GenusController>
	{
		/// <summary>
		/// Create a new Genus protocol.
		/// </summary>
		/// <param name="stream">The parent ephys stream.</param>
		/// <param name="config">The configuration for this protocol.</param>
		/// <param name="stimulusController">The stimulus controller for the protocol.</param>
		public GenusClosedLoopProtocol(EphysTerminal stream, GenusClosedLoopConfig config, GenusController stimulusController)
			: base(stream, config, stimulusController)
		{
			// load the configuration
			Config		= config;
			TrialLogger = null;

			if (Config.TrialSelfInitiate && !Config.UseProtocolScreen)
			{
				if (stream.InputStream is not BiosemiTcpStream biosemiStream)
					throw new Exception("Trial self initiation is only compatible with the Biosemi stream.");
				if (!biosemiStream.UseResponseSwitches && !Config.UseProtocolScreen)
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
						"UpdateIndex",
						"SourceChannel",
						"OldFrequency",
						"NewFrequency"
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
		public override bool IsRunning => _stateMachine.CurrentState != GenusClosedLoopState.Idle;

		/// <summary>
		/// Text output for this protocol. Is active only when recording.
		/// </summary>
		public TrialInfoLogger TrialLogger { get; protected set; }

		/// <summary>
		/// Number of trials.
		/// </summary>
		public int TrialCount => _stateMachine.TotalTrialCount;

		/// <summary>
		/// 
		/// </summary>
		protected StateMachine _stateMachine;

		/// <summary>
		/// 
		/// </summary>
		protected class StateMachine
			: StateMachine<GenusClosedLoopState, GenusEvent>
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
			public StateMachine(GenusClosedLoopProtocol protocol)
			{
				_p					= protocol;
				_gc					= protocol.StimulusController;
				_stateTimeout		= 0;
				_stimUpdateTimeout	= 0;
				TotalTrialCount		= _p.Config.TrialCount;
				CurrentTrialIndex	= 0;

				// init spectrum primitives
				_input		= _p.SourceStream.ProcessingPipeline.GetBuffer(_p.Config.InputBuffer);
				_sourceCh	= _input.ChannelLabels.Front;
				var nfft	= Math.Min(Numerics.Round(_p.Config.SupportedSamplingPeriod *  _p.Config.UpdateTimeout *  _input.SamplingRate), _input.BufferSize);
				_ft			.Initialize(nfft);
				_spec		.Initialize(nfft, (0, _input.SamplingRate / 2));

			}

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
					//eventAction: (e) =>
					//{
					//	if (CurrentTrialIndex < TotalTrialCount)
					//	{
					//		switch (e)
					//		{
					//			case GenusEvent.NewBlock:
					//				return Elapse(_p.Config.TrialSelfInitiate ? GenusClosedLoopState.Await : GenusClosedLoopState.Mask);
					//			case GenusEvent.Stop:
					//				RunCompleted?.Invoke();
					//				return GenusClosedLoopState.Idle;
					//			default:
					//				return CurrentState;
					//		}
					//	}
					//	else
					//	{
					//		RunCompleted?.Invoke();
					//		return GenusClosedLoopState.Idle;
					//	}
					//},
					eventAction:		(e) => Elapse(
						CurrentTrialIndex < TotalTrialCount // check stopping condition
							? (_p.Config.TrialSelfInitiate ? GenusClosedLoopState.Await : GenusClosedLoopState.Mask) 
							: GenusClosedLoopState.Idle, e),
					enterStateAction:	() =>
					{
						_stateTimeout = _p.Config.IntertrialTimeout;
						_pd?.ClearScreenAsync(Color.FromRgb(0, 0, 0));
					});


				// AWAIT
				AddState(GenusClosedLoopState.Await,
					eventAction: (e) => e switch 
					{ 
						GenusEvent.InitiateTrial	=> GenusClosedLoopState.Mask,
						GenusEvent.Stop				=> GenusClosedLoopState.Idle,
						_							=> CurrentState
					},
					enterStateAction: () => _pd?.SwitchToChannelSelectAsync(Color.FromRgb(0, 0, 0), 
					_input.ChannelLabels,
					_input.ChannelLabels.IndexOf(_sourceCh),
					"Select source channel and press SPACE or one of the EEG buttons to initiate next trial...", 
					(CurrentTrialIndex, TotalTrialCount)),
					exitStateAction: () =>
					{
						if (_pd is not null)
							_sourceCh = _input.ChannelLabels[_pd.SelectedChannelIndex];
					});

				// MASK
				AddState(GenusClosedLoopState.Mask,
					eventAction: (e) => Elapse(GenusClosedLoopState.Prestimulus, e),
					enterStateAction: () =>
					{
						TrialBegin?.Invoke(CurrentTrialIndex);
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

						_p.TrialLogger?.LogTrial(
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
						_gc.EmitTrigger(_p.Config.TrialEndTrigger);
						CurrentTrialIndex++;
					});
			}



			/// <summary>
			/// Total number of trials.
			/// </summary>
			public int TotalTrialCount { get; protected set; }

			/// <summary>
			/// Index of current trial.
			/// </summary>
			public int CurrentTrialIndex { get; protected set; }

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
					if (_stimUpdateTimeout == 0)
					{
						_stimUpdateTimeout = _p.Config.UpdateTimeout;

						float oldFreq	= _stimFreq;
						_stimFreq		= SpectrumMax();
						_updateIndex++;
						
						// emit eti line
						_p.TrialLogger?.LogTrial(
							CurrentTrialIndex + 1,
							_updateIndex,
							_sourceCh,
							oldFreq,
							_stimFreq);

						// update trigger
						_gc.ChangeParameters(_stimFreq, null, _stimFreq, 10000, _p.Config.StimUpdateTrigger);
						//_gc.EmitTrigger(_p.Config.StimUpdateTrigger);
					}
					else
						--_stimUpdateTimeout;
				}

				return CurrentState;
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
						RunCompleted?.Invoke();
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
				if (_stateTimeout == 0)
					return onTimeoutReached;
				--_stateTimeout;
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

			/// <summary>
			/// 
			/// </summary>
			protected float SpectrumMax()
			{
				// find buffer
				var buf = _input.GetChannelBuffer(_sourceCh);

				// perform FFT
				_ft.Analyze(buf);
				_ft.GetOneSidedPowerSpectrum(_spec);

				// use log10 if necessary
				if (_p.Config.UseLog10)
				{
					for (int i = 0; i < _spec.Size; ++i)
						_spec[i] = MathF.Log10(_spec[i] + 1e-5f);
				}

				// find maximum
				int iMax	= _spec.FrequencyToBin(_p.Config.FrequencyRange.Front);
				int end		= _spec.FrequencyToBin(_p.Config.FrequencyRange.Back) + 1;

				for (int i = iMax + 1; i < end; ++i)
				{
					if (_spec[i] > _spec[iMax])
						iMax = i;
				}

				return Numerics.Clamp(MathF.Round(_spec.BinToFrequency(iMax)), (_p.Config.FrequencyRange.Front, _p.Config.FrequencyRange.Back));
			}


			GenusClosedLoopProtocol _p;
			GenusController			_gc;
			SkiaProtocolDisplay		_pd;
			int						_stateTimeout;
			int						_stimUpdateTimeout;
			int						_updateIndex;
			float					_stimFreq;

			// fourier spectrum
			FourierTransform		_ft		= new();
			Spectrum1D				_spec	= new();
			MultichannelBuffer		_input;
			string					_sourceCh;
		}
	}



	/// <summary>
	/// 
	/// </summary>
	public class GenusClosedLoopConfig
		: ProtocolConfig
	{
		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("supportedSamplingPeriod")]
		public float SupportedSamplingPeriod { get; set; }

		/// <summary>
		/// User input is required to start a trial.
		/// </summary>
		[JsonPropertyName("trialSelfInitiate")]
		public bool TrialSelfInitiate { get; set; }

		/// <summary>
		/// Use the protocol screen with fixation cross.
		/// </summary>
		[JsonPropertyName("useProtocolScreen")]
		public bool UseProtocolScreen { get; set; }

		/// <summary>
		/// Number of trials in the protocol.
		/// </summary>
		[JsonPropertyName("trialCount")]
		public int TrialCount { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("inputBuffer")]
		public string InputBuffer { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("startingFlickerFrequency")]
		public float StartingFlickerFrequency { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("frequencyRange")]
		public Vector<float> FrequencyRange { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("useLog10")]
		public bool UseLog10 { get; set; }

		/// <summary>
		/// The intertrial timeout in blocks.
		/// </summary>
		[JsonPropertyName("intertrialTimeout")]
		public int IntertrialTimeout { get; set; }

		/// <summary>
		/// The stimulation timeout in blocks.
		/// </summary>
		[JsonPropertyName("stimulationTimeout")]
		public int StimulationTimeout { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("updateTimeout")]
		public int UpdateTimeout { get; set; }

		/// <summary>
		/// The post-mask timeout in blocks.
		/// </summary>
		[JsonPropertyName("maskTimeout")]
		public int MaskTimeout { get; set; }

		/// <summary>
		///  The prestimulus timeout in blocks.
		/// </summary>
		[JsonPropertyName("prestimulusTimeout")]
		public byte PrestimulusTimeout { get; set; }

		/// <summary>
		///  The poststimulus timeout in blocks.
		/// </summary>
		[JsonPropertyName("poststimulusTimeout")]
		public byte PoststimulusTimeout { get; set; }

		/// <summary>
		/// The mask timeout in blocks.
		/// </summary>
		[JsonPropertyName("postMaskTimeout")]
		public int PostMaskTimeout { get; set; }

		/// <summary>
		/// The trial start trigger.
		/// </summary>
		[JsonPropertyName("trialStartTrigger")]
		public byte TrialStartTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("prestimulusStartTrigger")]
		public byte PrestimulusStartTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("stimulationStartTrigger")]
		public byte StimulationStartTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("stimulationEndTrigger")]
		public byte StimulationEndTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("poststimulusEndTrigger")]
		public byte PoststimulusEndTrigger { get; set; }

		/// <summary>
		/// The trial end trigger.
		/// </summary>
		[JsonPropertyName("trialEndTrigger")]
		public byte TrialEndTrigger { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[JsonPropertyName("updateTrigger")]
		public byte StimUpdateTrigger { get; set; }
	}
}
