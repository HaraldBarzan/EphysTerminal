using System;
using System.IO;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using TINS.Conexus.Data;
using TINS.Containers;
using TINS.Ephys.Analysis;
using TINS.Terminal.Display.Protocol;
using TINS.Terminal.Protocols.Genus.CL2;
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
		/// Constructor.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="config"></param>
		/// <param name="controller"></param>
		/// <exception cref="Exception"></exception>
		public GenusCL2(EphysTerminal stream, GenusCL2Config config, GenusController controller)
			: base(stream, config, controller)
		{
			// unset the stimulus controller if debug run
			if (!config.UseGenusController)
				StimulusController = null;

			// load the configuration
			Config			= config;
			UpdateLogger	= null;
			TrialLogger		= null;

			// check compatibility
			if (Config.BlockPeriod != SourceStream.Settings.Input.PollingPeriod)
				throw new Exception("The protocol's polling period does not match the current settings.");

			// setup for biosemi stuff
			if (Config.TrialSelfInitiate && !Config.UseProtocolScreen)
			{
				if (stream.InputStream is not BiosemiTcpStream biosemiStream)
					throw new Exception("Trial self initiation is only compatible with the Biosemi stream.");
				if (!biosemiStream.UseResponseSwitches && !Config.UseProtocolScreen)
					throw new Exception("Trial self initiation requires a Biosemi stream with UseResponseSwitches set to \'true\'.");

				biosemiStream.ResponseSwitchPressed += BiosemiStream_ResponseSwitchPressed;
			}

			// create state machine
			StateMachine = new CL2StateMachine(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			//if (SourceStream.UI is not null  && SourceStream.UI is MainWindow mw)
			//	mw.
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
			StimulusController?.Connect();
			StimulusController?.Reset();

			// start state machine
			StateMachine.ProcessEvent(GenusEvent.Start);

			// create a text writer 
			if (SourceStream.OutputStream is not null)
			{
				// obtain path from output 
				SourceStream.OutputStream.GetPath(out var dir, out var dsName);

				// create the text writer
				UpdateLogger = new TrialInfoLogger(Path.Combine(dir, dsName + "-updates.eti"),
					header: new()
					{
						"Trial",
						"UpdateIndex",
						"OldFrequency",
						"NewFrequency",
						"BlockType",
						"BlockResult",
					});
				TrialLogger = new TrialInfoLogger(Path.Combine(dir, dsName + ".eti"),
					header: new()
					{
						"Trial",
						"StartFrequency",
						"NumBlocks",
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

		/// <summary>
		/// The state machine.
		/// </summary>
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

			// determine trial start frequencies
			_startFrequencies = _p.Config.RampStartingFrequency
				? Numerics.Linspace(_p.Config.StimulationFrequencyRange, _p.Config.TrialCount)
				: new Vector<float>(_p.Config.TrialCount, _p.Config.StartingFlickerFrequency);

			// init spectrum primitives
			if (_p.SourceStream.AnalysisPipeline.TryGetComponent(_p.Config.FrequencyAnalyzer, out var a) &&
				a is TFSpectrumAnalyzer analyzer)
			{
				// set closed loop algo
				switch (_p.Config.CLAlgVersion)
				{
					case CL2AlgorithmVersion.ArgMaxFollower:		_alg = new CL2ArgMaxFollower(_p, analyzer);			break;
					case CL2AlgorithmVersion.PeakFollowerDelta:		_alg = new CL2PeakFollowerDelta(_p, analyzer);		break;
					case CL2AlgorithmVersion.DichotomicExplorator:	_alg = new CL2DichotomicExplorator(_p, analyzer);	break;
					case CL2AlgorithmVersion.Washout:				_alg = new CL2Washout(_p, analyzer);				break;
					default:
						throw new Exception();
				}
				_alg.OpenSpectrumViewer();
			}
			else
				throw new Exception("Source spectrum analyzer not found.");
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
					_gc?.Reset();
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
							return e switch
							{
								GenusEvent.NewBlock => Elapse(_p.Config.TrialSelfInitiate ? GenusClosedLoopState.Await : GenusClosedLoopState.Mask),
								_					=> CurrentState,
							};
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
					GenusEvent.InitiateTrial	=> GenusClosedLoopState.Mask,
					GenusEvent.Stop				=> GenusClosedLoopState.Idle,
					_							=> CurrentState
				},
				enterStateAction: () =>
				{
					_pd?.SwitchToTextAsync(Colors.Black, 
						"Select source channel and press SPACE or one of the EEG buttons to initiate next trial...",
						Colors.White);
					//_pd?.SwitchToChannelSelectAsync(Color.FromRgb(0, 0, 0),
					//	_input.ChannelLabels,
					//	_input.ChannelLabels.IndexOf(_sourceCh),
					//	"Select source channel and press SPACE or one of the EEG buttons to initiate next trial...",
					//	(CurrentTrialIndex, TotalTrialCount));
				},
				exitStateAction: () =>
				{
					
				});

			// MASK
			AddState(GenusClosedLoopState.Mask,
				eventAction: (e) => Elapse(GenusClosedLoopState.Prestimulus, e),
				enterStateAction: () =>
				{
					_p.NotifyTrialBegin(CurrentTrialIndex);
					_stateTimeout = _p.Config.MaskTimeout;
					_gc?.EmitTrigger(_p.Config.TrialStartTrigger);
					_pd?.SwitchToFixationCrossAsync(Color.FromRgb(0, 0, 0), null);
				});
			
			// PRESTIMULUS
			AddState(GenusClosedLoopState.Prestimulus,
				eventAction: (e) => Elapse(GenusClosedLoopState.Stimulation, e),
				enterStateAction: () =>
				{
					_stateTimeout = _p.Config.PrestimulusTimeout;
					_gc?.EmitTrigger(_p.Config.PrestimulusStartTrigger);
					_pd?.SwitchToFixationCrossAsync(Color.FromRgb(128, 128, 128), null);
				},
				exitStateAction: () =>
				{
					if (_p.Config.UsePrestimulusBaseline && _p.Config.PrestimulusTimeout > 0)
						_alg.AccumulateBaseline(_p.Config.PrestimulusTimeout);
				});

			// STIMULATION
			AddState(GenusClosedLoopState.Stimulation, 
				eventAction: ClosedLoopProc,
				enterStateAction: () =>
				{
					_stateTimeout		= _p.Config.StimulationTimeout;
					_stimUpdateTimeout	= _p.Config.UpdateTimeout;
					_updateIndex		= 0;

					// notify algorithm of trial start
					_alg.ResetBlockCounter();

					// stimulus parameters
					_stimFreq = _startFrequencies[CurrentTrialIndex];
					_gc?.ChangeParameters(_stimFreq, null, _stimFreq, 10000, _p.Config.StimulationStartTrigger);
					Console.WriteLine($"Beginning trial {CurrentTrialIndex + 1}:\n\tblock 1: set start frequency to {_stimFreq}.");

					// log beginning of trial
					_p.UpdateLogger?.LogTrial(
						CurrentTrialIndex + 1,
						_updateIndex,
						0,
						_stimFreq,
						_alg.CurrentBlockType,
						"start");
				},
				exitStateAction: () =>
				{
					Console.WriteLine("Stimulation end");
					_gc?.ChangeParameters(0, null, 0, null, _p.Config.StimulationEndTrigger);
				});

			// POSTSTIMULUS
			AddState(GenusClosedLoopState.Poststimulus,
				eventAction:		(e) => Elapse(GenusClosedLoopState.PostMask, e),
				enterStateAction:	() => _stateTimeout = _p.Config.PoststimulusTimeout,
				exitStateAction:	() => _gc?.EmitTrigger(_p.Config.PoststimulusEndTrigger));

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
					_p.TrialLogger?.LogTrial(CurrentTrialIndex + 1, _startFrequencies[CurrentTrialIndex], _p.Config.StimulationTimeout / _p.Config.UpdateTimeout);
					_gc?.EmitTrigger(_p.Config.TrialEndTrigger);
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
					_updateIndex++;

					// get next frequency
					float oldFreq	= _stimFreq;
					_stimFreq		= _alg.ComputeNextStimulusFrequency(
						currentFrequency:	_stimFreq, 
						periods:			_p.Config.UpdateTimeout, 
						blockResult:		out var blockResult);
					
					// debug
					if (oldFreq == _stimFreq)
						Console.WriteLine($"\tblock {_updateIndex + 1}: no change in frequency ({blockResult}).");
					else
						Console.WriteLine($"\tblock {_updateIndex + 1}: changed frequency from {oldFreq} to {_stimFreq} ({blockResult}).");

					// emit eti line
					_p.UpdateLogger?.LogTrial(
						CurrentTrialIndex + 1,
						_updateIndex,
						oldFreq,
						_stimFreq,
						_alg.CurrentBlockType,
						blockResult);

					// update trigger
					//_gc?.ChangeParameters(_stimFreq, null, _stimFreq, 10000, _p.Config.StimUpdateTrigger);
					if (_p.Config.UseAudioStimulation && _p.Config.UseVisualStimulation)
						_gc?.ChangeParameters(_stimFreq, _p.Config.AudioToneFrequency, _p.Config.StimUpdateTrigger);
					else
					{
						_gc?.ChangeParameters(
							frequencyL:		_p.Config.UseVisualStimulation ? _stimFreq : null,
							frequencyR:		null,
							frequencyAudio: _p.Config.UseAudioStimulation ? _stimFreq : null,
							frequencyTone:	_p.Config.AudioToneFrequency,
							trigger:		_p.Config.StimUpdateTrigger);
					}

					Thread.Sleep(20);
					_gc?.EmitTrigger(0);
					//_gc?.EmitTrigger(_p.Config.StimUpdateTrigger);
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
		CL2Algorithm			_alg;

		// starting frequencies 
		Vector<float>			_startFrequencies;
	}
}
