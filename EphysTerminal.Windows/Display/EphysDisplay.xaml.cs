using System;
using System.Windows;
using System.Windows.Controls;
using TINS.Ephys;
using TINS.Ephys.Analysis.Events;
using TINS.Terminal.Display.Ephys;
using TINS.Terminal.Settings;
using TINS.Terminal.UI;

namespace TINS.Terminal.Display
{
	/// <summary>
	/// Interaction logic for ElectrophysiologyDisplay.xaml
	/// </summary>
	public partial class EphysDisplay 
		: UserControl
		, IChannelDisplay
    {
		/// <summary>
		/// The display type.
		/// </summary>
		public DisplayType DisplayType => DisplayType.Electrophysiology;

		/// <summary>
		/// Default constructor.
		/// </summary>
        public EphysDisplay()
        {
            InitializeComponent();
			drawMua.ThresholdChanged	+= OnMUAThresholdChange;
			drawMua.LiveChannelChanged	+= OnMUALiveChannelChange;

			// Y range combo boxes
			foreach (var range in SupportedYRanges)
			{
				cmbMuaYRange.Items.Add(range);
				cmbLfpYRange.Items.Add(range);
			}
		}

		/// <summary>
		/// Update user interface activity regarding multiunit activity (spikes).
		/// </summary>
		/// <param name="muaAccumulator">Multiunit activity.</param>
		/// <param name="spikeAccumulator">Spiking activity.</param>
		public void UpdateMUA(ContinuousDisplayAccumulator muaAccumulator, SpikeDisplayAccumulator spikeAccumulator = null)
			=> drawMua.Update(muaAccumulator, spikeAccumulator);


		/// <summary>
		/// Update user interface activity regarding local field potentials.
		/// </summary>
		/// <param name="lfpAccumulator">Local field potentials.</param>
		public void UpdateLFP(ContinuousDisplayAccumulator lfpAccumulator)
			=> drawLfp.Update(lfpAccumulator);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="channelName"></param>
		public void SetLiveChannel(string channelName)
			=> drawMua.LiveChannel = channelName;

		/// <summary>
		/// Update the MUA and LFP channel display matrices.
		/// </summary>
		public void InitializeChannelDisplay(EphysTerminal terminal)
		{
			// set the new terminal
			EphysTerminal	= terminal;
			var settings	= EphysTerminal.Settings as EphysTerminalSettings;

			// create channel mapping matrix
			var mapping = new Matrix<DataDisplay.Mapping>(settings.UI.DisplayGridRows, settings.UI.DisplayGridColumns);
			var channels = settings.UI.DisplayChannels;
			for (int i = 0; i < channels.Size; ++i)
			{
				int sourceIndex;
				if (!string.IsNullOrEmpty(channels[i]) &&   // valid label
					(sourceIndex = settings.Input.ChannelLabels.IndexOf(channels[i])) >= 0)     // label found in inputs
				{
					mapping[i] = new() { Label = channels[i], SourceIndex = sourceIndex };
				}
			}

			// setup MUA axes
			cmbMuaYRange.Text = settings.UI.MUAYRange.ToString();
			drawMua.Setup(
				channelMapping: mapping,
				xRange: (0, settings.Input.PollingPeriod * settings.UI.MUARefreshRate * 1000 /*conv to ms*/),
				yRange: (-settings.UI.MUAYRange, settings.UI.MUAYRange));

			// set thresholds and waveform size
			foreach (var set in settings.Analysis.Components)
			{
				if (set.Name == settings.UI.MUASpikeDetector &&
					set is SpikeSettings spikeSet)
				{
					drawMua.WaveformXRange = (-spikeSet.PeakOffset, MathF.Round(spikeSet.SpikeCutWidth - spikeSet.PeakOffset, 2));
					using var thr = new Matrix<float>(mapping.Dimensions);
					thr.Fill(spikeSet.Threshold);
					drawMua.SetThresholds(thr);
				}
			}

			// setup LFP axes
			cmbLfpYRange.Text = settings.UI.LFPYRange.ToString();
			drawLfp.Setup(
				channelMapping: mapping,
				xRange: (0, settings.Input.PollingPeriod * settings.UI.LFPRefreshRate * 1000 /*conv to ms*/),
				yRange: (-settings.UI.LFPYRange, settings.UI.LFPYRange));

			// create the Audio stream
			AudioStream ??= new MultiunitAudioStream(EphysTerminal);
			SetLiveChannel((EphysTerminal.Settings as EphysTerminalSettings).UI.DefaultAudioChannel);
		}

		/// <summary>
		/// 
		/// </summary>
		public void ClearDisplay()
		{
			drawMua.Clear();
			drawLfp.Clear();
		}

		/// <summary>
		/// The ephys stream.
		/// </summary>
		public EphysTerminal EphysTerminal { get; protected set; }

		/// <summary>
		/// Audio stream.
		/// </summary>
		public MultiunitAudioStream AudioStream { get; protected set; }


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnMUAThresholdChange(object sender, ThresholdChangedEventArgs e)
		{
			if (sender == drawMua && EphysTerminal is object)
			{
				EphysTerminal.ChangeDetectorThresholdAsync(e.Mapping.SourceIndex, e.NewValue);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnMUALiveChannelChange(object sender, LiveChannelChangedEventArgs e)
		{
			if (sender == drawMua && EphysTerminal is object && AudioStream is object)
			{
				if (e.NewChannelLabel is null)
					AudioStream.Stop();
				else
					AudioStream.ChangeSourceChannel(e.NewChannelLabel);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnAutoThreshold_Click(object sender, RoutedEventArgs e)
		{
			if (double.TryParse(txbThreshold.Text, out var thSDs))
			{
				EphysTerminal?.AutoDetectSpikeThreshold(
					thresholdSD: (float)thSDs,
					autoApply: true,
					callback: (thresholds) => Dispatcher.BeginInvoke(() => drawMua?.SetThresholds(thresholds)));
			}
		}

		

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void cmbYRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ReferenceEquals(sender, cmbMuaYRange))
			{
				var range = (float)cmbMuaYRange.SelectedItem;
				drawMua.SetYAxisRange((-range, range));
				drawMua.UpdateDisplay();
			}

			if (ReferenceEquals(sender, cmbLfpYRange))
			{
				var range = (float)cmbLfpYRange.SelectedItem;
				drawLfp.SetYAxisRange((-range, range));
				drawLfp.UpdateDisplay();
			}
		}

		/// <summary>
		/// A list of supported y ranges.
		/// </summary>
		protected static Vector<float> SupportedYRanges = new()
		{
			0.1f,   0.2f,   0.5f,
			1,      2,      5,
			10,     20,     50,
			100,    200,    500,
			1000,   2000,   5000
		};
	}
}
