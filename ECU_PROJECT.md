# ECU Project Metadata & Architecture Boundary (Phase 5)

## 1. Domain Separation

| Domain | Scope | Modifiable? | Data Sources |
| :--- | :--- | :--- | :--- |
| **Domain A: Live ECU Data** | Real-time vehicle runtime signals | Ephemeral / Stream | CAN Bus, DBC Decoder, OBD-II Mode 01, UDS Service 0x22 |
| **Domain B: Flash Analyzer** | ECU binary image & calibration maps | **READ-ONLY (Locked)** | Binary ROM dumps, Confirmed map descriptors |

---

## 2. Project Metadata Model

- **Hardware Part Number**: `06J906026AB`
- **Software Calibration Version**: `1037397854`
- **ECU Microcontroller**: `Infineon TriCore TC1766`
- **Observed Bus Rate**: 500 kbit/s Standard CAN
- **Active DBC Database**: Vector DBC mapping
- **Binary Hash (SHA-256)**: Cryptographically verified per file import
