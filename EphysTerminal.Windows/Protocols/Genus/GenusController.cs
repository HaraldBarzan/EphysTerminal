using System;
using System.IO.Ports;
using System.Runtime.InteropServices;
using TINS.Terminal.Stimulation;

namespace TINS.Terminal.Protocols.Genus
{
	/// <summary>
	/// Class used to control a Teensy microcontroller as a stimulation device.
	/// </summary>
	public unsafe class GenusController
		: StimulusController
	{
		/// <summary>
		/// The operating frequency range of visual stimulation.
		/// </summary>
		public static (float Lower, float Upper) FlickerFrequencyRange { get; } = (0, 100);

		/// <summary>
		/// The operating frequency range of audio stimulation.
		/// </summary>
		public static (float Lower, float Upper) AudioToneRange { get; } = (25, 20000);

		/// <summary>
		/// The default audio tone frequency.
		/// </summary>
		public const float DefaultToneFrequency = 10000;

		/// <summary>
		/// Signal the receival of a feedback.
		/// </summary>
		public event EventHandler<Feedback> FeedbackReceived;

		/// <summary>
		/// Feedback provided by the device.
		/// </summary>
		public enum Feedback : byte
		{
			OK,
			StimulationComplete,
			Error
		}

		/// <summary>
		/// Flicker trigger attachment.
		/// </summary>
		public enum FlickerTriggerAttach : int
		{
			None,
			LedLeftFlicker,
			LedRightFlicker,
			AudioFlicker
		}

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
				FreqFlickerL,               // float left frequency bounded (0, 100), 0 means turn off
				FreqFlickerR,               // float right frequency bounded (0, 100), 0 means turn off
				FreqFlickerLed,             // float led frequency bounded (0, 100), 0 means turn off
				FreqFlickerAudio,           // float audio flicker frequency bounded (0, 100), 0 means turn off
				FreqFlickerAll,             // float all frequency bounded (0, 100), 0 means turn off
				FreqToneAudio,              // float audio frequency bounded (0, 20000), 0 means turn off
				EmitTrigger,                // emit a trigger
				AwaitFullInstructionList,   // signal the board to wait for a specific number of instructions before execution
				ChangeFlickerTriggerAttach, // turn flicker rise and fall triggers on or off
				ChangeFlickerTriggers,      // change fllicker rise and fall triggers (s1 = rise, s2 = fall)
				Sleep,                      // wait a number of milliseconds (int)
				SleepMicroseconds,          // wait a number of microseconds (int)
				Reset,                      // reset all parameters and stop flickering
				Feedback                    // send feedback to the computer
			}

			public Commands Command;
			public int Parameter;

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
			public (short S1, short S2) P2Short
			{
				get { int p = Parameter; return *(ValueTuple<short, short>*)&p; }
				set => Parameter = *(int*)&value;
			}

			/// <summary>
			/// Get or set the flicker trigger attach.
			/// </summary>
			public FlickerTriggerAttach PFTAttach
			{
				get => (FlickerTriggerAttach)Parameter;
				set => Parameter = (int)value;
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
					PFloat = frequency
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
					PFloat = frequency
				};

			/// <summary>
			/// Start flickering both LED panels.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <returns>An instruction set.</returns>
			public static Instruction StartLedFlicker(float frequency)
				=> new()
				{
					Command = Commands.FreqFlickerL,
					PFloat = frequency
				};

			/// <summary>
			/// Start flickering the audio tone.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <returns>An instruction.</returns>
			public static Instruction StartAudioFlicker(float frequency)
				=> new()
				{
					Command = Commands.FreqFlickerAudio,
					PFloat = frequency
				};

			/// <summary>
			/// Start flickering both LED panels and the the audio tone.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <returns>An instruction set.</returns>
			public static Instruction StartFlicker(float frequency)
				=> new()
				{
					Command = Commands.FreqFlickerAll,
					PFloat = frequency
				};

			/// <summary>
			/// Start flickering both LED panels and the the audio tone and emit a trigger.
			/// </summary>
			/// <param name="frequency">The desired flicker frequency (50% duty cycle square wave).</param>
			/// <param name="trigger">The trigger value.</param>
			/// <returns>An instruction set.</returns>
			public static Instruction[] StartFlicker(float frequency, byte trigger)
				=> new[] { StartFlicker(frequency), EmitTrigger(trigger) };

			/// <summary>
			/// Stop flickering the left LED panel.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction StopLedFlickerLeft()
				=> new()
				{
					Command = Commands.FreqFlickerL,
					PFloat = 0
				};

			/// <summary> 
			/// Stop flickering the right LED panel.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction StopLedFlickerRight()
				=> new()
				{
					Command = Commands.FreqFlickerR,
					PFloat = 0
				};

			/// <summary>
			/// Stop flickering the audio tone.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction StopAudioFlicker()
				=> new()
				{
					Command = Commands.FreqFlickerAudio,
					PFloat = 0
				};

			/// <summary>
			/// Stop flickering both LED panels and the audio tone.
			/// </summary>
			/// <returns>An instruction set.</returns>
			public static Instruction StopFlicker()
				=> new()
				{
					Command = Commands.FreqFlickerAll,
					PFloat = 0
				};

			/// <summary>
			/// Stop flickering both LED panels and the audio tone and emit a trigger.
			/// </summary>
			/// <param name="trigger">The trigger value.</param>
			/// <returns>An instruction set.</returns>
			public static Instruction[] StopFlicker(byte trigger)
				=> new[] { StopFlicker(), EmitTrigger(trigger) };

			/// <summary>
			/// Change the tone of the audio signal.
			/// </summary>
			/// <param name="frequency">The tone frequency.</param>
			/// <returns>An instruction.</returns>
			public static Instruction ChangeAudioTone(float frequency)
				=> new()
				{
					Command = Commands.FreqToneAudio,
					PFloat = frequency
				};

			/// <summary>
			/// Emit a trigger from the device.
			/// </summary>
			/// <param name="trigger">The trigger (each bit is a signal line).</param>
			/// <returns>An instruction.</returns>
			public static Instruction EmitTrigger(byte trigger)
				=> new()
				{
					Command = Commands.EmitTrigger,
					Parameter = trigger
				};

			/// <summary>
			/// Enable flicker triggers.
			/// </summary>
			/// <param name="riseTrigger">The rise trigger/</param>
			/// <param name="fallTrigger">The fall trigger.</param>
			/// <returns>An instruction.</returns>
			public static Instruction[] SetFlickerTriggers(FlickerTriggerAttach attach, byte riseTrigger, byte fallTrigger)
				=> new[]
				{
					new Instruction { Command = Commands.ChangeFlickerTriggerAttach,    PFTAttach   = attach },
					new Instruction { Command = Commands.ChangeFlickerTriggers,         P2Short     = (riseTrigger, fallTrigger) }
				};

			/// <summary>
			/// Disable flicker triggers.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction DisableFlickerTriggers()
				=> new()
				{
					Command = Commands.ChangeFlickerTriggerAttach,
					PFTAttach = FlickerTriggerAttach.None
				};

			/// <summary>
			/// Causes the device to wait for a number of instructions before executing. Maximum of 1024 instructions.
			/// </summary>
			/// <param name="count">The number of instructions to wait for.</param>
			/// <returns>An instruction.</returns>
			public static Instruction AwaitFullInstructionList(int count)
				=> new()
				{
					Command = Commands.AwaitFullInstructionList,
					Parameter = count
				};

			/// <summary>
			/// Wait <paramref name="milliseconds"/> before processing the next instruction.
			/// </summary>
			/// <param name="milliseconds">The number of milliseconds to wait.</param>
			/// <returns>An instruction.</returns>
			public static Instruction Sleep(int milliseconds)
				=> new()
				{
					Command = Commands.Sleep,
					Parameter = milliseconds
				};

			/// <summary>
			/// Wait <paramref name="microseconds"/> before processing the next instruction.
			/// </summary>
			/// <param name="microseconds">The number of microseconds to wait.</param>
			/// <returns>An instruction.</returns>
			public static Instruction SleepMicroseconds(int microseconds)
				=> new()
				{
					Command = Commands.SleepMicroseconds,
					Parameter = microseconds
				};

			/// <summary>
			/// Reset all parameters and stop flickering.
			/// </summary>
			/// <returns>An instruction.</returns>
			public static Instruction Reset() => new() { Command = Commands.Reset };

			/// <summary>
			/// Send feedback to the computer.
			/// </summary>
			/// <param name="fb">The feedback enum.</param>
			/// <returns>An instruction.</returns>
			public static Instruction Feedback(Feedback fb)
				=> new()
				{
					Command = Commands.Feedback,
					Parameter = (int)fb
				};

			/// <summary>
			/// Obtain a string representation of this instruction.
			/// </summary>
			/// <returns>A string.</returns>
			public override string ToString()
			{
				switch (Command)
				{
					case Commands.FreqFlickerL:
					case Commands.FreqFlickerR:
					case Commands.FreqFlickerAudio:
					case Commands.FreqToneAudio:
					case Commands.FreqFlickerLed:
					case Commands.FreqFlickerAll:
						return $"{Command}: {PFloat} Hz";
					case Commands.Sleep:
						return $"{Command}: {Parameter} ms";
					case Commands.SleepMicroseconds:
						return $"{Command}: {Parameter} us";
					case Commands.EmitTrigger:
					case Commands.Feedback:
						return $"{Command}: {(byte)Parameter}";
					case Commands.ChangeFlickerTriggers:
						return $"{Command}: {P2Short.S1}(L), {P2Short.S2}(R)";
					case Commands.ChangeFlickerTriggerAttach:
						return $"{Command}: {PBool.ToString().ToLower()}";
					case Commands.AwaitFullInstructionList:
						return $"{Command}: {Parameter} instructions";
					default:
					case Commands.NoOp:
					case Commands.Reset:
						return Command.ToString();
				}
			}
		}

		/// <summary>
		/// Send a single instruction to the device.
		/// </summary>
		/// <param name="instruction"></param>
		public virtual void SendInstruction(Instruction instruction)
		{
			if (instruction.Command == Instruction.Commands.AwaitFullInstructionList)
				throw new Exception("Cannot use AwaitFullInstructionList explicitly. It is used automatically by the SendInstructionList function.");

			var dataSpan = MemoryMarshal.Cast<byte, Instruction>(_portBuffer.AsSpan());
			dataSpan[0] = instruction;

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
		public virtual void SendInstructionList(Vector<Instruction> instructionList)
			=> SendInstructionList(instructionList.GetSpan());

		/// <summary>
		/// Send a list of instructions to be executed serially.
		/// </summary>
		/// <param name="instructionList">The list of instructions.</param>
		public virtual void SendInstructionList(Span<Instruction> instructionList)
		{
			byte[] data = new byte[(1 + instructionList.Length) * sizeof(Instruction)];
			var dataSpan = MemoryMarshal.Cast<byte, Instruction>(data.AsSpan());

			dataSpan[0] = Instruction.AwaitFullInstructionList(instructionList.Length);
			for (int i = 0; i < instructionList.Length; ++i)
			{
				if (instructionList[i].Command == Instruction.Commands.AwaitFullInstructionList)
					throw new Exception("Cannot use AwaitFullInstructionList explicitly. It is used automatically by the SendInstructionList function.");

				dataSpan[i + 1] = instructionList[i];
			}

			//int blockSize	= 256;
			//int	blockStart	= 0;
			//for (int i = 0; i < data.Length / blockSize; ++i)
			//{
			//	_port.Write(data, blockStart, blockSize);
			//	blockStart += blockSize;
			//}
			//_port.Write(data, blockStart, data.Length - blockStart);
			_port.Write(data, 0, data.Length);
		}

		/// <summary>
		/// Reset the stimulation parameters to their default values.
		/// </summary>
		public override void Reset()
		{
			_port.ReadExisting();
			SendInstruction(Instruction.Reset());
		}

		/// <summary>
		/// Emit a trigger.
		/// </summary>
		/// <param name="triggerValue">The trigger value.</param>
		public override void EmitTrigger(byte triggerValue) => SendInstruction(Instruction.EmitTrigger(triggerValue));

		/// <summary>
		/// Trigger a beep.
		/// </summary>
		/// <param name="beepDuration">The duration of the beep, in milliseconds.</param>
		public void Beep(int beepDuration = 500)
		{
			Span<Instruction> instructions = stackalloc Instruction[3];
			instructions[0] = Instruction.StartAudioFlicker(0);
			instructions[1] = Instruction.Sleep(Numerics.Round(beepDuration));
			instructions[2] = Instruction.StopAudioFlicker();
			SendInstructionList(instructions);
		}

		/// <summary>
		/// Change the stimulation parameters.
		/// </summary>
		/// <param name="frequencyL">The flicker frequency for the left panel, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		/// <param name="frequencyR">The flicker frequency for the right panel, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		/// <param name="frequencyAudio">The flicker frequency of the audio tone, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		/// <param name="frequencyTone">The flicker frequency of the audio tone, in Hz. Zero to shut it down completely or null to leave the current frequency unchanged.</param>
		/// <param name="trigger">A trigger to emit when the stimulation frequency actually changes. If null, the emitted trigger will not change.</param>
		public virtual void ChangeParameters(float? frequencyL, float? frequencyR, float? frequencyAudio, float? frequencyTone, byte? trigger)
		{
			using var instructions = new Vector<Instruction>();
			if (frequencyL.HasValue)		instructions.PushBack(Instruction.StartLedFlickerLeft(frequencyL.Value));
			if (frequencyR.HasValue)		instructions.PushBack(Instruction.StartLedFlickerRight(frequencyR.Value));
			if (frequencyAudio.HasValue)	instructions.PushBack(Instruction.StartAudioFlicker(frequencyAudio.Value));
			if (frequencyTone.HasValue)		instructions.PushBack(Instruction.ChangeAudioTone(frequencyTone.Value));
			if (trigger.HasValue)			instructions.PushBack(Instruction.EmitTrigger(trigger.Value));

			if (!instructions.IsEmpty)
				SendInstructionList(instructions.GetSpan());
		}

		/// <summary>
		/// Dispose method.
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			ClosePort();
			base.Dispose(disposing);
		}

		/// <summary>
		/// Connect to a USB stimulation device.
		/// </summary>
		/// <param name="portName"></param>
		public override void Connect(string portName = null)
		{
			ClosePort();

			// get a serial port
			portName ??= GetFirstSerialPortName();

			if (portName is null)
				throw new Exception("No port has been found!");

			// create and open the port
			_port = new SerialPort(portName);
			try
			{
				_port.DataReceived += OnPortDataReceived;
				_port.Open();

				Reset();
				IsConnected = true;
			}
			catch
			{
				ClosePort();
				throw;
			}
		}

		/// <summary>
		/// Terminate the connection with the stimulation device.
		/// </summary>
		public override void Disconnect() => ClosePort();

		/// <summary>
		/// Close the current port if open.
		/// </summary>
		protected void ClosePort()
		{
			if (_port is object)
			{
				_port.DataReceived -= OnPortDataReceived;
				if (_port.IsOpen)
					_port.Close();
				_port.Dispose();
			}

			_port = null;
			IsConnected = false;
		}

		/// <summary>
		/// Raised when data is received by the computer.
		/// </summary>
		/// <param name="sender">The port that sent the data.</param>
		/// <param name="e">The data.</param>
		protected void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			if (e.EventType is SerialData.Chars)
			{
				var fb = (Feedback)_port.ReadByte();
				FeedbackReceived?.Invoke(this, fb);
			}
		}

		/// <summary>
		/// Get the name of the first valid serial port.
		/// </summary>
		/// <returns>A serial port name if successful, null otherwise.</returns>
		protected static string GetFirstSerialPortName()
		{
			var ports = SerialPort.GetPortNames();
			if (ports is object && ports.Length > 0)
				return ports[0];

			return null;
		}

		protected SerialPort _port = null;
		protected byte[] _portBuffer = new byte[sizeof(Instruction)];
	}
}
