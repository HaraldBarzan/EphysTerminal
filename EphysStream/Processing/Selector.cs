using System;
using TINS.Ephys.Settings;
using TINS.IO;

namespace TINS.Ephys.Processing
{
	/// <summary>
	/// Selector pipe which selects and/or rearranges channels from a processing buffer.
	/// </summary>
	public class Selector 
		: ProcessingPipe
	{
		/// <summary>
		/// Create a selector pipe.
		/// </summary>
		/// <param name="settings">The settings for the selector pipe.</param>
		/// <param name="inputBuffer">The input buffer.</param>
		/// <param name="outputBuffer">The output buffer.</param>
		public Selector(SelectorSettings settings, MultichannelBuffer inputBuffer, MultichannelBuffer outputBuffer)
			: base(inputBuffer, outputBuffer, settings.Name)
		{
			_channelMap.Assign(settings.SelectedChannels);

			// configure the output buffer
			Output.Configure(
				rows:			_channelMap.Size, 
				cols:			Input.BufferSize,
				samplingRate:	Input.SamplingRate,
				labels:			new(Input.Labels.IndexFetch(_channelMap)));
		}

		/// <summary>
		/// Run the selector.
		/// </summary>
		public override void Run()
		{
			if (Output.Rows != _channelMap.Size)
				throw new Exception("Output buffer was resized.");

			for (int i = 0; i < _channelMap.Size; ++i)
			{
				var source		= Input.GetBuffer(_channelMap[i]);
				var destination = Output.GetBuffer(i);
				source.CopyTo(destination);
			}
		}

		protected Vector<int> _channelMap = new();
	}

	/// <summary>
	/// The settings for a selector pipe.
	/// </summary>
	public class SelectorSettings
		: ProcessingPipeSettings
	{
		/// <summary>
		/// The list of selected channels.
		/// </summary>
		[INIVector(Key = "SELECTED_CHANNELS_COUNT", ValueMask = "CHANNEL_*_INDEX")]
		public Vector<int> SelectedChannels { get; set; } = new();

		/// <summary>
		/// The type name of the selector pipe.
		/// </summary>
		public override string TypeName => "SELECTOR";
	}
}
