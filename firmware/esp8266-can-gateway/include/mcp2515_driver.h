#pragma once

#include <Arduino.h>
#include <SPI.h>
#include "can_frame.h"
#include "config.h"
#include "mcp2515_defs.h"

enum class McpReadResult : uint8_t {
    NO_FRAME,
    FRAME,
    INVALID_FRAME
};

struct McpErrorStatus {
    uint8_t interrupt_flags;
    uint8_t error_flags;
    uint8_t tx_error_count;
    uint8_t rx_error_count;

    bool hasErrorInterrupt() const {
        return (interrupt_flags & McpRegisters::INT_ERROR_MASK) != 0;
    }
    bool hasRxOverflow() const {
        return (error_flags & McpRegisters::EFLG_OVERFLOW_MASK) != 0;
    }
    bool isBusOff() const {
        return (error_flags & McpRegisters::EFLG_TXBO) != 0;
    }
};

class Mcp2515Driver {
public:
    Mcp2515Driver(uint8_t cs_pin, uint8_t int_pin);

    bool begin(CanBitRate bitrate, McpOscillatorFreq osc, bool listen_only);
    void end();

    // ISR entry point: only records a one-bit event. It performs no SPI,
    // allocation, Wi-Fi, WebSocket, JSON, logging, or queue work.
    static void IRAM_ATTR onInterrupt();
    bool consumeInterruptEvent();
    void requestService();
    bool interruptLineAsserted() const;

    // Application-context SPI reads only. Repeated calls drain RXB0 and RXB1.
    McpReadResult readFrame(CanFrame& frame);
    McpErrorStatus readAndClearErrorStatus();

    // Fast 2-byte SPI status polling instructions (usable without INT wire)
    uint8_t readStatus();
    uint8_t readRxStatus();
    bool hasPendingFrame();

    bool recoverListenOnly();
    bool reconfigureBitRate(CanBitRate bitrate);

    uint8_t getInterruptFlags();
    uint8_t getErrorFlags();
    uint8_t getRxErrorCount();
    uint8_t getTxErrorCount();
    bool setMode(uint8_t mode);
    bool isListenOnly();
    bool isModeCorrect();
    uint8_t getCsPin() const { return cs_pin_; }
    uint8_t readReg(uint8_t reg) { return readRegister(reg); }
    bool probePin(uint8_t pin);

    struct SpiDiagnostics {
        uint8_t diag_code;
        uint8_t normal_read;
        uint8_t swapped_read;
        uint8_t d1_read;
    };
    SpiDiagnostics runDiagnostics();

    struct RegisterForensics {
        uint8_t canstat;
        uint8_t canctrl;
        uint8_t cnf1;
        uint8_t cnf2;
        uint8_t cnf3;
        uint8_t caninte;
        uint8_t canintf;
        uint8_t eflg;
        uint8_t rxb0ctrl;
        uint8_t rxb1ctrl;
    };
    RegisterForensics readForensics();
    bool runSpiRegisterTest(uint16_t iterations = 100);
    bool runInternalLoopbackTest(CanFrame& received_frame);

private:
    bool configureController();
    bool configureBitTiming();
    void configureReceiveAll();
    void reset();
    uint8_t readRegister(uint8_t reg);
    void readRegisters(uint8_t reg, uint8_t* buffer, size_t len);
    void writeRegister(uint8_t reg, uint8_t value);
    void writeRegisters(uint8_t reg, const uint8_t* buffer, size_t len);
    void modifyRegister(uint8_t reg, uint8_t mask, uint8_t data);
    void clearInterruptFlags(uint8_t mask);
    McpReadResult parseRxBuffer(uint8_t start_reg, CanFrame& frame);

    uint8_t cs_pin_;
    uint8_t int_pin_;
    SPISettings spi_settings_;
    CanBitRate bitrate_;
    McpOscillatorFreq oscillator_;
    bool listen_only_;
    static volatile bool interrupt_pending_;
};
