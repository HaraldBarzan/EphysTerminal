using System;
using System.Threading;
using System.Threading.Tasks;
using TINS.Data;
using TINS.Ephys.Analysis;
using TINS.Ephys.Data;
using TINS.Ephys.Processing;
using TINS.Ephys.Settings;
using TINS.Ephys.Stimulation;
using TINS.Ephys.UI;
using TINS.Utilities;

namespace TINS.Ephys
{
	/// <summary>
	/// Main class.
	/// </summary>
	public class EphysTerminal 
		: AsynchronousObject
	{
		/// <summary>
		/// Asynchronously an input buffer.
		/// </summary>
		[AsyncInvoke(nameof(OnProcessInput))]
		public readonly EventHandler<InputDataFrame> ProcessInput = default;

		/// <summary>
		/// Asynchronously start acquisition.
		/// </summary>
		[AsyncInvoke(nameof(OnStartStream))]
		public readonly Action StartStream = default;

		/// <summary>
		/// Asynchronously stop acquisition.
		/// </summary>
		[AsyncInvoke(nameof(OnStopStream))]
		public readonly Action StopStream = default;

		/// <summary>
		/// Asynchronously start the recording procedure.
		/// </summary>
		[AsyncInvoke(nameof(OnStartRecording))]
		public readonly EventHandler<DataOutputEventArgs> StartRecording = default;

		/// <summary>
		/// Asynchronously stop the recording procedure.
		/// </summary>
		[AsyncInvoke(nameof(OnStopRecording))]
		public readonly Action StopRecording = default;

		/// <summary>
		/// Asynchronously start a new protocol run.
		/// </summary>
		[AsyncInvoke(nameof(OnStartProtocol))]
		public readonly Action StartProtocol = default;

		/// <summary>
		/// Asynchronously start a new protocol run.
		/// </summary>
		[AsyncInvoke(nameof(OnStopProtocol))]
		public readonly Action StopProtocol = default;

		/// <summary>
		/// Triggered after input was received.
		/// </summary>
		public event EventHandler<InputDataFrame> InputReceived;

		/// <summary>
		/// Triggered after the processing step is complete.
		/// </summary>
		public event EventHandler<int> ProcessingComplete;

		/// <summary>
		/// Triggered after the analysis step is complete.
		/// </summary>
		public event EventHandler AnalysisComplete;

		/// <summary>
		/// Triggered if events were found during the acquisition step.
		/// </summary>
		public event EventHandler<Vector<EventMarker>> EventsFound;

		/// <summary>
		/// The state (e.g. streaming, recording ...) of the stream has changed.
		/// </summary>
		public event Action StateChanged;

		/// <summary>
		/// Create an Ephys streamer.
		/// </summary>
		/// <param name="settings">The configuration for the streamer.</param>
		/// <param name="ui">A bound user interface (optional).</param>
		/// <param name="dataInputStream">The input stream for the streamer. A <c>DummyDataStream</c> will be created if null.</param>
		/// <remarks>The <paramref name="dataInputStream"/> is typically connected to external devices and is thus platform specific. 
		/// It may well not be found in this library, instead being implemented in another platform specific library or executable.</remarks>
		public EphysTerminal(
			EphysSettings			settings, 
			DataInputStream			dataInputStream, 
			IUserInterface			ui = null)
			: base(false)
		{
			Settings = settings;

			InputStream = dataInputStream;
			if (InputStream is null)
				InputStream = new DummyDataStream(settings);
			InputStream.DataAvailable		+= ProcessInput;
			InputStream.AcquisitionStarted	+= RaiseStateChanged;
			InputStream.AcquistionEnded		+= RaiseStateChanged;

			// initialize the processing pipeline
			ProcessingPipeline = new ProcessingPipeline(settings);

			// initialize the analysis pipeline
			AnalysisPipeline = new AnalysisPipeline(settings, ProcessingPipeline);

			// initialize the event finder
			EventFinder = new LineEventFinder(
				startEvent:				0, 
				correctArtefactEvents:	settings.Input.CorrectArtefactEvents, 
				eventFilter:			null);

			// initialize UI
			if (ui is object)
			{
				UI = ui;

				// set MUA accumulator
				if (Settings.UI.ShowMUA &&
					ProcessingPipeline.TryGetBuffer(Settings.UI.MUAInputBuffer, out var muaBuffer))
				{
					// create MUA continuous accumulator
					_muaAcc = new ContinuousDisplayAccumulator(Settings.UI.MUARefreshRate, muaBuffer);

					if (AnalysisPipeline.TryGetPipe<MUASpikeDetector>(Settings.UI.MUASpikeDetector, out var detector))
					{
						// create MUA spike accumulator
						_spikeAcc = new SpikeDisplayAccumulator(Settings.UI.MUARefreshRate, detector);
					}
				}

				// set LFP accumulator
				if (Settings.UI.ShowLFP && 
					ProcessingPipeline.TryGetBuffer(Settings.UI.LFPInputBuffer, out var lfpBuffer))
				{
					// create LFP continuous accumulator
					_lfpAcc = new ContinuousDisplayAccumulator(Settings.UI.LFPRefreshRate, lfpBuffer);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			// disconnect the link before attempting to dispose
			InputReceived		= null;
			ProcessingComplete	= null;
			AnalysisComplete	= null;
			EventsFound			= null;
			StateChanged		= null;

			if (StimulationProtocol is object)
			{
				StimulationProtocol.Stop();
				StimulationProtocol = null;
			}

			if (disposing)
			{
				InputStream			?.Dispose();
				AnalysisPipeline	?.Dispose();
				ProcessingPipeline	?.Dispose();
				OutputStream		?.Dispose();
				_lfpAcc				?.Dispose();
				_muaAcc				?.Dispose();
				_spikeAcc			?.Dispose();
			}

			base.Dispose(disposing);
		}

		/// <summary>
		/// Change the threshold of a single channel.
		/// </summary>
		/// <param name="channelIndex">The index of the channel.</param>
		/// <param name="newThreshold">The new threshold value.</param>
		/// <returns>An awaitable task that completes when the threshold is changed.</returns>
		public IAsyncResult ChangeDetectorThresholdAsync(int channelIndex, float newThreshold)
		{
			if (InvokeRequired)
				return BeginInvoke(new Func<int, float, IAsyncResult>(ChangeDetectorThresholdAsync), channelIndex, newThreshold);
			else
			{
				if (UI is object && _spikeAcc is object &&
					_spikeAcc.Source is object)
				{
					_spikeAcc.Source.ChangeThreshold(channelIndex, newThreshold);
				}

				return Task.CompletedTask;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="thresholdSD"></param>
		/// <param name="callback"></param>
		/// <param name="autoApply"></param>
		/// <returns></returns>
		public IAsyncResult AutoDetectSpikeThreshold(float thresholdSD, Action<Vector<(int SourceIndex, float Threshold)>> callback = null, bool autoApply = true)
		{
			if (InvokeRequired)
				return BeginInvoke(new Func<float, Action<Vector<(int SourceIndex, float Threshold)>>, bool, IAsyncResult>(AutoDetectSpikeThreshold), callback);
			else
			{
				if (_spikeAcc is not null && _spikeAcc.Source is not null)
				{
					_spikeAcc.Source.ComputeAutoThresholdSD(out var thresholds, thresholdSD, autoApply);
					if (callback is not null)
						callback(thresholds);
				}

				return Task.CompletedTask;
			}
		}


		/// <summary>
		/// Change the list of active channels.
		/// </summary>
		/// <param name="channelIndex">The list of channels to set as active.</param>
		/// <returns>An awaitable task that completes when the list is changed.</returns>
		public IAsyncResult ChangeActiveChannelsAsync(Vector<int> channelIndex)
		{
			if (InvokeRequired)
				return BeginInvoke(new Func<Vector<int>, IAsyncResult>(ChangeActiveChannelsAsync), channelIndex);
			else
			{
				// TODO

				return Task.CompletedTask;
			}
		}

		/// <summary>
		/// Set the current stimulation protocol.
		/// </summary>
		/// <param name="protocol">The protocol.</param>
		/// <returns>An awaitable task that completes when the protocol is changed.</returns>
		public IAsyncResult SetStimulationProtocolAsync(IStimulationProtocol protocol)
		{
			if (InvokeRequired)
				return BeginInvoke(new Func<IStimulationProtocol, IAsyncResult>(SetStimulationProtocolAsync), protocol);
			else
			{
				StimulationProtocol?.Stop();
				StimulationProtocol = protocol;
				if (protocol is not null)
				{
					protocol.ProtocolStarted	+= RaiseStateChanged; 
					protocol.ProtocolEnded		+= RaiseStateChanged; 
				}

				return Task.CompletedTask;
			}
		}

		/// <summary>
		/// Set the event filter. Providing null will clear the event filter.
		/// </summary>
		/// <param name="eventFilter">List of event filters</param>
		/// <returns></returns>
		public IAsyncResult SetEventFiltersAsync(Predicate<int> eventFilter = null)
		{
			if (InvokeRequired)
				return BeginInvoke(new Func<Predicate<int>, IAsyncResult>(SetEventFiltersAsync), eventFilter);
			else
			{
				EventFinder?.SetFilter(eventFilter);
				return Task.CompletedTask;
			}
		}

		/// <summary>
		/// The data input stream for the streamer.
		/// </summary>
		public DataInputStream InputStream { get; protected set; }

		/// <summary>
		/// The processing pipeline.
		/// </summary>
		public ProcessingPipeline ProcessingPipeline { get; protected set; }

		/// <summary>
		/// The analysis pipeline.
		/// </summary>
		public AnalysisPipeline AnalysisPipeline { get; protected set; }

		/// <summary>
		/// Output stream (for saving data).
		/// </summary>
		public DataOutputStream OutputStream { get; protected set; } = null;

		/// <summary>
		/// The controller for the stimulation device.
		/// </summary>
		public IStimulationProtocol StimulationProtocol { get; protected set; } = null;

		/// <summary>
		/// The user interface (may be null).
		/// </summary>
		public IUserInterface UI { get; set; }

		/// <summary>
		/// The configuration options for the streamer.
		/// </summary>
		public EphysSettings Settings { get; protected set; }

		/// <summary>
		/// Check whether the stream is active.
		/// </summary>
		public bool IsStreaming => InputStream is object && InputStream.Status is DataStreamStatus.Running;

		/// <summary>
		/// Check whether the stream is currently saving data.
		/// </summary>
		public bool IsRecording => OutputStream is object;

		/// <summary>
		/// Check whether the stream is currently running a protocol.
		/// </summary>
		public bool IsRunningProtocol => StimulationProtocol is object && StimulationProtocol.IsRunning;

		/// <summary>
		/// The line event finder.
		/// </summary>
		public LineEventFinder EventFinder { get; protected set; }

		/// <summary>
		/// Process an input data block.
		/// </summary>
		/// <param name="sender">The object making the call.</param>
		/// <param name="e">The call parameters.</param>
		protected void OnProcessInput(object sender, InputDataFrame e)
		{
			if (!ReferenceEquals(sender, InputStream))
				return;

			// check valid message
			if (e is null || e.AnalogInput is null || e.DigitalInput is null) return;

			// signal input received
			InputReceived?.Invoke(this, e);

			// detect new events
			EventFinder.FindEvents(e.DigitalInput);
			if (EventFinder.FoundEventCount > 0)
			{
				EventsFound?.Invoke(this,		EventFinder.FoundEvents);
				OutputStream?.WriteEvents(this, EventFinder.FoundEvents);
			}

			// run processing step
			ProcessingPipeline.RunPipeline(e);
			ProcessingComplete?.Invoke(this, e.AnalogInput.Cols);

			// run analysis step
			AnalysisPipeline.RunPipeline();
			AnalysisComplete?.Invoke(this, null);

			// notify stimulation protocol
			StimulationProtocol?.ProcessBlock();

			// do rendering step 
			NotifyUserInterface();

			// do recording step (if needed)
			OutputStream?.WriteData(this, e.AnalogInput);
		}

		/// <summary>
		/// Trigger a start call to the input stream.
		/// </summary>
		protected void OnStartStream()
		{
			if (InputStream is not null && InputStream.Status == DataStreamStatus.Idle)
			{
				if (UI is not null)
				{
					_spikeAcc	?.Reset();
					_muaAcc		?.Reset();
					_lfpAcc		?.Reset();
				}

				InputStream.StartAcquisition();
			}	
		}

		/// <summary>
		/// Trigger a stop call to the input stream.
		/// </summary>
		protected void OnStopStream()
		{
			if (InputStream is object && InputStream.Status == DataStreamStatus.Running)
			{
				if (IsRecording)
				{
					OutputStream?.Dispose();
					OutputStream = null;
				}

				InputStream.StopAcquisition();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected void OnStartRecording(object sender, DataOutputEventArgs e)
		{
			OnStopRecording();
			if (e is null) 
				return;

			// get the raw buffer and start the output stream
			OutputStream = new DataOutputStream(
				e.DatasetDirectory, 
				e.DatasetName, 
				Settings.Input.SamplingRate, 
				Settings.Input.ChannelLabels);
			new Thread(() => OutputStream.Start()).Start();
			RaiseStateChanged();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected void OnStopRecording()
		{
			if (OutputStream is not null)
			{
				OutputStream?.Dispose();
				OutputStream = null;
				RaiseStateChanged();
			}
		}

		/// <summary>
		/// Start protocol message received.
		/// </summary>
		protected void OnStartProtocol()
		{
			if (StimulationProtocol is not null && !StimulationProtocol.IsRunning)
			{
				StimulationProtocol.Start();
			}
		}

		/// <summary>
		/// Stop protocol message received.
		/// </summary>
		protected void OnStopProtocol()
		{
			if (StimulationProtocol is not null && StimulationProtocol.IsRunning)
			{
				StimulationProtocol.Stop();
			}
		}

		/// <summary>
		/// Update the user interface if present.
		/// </summary>
		protected void NotifyUserInterface()
		{
			if (UI is null) return;

			// lfp, lock while writing to it
			lock (_lfpAcc)			_lfpAcc?.Accumulate();
			if (_lfpAcc.IsFull)		UI.UpdateLFP(_lfpAcc);

			// mua, lock while writing to it
			lock (_muaAcc)			_muaAcc?.Accumulate();
			lock (_spikeAcc)		_spikeAcc.Accumulate();
			if (_muaAcc.IsFull)		UI.UpdateMUA(_muaAcc, _spikeAcc);

			// update event display
			if (EventFinder.FoundEventCount > 0)
			{
				var events = new Vector<int>();
				foreach (var e in EventFinder.FoundEvents)
					events.PushBack(e.EventCode);
				UI.UpdateEvents(events);
			}
		}

		/// <summary>
		/// Raise StateChanged event.
		/// </summary>
		protected void RaiseStateChanged() => StateChanged?.Invoke();



		protected ContinuousDisplayAccumulator	_lfpAcc			= null;
		protected ContinuousDisplayAccumulator	_muaAcc			= null;
		protected SpikeDisplayAccumulator		_spikeAcc		= null;
	}


}
