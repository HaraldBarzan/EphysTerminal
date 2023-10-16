namespace TINS.Terminal.Protocols.Genus
{
	using FlickerTriggerAttach = GenusController.FlickerTriggerAttach;
	using Instruction = GenusController.Instruction;

	/// <summary>
	/// Contains cached instruction lists which can be requested by name.
	/// </summary>
	public class GenusCachedTrial
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
		public static GenusCachedTrial Get(string name)
		{
			int i = _instructions.IndexOf(x => x.Name == name);
			if (i >= 0)
				return _instructions[i];
			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="trial"></param>
		/// <returns></returns>
		public static GenusCachedTrial Create(GenusConfig config, GenusProtocol.TrialTemplate trial)
		{
			if (trial.InstructionGenerator.Equals("static", System.StringComparison.InvariantCultureIgnoreCase) && 
				trial is GenusStaticTrial staticTrial)
			{
				return AVStatic(
					name:					staticTrial.TrialName, 
					frequency:				staticTrial.Frequency,
					totalTime:				Numerics.Round(staticTrial.Duration),
					audio:					staticTrial.UseAudioStimulation,
					visual:					staticTrial.UseVisualStimulation,
					audioFrequency:			staticTrial.AudioToneFrequency,
					startTrigger:			config.StimulationStartTrigger,
					endTrigger:				config.StimulationEndTrigger,
					flickerTriggers:		config.FlickerTriggersBinding != FlickerTriggerAttach.None 
											? (config.FlickerTriggersBinding, config.FlickerTriggersRiseTrigger, config.FlickerTriggersFallTrigger)
											: null,
					feedbackOnCompletion:	config.FeedbackOnStimulusEnd ? GenusController.Feedback.StimulationComplete : null);
			}
			else if (trial.InstructionGenerator.Equals("ramp", System.StringComparison.InvariantCultureIgnoreCase) &&
				trial is GenusRampTrial rampTrial)
			{
				return AVRamp(
					name:					rampTrial.TrialName,
					frequencyRange:			(rampTrial.FrequencyStart, rampTrial.FrequencyEnd),
					stepCount:				rampTrial.FrequencySteps,
					totalTime:				Numerics.Round(rampTrial.Duration),
					audio:					rampTrial.UseAudioStimulation,
					visual:					rampTrial.UseVisualStimulation,
					audioFrequency:			rampTrial.AudioToneFrequency,
					startTrigger:			config.StimulationStartTrigger,
					endTrigger:				config.StimulationEndTrigger,
					flickerTriggers:		config.FlickerTriggersBinding != FlickerTriggerAttach.None 
											? (config.FlickerTriggersBinding, config.FlickerTriggersRiseTrigger, config.FlickerTriggersFallTrigger)
											: null,
					feedbackOnCompletion:	config.FeedbackOnStimulusEnd ? GenusController.Feedback.StimulationComplete : null);
			}

			return null;
		}

				/// <summary>
		/// 
		/// </summary>
		/// <param name="trial"></param>
		/// <returns></returns>
		public static GenusCachedTrial Create(HumanGenusConfig config, HumanGenusProtocol.TrialTemplate trial)
		{
			if (trial.InstructionGenerator.Equals("static", System.StringComparison.InvariantCultureIgnoreCase) && 
				trial is HumanGenusStaticTrial staticTrial)
			{
				return AVStatic(
					name:					staticTrial.TrialName, 
					frequency:				staticTrial.Frequency,
					totalTime:				Numerics.Round(staticTrial.Duration),
					audio:					staticTrial.UseAudioStimulation,
					visual:					staticTrial.UseVisualStimulation,
					audioFrequency:			staticTrial.AudioToneFrequency,
					startTrigger:			config.StimulationStartTrigger,
					endTrigger:				config.StimulationEndTrigger,
					flickerTriggers:		config.FlickerTriggersBinding != FlickerTriggerAttach.None 
											? (config.FlickerTriggersBinding, config.FlickerTriggersRiseTrigger, config.FlickerTriggersFallTrigger)
											: null,
					feedbackOnCompletion:	config.FeedbackOnStimulusEnd ? GenusController.Feedback.StimulationComplete : null);
			}
			else if (trial.InstructionGenerator.Equals("ramp", System.StringComparison.InvariantCultureIgnoreCase) &&
				trial is HumanGenusRampTrial rampTrial)
			{
				return AVRamp(
					name:					rampTrial.TrialName,
					frequencyRange:			(rampTrial.FrequencyStart, rampTrial.FrequencyEnd),
					stepCount:				rampTrial.FrequencySteps,
					totalTime:				Numerics.Round(rampTrial.Duration),
					audio:					rampTrial.UseAudioStimulation,
					visual:					rampTrial.UseVisualStimulation,
					audioFrequency:			rampTrial.AudioToneFrequency,
					startTrigger:			config.StimulationStartTrigger,
					endTrigger:				config.StimulationEndTrigger,
					flickerTriggers:		config.FlickerTriggersBinding != FlickerTriggerAttach.None 
											? (config.FlickerTriggersBinding, config.FlickerTriggersRiseTrigger, config.FlickerTriggersFallTrigger)
											: null,
					feedbackOnCompletion:	config.FeedbackOnStimulusEnd ? GenusController.Feedback.StimulationComplete : null);
			}

			return null;
		}

		/// <summary>
		/// The list of cached instructions.
		/// </summary>
		static Vector<GenusCachedTrial> _instructions = new()
		{
			// static
			AVStatic("static-v-6000ms-07Hz",	07, 6000, audio: false,		flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker, 5, 6)),
			AVStatic("static-v-6000ms-10Hz",	10, 6000, audio: false,		flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker, 5, 6)),
			AVStatic("static-v-6000ms-20Hz",	20, 6000, audio: false,		flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker, 5, 6)),
			AVStatic("static-v-6000ms-30Hz",	30, 6000, audio: false,		flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker, 5, 6)),
			AVStatic("static-v-6000ms-40Hz",	40, 6000, audio: false,		flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker, 5, 6)),
			AVStatic("static-v-6000ms-50Hz",	50, 6000, audio: false,		flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker, 5, 6)),
			AVStatic("static-v-6000ms-60Hz",	60, 6000, audio: false,		flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker, 5, 6)),

			AVStatic("static-a-6000ms-07Hz",	07, 6000, visual: false,	flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-a-6000ms-10Hz",	10, 6000, visual: false,	flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-a-6000ms-20Hz",	20, 6000, visual: false,	flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-a-6000ms-30Hz",	30, 6000, visual: false,	flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-a-6000ms-40Hz",	40, 6000, visual: false,	flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-a-6000ms-50Hz",	50, 6000, visual: false,	flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-a-6000ms-60Hz",	60, 6000, visual: false,	flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),

			AVStatic("static-av-6000ms-07Hz",	07, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-10Hz",	10, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-20Hz",	20, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-30Hz",	30, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-35Hz",	35, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-40Hz",	40, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-45Hz",	45, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-50Hz",	50, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-55Hz",	55, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),
			AVStatic("static-av-6000ms-60Hz",	60, 6000,					flickerTriggers: (FlickerTriggerAttach.AudioFlicker, 5, 6)),

			// ramp slow
			AVRamp("ramp-v-6000ms-10:10:60Hz",	(10, 60), 6, 6000, audio: false,	transitionTrigger: 7, flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker,	5, 6)),
			AVRamp("ramp-a-6000ms-10:10:60Hz",	(10, 60), 6, 6000, visual: false,	transitionTrigger: 7, flickerTriggers: (FlickerTriggerAttach.AudioFlicker,		5, 6)),
			AVRamp("ramp-av-6000ms-10:10:60Hz", (10, 60), 6, 6000,					transitionTrigger: 7, flickerTriggers: (FlickerTriggerAttach.AudioFlicker,		5, 6)),

			// ramp fast
			AVRamp("ramp-v-6000ms-10:2:60Hz",	(10, 60), 26, 6000, audio: false,	transitionTrigger: 7, flickerTriggers: (FlickerTriggerAttach.LedLeftFlicker,	5, 6)),
			AVRamp("ramp-a-6000ms-10:2:60Hz",	(10, 60), 26, 6000, visual: false,	transitionTrigger: 7, flickerTriggers: (FlickerTriggerAttach.AudioFlicker,		5, 6)),
			AVRamp("ramp-av-6000ms-10:2:60Hz",	(10, 60), 26, 6000,					transitionTrigger: 7, flickerTriggers: (FlickerTriggerAttach.AudioFlicker,		5, 6)),
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
		public static GenusCachedTrial AVRamp(
			string													name,
			(float Lower, float Upper)								frequencyRange,
			int														stepCount,
			int														totalTime,
			bool													audio					= true,
			bool													visual					= true,
			float?													audioFrequency			= GenusController.DefaultToneFrequency,
			byte?													startTrigger			= 2,
			byte?													endTrigger				= 3,
			byte?													transitionTrigger		= null,
			(FlickerTriggerAttach Attach, byte Rise, byte Fall)?	flickerTriggers			= null,
			bool													signalStimulationTTL	= false,
			GenusController.Feedback?								feedbackOnCompletion	= GenusController.Feedback.StimulationComplete)
		{
			// init
			var result				= new Vector<Instruction>();
			using var frequencies	= Numerics.Linspace(frequencyRange, stepCount);
			int stepDurationMs		= totalTime / stepCount;

			// preparation
			if (audioFrequency.HasValue)	result.PushBack(Instruction.ChangeAudioTone(audioFrequency.Value));
			if (startTrigger.HasValue)		result.PushBack(Instruction.EmitTrigger(startTrigger.Value));
			if (flickerTriggers.HasValue)	result.PushBack(Instruction.SetFlickerTriggers(flickerTriggers.Value.Attach, flickerTriggers.Value.Rise, flickerTriggers.Value.Fall));
			if (signalStimulationTTL)		result.PushBack(Instruction.DigitalWrite(true));

			// loop through the list
			for (int iFreq = 0; iFreq < frequencies.Size; ++iFreq)
			{
				// enable flickering
				if (audio && visual)
				{
					result.PushBack(Instruction.StartFlicker(frequencies[iFreq]));
				}
				else
				{
					if (audio)	result.PushBack(Instruction.StartAudioFlicker(frequencies[iFreq]));
					if (visual)	result.PushBack(Instruction.StartLedFlicker(frequencies[iFreq]));
				}
				

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
			if (signalStimulationTTL)			result.PushBack(Instruction.DigitalWrite(false));
			if (endTrigger.HasValue)			result.PushBack(Instruction.EmitTrigger(endTrigger.Value));
			if (feedbackOnCompletion.HasValue)	result.PushBack(Instruction.Feedback(feedbackOnCompletion.Value));

			return new GenusCachedTrial()
			{
				Name					= name,
				Type					= "ramp",
				Instructions			= result,
				Audio					= audio,
				Visual					= visual,	
				StimulationRuntime		= totalTime,
				StepCount				= stepCount,
				FlickerFrequency		= frequencyRange,
				ToneFrequency			= audioFrequency.HasValue ? audioFrequency.Value : GenusController.DefaultToneFrequency,
				UseFlickerTriggers		= flickerTriggers.HasValue,
				UseTransitionTriggers	= transitionTrigger.HasValue
			};
		}

		/// <summary>
		/// Create static flickering stimulation.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="frequency"></param>
		/// <param name="totalTime"></param>
		/// <param name="audio"></param>
		/// <param name="visual"></param>
		/// <param name="audioFrequency"></param>
		/// <param name="startTrigger"></param>
		/// <param name="endTrigger"></param>
		/// <param name="flickerTriggers"></param>
		/// <param name="feedbackOnCompletion"></param>
		/// <returns></returns>
		public static GenusCachedTrial AVStatic(
			string													name,
			float													frequency,
			int														totalTime,
			bool													audio					= true,
			bool													visual					= true,
			float?													audioFrequency			= GenusController.DefaultToneFrequency,
			byte?													startTrigger			= 2,
			byte?													endTrigger				= 3,
			(FlickerTriggerAttach Attach, byte Rise, byte Fall)?	flickerTriggers			= null,
			bool													signalStimulationTTL	= false,
			GenusController.Feedback?								feedbackOnCompletion	= GenusController.Feedback.StimulationComplete)
		{
			var result = new Vector<Instruction>();

			// preparation
			if (audioFrequency.HasValue)	result.PushBack(Instruction.ChangeAudioTone(audioFrequency.Value));
			if (startTrigger.HasValue)		result.PushBack(Instruction.EmitTrigger(startTrigger.Value));
			if (flickerTriggers.HasValue)	result.PushBack(Instruction.SetFlickerTriggers(flickerTriggers.Value.Attach, flickerTriggers.Value.Rise, flickerTriggers.Value.Fall));
			if (signalStimulationTTL)		result.PushBack(Instruction.DigitalWrite(true));

			// start flickering
			if (audio && visual)
			{
				result.PushBack(Instruction.StartFlicker(frequency));
			}
			else
			{
				if (audio)	result.PushBack(Instruction.StartAudioFlicker(frequency));
				if (visual)	result.PushBack(Instruction.StartLedFlicker(frequency));
			}

			// sleep until trial end
			result.PushBack(Instruction.Sleep(totalTime));

			// finalize
			result.PushBack(Instruction.StopFlicker());
			if (endTrigger.HasValue)			result.PushBack(Instruction.EmitTrigger(endTrigger.Value));
			if (feedbackOnCompletion.HasValue)	result.PushBack(Instruction.Feedback(feedbackOnCompletion.Value));

			return new GenusCachedTrial()
			{
				Name					= name,
				Type					= "static",
				Instructions			= result,
				Audio					= audio,
				Visual					= visual,	
				StimulationRuntime		= totalTime,
				StepCount				= 1,
				FlickerFrequency		= (frequency, frequency),
				ToneFrequency			= audioFrequency.HasValue ? audioFrequency.Value : GenusController.DefaultToneFrequency,
				UseFlickerTriggers		= flickerTriggers.HasValue,
				UseTransitionTriggers	= false
			};
		}


	}
}
