#pragma once

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

// Flags bitmask matching platform backend
#define CAN_FLAG_EXTENDED   0x01
#define CAN_FLAG_RTR        0x02
#define CAN_FLAG_FD         0x04
#define CAN_FLAG_BRS        0x08

typedef struct {
    uint32_t id;
    uint8_t  dlc;
    uint8_t  flags;
    uint8_t  data[64];       // Supports both Classic CAN (8) and CAN FD (64)
    uint32_t timestamp_us;   // 32-bit microsecond timestamp
    uint8_t  channel;        // 0=FDCAN1, 1=FDCAN2, 2=FDCAN3
} CanMessage;

#ifdef __cplusplus
}
#endif
