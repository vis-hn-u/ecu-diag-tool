#include "transport_protocol.h"

#include <stdio.h>
#include <string.h>

bool TransportProtocol::serializeBinary(const CanFrame& frame, BinaryCanPacket& packet) {
    if (!isValidCanFrame(frame)) {
        return false;
    }

    memset(packet.bytes, 0, sizeof(packet.bytes));
    packet.bytes[0] = 0xAA;
    packet.bytes[1] = 0x55;
    writeLittleEndian32(&packet.bytes[2], frame.timestamp_us);
    writeLittleEndian32(&packet.bytes[6], frame.id);
    packet.bytes[10] = frame.dlc;
    packet.bytes[11] = frame.flags;
    memcpy(&packet.bytes[12], frame.data, frame.dlc);
    return true;
}

size_t TransportProtocol::serializeJson(const CanFrame& frame, char* out_buf, size_t max_len) {
    if (!isValidCanFrame(frame) || out_buf == nullptr || max_len == 0) {
        return 0;
    }

    static const char kHex[] = "0123456789ABCDEF";
    char hex_data[17] = {};
    for (uint8_t i = 0; i < frame.dlc; ++i) {
        hex_data[i * 2] = kHex[(frame.data[i] >> 4) & 0x0F];
        hex_data[i * 2 + 1] = kHex[frame.data[i] & 0x0F];
    }

    const int written = snprintf(
        out_buf, max_len,
        "{\"ts\":%lu,\"id\":%lu,\"ext\":%s,\"rtr\":%s,"
        "\"dlc\":%u,\"data\":\"%s\"}",
        static_cast<unsigned long>(frame.timestamp_us),
        static_cast<unsigned long>(frame.id),
        isExtendedFrame(frame) ? "true" : "false",
        isRtrFrame(frame) ? "true" : "false",
        static_cast<unsigned int>(frame.dlc), hex_data);
    return written >= 0 && static_cast<size_t>(written) < max_len
               ? static_cast<size_t>(written)
               : 0;
}

void TransportProtocol::writeLittleEndian32(uint8_t* destination, uint32_t value) {
    destination[0] = static_cast<uint8_t>(value);
    destination[1] = static_cast<uint8_t>(value >> 8);
    destination[2] = static_cast<uint8_t>(value >> 16);
    destination[3] = static_cast<uint8_t>(value >> 24);
}

uint8_t UsbSerialProtocol::crc8(const uint8_t* data, size_t len) {
    uint8_t crc = 0x00;
    for (size_t i = 0; i < len; i++) {
        crc ^= data[i];
        for (uint8_t bit = 0; bit < 8; bit++) {
            if (crc & 0x80) {
                crc = (crc << 1) ^ 0x07;
            } else {
                crc <<= 1;
            }
        }
    }
    return crc;
}

#ifdef ARDUINO
#include <Arduino.h>

void UsbSerialProtocol::writeCanFrame(const CanFrame& frame) {
    BinaryCanPacket packet = {};
    if (!TransportProtocol::serializeBinary(frame, packet)) return;
    uint8_t buf[21];
    memcpy(buf, packet.bytes, 20);
    buf[20] = crc8(buf, 20);
    Serial.write(buf, 21);
}

void UsbSerialProtocol::writeHandshake() {
    uint8_t buf[21] = {0};
    buf[0] = 0xAA;
    buf[1] = 0x55;
    const char id[8] = {'E', 'S', 'P', '8', '2', '6', '6', '\0'};
    memcpy(&buf[2], id, 8);
    buf[10] = 0xFF; // Universal Non-CAN DLC discriminator (DLC = 0xFF > 8)
    buf[11] = 0x02; // Message Type: Handshake
    buf[12] = 0x01; // Protocol Version: 1
    // buf[13..19] initialized to 0
    buf[20] = crc8(buf, 20);
    Serial.write(buf, 21);
}

void UsbSerialProtocol::writeHeartbeat(uint8_t mcp_ready, uint8_t canstat, uint8_t eflg, uint8_t rec, uint8_t tec, uint8_t baud_code, uint8_t diag_flags) {
    uint8_t buf[21] = {0};
    buf[0] = 0xAA;
    buf[1] = 0x55;
    uint32_t now = millis();
    buf[2] = static_cast<uint8_t>(now);
    buf[3] = static_cast<uint8_t>(now >> 8);
    buf[4] = static_cast<uint8_t>(now >> 16);
    buf[5] = static_cast<uint8_t>(now >> 24);
    // buf[6..9] initialized to 0
    buf[10] = 0xFF; // Universal Non-CAN DLC discriminator (DLC = 0xFF > 8)
    buf[11] = 0x03; // Message Type: Heartbeat
    buf[12] = 0x01; // Protocol Version: 1
    buf[13] = mcp_ready;
    buf[14] = canstat;
    buf[15] = eflg;
    buf[16] = rec;
    buf[17] = tec;
    buf[18] = baud_code;
    buf[19] = diag_flags;
    buf[20] = crc8(buf, 20);
    Serial.write(buf, 21);
}
#endif
