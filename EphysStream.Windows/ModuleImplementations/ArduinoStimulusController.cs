using System;
using System.IO.Ports;

namespace TINS.Ephys.Stimulation
{
	/// <summary>
	/// Class used to control an Arduino microcontroller as a stimulation device.
	/// </summary>
	public class ArduinoStimulusController : StimulusController
	{
		/// <summary>
		/// The clock frequency of the stimulator.
		/// </summary>
		public const float StimulatorClockFrequency = 1000;

		/// <summary>
		/// The timeout for a response, in milliseconds.
		/// </summary>
		public const int ResponseTimeout = 144;

		public const int BrightnessByte = 0;
		public const int FrequencyByte	= 1;
		public const int TriggerByte	= 2;

		/// <summary>
		/// Connect to a USB stimulation device.
		/// </summary>
		/// <param name="portName"></param>
		public override void ConnectToDevice(string portName = null)
		{
			CloseCurrentPort();

			// get a serial port
			portName ??= GetFirstSerialPortName();

			if (portName is null)
				throw new Exception("No port has been found!");

			// create and open the port
			_port = new SerialPort(portName)
			{
				BaudRate	= 9600,
				Parity		= Parity.None,
				StopBits	= StopBits.One,
				DataBits	= 8,
				ReadTimeout = ResponseTimeout
			};
			_port.Open();

			try
			{
				ResetParameters();
				IsConnected = true;
			}
			catch
			{
				CloseCurrentPort();
				throw;
			}
		}

		/// <summary>
		/// Terminate the connection with the stimulation device.
		/// </summary>
		public override void Disconnect() => CloseCurrentPort();

		/// <summary>
		/// Change the stimulation frequency of the device.
		/// </summary>
		/// <param name="brightness">The brightness of the screen, in percentage. If null or NaN, the brightness will not change.</param>
		/// <param name="frequency">The new stimulation frequency, in Hz. If null or NaN, the frequency will not change.</param>
		/// <param name="trigger">A trigger to emit when the stimulation frequency actually changes. If null or <c>255</c>, the emitted trigger will not change.</param>
		public override void ChangeParameters(float? brightness, float? frequency, byte? trigger)
		{
			if (!IsConnected || _port is null)
				return;

			// assign brightness value 
			_portBuffer[BrightnessByte] = 255; // 255 means use previous
			if (brightness.HasValue && !float.IsNaN(brightness.Value))
				_portBuffer[BrightnessByte] = (byte)Numerics.Round(Numerics.Clamp(brightness.Value, BrightnessRange));

			// assign frequency value
			_portBuffer[FrequencyByte] = 255; // 255 means use previous
			if (frequency.HasValue && !float.IsNaN(frequency.Value))
				_portBuffer[FrequencyByte] = (byte)Numerics.Round(Numerics.Clamp(frequency.Value, FrequencyRange));

			// assign trigger value
			_portBuffer[TriggerByte] = 255; // 255 means use previous
			if (trigger.HasValue)
				_portBuffer[TriggerByte] = Numerics.Clamp(trigger.Value, TriggerRange);

			// send to port
			_port.Write(_portBuffer, 0, _portBuffer.Length);
		}

		/// <summary>
		/// Close the current port if open.
		/// </summary>
		protected void CloseCurrentPort()
		{
			if (_port is object)
			{
				if (_port.IsOpen)
					_port.Close();
				_port.Dispose();
			}

			_port = null;
			IsConnected = false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected static string GetFirstSerialPortName()
		{
			var ports = SerialPort.GetPortNames();
			if (ports is object && ports.Length > 0)
				return ports[0];

			return null;
		}

		protected SerialPort _port = null;
		protected byte[] _portBuffer = new byte[3];
	}
}
