#pragma once

#include <cstddef>
#include <cstdint>
#include "wifi_credentials.h"

// ESP8266 HSPI wiring. CS is software-controlled and intentionally avoids
// GPIO15/D8, which is a boot-strap pin that must be held low at reset.
constexpr uint8_t PIN_CAN_SCK = 14;   // D5 (GPIO14, Hardware HSPI SCK)
constexpr uint8_t PIN_CAN_MISO = 12;  // D6 (GPIO12, Hardware HSPI MISO)
constexpr uint8_t PIN_CAN_MOSI = 13;  // D7 (GPIO13, Hardware HSPI MOSI)
constexpr uint8_t PIN_CAN_CS = 4;     // D2 (GPIO4, Software CS)
constexpr uint8_t PIN_CAN_INT = 255;  // 255 = Unused/disconnected in polling mode (legacy: D1 / GPIO5)

// Compile-time switch for MCP2515 receive architecture:
// false = High-performance SPI status polling (default, uses 4 level-shifter channels: SCK, MISO, MOSI, CS)
// true  = Interrupt-driven reception (requires 5th level-shifter channel for INT pin)
constexpr bool MCP2515_USE_INTERRUPT = false;

enum class McpOscillatorFreq : uint8_t {
    OSC_8MHZ = 8,
    OSC_16MHZ = 16
};

enum class CanBitRate : uint32_t {
    CAN_125KBPS = 125000,
    CAN_250KBPS = 250000,
    CAN_500KBPS = 500000,
    CAN_1000KBPS = 1000000
};

constexpr McpOscillatorFreq CAN_OSC_FREQ = McpOscillatorFreq::OSC_8MHZ;
constexpr CanBitRate CAN_BUS_SPEED = CanBitRate::CAN_500KBPS;
constexpr uint32_t SPI_CLOCK_SPEED = 1000000UL;

// Standalone bench testing requires CAN ACK generation (Normal mode) so the ECU does not enter BUS-OFF.
constexpr bool CAN_LISTEN_ONLY_DEFAULT = false;
constexpr bool CAN_RUN_LOOPBACK_SELF_TEST = false;
constexpr uint8_t CAN_DRAIN_YIELD_INTERVAL = 4;
constexpr uint8_t CAN_MAX_FRAMES_PER_SERVICE = 32;
constexpr uint32_t CAN_RECOVERY_BACKOFF_MS = 1000;

constexpr size_t CAN_RING_BUFFER_SIZE = 128;

constexpr uint16_t WEBSOCKET_PORT = 8088;
constexpr uint32_t WIFI_CONNECT_TIMEOUT_MS = 15000;
constexpr uint32_t WIFI_RECONNECT_INTERVAL_MS = 30000;

constexpr uint32_t USB_SERIAL_BAUD = 921600UL;
constexpr uint32_t USB_HEARTBEAT_INTERVAL_MS = 1000UL;
constexpr uint32_t USB_HANDSHAKE_INTERVAL_MS = 2000UL;
