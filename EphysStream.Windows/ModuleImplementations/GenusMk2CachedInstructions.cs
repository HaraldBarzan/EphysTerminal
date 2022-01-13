using System;
using System.Collections.Generic;

namespace TINS.Ephys.ModuleImplementations
{
	using Instruction = TINS.Ephys.Stimulation.GenusMk2Controller.Instruction;

	/// <summary>
	/// Contains cached instruction lists which can be requested by name.
	/// </summary>
	public static class GenusMk2CachedInstructions
	{
		/// <summary>
		/// The list of cached instructions.
		/// </summary>
		static Dictionary<string, Vector<Instruction>> _instructions = new()
		{
			{ "ramp-v-6000ms-10:10:60Hz-notriggers",	AVRamp((10, 60), 6, 6000, audio:	false) },
			{ "ramp-a-6000ms-10:10:60Hz-notriggers",	AVRamp((10, 60), 6, 6000, visual:	false) },
			{ "ramp-av-6000ms-10:10:60Hz-notriggers",	AVRamp((10, 60), 6, 6000) }
		};

		/// <summary>
		/// Get a cached instruction.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static Vector<Instruction> Get(string name)
		{
			if (_instructions.TryGetValue(name, out var value))
				return value;
			throw new Exception($"Instruction set \'{name}\' not cached.");
		}

		/// <summary>
		/// Create a ramp of audio and/or visual flickering.
		/// </summary>
		/// <param name="frequencyRange">The range of frequencies.</param>
		/// <param name="stepCount">The number of steps between these frequencies.</param>
		/// <param name="totalTime">The total duration of the ramp in milliseconds.</param>
		/// <param name="visual">Include visual flickering.</param>
		/// <param name="audio">Include audio flickering.</param>
		/// <returns>A list of instructions to perform the ramp.</returns>
		public static Vector<Instruction> AVRamp(
			(float Lower, float Upper)	frequencyRange,
			int							stepCount,
			int							totalTime,
			bool						audio				= true,
			bool						visual				= true,
			float?						audioFrequency		= 5000,
			byte?						startTrigger		= 129,
			byte?						endTrigger			= 150,
			byte?						transitionTrigger	= null,
			(byte Rise, byte Fall)?		flickerTriggers		= null
			)
		{
			// init
			var result				= new Vector<Instruction>();
			using var frequencies	= Numerics.Linspace(frequencyRange, stepCount);
			int stepDurationMs		= totalTime / stepCount;

			// preparation
			if (audioFrequency.HasValue)	result.PushBack(Instruction.ChangeAudioTone(audioFrequency.Value));
			if (startTrigger.HasValue)		result.PushBack(Instruction.EmitTrigger(startTrigger.Value));
			if (flickerTriggers.HasValue)	result.PushBack(Instruction.EnableFlickerTriggers(flickerTriggers.Value.Rise, flickerTriggers.Value.Fall));

			// loop through the list
			for (int iFreq = 0; iFreq < frequencies.Size; ++iFreq)
			{
				// signal transition if needed
				if (transitionTrigger.HasValue) result.PushBack(Instruction.EmitTrigger(transitionTrigger.Value));
				
				// enable flickering
				if (audio)						result.PushBack(Instruction.StartAudioFlicker(frequencies[iFreq]));
				if (visual)						result.PushBack(Instruction.StartLedFlicker(frequencies[iFreq]));

				// sleep until next transition
				result.PushBack(Instruction.Sleep(stepDurationMs));
			}

			// finalize
			result.PushBack(Instruction.StopFlicker());
			if (endTrigger.HasValue) result.PushBack(Instruction.EmitTrigger(endTrigger.Value));

			return result;
		}




	}
}
