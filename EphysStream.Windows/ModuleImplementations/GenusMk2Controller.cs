using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;

namespace TINS.Ephys.Stimulation
{
	/// <summary>
	/// Class used to control a Teensy microcontroller as a stimulation device.
	/// </summary>
	public unsafe class GenusMk2Controller 
		: StimulusController
	{
		/// <summary>
		/// The operating frequency range of visual stimulation.
		/// </summary>
		public static (float Lower, float Upper) FlickerFrequencyRange { get; } = (0, 100);

		/// <summary>
		/// The operating frequency range of audio stimulation.
		/// </summary>
		public static (float Lower, float Upper) AudioFrequencyRange { get; } = (25, 15000);

		/// <summary>
		/// The clock frequency of the stimulator.
		/// </summary>
		public const float StimulatorClockFrequency = 1000;

		/// <summary>
		/// An instruction given to the machine.
		/// </summary>
		public struct Instruction
		{
			/// <summary>
			/// Possible commands to issue to the machine.
			/// </summary>
			public enum Commands : int
			{
				NoOp,
				FreqFlickerL,				// float left frequency bounded (0, 100), 0 means turn off
				FreqFlickerR,				// float right frequency bounded (0, 100), 0 means turn off
				FreqFlickerAudio,			// float audio flicker frequency bounded (0, 100), 0 means turn off
				FreqToneAudio,				// float audio frequency bounded (0, 20000), 0 means turn off
				EmitTrigger,				// emit a trigger
				AwaitFullInstructionList,	// signal the board to wait for a specific number of instructions before execution
				ChangeFlickerTriggerStateL,	// turn left LED rise and fall triggers on or off
				ChangeFlickerTriggersL,		// change left LED rise and fall triggers (s1 = rise, s2 = fall)
				Sleep,						// wait a number of milliseconds (int)
				SleepMicroseconds,			// wait a number of microseconds (int)
				Reset						// reset all parameters and stop flickering
			}

			public Commands Command;
			public int		Parameter;
			
			/// <summary>
			/// Get or set the parameter as a 32-bit float.
			/// </summary>
			public float PFloat
			{
				get { int p = Parameter; return *(float*)&p; }
				set => Parameter = *(int*)&value; 
			}

			/// <summary>
			/// Get or set the parameter as a boolean value.
			/// </summary>
			public bool PBool
			{
				get => Parameter != 0;
				set => Parameter = value ? 1 : 0;
			}

			/// <summary>
			/// Get or set the parameter as a tuple of short integers.
			/// </summary>
			public (short, short) P2Short
			{
				get { int p = Parameter; return *(ValueTuple<short, short>*)&p; }
				set => Parameter = *(int*)&value;
			}

			/// <summary>
			/// No operation.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction NoOp() => new();

			/// <summary>
			/// Start flickering the left LED panel.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <returns>An instruction.</returns>
			public static Instruction StartLedFlickerLeft(float frequency)
				=> new()
				{
					Command = Commands.FreqFlickerL,
					PFloat	= frequency
				};

			/// <summary>
			/// Start flickering the right LED panel.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <returns>An instruction.</returns>
			public static Instruction StartLedFlickerRight(float frequency)
				=> new()
				{
					Command = Commands.FreqFlickerR,
					PFloat	= frequency
				};

			/// <summary>
			/// Start flickering both LED panels.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <returns>An instruction set.</returns>
			public static Instruction[] StartLedFlicker(float frequency)
				=> new[] { StartLedFlickerLeft(frequency), StartLedFlickerRight(frequency) };

			/// <summary>
			/// Start flickering the audio tone.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <returns>An instruction.</returns>
			public static Instruction StartAudioFlicker(float frequency)
				=> new()
				{
					Command = Commands.FreqFlickerAudio,
					PFloat	= frequency
				};

			/// <summary>
			/// Start flickering both LED panels and the the audio tone.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <returns>An instruction set.</returns>
			public static Instruction[] StartFlicker(float frequency)
				=> new[] { StartLedFlickerLeft(frequency), StartLedFlickerRight(frequency), StartAudioFlicker(frequency) };

			/// <summary>
			/// Start flickering both LED panels and the the audio tone and emit a trigger.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <param name="trigger">The trigger value.</param>
			/// <returns>An instruction set.</returns>
			public static Instruction[] StartFlicker(float frequency, byte trigger)
				=> new[] { StartLedFlickerLeft(frequency), StartLedFlickerRight(frequency), StartAudioFlicker(frequency), EmitTrigger(trigger) };

			/// <summary>
			/// Stop flickering the left LED panel.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction StopLedFlickerLeft()
				=> new()
				{
					Command = Commands.FreqFlickerL,
					PFloat	= 0
				};

			/// <summary> 
			/// Stop flickering the right LED panel.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction StopLedFlickerRight()
				=> new()
				{
					Command = Commands.FreqFlickerR,
					PFloat	= 0
				};

			/// <summary>
			/// Stop flickering the audio tone.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction StopAudioFlicker()
				=> new()
				{
					Command = Commands.FreqFlickerAudio,
					PFloat	= 0
				};

			/// <summary>
			/// Stop flickering both LED panels and the audio tone.
			/// </summary>
			/// <returns>An instruction set.</returns>
			public static Instruction[] StopFlicker()
				=> new[] { StopLedFlickerLeft(), StopLedFlickerRight(), StopAudioFlicker() };

			/// <summary>
			/// Stop flickering both LED panels and the audio tone and emit a trigger.
			/// </summary>
			/// <param name="trigger">The trigger value.</param>
			/// <returns>An instruction set.</returns>
			public static Instruction[] StopFlicker(byte trigger)
				=> new[] { StopLedFlickerLeft(), StopLedFlickerRight(), StopAudioFlicker(), EmitTrigger(trigger) };

			/// <summary>
			/// Change the tone of the audio signal.
			/// </summary>
			/// <param name="frequency">The tone frequency.</param>
			/// <returns>An instruction.</returns>
			public static Instruction ChangeAudioTone(float frequency)
				=> new()
				{
					Command = Commands.FreqToneAudio,
					PFloat	= frequency
				};

			/// <summary>
			/// Emit a trigger from the device.
			/// </summary>
			/// <param name="trigger">The trigger (each bit is a signal line).</param>
			/// <returns>An instruction.</returns>
			public static Instruction EmitTrigger(byte trigger)
				=> new()
				{
					Command		= Commands.EmitTrigger,
					Parameter	= trigger
				};

			/// <summary>
			/// Enable flicker triggers.
			/// </summary>
			/// <param name="riseTrigger">The rise trigger/</param>
			/// <param name="fallTrigger">The fall trigger.</param>
			/// <returns>An instruction.</returns>
			public static Instruction[] EnableFlickerTriggers(byte riseTrigger, byte fallTrigger)
				=> new[]
				{
					new Instruction { Command = Commands.ChangeFlickerTriggerStateL, PBool = true },
					new Instruction { Command = Commands.ChangeFlickerTriggersL, P2Short = (riseTrigger, fallTrigger) }
				};

			/// <summary>
			/// Disable flicker triggers.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction DisableFlickerTriggers()
				=> new()
				{
					Command = Commands.ChangeFlickerTriggerStateL,
					PBool	= false
				};

			/// <summary>
			/// Causes the device to wait for a number of instructions before executing. Maximum of 1024 instructions.
			/// </summary>
			/// <param name="count">The number of instructions to wait for.</param>
			/// <returns>An instruction.</returns>
			public static Instruction AwaitFullInstructionList(int count)
				=> new()
				{
					Command		= Commands.AwaitFullInstructionList,
					Parameter	= count
				};

			/// <summary>
			/// Wait <paramref name="milliseconds"/> before processing the next instruction.
			/// </summary>
			/// <param name="milliseconds">The number of milliseconds to wait.</param>
			/// <returns>An instruction.</returns>
			public static Instruction Sleep(int milliseconds)
				=> new()
				{
					Command		= Commands.Sleep,
					Parameter	= milliseconds
				};

			/// <summary>
			/// Wait <paramref name="microseconds"/> before processing the next instruction.
			/// </summary>
			/// <param name="microseconds">The number of microseconds to wait.</param>
			/// <returns>An instruction.</returns>
			public static Instruction SleepMicroseconds(int microseconds)
				=> new()
				{
					Command		= Commands.SleepMicroseconds,
					Parameter	= microseconds
				};

			/// <summary>
			/// Reset all parameters and stop flickering.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction Reset() => new() { Command = Commands.Reset };
		}

		/// <summary>
		/// Send a single instruction to the device.
		/// </summary>
		/// <param name="instruction"></param>
		public virtual void SendInstruction(Instruction instruction)
		{
			if (instruction.Command == Instruction.Commands.AwaitFullInstructionList)
				throw new Exception("Cannot use AwaitFullInstructionList explicitly. It is used automatically by the SendInstructionList function.");

			var dataSpan	= MemoryMarshal.Cast<byte, Instruction>(_portBuffer.AsSpan());
			dataSpan[0]		= instruction;

			_port.Write(_portBuffer, 0, _portBuffer.Length);
		}


		
		/// <summary>
		/// Send a list of instructions to be executed serially.
		/// </summary>
		/// <param name="instructionList">The list of instructions.</param>
		public virtual void SendInstructionList(Instruction[] instructionList)
			=> SendInstructionList(instructionList.AsSpan());

		/// <summary>
		/// Send a list of instructions to be executed serially.
		/// </summary>
		/// <param name="instructionList">The list of instructions.</param>
		public virtual void SendInstructionList(Span<Instruction> instructionList)
		{
			byte[] data		= new byte[(1 + instructionList.Length) * sizeof(Instruction)];
			var dataSpan	= MemoryMarshal.Cast<byte, Instruction>(data.AsSpan());

			dataSpan[0] = Instruction.AwaitFullInstructionList(instructionList.Length);
			for (int i = 0; i < instructionList.Length; ++i)
			{
				if (instructionList[i].Command == Instruction.Commands.AwaitFullInstructionList)
					throw new Exception("Cannot use AwaitFullInstructionList explicitly. It is used automatically by the SendInstructionList function.");

				dataSpan[i + 1] = instructionList[i];
			}

			_port.Write(data, 0, data.Length);
		}

		/// <summary>
		/// Reset the stimulation parameters to their default values.
		/// </summary>
		public override void Reset() => SendInstruction(Instruction.Reset());
		
		/// <summary>
		/// Emit a trigger.
		/// </summary>
		/// <param name="triggerValue">The trigger value.</param>
		public override void EmitTrigger(byte triggerValue) => SendInstruction(Instruction.EmitTrigger(triggerValue));

		/// <summary>
		/// Change the stimulation parameters.
		/// </summary>
		/// <param name="frequencyL">The flicker frequency for the left panel, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		/// <param name="frequencyR">The flicker frequency for the right panel, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		/// <param name="frequencyAudio">The flicker frequency of the audio tone, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		/// <param name="trigger">A trigger to emit when the stimulation frequency actually changes. If null, the emitted trigger will not change.</param>
		public virtual void ChangeParameters(float? frequencyL, float? frequencyR, float? frequencyAudio, byte? trigger)
		{
			var instructions = new Vector<Instruction>();
			if (frequencyL.HasValue)		instructions.PushBack(Instruction.StartLedFlickerLeft(frequencyL.Value));
			if (frequencyR.HasValue)		instructions.PushBack(Instruction.StartLedFlickerRight(frequencyR.Value));
			if (frequencyAudio.HasValue)	instructions.PushBack(Instruction.StartAudioFlicker(frequencyAudio.Value));
			if (trigger.HasValue)			instructions.PushBack(Instruction.EmitTrigger(trigger.Value));

			if (!instructions.IsEmpty)
				SendInstructionList(instructions.GetSpan());
		}

		public struct Message
		{
			// stimulation parameters to change
			public enum Actions : byte
			{
				actNone				= 0,
				actFrequencyL		= 1 << 0,
				actFrequencyR		= 1 << 1,
				actTrigger			= 1 << 2,
				actFrequencyAudio	= 1 << 3,
				actAll				= actFrequencyL | actFrequencyR | actTrigger | actFrequencyAudio
			};
		
			// stimulation parameters
			public Actions	Action;
			public byte		FrequencyL;
			public byte		FrequencyR;
			public byte		Trigger;
			public float	FrequencyAudio;
		};
		
		///// <summary>
		///// Change the stimulation parameters.
		///// </summary>
		///// <param name="frequencyL">The flicker frequency for the left panel, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		///// <param name="frequencyR">The flicker frequency for the right panel, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		///// <param name="frequencyAudio">The frequency of the audio tone, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		///// <param name="trigger">A trigger to emit when the stimulation frequency actually changes. If null, the emitted trigger will not change.</param>
		//public virtual void ChangeParameters(float? frequencyL, float? frequencyR, float? frequencyAudio, byte? trigger)
		//{
		//	if (!IsConnected || _port is null)
		//		return;
		//
		//	Message msg; 					msg.Action = Message.Actions.None;
		//	if (frequencyL.HasValue)		msg.Action |= Message.Actions.FrequencyL;
		//	if (frequencyR.HasValue)		msg.Action |= Message.Actions.FrequencyR;
		//	if (trigger.HasValue)			msg.Action |= Message.Actions.Trigger;
		//	if (frequencyAudio.HasValue)	msg.Action |= Message.Actions.FrequencyAudio;
		//
		//	msg.FrequencyL		= (byte)(frequencyL.HasValue ? Numerics.Round(frequencyL.Value) : 0);
		//	msg.FrequencyR		= (byte)(frequencyR.HasValue ? Numerics.Round(frequencyR.Value) : 0);
		//	msg.Trigger			= trigger ?? 0;
		//	msg.FrequencyAudio	= frequencyAudio ?? 0;
		//
		//	unsafe
		//	{
		//		new Span<Message>(&msg, 1).CopyTo(MemoryMarshal.Cast<byte, Message>(_portBuffer.AsSpan()));
		//	}
		//	_port.Write(_portBuffer, 0, _portBuffer.Length);
		//}
		//
		///// <summary>
		///// Reset the stimulation parameters to their default values.
		///// </summary>
		//public override void Reset() => ChangeParameters(0, 0, 0, 0);
		//
		///// <summary>
		///// Emit a trigger.
		///// </summary>
		///// <param name="triggerValue">The trigger value.</param>
		//public override void EmitTrigger(byte triggerValue) => ChangeParameters(null, null, null, triggerValue);


		public void OldDebugSerialTest()
		{
			void WriteMessage(Message m)
			{
				var data = new byte[sizeof(Message)];
				var dataSpan = MemoryMarshal.Cast<byte, Message>(data.AsSpan());
				dataSpan[0] = m;
				_port.Write(data, 0, data.Length);
			}

			Message m		= default;
			m.Action		= Message.Actions.actFrequencyL;
			m.FrequencyL	= 1;

			WriteMessage(m);
		}

		/// <summary>
		/// Connect to a USB stimulation device.
		/// </summary>
		/// <param name="portName"></param>
		public override void Connect(string portName = null)
		{
			CloseCurrentPort();

			// get a serial port
			portName ??= GetFirstSerialPortName();

			if (portName is null)
				throw new Exception("No port has been found!");

			// create and open the port
			_port = new SerialPort(portName);
			_port.Open();

			try
			{
				Reset();
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

		protected SerialPort	_port		= null;
		protected byte[]		_portBuffer = new byte[sizeof(Instruction)];
	}
}
