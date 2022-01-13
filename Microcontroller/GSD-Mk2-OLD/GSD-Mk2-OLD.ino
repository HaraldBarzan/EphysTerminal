#include <Audio.h>
#include <IntervalTimer.h>

// message struct
struct Message
{
  // stimulation parameters to change
  enum Actions : byte 
  {
    actNone             = 0,
    actFrequencyL       = 1 << 0,
    actFrequencyR       = 1 << 1,
    actTrigger          = 1 << 2,
    actFrequencyAudio   = 1 << 3,
    actAll              = actFrequencyL | actFrequencyR | actTrigger | actFrequencyAudio
  };

  // stimulation parameters
  Actions Action;
  byte    FrequencyL;
  byte    FrequencyR;
  byte    Trigger;
  float   FrequencyAudio;

  void ProcessMessage();
};

// objects
IntervalTimer           TimerL;
IntervalTimer           TimerR;
AudioSynthWaveformSine  AudioSine;
AudioOutputAnalog       DAC;
AudioConnection         PatchCord(AudioSine, DAC);

// fields
const byte  LedPinL      = 17;
const byte  LedPinR      = 16;
byte        LedStateL    = LOW;
byte        LedStateR    = LOW;

// helpers
int GetMicrosecondHalfPeriod(byte f)
{
  return static_cast<int>(5e5f / static_cast<float>(f));
}
void ToggleL()
{
  LedStateL = !LedStateL;
  digitalWrite(LedPinL, LedStateL);
}
void ToggleR()
{
  LedStateR = !LedStateR;
  digitalWrite(LedPinR, LedStateR);
}


void Message::ProcessMessage()
{
  if (!(Action && Actions::actAll)) 
    return;

  if (Action & Actions::actFrequencyL)
  {
    TimerL.end();
    LedStateL = LOW;
    digitalWrite(LedPinL, LOW);
    if (FrequencyL > 0) 
      TimerL.begin(ToggleL, GetMicrosecondHalfPeriod(FrequencyL)); 
  }

  if (Action & Actions::actFrequencyR)
  {
    TimerR.end();
    LedStateR = LOW;
    digitalWrite(LedPinR, LOW);
    if (FrequencyR > 0)
      TimerR.begin(ToggleR, GetMicrosecondHalfPeriod(FrequencyR));
  }

  if (Action & Actions::actTrigger)
    PORTD = Trigger;

  if (Action & Actions::actFrequencyAudio)
  {
    AudioSine.frequency(0);
    if (FrequencyAudio > 0 && FrequencyAudio < 2e4)
      AudioSine.frequency(FrequencyAudio);
  }
}


void setup() 
{
  AudioMemory(8);
  AudioSine.amplitude(0.2);
  pinMode(LedPinL, OUTPUT);
  pinMode(LedPinR, OUTPUT);

  // init port
  for (int i = 0; i < 8; ++i)
    pinMode(i, OUTPUT);

  // preinit
  Message msg;
  msg.Action          = Message::Actions::actAll;
  msg.FrequencyL      = 0;
  msg.FrequencyR      = 0;
  msg.Trigger         = 0b00000000;
  msg.FrequencyAudio  = 0;
  msg.ProcessMessage();
}

void loop() 
{
  // process messages if available
  if (Serial.available() >= (int)sizeof(Message))
  {
    Message msg;
    Serial.readBytes(reinterpret_cast<char*>(&msg), sizeof(Message));
    msg.ProcessMessage();
  }

  // go to sleep
  delay(1);
}
