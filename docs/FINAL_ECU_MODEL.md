# Automotive ECU Engineering Platform — FINAL ECU MODEL SPECIFICATION

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Unified ECU Entity Architecture

The platform models Electronic Control Units (ECUs) not as arbitrary CAN addresses, but as stateful automotive computing systems characterized by internal silicon architecture, firmware revisions, calibration tables, communication matrices, and flash memory maps.

```
+-----------------------------------------------------------------------------------+
|                                 ECU MODEL ENTITY                                  |
+-----------------------------------------------------------------------------------+
|  Identification:                                                                  |
|   - ECU Name (e.g., ECM, TCM, ABS, BCM, EPS)                                      |
|   - CAN Request ID (11-bit / 29-bit)                                              |
|   - CAN Response ID (11-bit / 29-bit)                                             |
|   - VIN (with evidence citations & confidence scoring)                            |
|   - Part Number / Hardware Number / Software Number                               |
|   - Calibration ID (CalID) & Calibration Verification Number (CVN)                |
+-----------------------------------------------------------------------------------+
|  Silicon Architecture Profile:                                                    |
|   - MCU Family (TriCore TC17xx/TC2xx/TC3xx, MPC5xxx, RH850, SH705x, ST10)         |
|   - Core Count & Clock Speed                                                      |
|   - Endianness (Little-Endian / Big-Endian)                                       |
|   - Internal Flash Size & External Flash Mapping                                  |
|   - Bootloader Protection & Security Level                                        |
+-----------------------------------------------------------------------------------+
|  Memory Map Regions:                                                              |
|   - Bootloader Sector (Protected / Read-Only)                                     |
|   - Application Code Sector (Executable Firmware)                                 |
|   - Calibration Sector (Maps, Tables, Scalars)                                    |
|   - EEPROM / Emulated Non-Volatile Sector (DTCs, Adaptations, Coding)             |
|   - RAM / Scratchpad Sector (Live variables, Measurement addresses)               |
+-----------------------------------------------------------------------------------+
|  Diagnostic & Security Capability:                                                |
|   - Primary Protocol (UDS, KWP2000, OBD-II, CCP/XCP)                              |
|   - Supported Diagnostic Sessions (Default, Programming, Extended, Engineering)   |
|   - Security Access Levels (Level 01/02 Standard, Level 09/0A Engineering)        |
|   - Seed-Key Algorithm Descriptor & Timing Constraints (P2, P2*)                  |
+-----------------------------------------------------------------------------------+
```

---

## 2. Evidence-Based Field Scoring Engine

To eliminate false-positive identifications common in automotive scan tools, every property of the ECU is assigned an explicit `IdentificationConfidence`:
* `Confirmed`: Validated against official OEM factory specification and multiple concordant DIDs/PIDs.
* `Probable`: Validated by structured UDS response with correct CRC/checksum.
* `Inferred`: Inferred from CAN bus message frequency, broadcast ID, and DLC.
* `Unknown`: No positive evidence obtained; graceful degradation.

Each field retains an array of `EvidenceSource` citations storing the exact CAN ID, protocol service, raw hex bytes, and discovery timestamp.

---

## 3. Seeded Compatibility Catalog

The platform ships with a built-in repository of verified ECU profiles:
1. **Bosch MED17.5.5 / MED17.5.2:** TriCore TC1766, 1.5MB Flash, VAG 2.0 TSI.
2. **Bosch EDC17C64 / EDC17C74:** TriCore TC1793, 4.0MB Flash, VAG 2.0 TDI Common Rail.
3. **Continental / Siemens Simos 18.1 / 18.10:** TriCore TC1791, 4.0MB Flash, Dual High-Pressure Fueling.
4. **Delphi DCM3.7 / DCM6.2:** Renesas SH72513 / RH850, PSA / Hyundai / Kia CRDI.
5. **Denso Gen 2 / Gen 3:** Renesas SH7058 / D76F019x, Toyota / Lexus D-4S.
6. **Magneti Marelli IAW 9GF / 8GMF:** Freescale MPC55xx, Fiat / Chrysler MultiAir.
