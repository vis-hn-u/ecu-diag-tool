#pragma once

#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>
#include "can_frame.h"

#ifdef __cplusplus
extern "C" {
#endif

// 21-byte USB Serial wire protocol matching platform backend:
// Byte 0: 0xAA (magic)
// Byte 1: 0x55 (magic)
// Byte 2-5: timestamp_us LE uint32
// Byte 6-9: CAN ID LE uint32
// Byte 10: DLC (0..8 for Classic CAN)
// Byte 11: flags (0x01=Extended, 0x02=RTR)
// Byte 12-19: 8 bytes data payload
// Byte 20: CRC-8 (SMBUS poly 0x07, init 0x00 over bytes 0..19)
#define BINARY_USB_FRAME_SIZE 21

uint8_t Protocol_Crc8(const uint8_t* data, size_t len);
bool    Protocol_SerializeClassicCan(const CanMessage* msg, uint8_t* out_buf);
void    Protocol_BuildHandshake(uint8_t* out_buf);
void    Protocol_BuildHeartbeat(uint32_t uptime_ms, uint8_t* out_buf);

#ifdef __cplusplus
}
#endif
