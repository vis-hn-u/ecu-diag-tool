# Automotive ECU Engineering Platform — FINAL ECU ENGINEERING SPECIFICATION

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  
**Classification:** Authoritative Technical Specification  

---

## 1. Domain Overview & Scope

The ECU Engineering Subsystem provides evidence-based identification, memory mapping, safe non-destructive memory reading, diagnostic session control, flash programming, and health normalization for Electronic Control Units across light-duty, heavy-duty, and commercial vehicles.

```
+-----------------------------------------------------------------------------------+
|                        ECU ENGINEERING SUBSYSTEM PIPELINE                         |
+-----------------------------------------------------------------------------------+
|  1. DISCOVERY & PROBE      : Multi-DID UDS querying + OBD Mode 09 identification   |
|  2. EVIDENCE CORRELATION   : Confidence scoring (Confirmed, Probable, Inferred)   |
|  3. CATALOG RESOLUTION     : Silicon architecture, flash boundaries, memory map   |
|  4. MEMORY MAP INSPECTION  : Sector classification (Boot, App, Cal, EEPROM, RAM)  |
|  5. SAFE CHUNKED READ      : UDS 0x23 / 0x22 chunked reads with SHA-256 hashing   |
|  6. 12-STAGE FLASHING      : Safe voltage interlock, dual-hash, sector alignment  |
|  7. HEALTH & NORMALIZATION : 8-bit status DTCs, plausibility, OEM target matching |
+-----------------------------------------------------------------------------------+
```

---

## 2. Multi-DID ECU Identification Profile

Identification aggregates multi-source evidence without fabricating unknown attributes:

| Field | Source Services | Verification / Evidence Validation | Confidence |
| :--- | :--- | :--- | :---: |
| **VIN** | UDS DID `0xF190`, OBD Mode 09 PID 02 | ISO 3779 checksum calculation, 17 ASCII characters | Confirmed |
| **ECU Hardware Number** | UDS DID `0xF191`, `0xF192` | Catalog match against OEM hardware database | Confirmed |
| **ECU Software Number** | UDS DID `0xF194`, `0xF195` | Validated against OEM firmware revision table | Confirmed |
| **Software Version** | UDS DID `0xF189` | Standard ASCII / BCD software string | Confirmed |
| **Calibration ID (CalID)**| OBD Mode 09 PID 04, DID `0xF187` | Up to 16-byte calibration descriptor | Confirmed |
| **Calibration Verification (CVN)**| OBD Mode 09 PID 06 | 4-byte CRC / cryptographic checksum | Confirmed |
| **ECU Family / Series** | Compatibility Manager Heuristic | Matched MCU architecture and memory size | Probable |
| **Manufacturer** | Vehicle Metadata & UDS DID `0xF18A` | OEM corporate identity | Confirmed |

---

## 3. Memory Model & Sector Isolation

Every ECU profile maps address spaces into discrete, statefully guarded regions:
* **Bootloader Region (`0x80000000 - 0x8001FFFF`):** Factory boot code; permanently read-only; protected by hardware lock.
* **Application Code Region (`0x80020000 - 0x801BFFFF`):** Main control loop firmware; requires diagnostic session `0x02` and security access level `0x01` to modify.
* **Calibration Region (`0x801C0000 - 0x801FFFFF`):** Operating maps, fuel tables, boost targets, torque curves; reprogrammable through 12-stage flasher.
* **EEPROM / Emulated NVRAM (`0xDF000000 - 0xDF00FFFF`):** Odometer, adaptation values, fault logs, immobilizer cryptographic seed keys.

---

## 4. Safe Chunked Memory Reader Protocol

To prevent bus saturation and watch-dog timeouts:
1. Max chunk size is capped at 256 bytes per transfer block.
2. Inter-chunk delay (`10ms - 50ms`) is enforced to allow ECU scheduler execution.
3. Every completed block is validated with running SHA-256 and CRC-32 digests.
4. If communication drops, the reader gracefully resumes from the last confirmed sector.
