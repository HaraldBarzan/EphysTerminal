using TINS.Ephys.Stimulation;

namespace TINS.Ephys.ModuleImplementations
{
	using Instruction = GenusMk2Controller.Instruction;

	/// <summary>
	/// Contains cached instruction lists which can be requested by name.
	/// </summary>
	public class GenusMk2CachedTrial
	{
		// data
		public string Name { get; init; }
		public string Type { get; init; }
		public Vector<Instruction> Instructions { get; init; }
		public bool	Audio { get; init; }
		public bool Visual { get; init; }
		public int StimulationRuntime { get; init; }
		public int StepCount { get; init; }
		public (float Lower, float Upper) FlickerFrequency { get; init; }
		public float ToneFrequency { get; init; }
		public bool UseFlickerTriggers { get; init; }
		public bool UseTransitionTriggers { get; init; }

		/// <summary>
		/// Get an instruction list by name.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns>An instruction list if successful, null otherwise.</returns>
		public static GenusMk2CachedTrial Get(string name)
		{
			int i = _instructions.IndexOf(x => x.Name == name);
			if (i >= 0)
				return _instructions[i];
			return null;
		}


		/// <summary>
		/// The list of cached instructions.
		/// </summary>
		static Vector<GenusMk2CachedTrial> _instructions = new()
		{
			AVRamp("ramp-v-6000ms-10:10:60Hz",	(10, 60), 6, 6000, audio: false,	transitionTrigger: 144, flickerTriggers: (11, 12)),
			AVRamp("ramp-a-6000ms-10:10:60Hz",	(10, 60), 6, 6000, visual: false,	transitionTrigger: 144, flickerTriggers: (11, 12)),
			AVRamp("ramp-av-6000ms-10:10:60Hz", (10, 60), 6, 6000,					transitionTrigger: 144, flickerTriggers: (11, 12)),
		};

		/// <summary>
		/// Create a ramp of audio and/or visual flickering.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="frequencyRange">The range of frequencies.</param>
		/// <param name="stepCount">The number of steps between these frequencies.</param>
		/// <param name="totalTime">The total duration of the ramp in milliseconds.</param>
		/// <param name="visual">Include visual flickering.</param>
		/// <param name="audio">Include audio flickering.</param>
		/// <param name="audioFrequency"></param>
		/// <param name="startTrigger"></param>
		/// <param name="endTrigger"></param>
		/// <param name="transitionTrigger"></param>
		/// <param name="flickerTriggers"></param>
		/// <returns>A list of instructions to perform the ramp.</returns>
		public static GenusMk2CachedTrial AVRamp(
			string							name,
			(float Lower, float Upper)		frequencyRange,
			int								stepCount,
			int								totalTime,
			bool							audio					= true,
			bool							visual					= true,
			float?							audioFrequency			= GenusMk2Controller.DefaultToneFrequency,
			byte?							startTrigger			= 129,
			byte?							endTrigger				= 150,
			byte?							transitionTrigger		= null,
			(byte Rise, byte Fall)?			flickerTriggers			= null,
			GenusMk2Controller.Feedback?	feedbackOnCompletion	= GenusMk2Controller.Feedback.StimulationComplete)
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
				// enable flickering
				if (audio)	result.PushBack(Instruction.StartAudioFlicker(frequencies[iFreq]));
				if (visual)	result.PushBack(Instruction.StartLedFlicker(frequencies[iFreq]));

				if (iFreq > 0 || !startTrigger.HasValue)
				{
					// do not emit on first trigger
					// signal transition if needed (needs to be after the flicker trigger if on)
					if (transitionTrigger.HasValue) result.PushBack(Instruction.EmitTrigger(transitionTrigger.Value));
				}

				// sleep until next transition
				result.PushBack(Instruction.Sleep(stepDurationMs));
			}

			// finalize
			result.PushBack(Instruction.StopFlicker());
			if (endTrigger.HasValue)			result.PushBack(Instruction.EmitTrigger(endTrigger.Value));
			if (feedbackOnCompletion.HasValue)	result.PushBack(Instruction.Feedback(feedbackOnCompletion.Value));

			return new GenusMk2CachedTrial()
			{
				Name					= name,
				Type					= "ramp",
				Instructions			= result,
				Audio					= audio,
				Visual					= visual,	
				StimulationRuntime		= totalTime,
				StepCount				= stepCount,
				FlickerFrequency		= frequencyRange,
				ToneFrequency			= audioFrequency.HasValue ? audioFrequency.Value : GenusMk2Controller.DefaultToneFrequency,
				UseFlickerTriggers		= flickerTriggers.HasValue,
				UseTransitionTriggers	= transitionTrigger.HasValue
			};
		}




	}
}
