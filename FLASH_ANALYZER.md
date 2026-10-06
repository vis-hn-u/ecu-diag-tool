# Read-Only ECU Flash Analyzer (Phase 5 Specification)

## 1. Domain B: Read-Only Flash Analysis Architecture

> [!IMPORTANT]
> **SAFETY DIRECTIVE**: The Flash Analyzer is strictly **READ-ONLY**.
> - NO parameter editing
> - NO Stage 1 / Stage 2 / Stage 3 tuning modifications
> - NO binary file modification or save-back
> - NO physical ECU flashing, bootloader writes, or security bypass

---

## 2. Capabilities & Tools

### A. Binary Image Inspection
- Import full ECU binary dumps (`.bin`).
- Computes SHA-256 and MD5 cryptographic checksums.
- Automatic detection of ECU architecture (e.g. Bosch MED17.5 TriCore TC1766, Siemens Continental Simos 18.1).
- Distinct identification of **File Offset** vs **Logical ECU Address**.

### B. Read-Only Calibration Objects
- Visualizes 1D curves, 2D maps, 3D maps, limiters, scalars, and switches.
- Categories: Torque, Air/Turbo, Fuel, Smoke/Lambda, RPM/Speed, Thermal, EGR, DPF, SCR/AdBlue, Intake/Actuators, Start/Idle, Sensors, General.
- Object Provenance: `CONFIRMED`, `PROJECT DEFINED`, `USER DEFINED`, `HEURISTIC`, `UNKNOWN`.

### C. Hex & ASCII Dump Viewer
- Virtualized byte inspector with offset jumping and ASCII text decoding.

### D. Byte-by-Byte Binary Diff
- Compares original stock binary against a second modified calibration image without altering either file.
- Displays offset, modified byte count, difference percentage, and hexadecimal chunk deltas.

### E. Read-Only Checksum Validation
- Validates 32-bit Additive checksums and Bosch/Siemens CRC-32 blocks.
- Verifies checksum integrity without modifying the binary file.

### F. Live CAN ↔ Flash Correlation
- Correlates calibrated flash ceilings (e.g. Gear Torque Limiter = 380 Nm) against live observed CAN telemetry (Actual Torque = 330 Nm) to calculate available dynamic headroom.
