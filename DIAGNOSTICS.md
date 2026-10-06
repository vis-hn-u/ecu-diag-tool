# Automotive Diagnostics Platform (Phase 3 Specification)

## 1. Overview & Architectural Hierarchy

Phase 3 upgrades the Automotive CAN / ECU Engineering Platform with a complete, standard-compliant automotive diagnostic engineering stack:

```
+-------------------------------------------------------------+
|                 APPLICATION & UI LAYER                      |
| (OBD-II Dashboard | UDS Command Center | DTC Manager | ...) |
+-------------------------------------------------------------+
                              ↑
+-------------------------------------------------------------+
|                DIAGNOSTIC SERVICE LAYER                     |
|  - UDS (ISO 14229-1): 0x10, 0x11, 0x14, 0x19, 0x22, 0x3E...  |
|  - OBD-II (SAE J1979): Modes 01, 02, 03, 04, 09            |
|  - Negative Response Management: 0x7F + NRC Dictionary       |
|  - DTC Engine: Status Byte Decoding (Masks 0x01..0x80)      |
+-------------------------------------------------------------+
                              ↑
+-------------------------------------------------------------+
|              ISO-TP TRANSPORT (ISO 15765-2)                 |
|  - Single Frame (SF: 0x0_), First Frame (FF: 0x10)          |
|  - Consecutive Frame (CF: 0x2_), Flow Control (FC: 0x3_)    |
|  - BlockSize, STmin, Multi-frame segmentation & reassembly  |
+-------------------------------------------------------------+
                              ↑
+-------------------------------------------------------------+
|               AUTHORITATIVE CAN PIPELINE                    |
|             (ICanPipeline bounded channel)                  |
+-------------------------------------------------------------+
                              ↑
+-------------------------------------------------------------+
|              HARDWARE GATEWAY / SIMULATOR                   |
|     (ESP8266 + MCP2515 Listen-Only / Virtual ECUs)          |
+-------------------------------------------------------------+
```

---

## 2. ISO 15765-2 (ISO-TP) Transport Layer

ISO-TP is stateful per communication channel (CAN ID pair):
- **Single Frame (SF)**: Transmits payloads of 1..7 bytes in a single CAN frame.
  - Byte 0: `0x00 | (length & 0x0F)`
  - Bytes 1..7: Payload
- **First Frame (FF)**: Initiates multi-frame transfers (> 7 bytes, up to 4095 bytes).
  - Bytes 0-1: `0x10 | ((length >> 8) & 0x0F)`, `length & 0xFF`
  - Bytes 2-7: Initial 6 payload bytes
- **Flow Control (FC)**: Receiver controls transmission pacing.
  - Byte 0: `0x30 | FlowStatus` (`0` = CTS ContinueToSend, `1` = Wait, `2` = Overflow)
  - Byte 1: `BlockSize` (`0` = all frames without intermediate FC)
  - Byte 2: `STmin` (Separation Time in milliseconds)
- **Consecutive Frame (CF)**: Carries segmented data payload chunks.
  - Byte 0: `0x20 | (sequenceNumber & 0x0F)` (Sequence numbers 1..15 rollover to 0)
  - Bytes 1-7: Up to 7 payload bytes

---

## 3. UDS (ISO 14229-1) Diagnostic Services

| Service ID | Name | Request Format | Positive Response | Key NRCs Handled |
| :--- | :--- | :--- | :--- | :--- |
| **0x10** | Diagnostic Session Control | `10 <SessionType>` | `50 <SessionType> <P2> <P2*>` | `0x12` SubFunctionNotSupported |
| **0x11** | ECU Reset | `11 <ResetType>` | `51 <ResetType>` | `0x12`, `0x22` ConditionsNotCorrect |
| **0x14** | Clear Diagnostic Info | `14 FF FF FF` | `54` | `0x31` RequestOutOfRange |
| **0x19** | Read DTC Information | `19 02 <StatusMask>` | `59 02 <Mask> [DTC + Status]...` | `0x12`, `0x13` |
| **0x22** | Read Data By Identifier | `22 <DID_MSB> <DID_LSB>` | `62 <DID> <Data...>` | `0x31` RequestOutOfRange, `0x33` SecurityAccessDenied |
| **0x27** | Security Access | `27 <Sub>` | `67 <Sub> [Seed/KeyAck]` | `0x35` InvalidKey, `0x36` ExceedAttempts |
| **0x2E** | Write Data By Identifier | `2E <DID> <Data...>` | `6E <DID>` | `0x7F` ServiceNotSupportedInActiveSession |
| **0x31** | Routine Control | `31 <Sub> <RoutineId>` | `71 <Sub> <RoutineId> <Status>` | `0x22`, `0x31` |
| **0x3E** | Tester Present | `3E 00` | `7E 00` | None |
| **0x85** | Control DTC Setting | `85 <Type>` | `C5 <Type>` | `0x12` |
| **0x7F** | Negative Response | N/A (Response Only) | `7F <ReqSID> <NRC>` | Full 35+ standard NRC dictionary |

---

## 4. OBD-II (SAE J1979 / ISO 15031-5) Parameters

- **Mode 01**: Current Powertrain Diagnostic Data (Live PIDs: RPM, Vehicle Speed, Coolant Temp, Throttle, MAF, Fuel Level, Voltage).
- **Supported PID Bitmaps**: PIDs 0x00, 0x20, 0x40, 0x60, 0x80 decode 32-bit capabilities.
- **Mode 02**: Freeze Frame Data.
- **Mode 03**: Stored Diagnostic Trouble Codes (Powertrain `P`, Chassis `C`, Body `B`, Network `U`).
- **Mode 04**: Clear Diagnostic Trouble Codes & Reset Stored Malfunction Indicators.
- **Mode 09**: Vehicle Information (VIN extraction from PID 0x02).

---

## 5. Diagnostic Trouble Code (DTC) Status Byte Masks

| Bit | Mask | Name | Description |
| :--- | :--- | :--- | :--- |
| **Bit 0** | `0x01` | `testFailed` | Fault currently present at time of request |
| **Bit 1** | `0x02` | `testFailedThisOperationCycle` | Fault failed during current drive cycle |
| **Bit 2** | `0x04` | `pendingDTC` | Fault detected on 1st drive cycle (pending confirmation) |
| **Bit 3** | `0x08` | `confirmedDTC` | Fault confirmed across multiple drive cycles |
| **Bit 4** | `0x10` | `testNotCompletedSinceLastClear` | Diagnostic self-test has not run since last clear |
| **Bit 5** | `0x20` | `testFailedSinceLastClear` | Fault failed at least once since DTCs were cleared |
| **Bit 6** | `0x40` | `testNotCompletedThisOperationCycle` | Diagnostic test has not completed this cycle |
| **Bit 7** | `0x80` | `warningIndicatorRequested` (MIL) | Check engine light / malfunction indicator active |

---

## 6. Virtual ECU Simulator

The built-in software simulator exposes three virtual automotive nodes without requiring physical CAN hardware:
1. **ECM (Engine Control Module)**: CAN Request `0x7E0` / Response `0x7E8`. Supports OBD-II Modes 01-09, UDS 0x10, 0x11, 0x14, 0x19, 0x22, 0x27, 0x31, 0x3E. Dynamic DIDs for live RPM, Coolant, Voltage, and VIN (`1FA6P8CF5H5123456`).
2. **ABS (Anti-lock Braking System)**: CAN Request `0x7E1` / Response `0x7E9`. Supports UDS 0x10, 0x11, 0x14, 0x19, 0x22, 0x3E. Simulated Wheel Speed Fault DTC (`C0035`).
3. **BCM (Body Control Module)**: CAN Request `0x7E2` / Response `0x7EA`. Supports UDS 0x10, 0x11, 0x14, 0x19, 0x22, 0x3E. Simulated Low Power Voltage DTC (`B1325`).

---

## 7. Safety Constraints & Transport Boundaries

- **No Unrestricted CAN Transmission**: All frame injections pass through explicit diagnostic APIs and are validated.
- **No Flashing or Bootloader Programming**: Flashing routines and memory write exploits are strictly absent.
- **No SecurityAccess Seed/Key Bypass**: Standard ISO 14229 challenge-response handshake models are supported without cracking algorithms.
- **Isolated Simulation vs Live Modes**: The user interface and backend headers explicitly declare operating mode at all times.
