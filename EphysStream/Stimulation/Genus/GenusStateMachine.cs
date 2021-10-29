using System;
using TINS.Ephys.UI;
using TINS.Utilities;

namespace TINS.Ephys.Stimulation.Genus
{
	/// <summary>
	/// Genus protocol state machine.
	/// </summary>
	public class GenusStateMachine
		: StateMachine<GenusState, GenusEvent>
	{
		/// <summary>
		/// Raised when a trial is completed.
		/// </summary>
		public event Action<int> TrialBegin;

		/// <summary>
		/// Raised when a trial run is completed.
		/// </summary>
		public event Action RunCompleted;

		/// <summary>
		/// Create a state machine for a Genus protocol.
		/// </summary>
		/// <param name="protocol">The parent Genus protocol.</param>
		public GenusStateMachine(GenusProtocol protocol)
		{
			_p				= protocol;
			_stim			= protocol.StimulusController;
			_ui				= protocol.ParentStream.UI;

			// just so we have a list at any time
			ResetTrials();
		}

		/// <summary>
		/// Configure the state machine.
		/// </summary>
		protected override void ConfigureStateMachine()
		{
			// IDLE
			AddState(GenusState.Idle,
				eventAction: (e) =>
				{
					if (e is GenusEvent.Start)
					{
						ResetTrials();
						return GenusState.Intertrial;
					}

					return CurrentState;
				},
				enterStateAction: () => _stim.ResetParameters());


			// PRESTIMULUS
			AddState(GenusState.Prestimulus,
				eventAction: (e) => e switch
				{
					GenusEvent.NewBlock => Elapse(GenusState.Stimulus),
					GenusEvent.Stop		=> GenusState.Idle,
					_					=> CurrentState
				},
				enterStateAction: () =>
				{
					_stateTimeout = _trials[CurrentTrialIndex].PrestimulusTimeout;
					_stim.EmitTrigger(_p.Config.PrestimulusTrigger);
				});


			// STIMULUS
			AddState(GenusState.Stimulus,
				eventAction: (e) => e switch
				{
					GenusEvent.NewBlock => Elapse(GenusState.Poststimulus),
					GenusEvent.Stop		=> GenusState.Idle,
					_					=> CurrentState
				},
				enterStateAction: () =>
				{
					_stateTimeout = _trials[CurrentTrialIndex].StimulusTimeout;
					_stim.ChangeParameters(2f, _trials[CurrentTrialIndex].StimulationFrequency, _p.Config.StimulusTrigger);
				},
				exitStateAction: () => _stim.ChangeParameters(0, 0, null));


			// POSTSTIMULUS
			AddState(GenusState.Poststimulus,
				eventAction: (e) => e switch
				{
					GenusEvent.NewBlock => Elapse(GenusState.Intertrial),
					GenusEvent.Stop		=> GenusState.Idle,
					_					=> CurrentState
				},
				enterStateAction: () =>
				{
					_stateTimeout = _trials[CurrentTrialIndex].PoststimulusTimeout;
					_stim.EmitTrigger(_p.Config.PoststimulusTrigger);
				},
				exitStateAction: () => _stim.EmitTrigger(_p.Config.TrialEndTrigger));


			// INTERTRIAL
			AddState(GenusState.Intertrial,
				eventAction: (e) => 
				{
					if (e is GenusEvent.NewBlock)
					{
						if (_stateTimeout == 0)
						{
							// emit line
							if (Numerics.IsClamped(CurrentTrialIndex, (0, TrialCount - 1)) && _p.TextOutput is not null)
								_p.TextOutput.WriteLine($"{CurrentTrialIndex + 1},{CurrentTrial.StimulationFrequency}");

							CurrentTrialIndex++;
							
							// check stopping condition
							if (CurrentTrialIndex == TrialCount)
							{
								RunCompleted?.Invoke();
								return GenusState.Idle;
							}

							// begin a new trial
							TrialBegin?.Invoke(CurrentTrialIndex);
							return GenusState.Prestimulus;
						}
						--_stateTimeout;
					}

					if (e is GenusEvent.Stop)
						return GenusState.Idle;
					return CurrentState;
				},
				enterStateAction: () => _stateTimeout = _p.Config.IntertrialTimeout);
		}

		/// <summary>
		/// Total number of trials.
		/// </summary>
		public int TrialCount => _trials.Size;

		/// <summary>
		/// Index of current trial.
		/// </summary>
		public int CurrentTrialIndex { get; protected set; }

		/// <summary>
		/// Current trial.
		/// </summary>
		public GenusTrial CurrentTrial => _trials.IsEmpty ? null : _trials[CurrentTrialIndex];


		/// <summary>
		/// Reset the trial list and the current index.
		/// </summary>
		protected void ResetTrials()
		{
			// prepare the trial list
			_trials.Clear();
			CurrentTrialIndex = -1;
			foreach (var t in _p.Config.Trials)
				for (int i = 0; i < t.Count; ++i)
					_trials.PushBack(t);

			// shuffle if necessary
			if (_p.Config.Randomize)
				new RNG().Shuffle(_trials);
		}

		/// <summary>
		/// If timeout is reached, go to another state, otherwise decrement the timeout counter.
		/// </summary>
		/// <param name="onTimeoutReached">The state to go to on timeout zero.</param>
		/// <returns>A state.</returns>
		protected GenusState Elapse(GenusState onTimeoutReached)
		{
			if (_stateTimeout == 0)
				return onTimeoutReached;
			--_stateTimeout;
			return CurrentState;
		}



		protected GenusProtocol			_p;
		protected StimulusController	_stim;
		protected IUserInterface		_ui;

		protected Vector<GenusTrial>	_trials	= new();
		protected int					_stateTimeout;
	}
}
