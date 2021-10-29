using System;
using TINS.Ephys.Processing;
using TINS.IO;

namespace TINS.Ephys.Settings
{
	/// <summary>
	/// Pipe description.
	/// </summary>
	public struct PipeDescription
	{
		/// <summary>
		/// The name of the pipe.
		/// </summary>
		[INILine(Key = "SECTION")]
		public string Section { get; set; }

		/// <summary>
		/// The type of the pipe.
		/// </summary>
		[INILine(Key = "TYPE")]
		public string TypeName { get; set; }
	}

	/// <summary>
	/// Settings for the processing pipeline.
	/// </summary>
	public class ProcessingPipelineSettings 
		: SerializableSettingsItem
	{
		/// <summary>
		/// A list of supported pipe types.
		/// </summary>
		public static Vector<string> SupportedPipeTypes { get; } = new()
		{
			"FILTERBANK", "DECIMATOR", "SELECTOR"
		};

		/// <summary>
		/// The named buffers.
		/// </summary>
		[INIVector(Key = "PROCESS_BUFFER_COUNT", ValueMask = "PROCESS_BUFFER_*_NAME")]
		public Vector<string> Buffers { get; set; } = new();

		/// <summary>
		/// The descriptions for the pipes.
		/// </summary>
		[INIStructVector(Key = "PROCESS_PIPE_COUNT", ValueMask = "PROCESS_PIPE_*_", StructType = typeof(PipeDescription))]
		public Vector<PipeDescription> PipeDescriptions { get; set; } = new();

		/// <summary>
		/// The actual pipe settings.
		/// </summary>
		public Vector<ProcessingPipeSettings> Pipes { get; } = new();

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
						case "FILTERBANK":	Pipes.PushBack(new FilterBankSettings());	break;
						case "DECIMATOR":	Pipes.PushBack(new DecimatorSettings());	break;
						case "SELECTOR":	Pipes.PushBack(new SelectorSettings());		break;

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
	/// Settings for processing pipes.
	/// </summary>
	public abstract class ProcessingPipeSettings 
		: SerializableSettingsItem
	{
		/// <summary>
		/// The name of the processing pipe.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The name of the input buffer for the pipe.
		/// </summary>
		[INILine(Key = "INPUT_BUFFER", Default = "")]
		public string InputName { get; set; }

		/// <summary>
		/// The name of the output buffer for the pipe.
		/// </summary>
		[INILine(Key = "OUTPUT_BUFFER", Default = "")]
		public string OutputName { get; set; }

		/// <summary>
		/// The number of threads the processing pipe should use.
		/// </summary>
		[INILine(Key = "THREAD_COUNT", Default = 1)]
		public int ThreadCount { get; set; }

		/// <summary>
		/// The type of the pipe.
		/// </summary>
		public abstract string TypeName { get; }

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

	


}
