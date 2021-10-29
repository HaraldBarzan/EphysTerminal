using CircuitGENUS.Windows;
using System;
using System.Windows;
using TINS.Utilities;

namespace TINS.Ephys
{
	/// <summary>
	/// Protocol wizard state.
	/// </summary>
	public enum WizardState
	{
		Idle,
		Prerun,
		Running,
		Postrun
	}

	/// <summary>
	/// Protocol wizard events.
	/// </summary>
	public enum WizardEvent
	{
		Start,
		NewBlock,
		ProtocolEnded,
		Cancel
	}

	/// <summary>
	/// State machine for the protocol wizard.
	/// </summary>
	public class ProtocolWizardStateMachine
		: StateMachine<WizardState, WizardEvent>
	{
		/// <summary>
		/// Create a protocol wizard state machine.
		/// </summary>
		/// <param name="parent">The parent wizard.</param>
		public ProtocolWizardStateMachine(ProtocolWizard parent)
		{
			_p = parent;
			_s = parent?.MainWindow?.EphysStream;
		}

		public void SetActions(
			Action<string>  startRecording  = null,
			Action<string>	startProtocol   = null,
			Action          stopProtocol    = null,
			Action          stopRecording   = null)
		{
			_startRecording = startRecording;
			_startProtocol  = startProtocol;
			_stopProtocol   = stopProtocol;
			_stopRecording  = stopRecording;
		}

		/// <summary>
		/// Configure the state machine.
		/// </summary>
		protected override void ConfigureStateMachine()
		{
			// IDLE
			AddState(WizardState.Idle,
				eventAction: (e) =>
				{
					if (e is WizardEvent.Start)
						return WizardState.Prerun;
					return CurrentState;
				});

			// PRERUN
			AddState(WizardState.Prerun,
				eventAction: (e) => e switch
				{
					WizardEvent.NewBlock    => Elapse(WizardState.Running),
					WizardEvent.Cancel      => Stop(),
					_                       => CurrentState
				},
				enterStateAction: () =>
				{
					// load parameters and start recording
					_p.GetParameters(out _protocolPath, out var output, out _timeouts);
					_currentStateTimeout = _timeouts.Pre;
					_startRecording?.Invoke(output);
				});

			// RUNNING
			AddState(WizardState.Running,
				eventAction: (e) => e switch
				{
					WizardEvent.ProtocolEnded	=> WizardState.Postrun,
					WizardEvent.Cancel			=> Stop(),
					_							=> CurrentState
				},
				enterStateAction: () => _startProtocol?.Invoke(_protocolPath),
				exitStateAction: () => _stopProtocol?.Invoke());

			// POSTRUN
			AddState(WizardState.Postrun,
				eventAction: (e) => e switch
				{
					WizardEvent.NewBlock    => Elapse(WizardState.Idle),
					WizardEvent.Cancel      => Stop(),
					_						=> CurrentState
				},
				enterStateAction: () => _currentStateTimeout = _timeouts.Post,
				exitStateAction: () => Stop());
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="onTimeoutElapsed"></param>
		/// <returns></returns>
		protected WizardState Elapse(WizardState onTimeoutElapsed)
		{
			if (_currentStateTimeout == 0)
				return onTimeoutElapsed;
			--_currentStateTimeout;
			return CurrentState;
		}

		/// <summary>
		/// 
		/// </summary>
		protected WizardState Stop()
		{
			_stopRecording?.Invoke();
			if (_p.ShowOnProtocolFinish)
				_p.Visibility = Visibility.Visible;
			App.MessageBoxAsync("Protocol run completed.", "Complete!");
			return WizardState.Idle;
		}

		protected ProtocolWizard		_p;
		protected EphysStream			_s;
		protected int					_currentStateTimeout;

		protected (int Pre, int Post)	_timeouts;
		protected string				_protocolPath;

		protected Action<string>		_startRecording;
		protected Action<string>		_startProtocol;
		protected Action				_stopProtocol;
		protected Action				_stopRecording;
	}
}
