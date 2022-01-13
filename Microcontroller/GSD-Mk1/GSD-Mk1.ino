#define USE_TIMER_2               true
#include <TimerInterrupt.h>


struct Settings
{
  static const byte Ignore = 0xFF;
  
  byte Brightness;
  byte Frequency;
  byte Trigger;
};

int   TargetPin = 5;
byte  LedState  = LOW;

// interrupt routine
void OnTimerInterrupt()
{
  LedState = !LedState;
  digitalWrite(TargetPin, LedState);
}

// process an incoming message
void ProcessMessage(const Settings& msg)
{  
    // set the new frequency (if a new one is specified)
    if (msg.Frequency != Settings::Ignore)
    {
      ITimer2.detachInterrupt();
      LedState = LOW;
      digitalWrite(TargetPin, LedState);
        
      if (msg.Frequency > 0)
      {
        // attach the interrupt
        ITimer2.attachInterrupt(msg.Frequency * 2, OnTimerInterrupt);
      }
    }

    // set the new trigger (if a new one is specified)
    if (msg.Trigger != Settings::Ignore)
      PORTB = msg.Trigger & 0b00111111;
}

void setup() 
{
  Serial.begin(9600);
  pinMode(TargetPin, OUTPUT);
  ITimer2.init();

  // initialize settings
  Settings s;
  s.Frequency = 0;
  s.Trigger   = 0b000000;
  
  // process the settings
  ProcessMessage(s);
}

void loop() 
{
  // put your main code here, to run repeatedly:
  if (Serial && Serial.available() >= sizeof(Settings))
  {
    // read the message buffer
    Settings s;
    Serial.readBytes(reinterpret_cast<uint8_t*>(&s), sizeof(Settings));
    ProcessMessage(s);
  }
  
  // go to sleep for 1 millisecond
  delay(1);
}
