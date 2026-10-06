#include <Arduino.h>
#include "fdcan_driver.h"
#include "transport_protocol.h"

// Telemetry & Intervals
static uint32_t s_last_heartbeat_ms = 0;
static constexpr uint32_t HEARTBEAT_INTERVAL_MS = 1000;
static constexpr size_t MAX_FRAMES_PER_SERVICE = 64;

#ifndef FDCAN_DEFAULT_MODE
#define FDCAN_DEFAULT_MODE FDCAN_OPMODE_LISTEN_ONLY
#endif

void setup() {
    // Initialize Virtual COM Port (ST-LINK V3 or Native USB CDC)
    Serial.begin(921600);

    // Initialize Onboard User LED (Green LD1 on NUCLEO-H723ZG is PB0)
    pinMode(LED_BUILTIN, OUTPUT);
    digitalWrite(LED_BUILTIN, HIGH);

    // Initialize Bosch FDCAN1 (PD0=RX CN11 Pin 57, PD1=TX CN11 Pin 55)
    // Default: FDCAN_OPMODE_LISTEN_ONLY (Bus Monitoring Mode - TX held recessive)
    bool fdcan_ok = FDCAN1_Driver_Init(FDCAN_DEFAULT_MODE);
    if (!fdcan_ok) {
        // Fast blink on error
        for (int i = 0; i < 10; i++) {
            digitalWrite(LED_BUILTIN, !digitalRead(LED_BUILTIN));
            delay(50);
        }
    }

    // Send initial handshake frame to identify gateway to backend
    uint8_t handshake_buf[BINARY_USB_FRAME_SIZE];
    Protocol_BuildHandshake(handshake_buf);
    Serial.write(handshake_buf, BINARY_USB_FRAME_SIZE);

    s_last_heartbeat_ms = millis();
}

void loop() {
    // 1. Service Incoming CAN Frames from Ring Buffer
    CanMessage msg;
    size_t processed = 0;
    uint8_t tx_wire_buf[BINARY_USB_FRAME_SIZE];

    while (processed < MAX_FRAMES_PER_SERVICE && FDCAN1_Driver_ReadFrame(&msg)) {
        if (Protocol_SerializeClassicCan(&msg, tx_wire_buf)) {
            Serial.write(tx_wire_buf, BINARY_USB_FRAME_SIZE);
            processed++;
        }
    }

    // Toggle LED on active traffic
    if (processed > 0) {
        digitalWrite(LED_BUILTIN, !digitalRead(LED_BUILTIN));
    }

    // 2. Periodic Gateway Heartbeat (Keepalive for backend connection watchdog)
    uint32_t now = millis();
    if (now - s_last_heartbeat_ms >= HEARTBEAT_INTERVAL_MS) {
        s_last_heartbeat_ms = now;
        uint8_t heartbeat_buf[BINARY_USB_FRAME_SIZE];
        Protocol_BuildHeartbeat(now, heartbeat_buf);
        Serial.write(heartbeat_buf, BINARY_USB_FRAME_SIZE);
    }
}
