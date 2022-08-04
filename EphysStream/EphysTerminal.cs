using System;
using System.Threading.Tasks;
using TINS.Ephys.Analysis;
using TINS.Ephys.Analysis.Events;
using TINS.Ephys.Data;
using TINS.Ephys.Processing;
using TINS.Ephys.Settings;
using TINS.Ephys.Stimulation;
using TINS.Ephys.UI;

namespace TINS.Ephys
{
	/// <summary>
	/// The input device.
	/// </summary>
	public enum TerminalInputDevice
	{
		Dummy	= 0,
		Local	= 1,
		ME64USB = 2
	}

	/// <summary>
	/// Main class.
	/// </summary>
	public class EphysTerminal 
		: EphysStream
	{
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
		/// Create an Ephys streamer.
		/// </summary>
		/// <param name="settings">The configuration for the streamer.</param>
		/// <param name="ui">A bound user interface (optional).</param>
		/// <param name="dataInputStream">The input stream for the streamer. A <c>DummyDataStream</c> will be created if null.</param>
		/// <remarks>The <paramref name="dataInputStream"/> is typically connected to external devices and is thus platform specific. 
		/// It may well not be found in this library, instead being implemented in another platform specific library or executable.</remarks>
		public EphysTerminal(
			EphysTerminalSettings	settings, 
			DataInputStream			dataInputStream, 
			IUserInterface			ui = null)
			: base(settings, dataInputStream)
		{
			// initialize UI
			if (ui is object)
			{
				UI = ui;

				// set MUA accumulator
				if (settings.UI.ShowMUA &&
					ProcessingPipeline.TryGetBuffer(settings.UI.MUAInputBuffer, out var muaBuffer))
				{
					// create MUA continuous accumulator
					_muaAcc = new ContinuousDisplayAccumulator(settings.UI.MUARefreshRate, muaBuffer);

					if (AnalysisPipeline.TryGetComponent<MUASpikeDetector>(settings.UI.MUASpikeDetector, out var detector))
					{
						// create MUA spike accumulator
						_spikeAcc = new SpikeDisplayAccumulator(settings.UI.MUARefreshRate, detector);
					}
				}

				// set LFP accumulator
				if (settings.UI.ShowLFP && 
					ProcessingPipeline.TryGetBuffer(settings.UI.LFPInputBuffer, out var lfpBuffer))
				{
					// create LFP continuous accumulator
					_lfpAcc = new ContinuousDisplayAccumulator(settings.UI.LFPRefreshRate, lfpBuffer);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (StimulationProtocol is object)
			{
				StimulationProtocol.Stop();
				StimulationProtocol = null;
			}

			if (disposing)
			{
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
		/// The controller for the stimulation device.
		/// </summary>
		public IStimulationProtocol StimulationProtocol { get; protected set; } = null;

		/// <summary>
		/// The user interface (may be null).
		/// </summary>
		public IUserInterface UI { get; set; }

		/// <summary>
		/// Check whether the stream is currently running a protocol.
		/// </summary>
		public bool IsRunningProtocol => StimulationProtocol is object && StimulationProtocol.IsRunning;

		/// <summary>
		/// Process an input data block.
		/// </summary>
		/// <param name="sender">The object making the call.</param>
		/// <param name="e">The call parameters.</param>
		protected override void OnProcessInput(object sender, InputDataFrame e)
		{
			if (!ReferenceEquals(sender, InputStream))
				return;

			base.OnProcessInput(sender, e);
			var x = new Vector<float>();
			x.PushBack(e.AnalogInput.Sum());
			foreach (var b in ProcessingPipeline.Buffers)
				x.PushBack(b.Sum());

			// notify stimulation protocol
			StimulationProtocol?.ProcessBlock();

			// do rendering step 
			NotifyUserInterface();
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


		protected ContinuousDisplayAccumulator	_lfpAcc			= null;
		protected ContinuousDisplayAccumulator	_muaAcc			= null;
		protected SpikeDisplayAccumulator		_spikeAcc		= null;
	}


}
