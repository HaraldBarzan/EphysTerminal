#include <Audio.h>
#include <IntervalTimer.h>



struct Instruction
{
  enum Commands : long
  {
        NoOp,
        FreqFlickerL,               // float left frequency bounded (0, 100), 0 means turn off
        FreqFlickerR,               // float right frequency bounded (0, 100), 0 means turn off
        FreqFlickerAudio,           // float audio flicker frequency bounded (0, 100), 0 means turn off
        FreqToneAudio,              // float audio frequency bounded (0, 20000), 0 means turn off
        EmitTrigger,                // emit a trigger
        AwaitFullInstructionList,   // signal the board to wait for a specific number of instructions before execution
        ChangeFlickerTriggerStateL, // turn left LED rise and fall triggers on or off
        ChangeFlickerTriggersL,     // change left LED rise and fall triggers (s1 = rise, s2 = fall)
        Sleep,                      // wait a number of milliseconds (int)
        SleepMicroseconds,          // wait a number of microseconds (int)
        Reset,                      // reset all parameters and stop flickering
        Feedback                    // send feedback to the computer
  };

  Commands  Command;
  long      Parameter;

  float GetPFloat();
  void  SetPFloat(float p);
  void  GetP2Short(short s[2]);
  void  SetP2Short(short s[2]);
  void  ProcessInstruction();
};


enum Feedback : byte
{
  OK,
  Error
};


// objects
IntervalTimer           TimerFlickerL;
IntervalTimer           TimerFlickerR;
IntervalTimer           TimerFlickerAudio;
AudioSynthWaveformSine  AudioSine;
AudioOutputAnalog       DAC;
AudioConnection         PatchCord(AudioSine, DAC);
const byte              LedPinL             = 17;
const byte              LedPinR             = 16;
byte                    LedStateL           = LOW;
byte                    LedStateR           = LOW;
byte                    AudioState          = LOW;
float                   AudioAmpOff         = 0;
float                   AudioAmpOn          = 0.2;
bool                    UseFlickerTriggers  = false;
byte                    LedRiseTrigger      = 0;
byte                    LedFallTrigger      = 0  ;
float                   DefaultAudioTone    = 10000;

// helpers
int GetMicrosecondHalfPeriod(float f)
{
  return static_cast<int>(5e5f / f);
}
void ToggleL()
{
  LedStateL = !LedStateL;
  digitalWrite(LedPinL, LedStateL);
  if (UseFlickerTriggers)
    PORTD = LedStateL ? LedRiseTrigger : LedFallTrigger;
}
void ToggleR()
{
  LedStateR = !LedStateR;
  digitalWrite(LedPinR, LedStateR);
}
void ToggleAudio()
{
  AudioState = !AudioState;
  AudioSine.amplitude(AudioState ? AudioAmpOn : AudioAmpOff);
}

// send feedback to the computer
void SendFeedback(Feedback fb)
{
  Serial.write(fb);
}

// helper for conversions
template <typename TNew, typename TOld>
TNew ConvertValue(TOld value)
{
  int count = min(sizeof(TOld), sizeof(TNew));
  TNew result;
  memcpy(&result, &value, count);
  return result;
}

// implement Instruction members (cannot do so in class def)
float Instruction::GetPFloat()             
{ 
  return ConvertValue<float, long>(Parameter);
}
void Instruction::SetPFloat(float p)      
{ 
  Parameter = ConvertValue<long, float>(p);
}
void Instruction::GetP2Short(short s[2])  
{ 
  *reinterpret_cast<long*>(s) = Parameter;
}
void Instruction::SetP2Short(short s[2])  
{ 
  Parameter = *reinterpret_cast<long*>(s); 
} 

// process the instruction
void Instruction::ProcessInstruction()
{
  short triggerValues[2];
  
  switch (Command)
  {
    case Commands::NoOp:
    case Commands::AwaitFullInstructionList:
      return;

    case Commands::FreqFlickerL:
      TimerFlickerL.end();

      // use the trigger
      if (LedStateL && UseFlickerTriggers)
        PORTD = LedFallTrigger;

      // drive line down
      LedStateL = LOW;
      digitalWrite(LedPinL, LedStateL);

      // start again if frequency is non-zero
      if (GetPFloat() > 0)
        TimerFlickerL.begin(ToggleL, GetMicrosecondHalfPeriod(GetPFloat()));
      break;

    case Commands::FreqFlickerR:
      TimerFlickerR.end();
      
      // drive line down
      LedStateR = LOW;
      digitalWrite(LedPinR, LedStateR);

      // start again if frequency is non-zero
      if (GetPFloat() > 0)
        TimerFlickerR.begin(ToggleR, GetMicrosecondHalfPeriod(GetPFloat()));
      break;

    case Commands::FreqFlickerAudio:
      TimerFlickerAudio.end();

      AudioState = LOW;
      AudioSine.amplitude(AudioAmpOff);

      // start again if frequency is non-zero
      if (GetPFloat() > 0)
        TimerFlickerAudio.begin(ToggleAudio, GetMicrosecondHalfPeriod(GetPFloat()));
      break;

    case Commands::FreqToneAudio:
      AudioSine.frequency(GetPFloat());
      break;

    case Commands::EmitTrigger:
      PORTD = (byte)Parameter;
      break;

    case Commands::ChangeFlickerTriggerStateL:
      UseFlickerTriggers = Parameter != 0;
      break;

    case Commands::ChangeFlickerTriggersL:
      
      GetP2Short(triggerValues);
      LedRiseTrigger = triggerValues[0];
      LedFallTrigger = triggerValues[1];
      break;

    case Commands::Sleep:
      delay(Parameter);
      break;

    case Commands::SleepMicroseconds:
      delayMicroseconds(Parameter);
      break;

    case Commands::Reset:
      TimerFlickerL.end();
      TimerFlickerR.end();
      TimerFlickerAudio.end();
      
      digitalWrite(LedPinL, LOW);
      digitalWrite(LedPinR, LOW);
      AudioSine.frequency(10000);
      AudioSine.amplitude(AudioAmpOff);
      PORTD = 0b00000000;
      
      LedStateL = LOW;
      LedStateR = LOW;
      AudioState = LOW;

      UseFlickerTriggers = false;
      LedRiseTrigger = 0;
      LedFallTrigger = 0;
      break;

    case Commands::Feedback:
      SendFeedback((byte)Parameter);
      break;

    default:
      break;
  }
}


void setup() 
{
  pinMode(LedPinL, OUTPUT);
  pinMode(LedPinR, OUTPUT);

  // init port
  for (int i = 0; i < 8; ++i)
    pinMode(i, OUTPUT);

  // init sine
  AudioMemory(8);
  AudioSine.frequency(5000);
  AudioSine.amplitude(AudioAmpOff);
}

void loop() 
{
  Instruction pool[512];
  
  // process messages if available
  if (Serial.available() >= (int)sizeof(Instruction))
  {
    Instruction ins;
    Serial.readBytes(reinterpret_cast<char*>(&ins), sizeof(Instruction));

    // check if multiple instructions should be processed
    if (ins.Command == Instruction::Commands::AwaitFullInstructionList)
    {
      int pooledCount = 0;
      int awaitCount  = (int)ins.Parameter;        
      
      // read all instructions into the pool
      while (pooledCount < awaitCount)
      {
        Serial.readBytes(reinterpret_cast<char*>(pool + pooledCount), sizeof(Instruction));
        pooledCount++;
      }

      // execute instructions one by one
      for (int i = 0; i < awaitCount; ++i)
        pool[i].ProcessInstruction();
    }
    else
    {
      ins.ProcessInstruction();
    }
  }

  // go to sleep
  delay(1);
}
