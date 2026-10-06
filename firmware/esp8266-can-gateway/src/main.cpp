#include <Arduino.h>

#include "config.h"
#include "mcp2515_driver.h"
#include "ring_buffer.h"
#include "stats.h"
#include "transport_protocol.h"
#include "websocket.h"
#include "wifi.h"

namespace {
StatsCollector g_stats;
CanRingBuffer g_ring;
Mcp2515Driver g_mcp(PIN_CAN_CS, PIN_CAN_INT);
WifiService g_wifi;
WebSocketGateway g_websocket(WEBSOCKET_PORT);
bool g_mcp_ready = false;
bool g_network_started = false;
uint32_t g_last_mcp_recovery_ms = 0;

void startNetworkServices() {
    if (g_network_started) {
        return;
    }
    g_wifi.begin();
    g_websocket.begin(g_stats);
    g_network_started = true;
}

static bool g_spi_test_pass = false;
static bool g_loopback_test_pass = false;
static Mcp2515Driver::RegisterForensics g_forensics = {};

bool startMcp2515(bool /*verbose*/ = true) {
    g_mcp_ready = g_mcp.begin(CAN_BUS_SPEED, CAN_OSC_FREQ, false);
    if (!g_mcp_ready) {
        g_stats.recordSpiError();
        return false;
    }

    // 1. Perform 100-cycle SPI register test
    g_spi_test_pass = g_mcp.runSpiRegisterTest(100);

    // 2. Read register forensics before loopback
    g_forensics = g_mcp.readForensics();

    // 3. Perform internal MCP2515 loopback test with ID 0x123
    CanFrame loopback_frame = {};
    g_loopback_test_pass = g_mcp.runInternalLoopbackTest(loopback_frame);

    if (g_loopback_test_pass) {
        g_ring.push(loopback_frame);
        g_stats.recordFrameReceived();
    }

    // 4. Switch to Normal Mode to connect to physical CAN box (TJA1050 differential wires)
    g_mcp.setMode(McpRegisters::MODE_NORMAL);

    return true;
}

void serviceCanReceive() {
    if (!g_mcp_ready) {
        return;
    }

    if (MCP2515_USE_INTERRUPT) {
        if (!g_mcp.consumeInterruptEvent() && !g_mcp.interruptLineAsserted()) {
            return;
        }
    } else {
        // High-performance SPI status polling: fast 2-byte SPI check (CMD_READ_STATUS 0xA0)
        if (!g_mcp.hasPendingFrame()) {
            return;
        }
    }

    uint8_t processed = 0;
    while (processed < CAN_MAX_FRAMES_PER_SERVICE) {
        CanFrame frame = {};
        const McpReadResult result = g_mcp.readFrame(frame);
        if (result == McpReadResult::NO_FRAME) {
            break;
        }
        ++processed;
        if (result == McpReadResult::INVALID_FRAME) {
            g_stats.recordCanError();
            g_stats.recordFrameDropped();
            continue;
        }

        g_stats.recordFrameReceived();
        if (!g_ring.push(frame)) {
            g_stats.recordFrameDropped();
        }
        if ((processed % CAN_DRAIN_YIELD_INTERVAL) == 0) {
            yield();
        }
    }

    const McpErrorStatus errors = g_mcp.readAndClearErrorStatus();
    if (errors.hasErrorInterrupt() || errors.hasRxOverflow()) {
        g_stats.setCanErrorState(errors.error_flags, errors.tx_error_count,
                                 errors.rx_error_count);
        g_stats.recordCanError();
        if ((errors.error_flags & McpRegisters::EFLG_RX0OVR) != 0) {
            g_stats.recordFrameDropped();
        }
        if ((errors.error_flags & McpRegisters::EFLG_RX1OVR) != 0) {
            g_stats.recordFrameDropped();
        }
        if (errors.isBusOff()) {
            const uint32_t now = millis();
            if (now - g_last_mcp_recovery_ms >= CAN_RECOVERY_BACKOFF_MS) {
                g_last_mcp_recovery_ms = now;
                g_mcp_ready = g_mcp.recoverListenOnly();
                if (!g_mcp_ready) {
                    g_stats.recordSpiError();
                }
            }
        }
    }

    // In interrupt mode, if the bounded drain stopped with INT still low, service again next loop
    if (MCP2515_USE_INTERRUPT && g_mcp_ready && g_mcp.interruptLineAsserted()) {
        g_mcp.requestService();
    }
    g_stats.setQueueUsage(g_ring.available());
}

void serviceOutputs() {
    uint8_t sent = 0;
    CanFrame frame = {};
    while (sent < CAN_MAX_FRAMES_PER_SERVICE && g_ring.pop(frame)) {
        if (g_network_started) {
            g_websocket.broadcastFrame(frame);
        }
        UsbSerialProtocol::writeCanFrame(frame);
        ++sent;
        if ((sent % CAN_DRAIN_YIELD_INTERVAL) == 0) {
            yield();
        }
    }
    g_stats.setQueueUsage(g_ring.available());
}
}

static uint32_t g_last_heartbeat_ms = 0;

void setup() {
    Serial.begin(USB_SERIAL_BAUD);
    delay(200);
    UsbSerialProtocol::writeHandshake();

    // Start Wi-Fi AP and WebSocket server first
    startNetworkServices();
    yield();

    // Check for MCP2515 CAN transceiver
    startMcp2515(true);
    g_last_mcp_recovery_ms = millis();
}

void loop() {
    const uint32_t loop_start_us = micros();
    const uint32_t now = millis();
    
    // Stage 1: Poll CAN receive buffers at start of loop iteration
    serviceCanReceive();

    if (g_network_started) {
        g_wifi.tick();
        g_websocket.tick();
    }

    // Stage 2: Poll CAN receive buffers immediately after network processing
    serviceCanReceive();

    if (!g_mcp_ready) {
        // Silent retry every 10 seconds for hotplugging CAN shield
        constexpr uint32_t MCP_RETRY_INTERVAL_MS = 10000;
        if (now - g_last_mcp_recovery_ms >= MCP_RETRY_INTERVAL_MS) {
            g_last_mcp_recovery_ms = now;
            startMcp2515(false);
            yield();
        }
    }

    static uint32_t last_diag_ms = 0;
    static Mcp2515Driver::SpiDiagnostics g_diag = {};
    if (!g_mcp_ready && (now - last_diag_ms >= 2000)) {
        last_diag_ms = now;
        g_diag = g_mcp.runDiagnostics();
        if (g_diag.diag_code == 0x01) {
            startMcp2515(false);
        }
    }

    // Auto-baud = OFF (Fixed 500 kbps, 8 MHz oscillator per directive)
    // CAN mode = NORMAL (Listening to physical CAN box through TJA1050)

    static uint32_t g_last_handshake_ms = 0;
    if (now - g_last_handshake_ms >= USB_HANDSHAKE_INTERVAL_MS) {
        g_last_handshake_ms = now;
        UsbSerialProtocol::writeHandshake();
    }

    if (now - g_last_heartbeat_ms >= USB_HEARTBEAT_INTERVAL_MS) {
        g_last_heartbeat_ms = now;
        const uint8_t mcp_stat = g_mcp_ready ? 0x01 : 0x00;
        const uint8_t canstat = g_mcp_ready ? g_mcp.readReg(0x0E) : g_diag.diag_code;
        const uint8_t eflg = g_mcp_ready ? g_mcp.getErrorFlags() : g_diag.normal_read;
        const uint8_t rec = g_mcp_ready ? g_mcp.getRxErrorCount() : g_diag.swapped_read;
        const uint8_t tec = g_mcp_ready ? g_mcp.getTxErrorCount() : g_diag.d1_read;
        const uint8_t baud_code = 50; // Fixed 500 kbps

        uint8_t diag_flags = 0;
        if (g_spi_test_pass) diag_flags |= 0x01;          // SPI 100-cycle test passed
        if (g_loopback_test_pass) diag_flags |= 0x02;     // Internal loopback test passed
        diag_flags |= 0x04;                               // Direct SPI / Level shifter NOT present
        if ((canstat & 0xE0) == 0x40) diag_flags |= 0x08; // Loopback mode active
        diag_flags |= 0x10;                               // External CAN node NOT present
        diag_flags |= 0x20;                               // Vehicle NOT connected

        UsbSerialProtocol::writeHeartbeat(mcp_stat, canstat, eflg, rec, tec, baud_code, diag_flags);
    }

    // Stage 3: Service USB and WebSocket frame outputs
    serviceOutputs();

    // Stage 4: Poll CAN receive buffers after output service
    serviceCanReceive();
    
    g_stats.tickOneSecond();
    g_stats.recordLatency(micros() - loop_start_us);
    yield();
}
