using System;
using TINS.Ephys.Analysis.Events;
using TINS.Terminal.Settings;
using TINS.Terminal.Settings.UI;

namespace TINS.Terminal.UI
{
	/// <summary>
	/// 
	/// </summary>
	public class EphysDisplayData
		: AbstractDisplayData
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="stream"></param>
		/// <exception cref="Exception"></exception>
		public EphysDisplayData(EphysTerminal stream)
		{
			// validate settings item
			var settings = stream.Settings as EphysTerminalSettings;
			if (settings.UI is not UISettingsEphys ephysUISettings)
				throw new Exception("This engine requires the configuration to be set to \'EPHYS\'.");

			// create the MUA accumulator
			if (ephysUISettings.ShowMUA &&
				stream.ProcessingPipeline.TryGetBuffer(ephysUISettings.MUAInputBuffer, out var muaBuffer))
			{
				MUA = new ContinuousDisplayAccumulator(ephysUISettings.MUAUpdatePeriod, muaBuffer);

				if (stream.AnalysisPipeline.TryGetComponent<MUASpikeDetector>(ephysUISettings.MUASpikeDetector, out var detector))
				{
					// create MUA spike accumulator
					Spikes = new SpikeDisplayAccumulator(ephysUISettings.MUAUpdatePeriod, detector);
				}
			}

			// set LFP accumulator
			if (ephysUISettings.ShowLFP &&
				stream.ProcessingPipeline.TryGetBuffer(ephysUISettings.LFPInputBuffer, out var lfpBuffer))
			{
				// create LFP continuous accumulator
				LFP = new ContinuousDisplayAccumulator(ephysUISettings.LFPUpdatePeriod, lfpBuffer);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				MUA		?.Dispose();
				Spikes	?.Dispose();
				LFP		?.Dispose();
			}
			_disposed = true;
		}

		/// <summary>
		/// Trigger an accumulation round.
		/// </summary>
		/// <param name="updatePeriod">The time frame, in seconds, at the end of the buffer to accumulate.</param>
		/// <returns>True if the buffer has reached maximum capacity.</returns>
		public override bool Accumulate(float updatePeriod = float.PositiveInfinity)
		{
			bool muaFull = MUA?.Accumulate(updatePeriod) ?? false;
			bool lfpFull = LFP?.Accumulate(updatePeriod) ?? false;
			Spikes?.Accumulate(updatePeriod);

			return muaFull | lfpFull;
		}



		public ContinuousDisplayAccumulator MUA { get; protected set; }
		public SpikeDisplayAccumulator Spikes { get; protected set; }
		public ContinuousDisplayAccumulator LFP { get; protected set; }

		private bool _disposed = false;
	}
}
