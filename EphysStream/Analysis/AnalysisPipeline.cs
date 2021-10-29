using System;
using TINS.Ephys.Processing;
using TINS.Ephys.Settings;

namespace TINS.Ephys.Analysis
{
	/// <summary>
	/// 
	/// </summary>
	public sealed class AnalysisPipeline 
		: IDisposable
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="settings"></param>
		public AnalysisPipeline(EphysSettings settings, ProcessingPipeline processingPipeline)
		{
			foreach (var pipeSpec in settings.Analysis.Pipes)
			{
				// get the input buffer
				if (!processingPipeline.TryGetBuffer(pipeSpec.InputName, out var inputBuffer))
					throw new Exception($"Pipe could not be created. Input buffer \'{pipeSpec.InputName}\' not found.");

				// create the pipe
				if (pipeSpec is SpikeSettings detectorSpec)
					PipeSequence.PushBack(new MUASpikeDetector(detectorSpec, inputBuffer));
				if (pipeSpec is SpectrumSettings spectrumSpec)
					PipeSequence.PushBack(new SpectrumAnalyzer(spectrumSpec, inputBuffer));
			}
		}

		/// <summary>
		/// Dispose of the analysis pipeline.
		/// </summary>
		public void Dispose()
		{
			foreach (var pipe in PipeSequence) 
				pipe?.Dispose();
			PipeSequence?.Dispose();
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <param name="pipe"></param>
		/// <returns></returns>
		public bool TryGetPipe<T>(string name, out T pipe) where T : AnalysisPipe
		{
			pipe = null;

			foreach (var candidate in PipeSequence)
			{
				if (candidate is T tCandidate && candidate.Name == name)
				{
					pipe = tCandidate;
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		public void RunPipeline()
		{
			// run each pipe in order
			foreach (var pipe in PipeSequence)
				pipe.Run();
		}


		/// <summary>
		/// The collection of analyzers.
		/// </summary>
		public Vector<AnalysisPipe> PipeSequence { get; } = new();
	}

}
