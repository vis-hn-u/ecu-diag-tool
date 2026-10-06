#pragma once

#include <stdint.h>
#include <stdbool.h>
#include "can_frame.h"

#ifdef __cplusplus
extern "C" {
#endif

// Ring buffer capacity (allocated in fast DTCM RAM)
#define FDCAN_RX_RING_BUFFER_SIZE 1024

// Operating modes for hardware validation
typedef enum {
    FDCAN_OPMODE_NORMAL = 0,            // Normal TX/RX on physical pins
    FDCAN_OPMODE_LISTEN_ONLY = 1,        // True Bus Monitoring: TX held recessive, no ACKs, no error frames
    FDCAN_OPMODE_INTERNAL_LOOPBACK = 2,  // Silicon self-test: TX disconnected from pin, looped internally
    FDCAN_OPMODE_EXTERNAL_LOOPBACK = 3   // External loopback: TX connected to pin & looped to RX
} FdcanOperatingMode;

// Initialize FDCAN1 on STM32H723ZG with specified operating mode
// Default: 500 kbps nominal, 2 Mbps data phase (80 MHz kernel clock)
bool FDCAN1_Driver_Init(FdcanOperatingMode mode);

// Non-blocking dequeue of a received frame
bool FDCAN1_Driver_ReadFrame(CanMessage* out_msg);

// Transmit a frame via FDCAN1 (will return false in LISTEN_ONLY mode)
bool FDCAN1_Driver_Transmit(const CanMessage* msg);

// Driver telemetry
typedef struct {
    uint32_t rx_frames;
    uint32_t tx_frames;
    uint32_t dropped_frames;
    uint32_t bus_errors;
    FdcanOperatingMode current_mode;
    bool is_listen_only;
} FdcanStats;

void FDCAN1_Driver_GetStats(FdcanStats* out_stats);

#ifdef __cplusplus
}
#endif
