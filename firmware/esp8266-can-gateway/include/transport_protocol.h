#pragma once

#include <cstddef>
#include <cstdint>
#include "can_frame.h"

// Binary high-rate format: AA 55, timestamp LE (4), CAN ID LE (4), DLC,
// flags, data[8]. All packets are exactly 20 bytes, independent of host ABI.
constexpr size_t BINARY_CAN_PACKET_SIZE = 20;

struct BinaryCanPacket {
    uint8_t bytes[BINARY_CAN_PACKET_SIZE];
};

static_assert(sizeof(BinaryCanPacket) == BINARY_CAN_PACKET_SIZE,
              "Binary CAN transport must remain 20 bytes");

class TransportProtocol {
public:
    static bool serializeBinary(const CanFrame& frame, BinaryCanPacket& packet);
    static size_t serializeJson(const CanFrame& frame, char* out_buf, size_t max_len);

private:
    static void writeLittleEndian32(uint8_t* destination, uint32_t value);
};

// USB Serial Protocol framing: 21 bytes [0xAA, 0x55, 18-byte payload, byte 20: CRC-8].
// CRC algorithm: CRC-8/SMBUS-compatible parameters:
//   Polynomial: 0x07 (x^8 + x^2 + x + 1)
//   Initial Value: 0x00
//   RefIn: false
//   RefOut: false
//   XorOut: 0x00
//   Coverage: Bytes 0 through 19 (first 20 bytes of frame)
//   Check value ("123456789"): 0xF4
class UsbSerialProtocol {
public:
    static uint8_t crc8(const uint8_t* data, size_t len);
    static void writeCanFrame(const CanFrame& frame);
    static void writeHandshake();
    static void writeHeartbeat(uint8_t mcp_ready = 0, uint8_t canstat = 0, uint8_t eflg = 0, uint8_t rec = 0, uint8_t tec = 0, uint8_t baud_code = 0, uint8_t diag_flags = 0);
};
