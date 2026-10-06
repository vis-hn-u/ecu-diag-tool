# Automotive ECU Engineering Platform — FINAL CAPABILITY MATRIX

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Transport Hardware Capabilities

| Feature / Capability | Waveshare USB-CAN-FD | ESP8266 (USB Serial) | ESP8266 (WiFi SoftAP) | ELM327 / OBDLink | Simulation Bus |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Physical Interface** | USB 2.0 High-Speed | USB Virtual COM | 802.11 b/g/n SoftAP | USB / BT Serial | Memory Channel |
| **Max Baud Rate** | 1 Mbps Nom / 5 Mbps Data | 921,600 bps UART | 8088 WebSocket | 115,200 - 500k bps | Sub-microsecond |
| **Channel Count** | 2 Isolated Channels | 1 Channel | 1 Channel | 1 Channel | 8 Virtual Busses |
| **Classic CAN (11/29-bit)** | YES | YES | YES | YES | YES |
| **ISO CAN-FD (64B + BRS)** | YES | NO | NO | NO | YES |
| **Transmit Capability** | YES | **NO (Hardware Locked)** | **NO (Hardware Locked)** | YES (Restricted) | YES |
| **Hardware Timestamping** | YES (Microsecond) | YES (Microsecond) | YES (Microsecond) | Inferred | YES (Exact) |
| **Max Payload Size** | 64 Bytes | 8 Bytes | 8 Bytes | 8 Bytes | 64 Bytes |
| **Safety Mode** | Full Duplex | Strict Listen-Only | Strict Listen-Only | Query/Response | Emulated Bench |

---

## 2. Protocol & Diagnostic Engine Capabilities

| Protocol / Standard | Support Level | Implementation Location | Capabilities Supported |
| :--- | :---: | :--- | :--- |
| **ISO 11898 (CAN 2.0A/B)** | Native / Full | `CanEngineeringPlatform.Core` | Standard 11-bit, Extended 29-bit, RTR, Error flags |
| **ISO 11898-1:2015 (CAN FD)** | Native / Full | `CanEngineeringPlatform.Core` | Payloads up to 64B, BRS bit-rate switching, ESI indicator |
| **ISO 15765-2 (ISO-TP)** | Native / Full | `IsoTpTransportService.cs` | Single Frame, First Frame, Consecutive Frame, Flow Control (Wait/CTS) |
| **ISO 14229-1 (UDS)** | Native / Full | `DiagnosticEnginePhase3.cs` | Services: 0x10, 0x11, 0x14, 0x19, 0x22, 0x27, 0x2E, 0x31, 0x34, 0x3E, 0x85 |
| **SAE J1979 / ISO 15031 (OBD-II)** | Native / Full | `UdsAndObdDiagnostics.cs` | Modes 01 (Live Data), 02 (Freeze Frame), 03 (DTCs), 04 (Clear), 09 (VIN/CalID) |
| **ISO 14230-4 (KWP2000)** | Native / Full | `AutomotiveTransportAbstraction.cs` | 5-baud init, fast init, keyword verification, checksum calculation |
| **SAE J1939** | Native / Full | `AutomotiveTransportAbstraction.cs` | 29-bit PGN extraction, Source/Destination address, Priority, Transport Layer |
| **LIN 2.1 / 2.2** | Native / Full | `AutomotiveTransportAbstraction.cs` | Classic & Enhanced checksums, Parity Protected Identifier (PID) tables |
| **ISO 13400 (DoIP)** | Architecture | `AutomotiveTransportAbstraction.cs` | DoIP header framing, vehicle announcement, payload routing |
| **ASAM MCD-2MC (A2L)** | Native / Full | `AsamProtocolService.cs` | Parser for `MEASUREMENT`, `CHARACTERISTIC`, `COMPU_METHOD`, `RECORD_LAYOUT` |
| **ASAM MCD-2D (ODX)** | Native / Full | `AsamProtocolService.cs` | Variant decoding, service lookup, parameter conversion |
| **ASAM XCP v1.3 (CAN)** | Native / Full | `AsamProtocolService.cs` | Connect, Disconnect, Upload, Download, DAQ lists, Resource protection |

---

## 3. Engineering Workstation Subsystems

| Subsystem | Operational Scope | Interactive Controls |
| :--- | :--- | :--- |
| **Project Workspace** | Cryptographic provenance tracking, SHA-256 state hashing | Project creation, JSON export/import, note tagging, evidence attachment |
| **Unified Forensic Timeline**| Nanosecond/microsecond unified event sequencing | Multi-category filter, severity ranking, search query, raw hex inspection |
| **One-Click Automated Suite**| Automated sequence: Transport -> Identify -> Protocol -> DTCs -> Health | One-click trigger, progress bar, stage duration logging, final PDF export |
| **CAN Reverse Engineering Lab**| Unsupervised bus signal and protocol extraction | Shannon entropy sorting, bit transition heatmaps, counter/checksum identification |
| **Vehicle Digital Twin** | Full vehicle simulation across 8 ECUs | Node online/offline toggle, DTC injection, bus drop, sensor override |
| **HIL Bench Architecture** | Electrical simulation & fault injection | Terminal 30/15 power control, sensor overrides, Short-to-GND/BAT, Open-Circuit |
| **12-Stage Safe Flasher** | Critical ECU reflashing with safety interlocks | Supply voltage monitor (13.8V check), dual-hash validation, operator attestation |
| **Automotive AI Copilot** | Evidence-grounded reasoning assistant | Query prompt, domain filter, supporting citations, alternative diagnosis |
