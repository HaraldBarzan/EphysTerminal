using System;
using TINS.Ephys.Analysis;
using TINS.IO;

namespace TINS.Ephys.Settings
{
	/// <summary>
	/// 
	/// </summary>
	public class AnalysisPipelineSettings
		: SerializableSettingsItem
	{
		/// <summary>
		/// A list of supported pipe types.
		/// </summary>
		public static Vector<string> SupportedPipeTypes { get; } = new()
		{
			"SPIKEDETECTOR", "SPECTRUMANALYZER"
		};

		/// <summary>
		/// The descriptions for the pipes.
		/// </summary>
		[INIStructVector(Key = "ANALYSIS_PIPE_COUNT", ValueMask = "ANALYSIS_PIPE_*_", StructType = typeof(PipeDescription))]
		public Vector<PipeDescription> PipeDescriptions { get; set; } = new();

		/// <summary>
		/// The actual pipe settings.
		/// </summary>
		public Vector<AnalysisPipeSettings> Pipes { get; } = new();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ini"></param>
		/// <param name="sectionName"></param>
		/// <param name="direction"></param>
		public override void Serialize(INI ini, string sectionName, SerializationDirection direction)
		{
			base.Serialize(ini, sectionName, direction);

			if (direction == SerializationDirection.In)
			{
				// serialize in
				Pipes.Clear();
				foreach (var desc in PipeDescriptions)
				{
					switch (desc.TypeName)
					{
						case "SPIKEDETECTOR":		Pipes.PushBack(new SpikeSettings());	break;
						case "SPECTRUMANALYZER":	Pipes.PushBack(new SpectrumSettings());	break;

						default:
							throw new Exception($"Unsupported pipe type \'{desc.TypeName}\'.");
					}

					Pipes.Back.Serialize(ini, desc.Section, direction);
				}
			}
			else
			{
				// serialize out
				foreach (var pipe in Pipes)
					pipe.Serialize(ini, pipe.Name, direction);
			}
		}
	}


	/// <summary>
	/// Base class for analysis pipes.
	/// </summary>
	public abstract class AnalysisPipeSettings
		: SerializableSettingsItem
	{
		/// <summary>
		/// The name of the analysis pipe.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The number of input buffers.
		/// </summary>
		[INILine(Key = "INPUT_BUFFER", Default = "RAW")]
		public string InputName { get; set; }

		/// <summary>
		/// The number of processing threads.
		/// </summary>
		[INILine(Key = "THREAD_COUNT", Default = 1)]
		public int ThreadCount { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ini"></param>
		/// <param name="sectionName"></param>
		/// <param name="direction"></param>
		public override void Serialize(INI ini, string sectionName, SerializationDirection direction)
		{
			base.Serialize(ini, sectionName, direction);
			if (direction == SerializationDirection.In)
				Name = sectionName;
		}
	}

	/// <summary>
	/// Settings for a spike detector.
	/// </summary>
	public class SpikeSettings 
		: AnalysisPipeSettings
	{
		/// <summary>
		/// The width of a spike in milliseconds.
		/// </summary>
		[INILine(Key = "CUT_WIDTH", Default = 1.8f)]
		public float SpikeCutWidth { get; set; }

		/// <summary>
		/// The offset to the peak in milliseconds.
		/// </summary>
		[INILine(Key = "PEAK_OFFSET", Default = 0.6f)]
		public float PeakOffset { get; set; }

		/// <summary>
		/// Refractory period after each spike peak (detector will ignore successive spikes).
		/// </summary>
		[INILine(Key = "REFRACTORINESS", Default = 0.2f)]
		public float Refractoriness { get; set; }

		/// <summary>
		/// The negative threshold, in signal units (typically millivolts).
		/// </summary>
		[INILine(Key = "THRESHOLD", Default = 50f)]
		public float Threshold { get; set; }
	}
}
