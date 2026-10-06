#include "transport_protocol.h"
#include <string.h>

// CRC-8/SMBUS implementation
// Polynomial: 0x07 (x^8 + x^2 + x + 1), Init: 0x00, RefIn: false, RefOut: false, XorOut: 0x00
uint8_t Protocol_Crc8(const uint8_t* data, size_t len) {
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

bool Protocol_SerializeClassicCan(const CanMessage* msg, uint8_t* out_buf) {
    if (!msg || !out_buf) return false;

    // Byte 0-1: Magic bytes
    out_buf[0] = 0xAA;
    out_buf[1] = 0x55;

    // Bytes 2-5: timestamp_us Little Endian
    out_buf[2] = (uint8_t)(msg->timestamp_us);
    out_buf[3] = (uint8_t)(msg->timestamp_us >> 8);
    out_buf[4] = (uint8_t)(msg->timestamp_us >> 16);
    out_buf[5] = (uint8_t)(msg->timestamp_us >> 24);

    // Bytes 6-9: CAN ID Little Endian
    out_buf[6] = (uint8_t)(msg->id);
    out_buf[7] = (uint8_t)(msg->id >> 8);
    out_buf[8] = (uint8_t)(msg->id >> 16);
    out_buf[9] = (uint8_t)(msg->id >> 24);

    // Byte 10: DLC (capped at 8 for Classic CAN framing)
    uint8_t dlc = msg->dlc > 8 ? 8 : msg->dlc;
    out_buf[10] = dlc;

    // Byte 11: Flags (0x01=Extended, 0x02=RTR)
    out_buf[11] = msg->flags & 0x03;

    // Bytes 12-19: Data payload (zero padded if dlc < 8)
    memset(&out_buf[12], 0, 8);
    if (dlc > 0) {
        memcpy(&out_buf[12], msg->data, dlc);
    }

    // Byte 20: CRC-8 over bytes 0-19
    out_buf[20] = Protocol_Crc8(out_buf, 20);

    return true;
}

void Protocol_BuildHandshake(uint8_t* out_buf) {
    if (!out_buf) return;
    memset(out_buf, 0, BINARY_USB_FRAME_SIZE);
    out_buf[0] = 0xAA;
    out_buf[1] = 0x55;
    
    // Bytes 2-9: Identifier ASCII ("ESP8266\0" for 100% plug-and-play backend match)
    const char id[8] = {'E', 'S', 'P', '8', '2', '6', '6', '\0'};
    memcpy(&out_buf[2], id, 8);
    
    out_buf[10] = 0xFF; // Universal Non-CAN DLC discriminator (0xFF > 8)
    out_buf[11] = 0x02; // Message Type: Handshake
    out_buf[12] = 0x01; // Protocol Version: 1
    
    out_buf[20] = Protocol_Crc8(out_buf, 20);
}

void Protocol_BuildHeartbeat(uint32_t uptime_ms, uint8_t* out_buf) {
    if (!out_buf) return;
    memset(out_buf, 0, BINARY_USB_FRAME_SIZE);
    out_buf[0] = 0xAA;
    out_buf[1] = 0x55;
    
    // Bytes 2-5: Uptime milliseconds
    out_buf[2] = (uint8_t)(uptime_ms);
    out_buf[3] = (uint8_t)(uptime_ms >> 8);
    out_buf[4] = (uint8_t)(uptime_ms >> 16);
    out_buf[5] = (uint8_t)(uptime_ms >> 24);
    
    out_buf[10] = 0xFF; // Universal Non-CAN DLC discriminator
    out_buf[11] = 0x03; // Message Type: Heartbeat
    out_buf[12] = 0x01; // Protocol Version: 1
    
    out_buf[20] = Protocol_Crc8(out_buf, 20);
}
