#include <Audio.h>
#include <IntervalTimer.h>



struct Instruction
{
  enum Commands : long
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
        Feedback,                   // send feedback to the computer
        TriggerPinSetting,          // set if feedback should be emitted on trigger pin change
        DigitalWrite,               // write a digital value on pin 19
        ChangePhase                 // set the phase of the flickers (start with HIGH or LOW)
  };

  Commands  Command;
  long      Parameter;
  
  float GetPFloat();
  byte  GetPByte();
  void  SetPFloat(float p);
  void  GetP2Short(short s[2]);
  void  SetP2Short(short s[2]);
  void  ProcessInstruction();
  bool  GetPBool();
};


enum Feedback : byte
{
  OK,
  StimulationComplete,
  Error,
  TriggerPinRise,
  TriggerPinFall
};

enum FlickerTriggerAttach : long
{
  None,
  LedLeftFlicker,
  LedRightFlicker,
  AudioFlicker
};

enum TriggerPinSetting : long
{
    Disabled,
    SerialFeedback,
    TriggerFeedback,
    FullFeedback
};


// objects
IntervalTimer           TimerFlickerL;
IntervalTimer           TimerFlickerR;
IntervalTimer           TimerFlickerAudio;
IntervalTimer           TimerFlickerAll;
AudioSynthWaveformSine  AudioSine;
AudioOutputAnalog       DAC;
AudioConnection         PatchCord(AudioSine, DAC);
const byte              LedPinL             = 17;
const byte              LedPinR             = 16;
const byte              TriggerPin          = 20;
const byte              TTLPin              = 19;
byte                    LedStateL           = LOW;
byte                    LedStateR           = LOW;
byte                    AudioState          = LOW;
float                   AudioAmpOff         = 0;
float                   AudioAmpOn          = 0.2;
byte                    LedRiseTrigger      = 0;
byte                    LedFallTrigger      = 0  ;
float                   DefaultAudioTone    = 10000;
FlickerTriggerAttach    FlickerTriggers     = FlickerTriggerAttach::None;
byte                    Phase               = HIGH;

// helpers
void PutTrigger(byte value)
{
     //PORTD bits 0 and 1 are part of USB serial interface, we skip those instead (only 64 values possible now)
     //PORTD = (value << 2) & 0b11111100;
     PORTD = value;
}
int GetMicrosecondHalfPeriod(float f)
{
  return static_cast<int>(5e5f / f);
}
void ToggleL()
{
  LedStateL = !LedStateL;
  digitalWrite(LedPinL, LedStateL);
  
  if (FlickerTriggers == FlickerTriggerAttach::LedLeftFlicker)
      PutTrigger(LedStateL ? LedRiseTrigger : LedFallTrigger);
}
void ToggleR()
{
  LedStateR = !LedStateR;
  digitalWrite(LedPinR, LedStateR);
  
  if (FlickerTriggers == FlickerTriggerAttach::LedRightFlicker)
    PutTrigger(LedStateR ? LedRiseTrigger : LedFallTrigger);
}
void ToggleAudio()
{
  AudioState = !AudioState;
  AudioSine.amplitude(AudioState ? AudioAmpOn : AudioAmpOff);

  if (FlickerTriggers == FlickerTriggerAttach::AudioFlicker)
    PutTrigger(AudioState ? LedRiseTrigger : LedFallTrigger);
}
void ToggleAll()
{
    LedStateL = !LedStateL;
    LedStateR = !LedStateR;
    AudioState = !AudioState;
    digitalWrite(LedPinL, LedStateL);
    digitalWrite(LedPinR, LedStateR);
    AudioSine.amplitude(AudioState ? AudioAmpOn : AudioAmpOff);

    if (FlickerTriggers == FlickerTriggerAttach::LedLeftFlicker ||
        FlickerTriggers == FlickerTriggerAttach::LedRightFlicker ||
        FlickerTriggers == FlickerTriggerAttach::AudioFlicker)
    {
        PutTrigger(AudioState ? LedRiseTrigger : LedFallTrigger);        
    }
}


// send feedback to the computer
void SendFeedback(byte fb)
{
  Serial.write(fb);
}

// triggered on trigger pin change
void TriggerPinChange()
{
    auto value = digitalRead(TriggerPin);
    if (value == HIGH)
        SendFeedback(Feedback::TriggerPinRise);
    else
        SendFeedback(Feedback::TriggerPinFall);
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
byte Instruction::GetPByte()
{
    return static_cast<byte>(Parameter);
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
bool Instruction::GetPBool()
{
    return Parameter != 0;
}



void _FreqFlickerL(float frequency)
{
  TimerFlickerL.end();

  // use the trigger
  if (LedStateL && FlickerTriggers == FlickerTriggerAttach::LedLeftFlicker)
    PutTrigger(LedFallTrigger);

  // start again if frequency is non-zero
  if (frequency > 0)
  {
    // drive line down
    LedStateL = Phase;
    digitalWrite(LedPinL, LedStateL);
    TimerFlickerL.begin(ToggleL, GetMicrosecondHalfPeriod(frequency));
  }
  else
  {
    LedStateL = LOW;
    digitalWrite(LedPinL, LedStateL);
  }
}

void _FreqFlickerR(float frequency)
{
  TimerFlickerR.end();

  // use the trigger
  if (LedStateR && FlickerTriggers == FlickerTriggerAttach::LedRightFlicker)
    PutTrigger(LedFallTrigger);
  
  // start again if frequency is non-zero
  if (frequency > 0)
  {
    // drive line down
    LedStateR = Phase;
    digitalWrite(LedPinR, LedStateR);
    TimerFlickerR.begin(ToggleR, GetMicrosecondHalfPeriod(frequency));
  }
  else
  {
    LedStateR = LOW;
    digitalWrite(LedPinR, LedStateR);
  }
}

void _FreqFlickerAudio(float frequency)
{
  TimerFlickerAudio.end();

  // use the trigger
  if (AudioState && FlickerTriggers == FlickerTriggerAttach::AudioFlicker)
    PutTrigger(LedFallTrigger);

  // start again if frequency is non-zero
  if (frequency > 0)
  {
    // drive line down
    AudioState = Phase;
    AudioSine.amplitude(Phase == LOW ? AudioAmpOff : AudioAmpOn);
    TimerFlickerAudio.begin(ToggleAudio, GetMicrosecondHalfPeriod(frequency));
  }
  else
  {
    AudioState = LOW;
    AudioSine.amplitude(AudioAmpOff);
  }
}

void _FreqFlickerAll(float frequency)
{
    TimerFlickerL.end();
    TimerFlickerR.end();
    TimerFlickerAudio.end();
    TimerFlickerAll.end();

    if ((LedStateL && FlickerTriggers == FlickerTriggerAttach::LedLeftFlicker) ||
        (LedStateR && FlickerTriggers == FlickerTriggerAttach::LedRightFlicker) ||
        (AudioState && FlickerTriggers == FlickerTriggerAttach::AudioFlicker))
    {
        PutTrigger(Phase == LOW ? LedFallTrigger : LedRiseTrigger);        
    }

    if (frequency > 0)
    {
      LedStateL = Phase;
      LedStateR = Phase;
      AudioState = Phase;

      digitalWrite(LedPinL, LedStateL);
      digitalWrite(LedPinR, LedStateR);
      AudioSine.amplitude(Phase == LOW ? AudioAmpOff : AudioAmpOn);

      TimerFlickerAll.begin(ToggleAll, GetMicrosecondHalfPeriod(frequency));
    }
    else
    {
      LedStateL = LOW;
      LedStateR = LOW;
      AudioState = LOW;
      digitalWrite(LedPinL, LedStateL);
      digitalWrite(LedPinR, LedStateR);
      AudioSine.amplitude(AudioAmpOff);
    }
}

void _Reset()
{
  TimerFlickerL.end();
  TimerFlickerR.end();
  TimerFlickerAudio.end();
  TimerFlickerAll.end();
  
  digitalWrite(LedPinL, LOW);
  digitalWrite(LedPinR, LOW);
  AudioSine.frequency(10000);
  AudioSine.amplitude(AudioAmpOff);
  PutTrigger(0);
  
  LedStateL = LOW;
  LedStateR = LOW;
  AudioState = LOW;
  
  FlickerTriggers = FlickerTriggerAttach::None;
  LedRiseTrigger = 0;
  LedFallTrigger = 0;

  detachInterrupt(digitalPinToInterrupt(TriggerPin));
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

    // FLICKER LEFT
    case Commands::FreqFlickerL:
        _FreqFlickerL(GetPFloat());
        break;

    // FLICKER RIGHT
    case Commands::FreqFlickerR:
        _FreqFlickerR(GetPFloat());
        break;

    // FLICKER LED
    case Commands::FreqFlickerLed:
        _FreqFlickerL(GetPFloat());
	    _FreqFlickerR(GetPFloat());
        break;

    // FLICKER AUDIO
    case Commands::FreqFlickerAudio:
        _FreqFlickerAudio(GetPFloat());
        break;

    // FLICKER ALL
    case Commands::FreqFlickerAll:
        _FreqFlickerAll(GetPFloat());
        break;

    // TONE FREQUENCY
    case Commands::FreqToneAudio:
        AudioSine.frequency(GetPFloat());
        break;

    // EMIT TRIGGER
    case Commands::EmitTrigger:
        PutTrigger((byte)Parameter);
        break;

    // CHANGE FLICKER TRIGGER ATTACH
    case Commands::ChangeFlickerTriggerAttach:
        FlickerTriggers = (FlickerTriggerAttach)Parameter;
        break;

    // CHANGE FLICKER TRIGGERS
    case Commands::ChangeFlickerTriggers:
        GetP2Short(triggerValues);
        LedRiseTrigger = triggerValues[0];
        LedFallTrigger = triggerValues[1];
        break;

    // SLEEP
    case Commands::Sleep:
        delay(Parameter);
        break;

    // SLEEP MICROSECONDS
    case Commands::SleepMicroseconds:
        delayMicroseconds(Parameter);
        break;

    // RESET
    case Commands::Reset:
        _Reset();
        break;

    // FEEDBACK
    case Commands::Feedback:
        SendFeedback(GetPByte());
        break;

    // TRIGGER PIN SETTING
    case Commands::TriggerPinSetting:
        if (GetPBool())
            attachInterrupt(digitalPinToInterrupt(TriggerPin), TriggerPinChange, CHANGE);
        else
            detachInterrupt(digitalPinToInterrupt(TriggerPin));
        break;

    case Commands::DigitalWrite:
        if (GetPBool())
            digitalWrite(TTLPin, HIGH);
        else
            digitalWrite(TTLPin, LOW);
        break;

    case Commands::ChangePhase:


    // DEFAULT
    default:
        break;
  }
}






void setup() 
{
  pinMode(LedPinL, OUTPUT);
  pinMode(LedPinR, OUTPUT);
  pinMode(TriggerPin, INPUT_PULLDOWN);
  pinMode(TTLPin, OUTPUT);

  // init port
  for (int i = 0; i < 8; ++i)
    pinMode(i, OUTPUT);

  // init sine
  AudioMemory(8);
  AudioSine.frequency(10000);
  AudioSine.amplitude(AudioAmpOff);
  //AudioSine.amplitude(AudioAmpOn);

  digitalWrite(LedPinL, HIGH);
  digitalWrite(LedPinR, HIGH);
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
