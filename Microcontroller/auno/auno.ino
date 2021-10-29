#define USE_TIMER_1               true
#include <TimerInterrupt.h>

#define MESSAGE_SIZE              3
#define MESSAGE_BUFFER_BRIG_POS   0
#define MESSAGE_BUFFER_FREQ_POS   1
#define MESSAGE_BUFFER_TRIG_POS   2
#define LED_OFF                   0x0
#define LED_ON                    0x1

#define MESSAGE_VAL_IGNORE        0xFF
#define uint                      unsigned int

int       TargetPin           = 5;
int       Brightness          = 0;
byte      MessageBuffer       [MESSAGE_SIZE];
bool      NewMessage          = false;
byte      LedState            = LOW;
uint      Counter             = 0;
uint      Period              = 0;

// interrupt routine
void OnTimerInterrupt()
{
  if (NewMessage)
  {
    // set the new brightness (if a new one is specified)
    if (MessageBuffer[MESSAGE_BUFFER_BRIG_POS] != MESSAGE_VAL_IGNORE)
      Brightness = map(MessageBuffer[MESSAGE_BUFFER_BRIG_POS], 0, 100, 0, 255);
    
    // set the new frequency (if a new one is specified)
    if (MessageBuffer[MESSAGE_BUFFER_FREQ_POS] != MESSAGE_VAL_IGNORE)
    {
      Period          = round(1000.f / MessageBuffer[MESSAGE_BUFFER_FREQ_POS]) / 2;
      Counter         = Period;
      LedState        = LOW;
    }

    // set the new trigger (if a new one is specified)
    if (MessageBuffer[MESSAGE_BUFFER_TRIG_POS] != MESSAGE_VAL_IGNORE)
      PORTB = MessageBuffer[MESSAGE_BUFFER_TRIG_POS] & 0b00111111;

    NewMessage = false;
  }

  // just stay shut if frequency = 0
  if (Period == 0)
  {
    digitalWrite(TargetPin, LOW);
    return;    
  }

  // perform down count, switch the pin when counter is zero
  if (Counter > 0) Counter--;
  else
  {
    LedState = !LedState;
    if (LedState)
      //analogWrite(TargetPin, Brightness);
      digitalWrite(TargetPin, HIGH);
    else
      digitalWrite(TargetPin, LOW);
    Counter = Period / 2;
  }
}

void setup() 
{
  // start serial
  Serial.begin(9600);
  
  // pinmode for the target pin
  pinMode(TargetPin, OUTPUT);

  // default values
  MessageBuffer[MESSAGE_BUFFER_BRIG_POS] = 1;         // 10% brightness
  MessageBuffer[MESSAGE_BUFFER_FREQ_POS] = 0;         // 2Hz frequency
  MessageBuffer[MESSAGE_BUFFER_TRIG_POS] = 0b000000;  // trigger code 0
  NewMessage = true;

  // init port d
  DDRB  = 0b111111;
  PORTB = 0b000000;
  
  // init timer
  ITimer1.init();
  ITimer1.attachInterruptInterval(1, OnTimerInterrupt);
}

void loop() 
{
  // put your main code here, to run repeatedly:
  if (Serial && Serial.available() >= MESSAGE_SIZE)
  {
    // read the message buffer
    Serial.readBytes(MessageBuffer, MESSAGE_SIZE);
    NewMessage = true;
  }
  
  // go to sleep for 1 millisecond
  delay(1);
}
