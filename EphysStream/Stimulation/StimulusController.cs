namespace TINS.Ephys.Stimulation
{
	/// <summary>
	/// Class used to control a stimulation device.
	/// </summary>
	public abstract class StimulusController
	{
		/// <summary>
		/// The operating frequency range of the stimulator.
		/// </summary>
		public static (float Lower, float Upper) FrequencyRange { get; } = (0, 100);

		/// <summary>
		/// The range of the brightness, in percentage.
		/// </summary>
		public static (float Lower, float Upper) BrightnessRange { get; } = (0, 100);
		
		/// <summary>
		/// The range of the triggers.
		/// </summary>
		public static (byte Lower, byte Upper) TriggerRange { get; } = (0, 63);

		/// <summary>
		/// Connect to a USB stimulation device.
		/// </summary>
		/// <param name="port">An USB serial port. If null, the system 
		/// will attempt to search for a compatible device.</param>
		public abstract void ConnectToDevice(string port = null);

		/// <summary>
		/// Terminate the connection with the stimulation device.
		/// </summary>
		public abstract void Disconnect();

		/// <summary>
		/// Reset the stimulation parameters to zero.
		/// </summary>
		public virtual void ResetParameters() => ChangeParameters(0, 0, 0);

		/// <summary>
		/// Change the stimulation frequency of the device.
		/// </summary>
		/// <param name="brightness">The brightness of the stimulation device.</param>
		/// <param name="frequency">The new stimulation frequency, in Hz. If null or NaN, the frequency will not change.</param>
		/// <param name="trigger">A trigger to emit when the stimulation frequency actually changes. If null, the previously emitted trigger will not change.</param>
		public abstract void ChangeParameters(float? brightness, float? frequency, byte? trigger);

		/// <summary>
		/// Change the stimulation frequency.
		/// </summary>
		/// <param name="newFrequency">The new stimulation frequency.</param>
		public virtual void ChangeFrequency(float newFrequency) => ChangeParameters(null, newFrequency, null);

		/// <summary>
		/// Emit a new trigger.
		/// </summary>
		/// <param name="triggerValue">The trigger value.</param>
		public virtual void EmitTrigger(byte triggerValue) => ChangeParameters(null, null, triggerValue);

		/// <summary>
		/// The state of this stimulus controller.
		/// </summary>
		public bool IsConnected { get; protected set; }
	}
}
