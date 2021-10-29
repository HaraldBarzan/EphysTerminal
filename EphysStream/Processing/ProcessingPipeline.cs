using System;
using TINS.Ephys.Data;
using TINS.Ephys.Settings;

namespace TINS.Ephys.Processing
{
	/// <summary>
	/// A processing pipeline for the electrophysiology recorder.
	/// </summary>
	public class ProcessingPipeline
		: IDisposable
	{
		/// <summary>
		/// Create a processing pipeline.
		/// </summary>
		/// <param name="settings">The settings item.</param>
		public ProcessingPipeline(EphysSettings settings)
		{
			// initialize buffers
			foreach (var name in settings.Processing.Buffers)
				Buffers.PushBack(new MultichannelBuffer(name));

			// set sampling rate of raw buffer
			if (TryGetBuffer(settings.Input.TargetBuffer, out var rawInput))
			{
				// set size and sampling rate
				InputBuffer = rawInput;
				InputBuffer.Configure(settings.ChannelCount, settings.SamplesPerBlock, settings.SamplingRate, settings.Input.ChannelLabels);
			}
			else
				throw new Exception("Raw input buffer not found.");

			// initialize pipes
			foreach (var pipeSpec in settings.Processing.Pipes)
			{
				// get the input and output buffers
				if (!TryGetBuffer(pipeSpec.InputName, out var inputBuffer))
					throw new Exception($"Pipe could not be created. Input buffer \'{pipeSpec.InputName}\' not found.");
				if (!TryGetBuffer(pipeSpec.OutputName, out var outputBuffer))
					throw new Exception($"Pipe could not be created. Output buffer \'{pipeSpec.OutputName}\' not found.");

				// create the pipe
				if (pipeSpec is FilterBankSettings filterSpec)
					PipeSequence.PushBack(new FilterBank(filterSpec, inputBuffer, outputBuffer));
				if (pipeSpec is DecimatorSettings decimatorSpec)
					PipeSequence.PushBack(new Decimator(decimatorSpec, inputBuffer, outputBuffer));
				if (pipeSpec is SelectorSettings selectorSpec)
					PipeSequence.PushBack(new Selector(selectorSpec, inputBuffer, outputBuffer));
			}
		}

		/// <summary>
		/// Clear the processing pipeline.
		/// </summary>
		public void Dispose()
		{
			foreach (var buffer in Buffers)		buffer?.Dispose();
			foreach (var pipe in PipeSequence)	pipe?.Dispose();
			
			Buffers		.Clear();
			PipeSequence.Clear();

			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void RunPipeline(InputDataFrame inputData)
		{
			// push data to input buffer
			if (inputData.AnalogInput.Dimensions != InputBuffer.Dimensions)
				throw new Exception("Input data does not fit into the pipeline's input buffer.");

			lock (inputData)
			lock (InputBuffer)
				InputBuffer.Assign(inputData.AnalogInput);

			// run each pipe in order
			foreach (var pipe in PipeSequence)
			{
				lock (pipe.Input)
					lock (pipe.Output)
						pipe.Run();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public ProcessingPipe GetPipe(string name)
		{
			if (TryGetPipe(name, out var pipe))
				return pipe;
			else
				throw new Exception($"Pipe with name \'{name}\' not found.");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public bool TryGetPipe(string name, out ProcessingPipe pipe)
		{
			pipe = null;

			foreach (var candidate in PipeSequence)
			{
				if (candidate.Name == name)
				{
					pipe = candidate;
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <param name="pipe"></param>
		/// <returns></returns>
		public bool TryGetPipe<T>(string name, out T pipe) where T : ProcessingPipe
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
		/// <param name="name"></param>
		/// <returns></returns>
		public MultichannelBuffer GetBuffer(string name)
		{
			if (TryGetBuffer(name, out var buffer))
				return buffer;
			else
				throw new Exception($"Buffer with name \'{name}\' not found.");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public bool TryGetBuffer(string name, out MultichannelBuffer buffer)
		{
			buffer = null;

			foreach (var candidate in Buffers)
			{
				if (candidate.Name == name)
				{
					buffer = candidate;
					return true;
				}
			}

			return false;
		}

		

		/// <summary>
		/// The collection of pipes.
		/// </summary>
		public Vector<ProcessingPipe> PipeSequence { get; } = new();
		
		/// <summary>
		/// The collection of named buffers.
		/// </summary>
		public Vector<MultichannelBuffer> Buffers { get; } = new();

		/// <summary>
		/// The buffer that receives input into the pipeline.
		/// </summary>
		public MultichannelBuffer InputBuffer { get; protected set; }
	}
}
