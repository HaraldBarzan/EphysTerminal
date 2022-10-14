using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TINS.Terminal.Settings;
using TINS.Terminal.Settings.UI;

namespace TINS.Terminal.UI
{

	/// <summary>
	/// 
	/// </summary>
	public class EEGDisplayData
		: AbstractDisplayData
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="stream"></param>
		/// <exception cref="Exception"></exception>
		public EEGDisplayData(EphysTerminal stream)
		{
			// validate settings item
			var settings = stream.Settings as EphysTerminalSettings;
			if (settings.UI is not UISettingsEEG ephysUISettings)
				throw new Exception("This engine requires the configuration to be set to \'EPHYS\'.");

			// create the MUA accumulator
			if (ephysUISettings.ShowEEG &&
				stream.ProcessingPipeline.TryGetBuffer(ephysUISettings.EEGInputBuffer, out var muaBuffer))
			{
				EEG = new ContinuousDisplayAccumulator(ephysUISettings.EEGUpdatePeriod, muaBuffer);
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
				EEG?.Dispose();
			}
			_disposed = true;
		}

		/// <summary>
		/// Trigger an accumulation round.
		/// </summary>
		/// <param name="updatePeriod">The time frame, in seconds, at the end of the buffer to accumulate.</param>
		/// <returns>True if the buffer has reached maximum capacity.</returns>
		public override bool Accumulate(float updatePeriod = float.PositiveInfinity)
			=> EEG?.Accumulate(updatePeriod) ?? false;

		public ContinuousDisplayAccumulator EEG { get; protected set; }

		private bool _disposed = false;
	}
}
