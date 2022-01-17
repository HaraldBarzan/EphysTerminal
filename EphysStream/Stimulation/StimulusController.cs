using System;

namespace TINS.Ephys.Stimulation
{
	/// <summary>
	/// Class used to control a stimulation device.
	/// </summary>
	public abstract class StimulusController
		: IDisposable
	{
		/// <summary>
		/// Dispose of this stimulus controller.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Protected dispose method.
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
		}

		/// <summary>
		/// Connect to a USB stimulation device.
		/// </summary>
		/// <param name="port">An USB serial port. If null, the system 
		/// will attempt to search for a compatible device.</param>
		public abstract void Connect(string port = null);

		/// <summary>
		/// Terminate the connection with the stimulation device.
		/// </summary>
		public abstract void Disconnect();

		/// <summary>
		/// Reset the stimulation parameters to zero.
		/// </summary>
		public abstract void Reset();

		/// <summary>
		/// Emit a new trigger.
		/// </summary>
		/// <param name="triggerValue">The trigger value.</param>
		public abstract void EmitTrigger(byte triggerValue);

		/// <summary>
		/// The state of this stimulus controller.
		/// </summary>
		public bool IsConnected { get; protected set; }
	}
}
