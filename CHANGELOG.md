# Changelog

All notable changes to the Automotive CAN / ECU Engineering Platform are documented in this file.

---

## [Phase 5 — Live ECU Data & Read-Only Flash Analysis Platform] — 2026-09-02

### Added
- **Domain A: Live ECU Data & Runtime Telemetry (`LiveEcuDataService`)**:
  - Automotive parameter categorization across 12 standard domains (`Engine`, `Air / Turbo`, `Fuel`, `EGR`, `DPF`, `SCR / AdBlue`, `NOx`, `Thermal`, `Vehicle`, `Transmission`, `Diagnostics`, `Other`).
  - Strict Data Provenance labeling (`DBC`, `OBSERVED CAN`, `DIAGNOSTIC RESPONSE`, `SIMULATION`, `UNKNOWN`).
  - Real-time Requested vs Actual runtime comparison engine computing Error Deltas and % deviations (Requested Torque vs Actual Torque, Boost Target vs Actual Boost, Rail Target vs Actual Rail Pressure).
  - High-performance live parameter workspace with category filters, dynamic search, pin toggles, min/max tracking, and CSV export.
- **Domain B: Read-Only ECU Flash Analyzer (`FlashAnalyzerService`)**:
  - **Strict Safety Constraint**: Zero write/flash APIs, zero calibration cell editing, zero file modification.
  - Binary image inspection with SHA-256 and MD5 cryptographic hashes, detected ECU architecture (Bosch MED17.5 TriCore TC1766), hardware number (`06J906026AB`), and software ID (`1037397854`).
  - Read-only 2D/3D calibration object visualizations (Driver Demand Torque, Gear Torque Limiter, Boost Pressure Target, Rail Pressure Target, Rev Limiter) with explicit `NO EDIT` locking.
  - Virtualized Hex & ASCII byte inspector with arbitrary offset navigation.
  - Byte-by-byte binary diff comparison engine between original stock ROM and modified calibration dumps.
  - Read-only checksum integrity validator (32-bit Additive, Bosch/Siemens CRC-32 blocks).
  - Live CAN $\leftrightarrow$ Flash read-only correlation engine comparing calibrated flash ceilings against live observed CAN signals.
- **Phase 5 Test Suite**:
  - 6 new unit, integration, and safety reflection tests (56 total tests passing).

---

## [Phase 4 — Engine, Dyno & Calibration Platform] — 2026-09-02

### Added
- **Physical Engine Modeling & Deterministic Simulation**:
  - `EngineSimulator`: Implements physics-based internal combustion equations modeling Manifold Absolute Pressure (MAP: 32 kPa vacuum up to 1.35 bar boost), Mass Airflow (MAF), Volumetric Efficiency (VE), Engine Load %, Target Lambda/AFR, Spark Advance, Fuel Flow, Coolant/Oil temperatures, and gear dynamics.
  - Closed-loop CAN integration via `EngineCanBridgeService` streaming `0x280 EngineData` and `0x390 VehicleDynamics` at 20 Hz into `ICanPipeline` and updating virtual ECM diagnostic DIDs.
- **Dyno Power & Torque Analysis Subsystem**:
  - `DynoAnalysisService`: Full dyno pull simulation (WOT from 2000 to 6800 RPM in 4th gear) with dual-axis Power (HP / kW) and Torque (Nm / lb-ft) curves.
  - Peak torque & peak power detection with exact RPM coordinates.
  - Physical acceleration performance estimator ($0-60\text{ mph}$, $0-100\text{ km/h}$, $1/4\text{ mile}$ ET and trap speed).
  - Explicit data source labeling: `SIMULATED`, `MEASURED`, `CALCULATED`.
- **ECU Calibration Studio Workspace**:
  - `CalibrationService`: Manages multidimensional control maps (Ignition Timing Advance, Volumetric Efficiency, Target Lambda, Boost Pressure Target, Maximum Torque Limiter).
  - Interactive 2D/3D matrix heatmap grid with real-time active engine operating point crosshair tracking.
  - Cell editing tools: direct numeric set, bulk $+X$ offset, and bulk $\times Y\%$ scaling.
  - Diff Engine: Compares working calibration copy against stock baseline (`UNCHANGED`, `INCREASED`, `DECREASED`) with absolute and percentage differences.
  - Undo & Redo stack with audit log recording all calibration operations.
- **Engineering Desktop UI Extensions**:
  - `EnginePage`: Digital strip gauges for RPM, MAP/Boost, Torque, Power, Airflow, Lambda, Fuel Flow, and interactive throttle/gear controls.
  - `DynoPage`: Dyno curve viewer with dual-axis plots, peak badges, and $0-60$ / $1/4$ mile acceleration cards.
  - `CalibrationPage`: Professional ECU calibration table editor with 3D heatmap, diff mode, and history inspector.
- **Phase 4 Test Suite**:
  - 8 new unit and integration tests covering power/torque formulas, engine simulation determinism, dyno peak detection, calibration diffs, undo/redo, and engine-to-CAN frame transmission (50 total tests passing).

---

## [Phase 3 — Automotive Diagnostics Platform] — 2026-09-02

### Added
- **ISO-TP Transport Protocol (ISO 15765-2)**:
  - Multi-channel `IsoTpRouter` and stateful `IsoTpSession`.
  - Supports Single Frame (SF), First Frame (FF), Consecutive Frame (CF), and Flow Control (FC).
  - Sequence number tracking (1..15 rollover to 0), BlockSize and STmin flow controls, timeout recovery, and multi-frame segmentation & reassembly.
- **UDS Diagnostic Services (ISO 14229-1)**:
  - `0x10 DiagnosticSessionControl` (Default, Programming, Extended, Safety).
  - `0x11 EcuReset` (Hard Reset, Key Off/On, Soft Reset).
  - `0x14 ClearDiagnosticInformation` (all DTC groups).
  - `0x19 ReadDTCInformation` (ReportDTCByStatusMask with 4-byte DTC/Status structures).
  - `0x22 ReadDataByIdentifier` (Standard DIDs: VIN 0xF190, HW Number 0xF18A, SW Version 0xF189, System Name 0xF197, Active Session 0xF186, Dynamic Engine parameters).
  - `0x27 SecurityAccess` challenge-response framework (request seed / send key models without security circumvention).
  - `0x2E WriteDataByIdentifier` framework.
  - `0x31 RoutineControl` framework (Start, Stop, Request Results).
  - `0x3E TesterPresent`.
  - `0x85 ControlDTCSetting` (ON / OFF).
  - Comprehensive Negative Response Code (NRC) dictionary covering 35+ standard NRCs.
- **OBD-II Parameter Interrogation (SAE J1979 / ISO 15031-5)**:
  - Mode 01 Live Data PIDs (RPM, Vehicle Speed, Coolant Temp, Throttle Position, MAF Rate, Fuel Tank Level, Module Voltage).
  - Supported PID Bitmaps parsing (32-bit bitfield capability decoding for PIDs 0x00, 0x20, 0x40, 0x60, 0x80).
  - Mode 02 Freeze Frame Data.
  - Mode 03 & Mode 07 Stored and Pending DTCs.
  - Mode 04 Clear DTCs and Reset Malfunction Indicators.
  - Mode 09 Vehicle Information (VIN extraction).
- **DTC Engine & Status Byte Decoders**:
  - ISO 14229 status byte mask breakdown: `testFailed (0x01)`, `testFailedThisOperationCycle (0x02)`, `pendingDTC (0x04)`, `confirmedDTC (0x08)`, `testNotCompletedSinceLastClear (0x10)`, `testFailedSinceLastClear (0x20)`, `testNotCompletedThisOperationCycle (0x40)`, `warningIndicatorRequested / MIL (0x80)`.
- **Virtual ECU Simulator**:
  - Standalone software simulation of virtual automotive nodes: `ECM (0x7E0/0x7E8)`, `ABS (0x7E1/0x7E9)`, `BCM (0x7E2/0x7EA)`.
  - Simulates dynamic DID readouts, multi-frame ISO-TP segmentation, and fault DTC injection (`P0300`, `P0115`, `C0035`, `B1325`).
- **Diagnostics Workspace UI**:
  - `Live Diagnostic Trace` with request/response correlation, hex payload inspection, and latency metrics.
  - `OBD-II Dashboard` with live gauges and supported PID discovery.
  - `UDS Command Center` with interactive service invocation and negative response alerts.
  - `DTC Manager` with status flags and 1-click Clear DTCs.
  - `ECU Identification` table displaying VIN, hardware parts, and software versions.
  - `ECU Simulator Control Panel` for fault injection.

---

## [Phase 2 — Offline-First Extension] — 2026-09-02

### Added
- **Progressive Web App (PWA) Offline Support**:
  - `frontend/public/manifest.webmanifest`: Web app manifest for standalone desktop installation.
  - `frontend/public/sw.js`: Service Worker providing cache versioning (`can-platform-shell-v1`) and cache-first offline shell fallback.
  - `frontend/public/favicon.svg`: Local SVG vector icon.
- **Three Explicit Operating Modes**:
  - `LIVE HARDWARE`: Ingests real-time binary CAN traffic from local ESP8266 + MCP2515 gateway.
  - `OFFLINE ANALYSIS`: Full analysis workstation without hardware (inspect saved logs, parse DBC, decode signals, trace topology, export CSV).
  - `SIMULATION`: Realistic 30-second driving cycle simulation player for bench software verification.

---

## [Phase 1] — 2026-09-02

### Added
- **ESP8266 + MCP2515 Firmware Gateway**:
  - Receive-only CAN-to-WebSocket gateway firmware (500 kbit/s, 8 MHz crystal, listen-only mode).
  - 128-slot circular FIFO buffer and 20-byte little-endian binary packet framing.
