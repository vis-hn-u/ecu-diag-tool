#pragma once

#include <cstdint>
#include "config.h"

namespace McpRegisters {
constexpr uint8_t CANSTAT = 0x0E;
constexpr uint8_t CANCTRL = 0x0F;
constexpr uint8_t TEC = 0x1C;
constexpr uint8_t REC = 0x1D;
constexpr uint8_t CNF3 = 0x28;
constexpr uint8_t CNF2 = 0x29;
constexpr uint8_t CNF1 = 0x2A;
constexpr uint8_t CANINTE = 0x2B;
constexpr uint8_t CANINTF = 0x2C;
constexpr uint8_t EFLG = 0x2D;

constexpr uint8_t RXF0SIDH = 0x00;
constexpr uint8_t RXF1SIDH = 0x04;
constexpr uint8_t RXF2SIDH = 0x08;
constexpr uint8_t RXF3SIDH = 0x10;
constexpr uint8_t RXF4SIDH = 0x14;
constexpr uint8_t RXF5SIDH = 0x18;
constexpr uint8_t RXM0SIDH = 0x20;
constexpr uint8_t RXM1SIDH = 0x24;

constexpr uint8_t TXB0CTRL = 0x30;
constexpr uint8_t TXB0SIDH = 0x31;
constexpr uint8_t RXB0CTRL = 0x60;
constexpr uint8_t RXB0SIDH = 0x61;
constexpr uint8_t RXB1CTRL = 0x70;
constexpr uint8_t RXB1SIDH = 0x71;

constexpr uint8_t CMD_RESET = 0xC0;
constexpr uint8_t CMD_READ = 0x03;
constexpr uint8_t CMD_WRITE = 0x02;
constexpr uint8_t CMD_READ_STATUS = 0xA0;
constexpr uint8_t CMD_RX_STATUS = 0xB0;
constexpr uint8_t CMD_RTS_TXB0 = 0x81;
constexpr uint8_t CMD_BIT_MODIFY = 0x05;

constexpr uint8_t MODE_NORMAL = 0x00;
constexpr uint8_t MODE_SLEEP = 0x20;
constexpr uint8_t MODE_LOOPBACK = 0x40;
constexpr uint8_t MODE_LISTENONLY = 0x60;
constexpr uint8_t MODE_CONFIG = 0x80;
constexpr uint8_t MODE_MASK = 0xE0;

constexpr uint8_t INT_RX0IF = 0x01;
constexpr uint8_t INT_RX1IF = 0x02;
constexpr uint8_t INT_ERRIF = 0x20;
constexpr uint8_t INT_MERRF = 0x80;
constexpr uint8_t INT_ERROR_MASK = INT_ERRIF | INT_MERRF;

constexpr uint8_t INTE_RX0IE = 0x01;
constexpr uint8_t INTE_RX1IE = 0x02;
constexpr uint8_t INTE_ERRIE = 0x20;
constexpr uint8_t INTE_MERRE = 0x80;

constexpr uint8_t EFLG_RX1OVR = 0x80;
constexpr uint8_t EFLG_RX0OVR = 0x40;
constexpr uint8_t EFLG_TXBO = 0x20;
constexpr uint8_t EFLG_RXEP = 0x08;
constexpr uint8_t EFLG_TXEP = 0x10;
constexpr uint8_t EFLG_RX_WARNING = 0x02;
constexpr uint8_t EFLG_TX_WARNING = 0x04;
constexpr uint8_t EFLG_OVERFLOW_MASK = EFLG_RX1OVR | EFLG_RX0OVR;

constexpr uint8_t RXB_RX_ANY = 0x60;
constexpr uint8_t RXB_BUKT = 0x04;
}

struct McpBitTiming {
    uint8_t cnf1;
    uint8_t cnf2;
    uint8_t cnf3;
    uint8_t tq_per_bit;
    bool supported;
};

namespace McpBitTimings {
// TQ = 2 * (BRP + 1) / FOSC. All use BTLMODE=1, SAM=1, 75% sample point (Cory Fowler automotive standard).
// 125k: BRP=1, 16 TQ, Sync=1 Prop=6 PS1=5 PS2=4, sample 75%, 3x sampling.
constexpr McpBitTiming k8MHz125K = {0x81, 0xE5, 0x83, 16, true};
// 250k: BRP=0, 16 TQ, Sync=1 Prop=6 PS1=5 PS2=4, sample 75%, 3x sampling.
constexpr McpBitTiming k8MHz250K = {0x80, 0xE5, 0x83, 16, true};
// 500k: BRP=0, 8 TQ, Sync=1 Prop=2 PS1=3 PS2=2, sample 75%, 3x sampling.
constexpr McpBitTiming k8MHz500K = {0x00, 0xD1, 0x81, 8, true};
// 1M at 8MHz needs 4 TQ, but MCP2515 needs at least 5 legal TQ.
constexpr McpBitTiming kUnsupported = {0, 0, 0, 0, false};
}

inline McpBitTiming mcpBitTimingFor(CanBitRate bitrate, McpOscillatorFreq osc) {
    if (osc != McpOscillatorFreq::OSC_8MHZ) {
        return McpBitTimings::kUnsupported;
    }
    switch (bitrate) {
        case CanBitRate::CAN_125KBPS: return McpBitTimings::k8MHz125K;
        case CanBitRate::CAN_250KBPS: return McpBitTimings::k8MHz250K;
        case CanBitRate::CAN_500KBPS: return McpBitTimings::k8MHz500K;
        case CanBitRate::CAN_1000KBPS: return McpBitTimings::kUnsupported;
    }
    return McpBitTimings::kUnsupported;
}

static_assert(McpBitTimings::k8MHz125K.cnf1 == 0x81 &&
              McpBitTimings::k8MHz125K.cnf2 == 0xE5 &&
              McpBitTimings::k8MHz125K.cnf3 == 0x83, "125k CNF regression");
static_assert(McpBitTimings::k8MHz250K.cnf1 == 0x80 &&
              McpBitTimings::k8MHz250K.cnf2 == 0xE5 &&
              McpBitTimings::k8MHz250K.cnf3 == 0x83, "250k CNF regression");
static_assert(McpBitTimings::k8MHz500K.cnf1 == 0x00 &&
              McpBitTimings::k8MHz500K.cnf2 == 0xD1 &&
              McpBitTimings::k8MHz500K.cnf3 == 0x81, "500k CNF regression");
