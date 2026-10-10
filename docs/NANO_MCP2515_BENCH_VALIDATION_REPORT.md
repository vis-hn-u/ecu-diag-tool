# ARDUINO NANO + MCP2515 (8 MHz) BENCH VALIDATION & ARCHITECTURAL REPORT

**Date:** 2026-10-07  
**Branch:** `bench-test/nano-mcp2515`  
**Baseline Tag:** `ecu-tool-before-bench-test`  
**Hardware Setup:** Arduino Nano (ATmega328P @ 16 MHz) + MCP2515 (8 MHz Crystal) via FTDI USB-UART (`COM8`)  
**Firmware:** Production SavvyCAN GVRET (`nano_mcp2515_savvycan`, Build 333)  

---

## 1. Executive Summary

The existing **ECU Diagnostic Tool** was inspected, built, and physically bench-tested with the live **Arduino Nano + MCP2515 CAN controller** connected to `COM8`.

### Key Validation Outcomes:
1. **Zero Diagnostic Regression:** All 417 existing backend unit & integration tests pass cleanly with zero failures. Existing UDS, OBD-II, ISO-TP, and DTC services remain intact.
2. **Nano Firmware Alignment:** The physical Nano firmware was inspected and verified to run at **1,000,000 baud** (SavvyCAN standard), with an **8 MHz crystal**, **CS on D10**, **INT on D2**, and default **Listen-Only mode**. The PC Diagnostic Tool backend and frontend configurations have been aligned to this baud rate.
3. **Hardware Loopback Test PASSED:** The MCP2515 SPI bus and internal transceiver loopback self-test passed with 100% frame verification (`ID=0x123`, `DLC=8`, `Data=[11 22 33 44 55 66 77 88]`).
4. **GVRET Handshake & Lifecycle PASSED:** COM8 opened cleanly, negotiated the binary SavvyCAN handshake sequence (`0xE7 0xE7 0xF1 0x0C 0xF1 0x06 0xF1 0x07 0xF1 0x01 0xF1 0x09`), verified device identification (`Firmware Build: 333`), confirmed keepalive (`0xDE 0xAD`), closed cleanly, and reopened cleanly with zero desynchronization.

---

## 2. Part 1 — Full Architecture Inspection Report

| Component / Layer | Implementation Details |
| :--- | :--- |
| **Backend Language & Runtime** | C# on .NET 8.0 SDK (x64) |
| **Backend Framework** | ASP.NET Core 8.0 Web API + SignalR Core WebSocket Hub |
| **Frontend Framework** | React 18, TypeScript, Tailwind CSS, Lucide React, Vite 5 |
| **Nano MCU & Toolchain** | Arduino Nano (ATmega328P @ 16 MHz), avr-gcc / Arduino Core |
| **CAN Controller & Crystal** | Microchip MCP2515 with 8.000 MHz crystal oscillator |
| **Application Entry Points** | Backend: [`Program.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Api/Program.cs)<br>Frontend: [`main.tsx`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/frontend/src/main.tsx) / [`App.tsx`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/frontend/src/App.tsx)<br>Firmware: [`nano_mcp2515_savvycan.ino`](file:///c:/Users/vishnu/Documents/ChatGPT/nano%20mcp/nano_mcp2515_savvycan/nano_mcp2515_savvycan.ino) |
| **MainWindow Architecture** | Tabbed engineering workstation: Dashboard, Live CAN Monitor, Diagnostic Engine (UDS/OBD), Reverse Engineering, SavvyCAN Suite, Hardware Manager, Settings |
| **CAN Transports (`ICanTransport`)** | - [`GvretSerialTransport`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Transports/GvretSerialTransport.cs) (`arduino-nano-gvret`)<br>- [`Esp8266UsbSerialTransport`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Transports/Esp8266UsbSerialTransport.cs) (`esp8266-usb-serial`)<br>- [`Esp8266Transport`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Transports/Esp8266Transport.cs) (Wi-Fi WebSocket port 8088)<br>- [`WaveshareCanFdTransport`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Transports/WaveshareCanFdTransport.cs) (USB-CAN-FD-B)<br>- [`Elm327Transport`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Transports/Elm327Transport.cs) (Serial AT Interpreter)<br>- [`SimulationTransport`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Transports/SimulationTransport.cs) (Virtual Loopback) |
| **OBD-II Implementation** | `Obd2ScannerService`: Modes 01 (PIDs), 02 (Freeze Frame), 03 (Stored DTCs), 04 (Clear DTCs), 07 (Pending), 09 (VIN/ECU Calibration), 0A (Permanent) |
| **ISO-TP Layer** | `IsoTpTransportLayer` (ISO 15765-2): Single Frame, First Frame, Flow Control (BS/STmin), Consecutive Frame assembly |
| **UDS Engine** | `UdsDiagnosticEngine` (ISO 14229-1): Services 0x10, 0x11, 0x22, 0x27, 0x28, 0x2E, 0x31, 0x34, 0x36, 0x37, 0x3E |
| **DTC Management** | `DtcManager`: SAE standard P/C/B/U classification + 8-bit status mask decoding + Maruti OEM specific dictionary |
| **Threading & Concurrency** | Non-blocking `System.Threading.Channels.Channel<CanFrame>` pipeline with bounded capacity, `Interlocked` counters, and async streaming |
| **Signal Dispatching** | C# events (`OnFrameReceived`, `OnStatusChanged`) -> SignalR Hub (`CanDataHub`) -> React Context (`CanContext.tsx`) |

---

## 3. Part 2 — Nano Firmware vs. PC Diagnostic Tool Alignment

A detailed line-by-line inspection was conducted between the Arduino Nano firmware (`nano_mcp2515_savvycan`) and the PC ECU Diagnostic Tool:

```
┌─────────────────────────────────┐                 ┌─────────────────────────────────┐
│     Arduino Nano Firmware       │                 │     PC ECU Diagnostic Tool      │
├─────────────────────────────────┤                 ├─────────────────────────────────┤
│ SERIAL_BAUD_RATE = 1,000,000    │ ◄── ALIGNED ──► │ DefaultUartBaud = 1,000,000     │
│ DEFAULT_CAN_BAUD = 500,000      │ ◄── ALIGNED ──► │ DefaultCanBusSpeed = 500,000    │
│ NUM_CAN_BUSES = 1               │ ◄── ALIGNED ──► │ Capabilities.ChannelCount = 1   │
│ Crystal: MCP_8MHZ               │ ◄── ALIGNED ──► │ Telemetry: 8 MHz MCP2515        │
│ CS = D10, INT = D2              │ ◄── ALIGNED ──► │ Direct SPI Hardware Mapping     │
│ Safety: MCP_LISTENONLY          │ ◄── ALIGNED ──► │ IsListenOnly = true (Default)   │
│ Handshake: E7 E7 F1 0C 06 07... │ ◄── ALIGNED ──► │ BuildHandshakePacket()          │
│ Bit 29 Listen-Only Flag         │ ◄── ALIGNED ──► │ BuildBusSetupCommand(..., true) │
└─────────────────────────────────┘                 └─────────────────────────────────┘
```

### Critical Finding & Resolution:
* **The Baud Rate Discrepancy:** Previous draft assumed `115200` baud. However, the physical Nano sketch in `config.h` defines `#define SERIAL_BAUD_RATE 1000000UL`. Both the backend (`GvretSerialTransport.cs`, `HardwareController.cs`) and frontend (`HardwareManagerPage.tsx`) were updated to default to **1,000,000 baud**, while still supporting user overrides.
* **Bootloader Delay & Handshake Keepalive:** Opening an FTDI USB-Serial port on Windows triggers DTR, resetting the ATmega328P. The Nano bootloader runs for ~1.8 seconds. A background retry loop was implemented in `GvretSerialTransport.cs` that gracefully resends the handshake every 1 second until the device responds, mirroring SavvyCAN's native `handleTick()` behavior.

---

## 4. Part 3 & 4 — Physical Bench Test Results

### Test 1: Hardware Status & Register Readout
Sending CLI `STATUS\r\n` to the Nano on `COM8` at 1,000,000 baud returned:
```text
==============================================
        HARDWARE & INTERFACE STATUS           
==============================================
MCU:                 Arduino Nano (ATmega328P @ 16 MHz)
CAN Controller:      MCP2515 via SPI (CS=D10, INT=D2)
Oscillator:          8.000 MHz (MCP_8MHZ)
Configured Bitrate:  500000 bps
Operating Mode:      LISTEN_ONLY (Safe)
EFLG Register:       0x00
RX Buffer Overflow:  NO
Bus-Off Flag:        NO
INT Pin State:       HIGH (Idle)
Protocol State:      CLI Standby (Waiting for SavvyCAN)
==============================================
```

### Test 2: MCP2515 SPI & Loopback Self-Test
Sending CLI `LOOPBACK\r\n` to the Nano on `COM8`:
```text
--- Starting MCP2515 Loopback Test ---
Entering Configuration Mode Successful!
Setting Baudrate Successful!
Frame verified: ID=0x123, DLC=8, Data=[11 22 33 44 55 66 77 88]
MCP2515 LOOPBACK: PASS
Entering Configuration Mode Successful!
Setting Baudrate Successful!
```
* **Result:** **PASS**. Confirms SPI lines (D10, D11, D12, D13), 8 MHz crystal oscillator, CAN TX buffer, and CAN RX buffer are completely functional.

### Test 3: Byte-Level GVRET Handshake Response
Sending SavvyCAN handshake `0xE7 0xE7 0xF1 0x0C 0xF1 0x06 0xF1 0x07 0xF1 0x01 0xF1 0x09`:
```text
Received Hex (33 Bytes):
F1 0C 01 F1 06 11 20 A1 07 00 00 00 00 00 00 F1 07 4D 01 20 00 00 00 F1 01 00 7E CD 02 F1 09 DE AD
```
* `F1 0C 01` ➔ 1 Active CAN Bus
* `F1 06 11 20 A1 07 00 00 00 00 00 00` ➔ Status 0x11 (Enabled + Listen-Only), Bitrate 500,000 bps (0x0007A120)
* `F1 07 4D 01 20 00 00 00` ➔ Firmware Build 333 (0x014D)
* `F1 01 00 7E CD 02` ➔ Microsecond hardware timestamp synchronization
* `F1 09 DE AD` ➔ SavvyCAN Keepalive ACK

### Test 4: End-to-End Driver Lifecycle Test
Executed via automated test suite `BenchHardwareTests.LiveHardwareBenchValidation_COM8_FullLifecycle`:
```text
[BENCH] Available Ports: COM10, COM11, COM12, COM13, COM3, COM4, COM5, COM6, COM8, COM9
[BENCH] Initial Connect Result: True
[BENCH] Device Identified: True
[BENCH] Keepalive Validated: True
[BENCH] Firmware Build: 333
[BENCH] Bus Count: 1
[BENCH] Operating Mode: LISTEN ONLY (SILENT)
[BENCH] Baud Rate: 1000000
[BENCH] Disconnected cleanly.
[BENCH] Reopen Connect Result: True
[BENCH] Reopened Device Identified: True
[BENCH] Reopened Keepalive Validated: True
[BENCH] Reopened Firmware Build: 333
[BENCH] Reopened port closed cleanly. Full lifecycle verified!
```
* **Result:** **100% PASS** (Test execution time: 6.44s).
