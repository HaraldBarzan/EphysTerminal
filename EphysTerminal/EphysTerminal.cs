using System;
using System.Threading.Tasks;
using TINS.Ephys;
using TINS.Ephys.Analysis;
using TINS.Ephys.Analysis.Events;
using TINS.Ephys.Data;
using TINS.Ephys.Processing;
using TINS.Terminal.Settings;
using TINS.Terminal.Stimulation;
using TINS.Terminal.UI;

namespace TINS.Terminal
{
	/// <summary>
	/// The input device.
	/// </summary>
	public enum TerminalInputDevice
	{
		Dummy	= 0,
		Local	= 1,
		ME64USB = 2,
		NIDAQmx	= 3
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
			if (ui is not null)
			{
				UI = ui;
				DisplayData = settings.UIType switch
				{
					"EPHYS" => new EphysDisplayData(this),
					"EEG"	=> new EEGDisplayData(this),
					_		=> null
				};
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
				if (UI is not null &&
					DisplayData is EphysDisplayData ephysData &&
					ephysData.Spikes is not null &&
					ephysData.Spikes.Source is not null)
				{
					ephysData.Spikes.Source.ChangeThreshold(channelIndex, newThreshold);
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
				if (DisplayData is EphysDisplayData ephysData &&
					ephysData.Spikes is not null &&
					ephysData.Spikes.Source is not null)
				{
					ephysData.Spikes.Source.ComputeAutoThresholdSD(out var thresholds, thresholdSD, autoApply);
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
				return BeginInvoke(SetStimulationProtocolAsync, protocol);
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
		/// Get or set the display data.
		/// </summary>
		public AbstractDisplayData DisplayData { get; set; }

		/// <summary>
		/// Settings item.
		/// </summary>
		public EphysTerminalSettings TerminalSettings => Settings as EphysTerminalSettings;

		/// <summary>
		/// Check whether the stream is currently running a protocol.
		/// </summary>
		public bool IsRunningProtocol => StimulationProtocol is object && StimulationProtocol.IsRunning;

		/// <summary>
		/// Process an input data block.
		/// </summary>
		/// <param name="sender">The object making the call.</param>
		/// <param name="e">The call parameters.</param>
		protected override void OnProcessInput(object sender, ReadingFrame e)
		{
			if (!ReferenceEquals(sender, InputStream))
				return;

			base.OnProcessInput(sender, e);

			// notify stimulation protocol
			StimulationProtocol?.ProcessBlock();

			// check if UI can be updated
			if (UI is not null)
			{
				// update event display
				if (EventFinder.FoundEventCount > 0)
				{
					var events = new Vector<int>();
					foreach (var evt in EventFinder.FoundEvents)
						events.PushBack(evt.EventCode);
					UI.UpdateEvents(events);
				}

				// update data
				if (DisplayData is not null &&
					DisplayData.Accumulate(e.FrameDuration))
				{
					UI.UpdateData(DisplayData);
				}
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
	}
}
