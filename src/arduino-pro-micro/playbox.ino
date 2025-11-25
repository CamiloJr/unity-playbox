/*
  File        : playbox.ino
  Author      : Camilo Barreto
  GitHub      : camilojr
  Version     : 1.0.0
  Target HW   : Arduino Pro Micro (ATmega32U4)

  Description : Main sketch for the PlayBox project.
                Controls playback via push buttons, indicates status with an LED,
                and communicates over Bluetooth using an HC-05/HC-06 via Serial1.


  Pin Connections & Functionality

  Push Buttons (use INPUT_PULLUP; active LOW):
    - PIN  2 : Play/Stop button
    - PIN  3 : Restart button
    - PIN 15 : Pause button

  LED:
    - PIN 14 : System running (ON while system is active)
               Blinks briefly whenever any button is pressed

  Serial Communication with HC-05/HC-06 (Bluetooth):
    - Interface : Serial1  (Hardware UART on Pro Micro)
    - Wiring    :
        Arduino TX  (Serial1 TX)  -->  HC-05 RX
        Arduino RX  (Serial1 RX)  <--  HC-05 TX

    Note: On Arduino Pro Micro, Serial1 pins are:
          TX = TXO (D1), RX = RXI (D0).
          Cross TX->RX and RX<-TX between Arduino and the HC-05/06.

  Notes:
    - Verify "Arduino Pro Micro" board selection and correct COM port.
    - If buttons use INPUT_PULLUP, wire each button to GND on press.
    - Use a suitable series resistor if using an external LED.
*/


const uint8_t BTN_COUNT = 3;
const uint8_t BTN_PINS[BTN_COUNT]   = {2, 3, 15};
const char*   BTN_LABELS[BTN_COUNT] = {"down_a", "down_b", "down_c"};

const uint8_t ACTIVE_LEVEL = LOW;
const unsigned long DEBOUNCE_MS = 25;

const uint8_t LED_PIN = 14;
const unsigned long PRESS_BLINK_MS = 60;
unsigned long ledBlinkUntil = 0;

uint8_t lastReading[BTN_COUNT];
uint8_t debouncedState[BTN_COUNT];
unsigned long lastChangeMs[BTN_COUNT];

void sendEvent(const char* msg) {
  Serial1.println(msg);
  Serial.println(msg);  //echo
}

void setup() {
  Serial.begin(115200);

  unsigned long t0 = millis();   // timeout
  while (!Serial && (millis() - t0 < 1500)) { }

  // Linvor/HC-05 default bound: 9600
  Serial1.begin(9600);

  pinMode(LED_PIN, OUTPUT);
  for (int i = 0; i < 4; i++) {
    digitalWrite(LED_PIN, HIGH);
    delay(120);
    digitalWrite(LED_PIN, LOW);
    delay(120);
  }
  digitalWrite(LED_PIN, HIGH);


  for (uint8_t i = 0; i < BTN_COUNT; i++) {
    pinMode(BTN_PINS[i], INPUT_PULLUP);
    uint8_t r = digitalRead(BTN_PINS[i]);
    lastReading[i]    = r;
    debouncedState[i] = r;
    lastChangeMs[i]   = millis();
  }

  Serial.println("Ok: send by bluetooth (Serial1).");
}

void loop() {
  unsigned long now = millis();

  if (ledBlinkUntil && (long)(now - ledBlinkUntil) >= 0) {
    digitalWrite(LED_PIN, HIGH);
    ledBlinkUntil = 0;
  }

  for (uint8_t i = 0; i < BTN_COUNT; i++) {
    uint8_t r = digitalRead(BTN_PINS[i]);

    if (r != lastReading[i]) {
      lastReading[i]  = r;
      lastChangeMs[i] = now;
    }

    if ((now - lastChangeMs[i]) > DEBOUNCE_MS && debouncedState[i] != r) {
      debouncedState[i] = r;

      if (debouncedState[i] == ACTIVE_LEVEL) {
        sendEvent(BTN_LABELS[i]); // down_a/b/c

        digitalWrite(LED_PIN, LOW);           
        ledBlinkUntil = now + PRESS_BLINK_MS; 
      }
    }
  }
}

