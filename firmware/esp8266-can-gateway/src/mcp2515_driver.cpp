#include "mcp2515_driver.h"

#include <string.h>

volatile bool Mcp2515Driver::interrupt_pending_ = false;

Mcp2515Driver::Mcp2515Driver(uint8_t cs_pin, uint8_t int_pin)
    : cs_pin_(cs_pin),
      int_pin_(int_pin),
      spi_settings_(SPI_CLOCK_SPEED, MSBFIRST, SPI_MODE0),
      bitrate_(CAN_BUS_SPEED),
      oscillator_(CAN_OSC_FREQ),
      listen_only_(CAN_LISTEN_ONLY_DEFAULT) {}

bool Mcp2515Driver::begin(CanBitRate bitrate, McpOscillatorFreq osc, bool listen_only) {
    bitrate_ = bitrate;
    oscillator_ = osc;
    listen_only_ = listen_only;

    pinMode(cs_pin_, OUTPUT);
    digitalWrite(cs_pin_, HIGH); // idle CS is high before SPI starts

    if (MCP2515_USE_INTERRUPT && int_pin_ != 255) {
        pinMode(int_pin_, INPUT_PULLUP); // MCP2515 INT is active-low/open-drain
    }

    SPI.begin(); // ESP8266 HSPI is fixed to D5/D6/D7 from config.h

    if (!probePin(cs_pin_)) {
        // Auto-scan alternative pins in case CS was wired to D1 (GPIO5), D4 (GPIO2), D8 (GPIO15), D3 (GPIO0)
        const uint8_t candidates[] = {5, 2, 15, 0};
        for (uint8_t candidate : candidates) {
            if (probePin(candidate)) {
                cs_pin_ = candidate;
                break;
            }
        }
    }

    reset();
    if (!configureController() || !isModeCorrect()) {
        return false;
    }

    interrupt_pending_ = false;
    if (MCP2515_USE_INTERRUPT && int_pin_ != 255) {
        const int interrupt = digitalPinToInterrupt(int_pin_);
        if (interrupt == NOT_AN_INTERRUPT) {
            return false;
        }
        attachInterrupt(interrupt, Mcp2515Driver::onInterrupt, FALLING);
        if (interruptLineAsserted()) {
            requestService();
        }
    }
    return true;
}

void Mcp2515Driver::end() {
    if (MCP2515_USE_INTERRUPT && int_pin_ != 255) {
        const int interrupt = digitalPinToInterrupt(int_pin_);
        if (interrupt != NOT_AN_INTERRUPT) {
            detachInterrupt(interrupt);
        }
    }
    digitalWrite(cs_pin_, HIGH);
}

void IRAM_ATTR Mcp2515Driver::onInterrupt() {
    // This is intentionally the entire ISR. All slow work happens in loop().
    interrupt_pending_ = true;
}

bool Mcp2515Driver::consumeInterruptEvent() {
    noInterrupts();
    const bool pending = interrupt_pending_;
    interrupt_pending_ = false;
    interrupts();
    return pending;
}

void Mcp2515Driver::requestService() {
    noInterrupts();
    interrupt_pending_ = true;
    interrupts();
}

bool Mcp2515Driver::interruptLineAsserted() const {
    if (!MCP2515_USE_INTERRUPT || int_pin_ == 255) {
        return false;
    }
    return digitalRead(int_pin_) == LOW;
}

uint8_t Mcp2515Driver::readStatus() {
    SPI.beginTransaction(spi_settings_);
    digitalWrite(cs_pin_, LOW);
    SPI.transfer(McpRegisters::CMD_READ_STATUS);
    const uint8_t status = SPI.transfer(0x00);
    digitalWrite(cs_pin_, HIGH);
    SPI.endTransaction();
    return status;
}

uint8_t Mcp2515Driver::readRxStatus() {
    SPI.beginTransaction(spi_settings_);
    digitalWrite(cs_pin_, LOW);
    SPI.transfer(McpRegisters::CMD_RX_STATUS);
    const uint8_t status = SPI.transfer(0x00);
    digitalWrite(cs_pin_, HIGH);
    SPI.endTransaction();
    return status;
}

bool Mcp2515Driver::hasPendingFrame() {
    // Fast 2-byte SPI status instruction CMD_READ_STATUS (0xA0)
    // Bit 0 = RX0IF (RXB0 full), Bit 1 = RX1IF (RXB1 full)
    const uint8_t status = readStatus();
    return (status & (McpRegisters::INT_RX0IF | McpRegisters::INT_RX1IF)) != 0;
}

McpReadResult Mcp2515Driver::readFrame(CanFrame& frame) {
    // Fast 2-byte status check for RX0 and RX1
    const uint8_t status = readStatus();
    if ((status & McpRegisters::INT_RX0IF) != 0) {
        const McpReadResult result = parseRxBuffer(McpRegisters::RXB0SIDH, frame);
        clearInterruptFlags(McpRegisters::INT_RX0IF);
        return result;
    }
    if ((status & McpRegisters::INT_RX1IF) != 0) {
        const McpReadResult result = parseRxBuffer(McpRegisters::RXB1SIDH, frame);
        clearInterruptFlags(McpRegisters::INT_RX1IF);
        return result;
    }
    return McpReadResult::NO_FRAME;
}

McpErrorStatus Mcp2515Driver::readAndClearErrorStatus() {
    McpErrorStatus status = {};
    status.interrupt_flags = getInterruptFlags();
    status.error_flags = getErrorFlags();
    status.tx_error_count = getTxErrorCount();
    status.rx_error_count = getRxErrorCount();

    if (status.hasErrorInterrupt()) {
        clearInterruptFlags(status.interrupt_flags & McpRegisters::INT_ERROR_MASK);
    }
    const uint8_t overflow = status.error_flags & McpRegisters::EFLG_OVERFLOW_MASK;
    if (overflow != 0) {
        modifyRegister(McpRegisters::EFLG, overflow, 0);
    }
    return status;
}

bool Mcp2515Driver::recoverListenOnly() {
    if (MCP2515_USE_INTERRUPT && int_pin_ != 255) {
        const int interrupt = digitalPinToInterrupt(int_pin_);
        if (interrupt != NOT_AN_INTERRUPT) {
            detachInterrupt(interrupt);
        }
    }
    reset();
    if (!configureController() || !isModeCorrect()) {
        return false;
    }
    interrupt_pending_ = false;
    if (MCP2515_USE_INTERRUPT && int_pin_ != 255) {
        const int interrupt = digitalPinToInterrupt(int_pin_);
        if (interrupt == NOT_AN_INTERRUPT) {
            return false;
        }
        attachInterrupt(interrupt, Mcp2515Driver::onInterrupt, FALLING);
        if (interruptLineAsserted()) {
            requestService();
        }
    }
    return true;
}

bool Mcp2515Driver::reconfigureBitRate(CanBitRate bitrate) {
    bitrate_ = bitrate;
    if (!setMode(McpRegisters::MODE_CONFIG)) {
        return false;
    }
    if (!configureBitTiming()) {
        return false;
    }
    const uint8_t target_mode = listen_only_ ? McpRegisters::MODE_LISTENONLY : McpRegisters::MODE_NORMAL;
    return setMode(target_mode);
}


uint8_t Mcp2515Driver::getInterruptFlags() { return readRegister(McpRegisters::CANINTF); }
uint8_t Mcp2515Driver::getErrorFlags() { return readRegister(McpRegisters::EFLG); }
uint8_t Mcp2515Driver::getRxErrorCount() { return readRegister(McpRegisters::REC); }
uint8_t Mcp2515Driver::getTxErrorCount() { return readRegister(McpRegisters::TEC); }

bool Mcp2515Driver::isListenOnly() {
    return (readRegister(McpRegisters::CANSTAT) & McpRegisters::MODE_MASK) == McpRegisters::MODE_LISTENONLY;
}

bool Mcp2515Driver::isModeCorrect() {
    const uint8_t expected_mode = listen_only_ ? McpRegisters::MODE_LISTENONLY : McpRegisters::MODE_NORMAL;
    return (readRegister(McpRegisters::CANSTAT) & McpRegisters::MODE_MASK) == expected_mode;
}

bool Mcp2515Driver::probePin(uint8_t pin) {
    pinMode(pin, OUTPUT);
    digitalWrite(pin, HIGH);
    delay(2);

    SPI.beginTransaction(spi_settings_);
    digitalWrite(pin, LOW);
    SPI.transfer(McpRegisters::CMD_RESET);
    digitalWrite(pin, HIGH);
    SPI.endTransaction();
    delay(10);

    SPI.beginTransaction(spi_settings_);
    digitalWrite(pin, LOW);
    SPI.transfer(McpRegisters::CMD_READ);
    SPI.transfer(McpRegisters::CANSTAT);
    const uint8_t stat = SPI.transfer(0x00);
    digitalWrite(pin, HIGH);
    SPI.endTransaction();

    SPI.beginTransaction(spi_settings_);
    digitalWrite(pin, LOW);
    SPI.transfer(McpRegisters::CMD_READ);
    SPI.transfer(McpRegisters::CANCTRL);
    const uint8_t ctrl = SPI.transfer(0x00);
    digitalWrite(pin, HIGH);
    SPI.endTransaction();

    return (stat == 0x80 && ctrl == 0x87);
}

static uint8_t bitBangReadReg(uint8_t csPin, uint8_t sckPin, uint8_t mosiPin, uint8_t misoPin, uint8_t reg) {
    pinMode(csPin, OUTPUT);
    pinMode(sckPin, OUTPUT);
    pinMode(mosiPin, OUTPUT);
    pinMode(misoPin, INPUT_PULLUP);

    digitalWrite(csPin, HIGH);
    digitalWrite(sckPin, LOW);
    digitalWrite(mosiPin, LOW);
    delayMicroseconds(50);

    // Reset MCP2515 (CMD_RESET = 0xC0)
    digitalWrite(csPin, LOW);
    delayMicroseconds(20);
    uint8_t resetCmd = 0xC0;
    for (int bit = 7; bit >= 0; bit--) {
        digitalWrite(mosiPin, (resetCmd & (1 << bit)) ? HIGH : LOW);
        delayMicroseconds(10);
        digitalWrite(sckPin, HIGH);
        delayMicroseconds(10);
        digitalWrite(sckPin, LOW);
        delayMicroseconds(10);
    }
    digitalWrite(csPin, HIGH);
    delay(15);

    // Read register (CMD_READ = 0x03)
    digitalWrite(csPin, LOW);
    delayMicroseconds(20);
    uint8_t readCmd = 0x03;
    for (int bit = 7; bit >= 0; bit--) {
        digitalWrite(mosiPin, (readCmd & (1 << bit)) ? HIGH : LOW);
        delayMicroseconds(10);
        digitalWrite(sckPin, HIGH);
        delayMicroseconds(10);
        digitalWrite(sckPin, LOW);
        delayMicroseconds(10);
    }
    for (int bit = 7; bit >= 0; bit--) {
        digitalWrite(mosiPin, (reg & (1 << bit)) ? HIGH : LOW);
        delayMicroseconds(10);
        digitalWrite(sckPin, HIGH);
        delayMicroseconds(10);
        digitalWrite(sckPin, LOW);
        delayMicroseconds(10);
    }
    uint8_t result = 0;
    for (int bit = 7; bit >= 0; bit--) {
        digitalWrite(sckPin, HIGH);
        delayMicroseconds(10);
        if (digitalRead(misoPin) == HIGH) {
            result |= (1 << bit);
        }
        digitalWrite(sckPin, LOW);
        delayMicroseconds(10);
    }
    digitalWrite(csPin, HIGH);
    return result;
}

Mcp2515Driver::SpiDiagnostics Mcp2515Driver::runDiagnostics() {
    SpiDiagnostics diag = {};
    
    // 1. Normal wiring (CS=D2/GPIO4, SCK=D5/GPIO14, MOSI=D7/GPIO13, MISO=D6/GPIO12)
    diag.normal_read = bitBangReadReg(4, 14, 13, 12, McpRegisters::CANSTAT);
    
    // 2. Swapped SO/SI (CS=D2/GPIO4, SCK=D5/GPIO14, MOSI=D6/GPIO12, MISO=D7/GPIO13)
    diag.swapped_read = bitBangReadReg(4, 14, 12, 13, McpRegisters::CANSTAT);
    
    // 3. CS on D1 (CS=D1/GPIO5, SCK=D5/GPIO14, MOSI=D7/GPIO13, MISO=D6/GPIO12)
    diag.d1_read = bitBangReadReg(5, 14, 13, 12, McpRegisters::CANSTAT);
    
    // Restore hardware SPI pins
    SPI.begin();
    pinMode(cs_pin_, OUTPUT);
    digitalWrite(cs_pin_, HIGH);

    if (diag.normal_read == 0x80) {
        diag.diag_code = 0x01; // Normal wiring verified
    } else if (diag.swapped_read == 0x80) {
        diag.diag_code = 0x02; // SO and SI are SWAPPED
    } else if (diag.d1_read == 0x80) {
        diag.diag_code = 0x03; // CS is on D1
    } else if (diag.normal_read == 0x00 && diag.swapped_read == 0x00) {
        diag.diag_code = 0x04; // MISO shorted to GND
    } else {
        diag.diag_code = 0x05; // 0xFF on all lines
    }

    return diag;
}

bool Mcp2515Driver::setMode(uint8_t mode) {
    const uint8_t requested_mode = mode & McpRegisters::MODE_MASK;
    modifyRegister(McpRegisters::CANCTRL, McpRegisters::MODE_MASK, requested_mode);
    const uint32_t start = millis();
    while (millis() - start < 10U) {
        if ((readRegister(McpRegisters::CANSTAT) & McpRegisters::MODE_MASK) == requested_mode) {
            return true;
        }
        delay(1);
    }
    return false;
}

bool Mcp2515Driver::configureController() {
    if (!setMode(McpRegisters::MODE_CONFIG) || !configureBitTiming()) {
        return false;
    }
    configureReceiveAll();
    writeRegister(McpRegisters::CANINTE,
                  McpRegisters::INTE_RX0IE | McpRegisters::INTE_RX1IE |
                  McpRegisters::INTE_ERRIE | McpRegisters::INTE_MERRE);
    const uint8_t target_mode = listen_only_ ? McpRegisters::MODE_LISTENONLY : McpRegisters::MODE_NORMAL;
    return setMode(target_mode);
}

bool Mcp2515Driver::configureBitTiming() {
    const McpBitTiming timing = mcpBitTimingFor(bitrate_, oscillator_);
    if (!timing.supported) {
        return false;
    }
    writeRegister(McpRegisters::CNF3, timing.cnf3);
    writeRegister(McpRegisters::CNF2, timing.cnf2);
    writeRegister(McpRegisters::CNF1, timing.cnf1);
    return readRegister(McpRegisters::CNF3) == timing.cnf3 &&
           readRegister(McpRegisters::CNF2) == timing.cnf2 &&
           readRegister(McpRegisters::CNF1) == timing.cnf1;
}

void Mcp2515Driver::configureReceiveAll() {
    static const uint8_t kZeroFilter[4] = {0, 0, 0, 0};
    writeRegisters(McpRegisters::RXF0SIDH, kZeroFilter, sizeof(kZeroFilter));
    writeRegisters(McpRegisters::RXF1SIDH, kZeroFilter, sizeof(kZeroFilter));
    writeRegisters(McpRegisters::RXF2SIDH, kZeroFilter, sizeof(kZeroFilter));
    writeRegisters(McpRegisters::RXF3SIDH, kZeroFilter, sizeof(kZeroFilter));
    writeRegisters(McpRegisters::RXF4SIDH, kZeroFilter, sizeof(kZeroFilter));
    writeRegisters(McpRegisters::RXF5SIDH, kZeroFilter, sizeof(kZeroFilter));
    writeRegisters(McpRegisters::RXM0SIDH, kZeroFilter, sizeof(kZeroFilter));
    writeRegisters(McpRegisters::RXM1SIDH, kZeroFilter, sizeof(kZeroFilter));

    // RXM=11 accepts all valid frames. BUKT is valid only for RXB0 and lets a
    // second received frame roll over to RXB1 while RXB0 remains pending.
    writeRegister(McpRegisters::RXB0CTRL,
                  McpRegisters::RXB_RX_ANY | McpRegisters::RXB_BUKT);
    writeRegister(McpRegisters::RXB1CTRL, McpRegisters::RXB_RX_ANY);
}

void Mcp2515Driver::reset() {
    SPI.beginTransaction(spi_settings_);
    digitalWrite(cs_pin_, LOW);
    SPI.transfer(McpRegisters::CMD_RESET);
    digitalWrite(cs_pin_, HIGH);
    SPI.endTransaction();
    delay(10);
}

uint8_t Mcp2515Driver::readRegister(uint8_t reg) {
    SPI.beginTransaction(spi_settings_);
    digitalWrite(cs_pin_, LOW);
    SPI.transfer(McpRegisters::CMD_READ);
    SPI.transfer(reg);
    const uint8_t value = SPI.transfer(0x00);
    digitalWrite(cs_pin_, HIGH);
    SPI.endTransaction();
    return value;
}

void Mcp2515Driver::readRegisters(uint8_t reg, uint8_t* buffer, size_t len) {
    SPI.beginTransaction(spi_settings_);
    digitalWrite(cs_pin_, LOW);
    SPI.transfer(McpRegisters::CMD_READ);
    SPI.transfer(reg);
    for (size_t i = 0; i < len; ++i) {
        buffer[i] = SPI.transfer(0x00);
    }
    digitalWrite(cs_pin_, HIGH);
    SPI.endTransaction();
}

void Mcp2515Driver::writeRegister(uint8_t reg, uint8_t value) {
    writeRegisters(reg, &value, 1);
}

void Mcp2515Driver::writeRegisters(uint8_t reg, const uint8_t* buffer, size_t len) {
    SPI.beginTransaction(spi_settings_);
    digitalWrite(cs_pin_, LOW);
    SPI.transfer(McpRegisters::CMD_WRITE);
    SPI.transfer(reg);
    for (size_t i = 0; i < len; ++i) {
        SPI.transfer(buffer[i]);
    }
    digitalWrite(cs_pin_, HIGH);
    SPI.endTransaction();
}

void Mcp2515Driver::modifyRegister(uint8_t reg, uint8_t mask, uint8_t data) {
    SPI.beginTransaction(spi_settings_);
    digitalWrite(cs_pin_, LOW);
    SPI.transfer(McpRegisters::CMD_BIT_MODIFY);
    SPI.transfer(reg);
    SPI.transfer(mask);
    SPI.transfer(data);
    digitalWrite(cs_pin_, HIGH);
    SPI.endTransaction();
}

void Mcp2515Driver::clearInterruptFlags(uint8_t mask) {
    modifyRegister(McpRegisters::CANINTF, mask, 0);
}

McpReadResult Mcp2515Driver::parseRxBuffer(uint8_t start_reg, CanFrame& frame) {
    uint8_t raw[13]; // SIDH, SIDL, EID8, EID0, DLC, data[8]
    readRegisters(start_reg, raw, sizeof(raw));
    const uint8_t dlc = raw[4] & 0x0F;
    if (dlc > 8) {
        return McpReadResult::INVALID_FRAME;
    }

    const bool extended = (raw[1] & 0x08U) != 0;
    frame.timestamp_us = micros();
    frame.dlc = dlc;
    frame.flags = extended ? CAN_FLAG_EXTENDED : 0;
    if ((raw[4] & 0x40U) != 0) {
        frame.flags |= CAN_FLAG_RTR;
    }
    if (extended) {
        frame.id = (static_cast<uint32_t>(raw[0]) << 21) |
                   (static_cast<uint32_t>(raw[1] & 0xE0U) << 13) |
                   (static_cast<uint32_t>(raw[1] & 0x03U) << 16) |
                   (static_cast<uint32_t>(raw[2]) << 8) |
                   static_cast<uint32_t>(raw[3]);
    } else {
        frame.id = (static_cast<uint32_t>(raw[0]) << 3) |
                   static_cast<uint32_t>(raw[1] >> 5);
    }
    memset(frame.data, 0, sizeof(frame.data));
    memcpy(frame.data, &raw[5], dlc);
    return isValidCanFrame(frame) ? McpReadResult::FRAME : McpReadResult::INVALID_FRAME;
}

Mcp2515Driver::RegisterForensics Mcp2515Driver::readForensics() {
    RegisterForensics f = {};
    f.canstat = readRegister(McpRegisters::CANSTAT);
    f.canctrl = readRegister(McpRegisters::CANCTRL);
    f.cnf1 = readRegister(McpRegisters::CNF1);
    f.cnf2 = readRegister(McpRegisters::CNF2);
    f.cnf3 = readRegister(McpRegisters::CNF3);
    f.caninte = readRegister(McpRegisters::CANINTE);
    f.canintf = readRegister(McpRegisters::CANINTF);
    f.eflg = readRegister(McpRegisters::EFLG);
    f.rxb0ctrl = readRegister(McpRegisters::RXB0CTRL);
    f.rxb1ctrl = readRegister(McpRegisters::RXB1CTRL);
    return f;
}

bool Mcp2515Driver::runSpiRegisterTest(uint16_t iterations) {
    constexpr uint8_t test_reg = 0x36; // TXB0D0
    for (uint16_t i = 0; i < iterations; ++i) {
        uint8_t pattern = static_cast<uint8_t>(0xA5 ^ (i * 17 + 1));
        writeRegister(test_reg, pattern);
        uint8_t readback = readRegister(test_reg);
        if (readback != pattern) {
            return false;
        }
    }
    writeRegister(test_reg, 0x00);
    return true;
}

bool Mcp2515Driver::runInternalLoopbackTest(CanFrame& received_frame) {
    if (!setMode(McpRegisters::MODE_LOOPBACK)) {
        return false;
    }
    clearInterruptFlags(0xFF);

    uint8_t raw[13] = {0};
    raw[0] = 0x24; // TXB0SIDH (0x123 >> 3)
    raw[1] = 0x60; // TXB0SIDL ((0x123 & 0x07) << 5)
    raw[2] = 0x00; // TXB0EID8
    raw[3] = 0x00; // TXB0EID0
    raw[4] = 0x08; // TXB0DLC
    for (uint8_t i = 0; i < 8; ++i) {
        raw[5 + i] = i + 1;
    }
    writeRegisters(McpRegisters::TXB0SIDH, raw, sizeof(raw));

    SPI.beginTransaction(spi_settings_);
    digitalWrite(cs_pin_, LOW);
    SPI.transfer(McpRegisters::CMD_RTS_TXB0);
    digitalWrite(cs_pin_, HIGH);
    SPI.endTransaction();

    const uint32_t start_ms = millis();
    while (millis() - start_ms < 50U) {
        if (hasPendingFrame()) {
            if (readFrame(received_frame) == McpReadResult::FRAME) {
                if (received_frame.id == 0x123 && received_frame.dlc == 8) {
                    bool match = true;
                    for (uint8_t i = 0; i < 8; ++i) {
                        if (received_frame.data[i] != (i + 1)) {
                            match = false;
                            break;
                        }
                    }
                    if (match) {
                        return true;
                    }
                }
            }
        }
        delay(1);
    }
    return false;
}


