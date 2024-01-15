using System;
using System.IO;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using TINS.Conexus.Data;
using TINS.Containers;
using TINS.Ephys.Analysis;
using TINS.Ephys.Analysis.Events;
using TINS.Ephys.Processing;
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
		/// Constructor.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="config"></param>
		/// <param name="controller"></param>
		/// <exception cref="Exception"></exception>
		public GenusCL2(EphysTerminal stream, GenusCL2Config config, GenusController controller)
			: base(stream, config, controller)
		{
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

			// attach feedback detector
			controller.FeedbackReceived += OnDeviceFeedback;

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
					case ClosedLoopAlgorithmVersion.V1: _alg = new CL2AV1(_p, analyzer); break;
					case ClosedLoopAlgorithmVersion.V2: _alg = new CL2AV2(_p, analyzer); break;
					case ClosedLoopAlgorithmVersion.V3: _alg = new CL2AV3(_p, analyzer); break;
					case ClosedLoopAlgorithmVersion.V4: _alg = new CL2AV4(_p, analyzer); break;
					default:
						throw new Exception();
				}
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

					// notify algorithm of trial start
					_alg.ResetBlockCounter();

					// stimulus parameters
					_stimFreq = _startFrequencies[CurrentTrialIndex];
					_gc.ChangeParameters(_stimFreq, null, _stimFreq, 10000, _p.Config.StimulationStartTrigger);

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
					_p.TrialLogger?.LogTrial(CurrentTrialIndex + 1, _p.Config.StartingFlickerFrequency, _p.Config.StimulationTimeout / _p.Config.UpdateTimeout);
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
					_stimFreq		= _alg.ComputeNextStimulusFrequency(_stimFreq, out var blockResult);
					_updateIndex++;

					// emit eti line
					_p.UpdateLogger?.LogTrial(
						CurrentTrialIndex + 1,
						_updateIndex,
						oldFreq,
						_stimFreq,
						_alg.CurrentBlockType,
						blockResult);

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
		ClosedLoopAlgorithm		_alg;

		// starting frequencies 
		Vector<float>			_startFrequencies;
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
		public ClosedLoopAlgorithm(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
		{
			Protocol = protocol;
			SpectrumAnalyzer = analyzer;
		}

		/// <summary>
		/// Parent protocol.
		/// </summary>
		public GenusCL2 Protocol { get; protected set; }

		/// <summary>
		/// Spectrum analyzer.
		/// </summary>
		public TFSpectrumAnalyzer SpectrumAnalyzer { get; protected set; }

		/// <summary>
		/// Reset the block counter.
		/// </summary>
		public void ResetBlockCounter() => _blockCounter = 0;

		/// <summary>
		/// Get the type of the current block.
		/// </summary>
		public abstract string CurrentBlockType { get; }

		/// <summary>
		/// Get the 1D power spectrum.
		/// </summary>
		/// <returns></returns>
		public virtual Spectrum1D Get1DPowerSpectrum()
		{
			if (SpectrumAnalyzer.GetOutput(out var resultRing))
			{
				// only compute up to update block duration (if possible)
				int resultCount = Math.Min(Protocol.Config.UpdateTimeout, resultRing.Size);

				// frequency spectrum
				var spectrum1d	= new Spectrum1D(resultRing[0].Results[0].Rows, resultRing[0].Results[0].FrequencyRange);
				int pushedCount = 0;
				for (int iResult = 0; iResult < resultCount; ++iResult)
				{
					foreach (var item in resultRing[iResult].Results)
					{
						for (int i = 0; i < item.Rows; ++i)
							for (int j = 0; j < item.Cols; ++j)
								spectrum1d[i] += item[i, j];
						pushedCount += item.Cols;
					}
				}

				// scale by how many columns have been pushed
				if (pushedCount > 0)
					spectrum1d.Scale(1f / pushedCount);

				return spectrum1d;
			}

			throw new Exception("Could not fetch result ring.");
		}

		/// <summary>
		/// Find a peak inside the given spectrum.
		/// </summary>
		/// <param name="spectrum"></param>
		/// <returns></returns>
		public virtual int FindPeak(Spectrum1D spectrum)
		{
			// compute necessary parameters
			var freqRange			= spectrum.FrequencyRange;
			var powerRange			= Numerics.GetRange(spectrum.Span);
			using var minima		= Numerics.FindLocalMinima(spectrum);
			using var maxima		= Numerics.FindLocalMaxima(spectrum);
			int candidate			= -1;

			// we look at maxima, searching for two flanking minima
			if (maxima.Size < 1 || minima.Size < 2)
				return candidate;

			for (int i = 0; i < minima.Size - 1; ++i)
			{
				// look for maximum within 2 minima
				int iMax = maxima.IndexOf(m => m > minima[i] && m < minima[i + 1]);
				if (iMax > -1)
				{
					// get values in plot coordinates
					float left	= spectrum.BinToFrequency(minima[i]);
					float right = spectrum.BinToFrequency(minima[i + 1]);

					// compute prominence and basis
					float prominence	= spectrum[iMax] - Math.Max(spectrum[minima[i]], spectrum[minima[i + 1]]);
					float basis			= spectrum[iMax] - prominence;

					// check prominence criterion
					if (prominence / basis < Protocol.Config.PeakMinPromToBasisRatio)
						continue;

					// restrict width if needed
					if ((left, right).Size() > Protocol.Config.PeakMaxWidth)
					{
						float peak	= spectrum.BinToFrequency(iMax);
						left		= Numerics.Clamp(peak - freqRange.Size() * (Protocol.Config.PeakMaxWidth / 2), freqRange);
						right		= Numerics.Clamp(peak - freqRange.Size() + (Protocol.Config.PeakMaxWidth / 2), freqRange);
					}

					// check aspect ratio criterion (relative to freq and power ranges)
					float aspectRatio = (prominence / powerRange.Size()) / ((left, right).Size() / freqRange.Size());
					if (aspectRatio < Protocol.Config.PeakMinAspectRatio)
						continue;

					// save the peak if it is bigger
					if (candidate < 0 || spectrum[candidate] < spectrum[iMax])
						candidate = iMax;
				}
			}

			return candidate;
		}

		/// <summary>
		/// Compute the next stimulus frequency based on the input power spectrum 
		/// and the current stimulus frequency.
		/// </summary>
		/// <returns></returns>
		public virtual float ComputeNextStimulusFrequency(float currentFrequency, out string blockResult)
		{
			blockResult = string.Empty;
			_blockCounter++;
			return currentFrequency;
		}


		protected int _blockCounter = 0;
	}

	/// <summary>
	/// Closed loop algorithm variant I:
	/// always return the frequency at max power (old version)
	/// </summary>
	public class CL2AV1 : ClosedLoopAlgorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		/// <param name="delta"></param>
		public CL2AV1(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }

		/// <summary>
		/// CL1 block type.
		/// </summary>
		public override string CurrentBlockType => "cl1";

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentFrequency"></param>
		/// <returns></returns>
		public override float ComputeNextStimulusFrequency(float currentFrequency, out string blockResult)
		{
			base.ComputeNextStimulusFrequency(currentFrequency, out blockResult);

			// get frequency at peak
			using var spec = Get1DPowerSpectrum();
			int iPeak = spec.ArgMax();
			blockResult = "cl1-update";

			return spec.BinToFrequency(iPeak);
		}
	}

	/// <summary>
	/// Closed loop algorithm variant II:
	/// use peak to explore in range +- delta around the current stimulus frequency.
	/// </summary>
	public class CL2AV2 : ClosedLoopAlgorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		/// <param name="delta"></param>
		public CL2AV2(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }

		/// <summary>
		/// 
		/// </summary>
		public override string CurrentBlockType => "cl2";

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentFrequency"></param>
		/// <returns></returns>
		public override float ComputeNextStimulusFrequency(float currentFrequency, out string blockResult)
		{
			base.ComputeNextStimulusFrequency(currentFrequency, out blockResult);

			// get frequency at peak
			using var spec	= Get1DPowerSpectrum();
			var iPeak		= FindPeak(spec);

			// do not change frequency if no peak is detected
			if (iPeak < 0)
			{
				blockResult = "cl2-nopeak";
				return currentFrequency;
			}

			// clamp in +-delta from currentfrequency and stimulation frequency range
			float freq = spec.BinToFrequency(iPeak);
			freq = Numerics.Clamp(freq, (currentFrequency - Protocol.Config.CL2Delta, currentFrequency + Protocol.Config.CL2Delta));
			freq = Numerics.Clamp(freq, Protocol.Config.StimulationFrequencyRange);

			blockResult = "cl2-update";
			return freq;
		}
	}

	/// <summary>
	/// Closed loop algorithm variant III:
	/// add two fixed exploration blocks at +- delta around current stimulus frequency, continue with best one
	/// </summary>
	public class CL2AV3 : ClosedLoopAlgorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		public CL2AV3(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }

		/// <summary>
		/// 
		/// </summary>
		public override string CurrentBlockType
		{
			get
			{
				if (!_inExplorationBlocks)
					return "cl3";
				else
				{
					if (float.IsNaN(_lowerPeakPower))
						return "cl3-lower";
					else
						return "cl3-upper";
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentFrequency"></param>
		/// <returns></returns>
		public override float ComputeNextStimulusFrequency(float currentFrequency, out string blockResult)
		{
			base.ComputeNextStimulusFrequency(currentFrequency, out blockResult);
			
			// get frequency at peak
			using var spec	= Get1DPowerSpectrum();
			var iPeak		= FindPeak(spec);

			if (!_inExplorationBlocks)
			{
				// do not change stimulus frequency if no peak was found
				// and do not create exploration blocks if we're nearing the end
				if (iPeak < 0 || 
					_blockCounter + 2 >= Protocol.Config.BlocksPerTrial)
					return currentFrequency;

				// prepare exploration blocks
				_lowerPeakPower = float.NaN;
				_upperPeakPower = float.NaN;

				// determine exploration block frequencies
				_lowerFrequency = currentFrequency - Protocol.Config.CL3Delta;
				_upperFrequency = currentFrequency + Protocol.Config.CL3Delta;

				// start lower frequency block
				_inExplorationBlocks = true;
				return _lowerFrequency;
			}
			else
			{
				if (float.IsNaN(_lowerPeakPower))
				{
					_lowerPeakPower = iPeak >= 0 ? spec[iPeak] : 0;

					// go to upper frequency block
					return _upperFrequency;
				}
				else if (float.IsNaN(_upperPeakPower))
				{
					_upperPeakPower = iPeak >= 0 ? spec[iPeak] : 0;

					// set the stimulus frequency to the bigger power response
					_inExplorationBlocks = false;
					return _upperPeakPower > _lowerPeakPower ? _upperFrequency : _lowerFrequency;
				}

				return currentFrequency; // we should never get here though
			}
		}

		protected bool	_inExplorationBlocks	= false;
		protected float	_lowerFrequency			= 0;
		protected float	_upperFrequency			= 0;
		protected float _lowerPeakPower			= float.NaN;
		protected float _upperPeakPower			= float.NaN;
	}

	/// <summary>
	/// Closed loop algorithm variant IV:
	/// set the stimulus frequency to whatever peak is the most prominent
	/// </summary>
	public class CL2AV4 : ClosedLoopAlgorithm
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocol"></param>
		/// <param name="analyzer"></param>
		public CL2AV4(GenusCL2 protocol, TFSpectrumAnalyzer analyzer)
			: base(protocol, analyzer) { }


		public override string CurrentBlockType => "cl4";

		/// <summary>
		/// 
		/// </summary>
		/// <param name="spectrum"></param>
		/// <returns></returns>
		public override int FindPeak(Spectrum1D spectrum)
		{
			// compute necessary parameters
			var freqRange			= spectrum.FrequencyRange;
			var powerRange			= Numerics.GetRange(spectrum.Span);
			using var minima		= Numerics.FindLocalMinima(spectrum);
			using var maxima		= Numerics.FindLocalMaxima(spectrum);
			int candidate			= -1;
			float candidateProm		= 0;

			// we look at maxima, searching for two flanking minima
			if (maxima.Size < 1 || minima.Size < 2)
				return candidate;

			for (int i = 0; i < minima.Size - 1; ++i)
			{
				// look for maximum within 2 minima
				int iMax = maxima.IndexOf(m => m > minima[i] && m < minima[i + 1]);
				if (iMax > -1)
				{
					// get values in plot coordinates
					float left	= spectrum.BinToFrequency(minima[i]);
					float right = spectrum.BinToFrequency(minima[i + 1]);

					// compute prominence and basis
					float prominence	= spectrum[iMax] - Math.Max(spectrum[minima[i]], spectrum[minima[i + 1]]);
					float basis			= spectrum[iMax] - prominence;

					// check prominence criterion
					if (prominence / basis < Protocol.Config.PeakMinPromToBasisRatio)
						continue;

					// restrict width if needed
					if ((left, right).Size() > Protocol.Config.PeakMaxWidth)
					{
						float peak	= spectrum.BinToFrequency(iMax);
						left		= Numerics.Clamp(peak - freqRange.Size() * (Protocol.Config.PeakMaxWidth / 2), freqRange);
						right		= Numerics.Clamp(peak - freqRange.Size() + (Protocol.Config.PeakMaxWidth / 2), freqRange);
					}

					// check aspect ratio criterion (relative to freq and power ranges)
					float aspectRatio = (prominence / powerRange.Size()) / ((left, right).Size() / freqRange.Size());
					if (aspectRatio < Protocol.Config.PeakMinAspectRatio)
						continue;

					// save the peak if its prominence is bigger
					if (candidate < 0 || candidateProm < prominence)
					{
						candidate		= iMax;
						candidateProm	= prominence;
					}
				}
			}

			return candidate;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentFrequency"></param>
		/// <returns></returns>
		public override float ComputeNextStimulusFrequency(float currentFrequency, out string blockResult)
		{
			base.ComputeNextStimulusFrequency(currentFrequency, out blockResult);

			// get frequency at peak
			using var spec = Get1DPowerSpectrum();
			var iPeak = FindPeak(spec);

			if (iPeak >= 0)
			{
				blockResult = "cl4-update";
				return spec.BinToFrequency(iPeak);
			}
			else
			{
				blockResult = "cl4-nopeak";
				return currentFrequency;
			}
		}
	}

	/// <summary>
	/// Closed loop algorithm type.
	/// </summary>
	public enum ClosedLoopAlgorithmVersion
	{
		V1,
		V2, 
		V3,
		V4
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
		[INILine(Key = "TRIAL_COUNT")]
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
		/// If set, the starting frequency will ramp linearly inside the frequency range with each trial.
		/// </summary>
		[INILine(Key = "RAMP_STARTING_FREQUENCY", Default = false)]
		public bool RampStartingFrequency { get; set; }

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
		/// Minimum prominence to basis ratio for a peak to be considered.
		/// </summary>
		[INILine(Key = "PEAK_MIN_PROM_TO_BASIS_RATIO", Default = 1f)]
		public float PeakMinPromToBasisRatio { get; set; }

		/// <summary>
		/// Maximum width of the peak in Hz.
		/// </summary>
		[INILine(Key = "PEAK_MAX_RELATIVE_WIDTH", Default = 0.2f)]
		public float PeakMaxWidth { get; set; }

		/// <summary>
		/// Minimum aspect ratio (prominence/width).
		/// </summary>
		[INILine(Key = "PEAK_MIN_ASPECT_RATIO", Default = 1f)]
		public float PeakMinAspectRatio { get; set; }

		/// <summary>
		/// Get the version of the algorithm to use.
		/// </summary>
		[INILine(Key = "CLOSED_LOOP_ALGORITHM", Default = ClosedLoopAlgorithmVersion.V1)]
		public ClosedLoopAlgorithmVersion CLAlgVersion { get; set; }

		/// <summary>
		/// Delta parameter for closed loop algorithm variant 1.
		/// </summary>
		[INILine(Key = "CL1_DELTA", Default = 5f)]
		public float CL2Delta { get; set; }

		/// <summary>
		/// Delta parameter for closed loop algorithm variant 2.
		/// </summary>
		[INILine(Key = "CL2_DELTA", Default = 5f)]
		public float CL3Delta { get; set; }

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
		/// Number of blocks per trial.
		/// </summary>
		public int BlocksPerTrial => StimulationTimeout / UpdateTimeout;

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
