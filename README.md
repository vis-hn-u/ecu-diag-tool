# Automotive CAN / ECU Engineering, Diagnostics & Flash Analysis Platform

**Enterprise Release:** `RELEASE-v5.0-ENTERPRISE-FINAL` (Validated & Frozen)  
**Full System Test Pass Rate:** **395 / 395 Backend (.NET 8)** • **16 / 16 Firmware Native (PlatformIO)** • **100% Clean Frontend Build**

---

### Authoritative Documentation Suite (`docs/`)
* [FINAL_ARCHITECTURE.md](docs/FINAL_ARCHITECTURE.md) — 3-Tier Enterprise Architecture
* [FINAL_CAPABILITY_MATRIX.md](docs/FINAL_CAPABILITY_MATRIX.md) — Hardware & Protocol Matrix
* [FINAL_PROTOCOL_MATRIX.md](docs/FINAL_PROTOCOL_MATRIX.md) — UDS, OBD, ISO-TP, KWP2000, J1939 Specifications
* [FINAL_ECU_MODEL.md](docs/FINAL_ECU_MODEL.md) — ECU Silicon & Memory Model
* [FINAL_DATABASE_MODEL.md](docs/FINAL_DATABASE_MODEL.md) — Persistence & Schema Model
* [FINAL_HARDWARE_ABSTRACTION.md](docs/FINAL_HARDWARE_ABSTRACTION.md) — HAL Specification
* [FINAL_SECURITY_ARCHITECTURE.md](docs/FINAL_SECURITY_ARCHITECTURE.md) — Zero-Trust & Physical Safety
* [FINAL_TEST_REPORT.md](docs/FINAL_TEST_REPORT.md) — Complete 411-Test Execution Report
* [FINAL_PERFORMANCE_REPORT.md](docs/FINAL_PERFORMANCE_REPORT.md) — Throughput & Latency Benchmarks
* [FINAL_DEPLOYMENT_GUIDE.md](docs/FINAL_DEPLOYMENT_GUIDE.md) — Production Deployment Manual
* [FINAL_RECOVERY_GUIDE.md](docs/FINAL_RECOVERY_GUIDE.md) — Disaster & Interrupted Flash Recovery
* [FINAL_OPERATOR_GUIDE.md](docs/FINAL_OPERATOR_GUIDE.md) — Workstation User Manual
* [FINAL_ENGINEERING_GUIDE.md](docs/FINAL_ENGINEERING_GUIDE.md) — Developer & Calibration Extension Guide
* [KNOWN_LIMITATIONS.md](docs/KNOWN_LIMITATIONS.md) — Hardware Constraints & Physics
* [RELEASE_MANIFEST.md](docs/RELEASE_MANIFEST.md) — Release Package Inventory
* [SOFTWARE_FINAL_FREEZE.md](docs/SOFTWARE_FINAL_FREEZE.md) — Authoritative Freeze Attestation

---

## Architecture & Data Flow

```
                                  [REAL CAN BUS (500 kbit/s)]
                                              │
                                              ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 1: ESP8266 + MCP2515 Hardware Gateway (Listen-Only Mode)                         │
│  - Microsecond hardware timestamping                                                   │
│  - 20-byte Little-Endian binary packet streaming over Local Wi-Fi (ws://<ESP-IP>:8088) │
└──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                           │ (Local LAN / Wi-Fi)
                                           ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phases 2..5: ASP.NET Core Authoritative Local Backend (http://localhost:5000)          │
│  - 100% Offline (No cloud, no external APIs, no internet DNS required)                 │
│  - Authoritative Channel-based CAN Frame Pipeline                                      │
│  - Native Vector DBC Parser & Intel/Motorola Signal Decoding Engine                    │
│  - Real-Time Topology State Machine & Unknown CAN ID Discovery                         │
│  - ISO-TP (ISO 15765-2) Multi-frame segmentation & reassembly                          │
│  - UDS (ISO 14229-1) Diagnostic Services & Negative Response (0x7F + NRCs)             │
│  - OBD-II (SAE J1979) Parameter Interrogation (Modes 01, 02, 03, 04, 09)               │
│  - DTC Engine & Status Byte Decoding (Active, Pending, Confirmed, MIL)                 │
│  - Domain A: Live ECU Data categorizer (Engine, Turbo, Fuel, EGR, DPF, SCR, NOx, etc.) │
│  - Domain A: Requested vs Actual real-time error delta comparator                     │
│  - Domain B: Read-Only ECU Flash Analyzer (ROM maps, SHA-256, Checksums, Binary Diff)  │
│  - Closed-Loop Simulation Bridge streaming engine CAN telemetry at 20 Hz               │
│  - Embedded Static File Server hosting the React PWA                                   │
└──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                           │ (Local SignalR / WebSockets)
                                           ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phases 2..5: Progressive Web App (PWA) / Desktop Engineering Workstation               │
│  - 100% Offline-Capable (Service Worker, Web Manifest, Local Caching)                   │
│  - 3 Operating Modes: LIVE HARDWARE | OFFLINE ANALYSIS | SIMULATION                    │
│  - Navigation: Dashboard | CAN Analyzer | Live ECU Data | DBC Signals | Topology       │
│                DBC Manager | Logger | Replay | Graphs | Diagnostics & UDS | Engine     │
│                Dyno Analysis | ECU Calibration Studio | ECU Flash Analyzer (Read-Only) │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## One-Command Quick Start 🚀

Run any one of the following single commands from the project root:

### Option 1: Double-Click or Command Prompt (Windows)
```cmd
start.bat
```

### Option 2: PowerShell
```powershell
.\start.ps1
```

### Option 3: Node / npm
```bash
npm start
```

* This automatically builds the frontend PWA bundle, launches the ASP.NET Core backend server on `http://localhost:5000`, and opens your default browser directly to the engineering platform!

---

## Operating Modes

1. **LIVE HARDWARE**:
   - Ingests live high-speed CAN traffic streamed from the local ESP8266 + MCP2515 gateway. Diagnostic requests transmit explicitly when triggered by the engineer.
2. **OFFLINE ANALYSIS**:
   - Operates completely decoupled from hardware. Load saved local CAN CSV logs, import local `.dbc` files, reconstruct ISO-TP multi-frame diagnostic traces, analyze dyno power curves, inspect ROM binary dumps, and perform byte-by-byte diffs.
3. **SIMULATION**:
   - Generates realistic virtual vehicle nodes (ECM, ABS, BCM) and closed-loop engine dynamics (RPM, boost, throttle, torque, power) responding to UDS/OBD requests and streaming CAN frames without physical CAN connection.

---

## Manual Developer Workflow

### 1. Build & Test Firmware (Phase 1)
```bash
cd firmware/esp8266-can-gateway
pio run -e nodemcuv2
pio test -e nodemcuv2 --without-uploading --without-testing
```

### 2. Build & Test Backend (Phases 1..5)
```bash
dotnet test backend/CanEngineeringPlatform.sln
dotnet run --project backend/CanEngineeringPlatform.Api
```

### 3. Frontend Development (Hot Reloading)
```bash
cd frontend
npm install
npm run dev
```

---

## Phase 5 Capabilities

- **Domain A: Live ECU Data & Runtime Telemetry**:
  - Automotive domain classification across 12 categories: `Engine`, `Air / Turbo`, `Fuel`, `EGR`, `DPF`, `SCR / AdBlue`, `NOx`, `Thermal`, `Vehicle`, `Transmission`, `Diagnostics`, `Other`.
  - Data Provenance tracking (`DBC`, `OBSERVED CAN`, `DIAGNOSTIC RESPONSE`, `SIMULATION`, `UNKNOWN`).
  - Requested vs Actual runtime comparison cards (Torque, Boost, Rail Pressure) with error delta and % deviation.
  - CSV export, category filters, parameter search, and pin tool.
- **Domain B: Read-Only ECU Flash Analyzer**:
  - Strict **READ-ONLY** safety boundary (no cell editing, no binary modification, no physical flashing).
  - Binary image inspection with SHA-256 and MD5 cryptographic hashes, detected ECU family (Bosch MED17.5 / TriCore TC1766), and hardware/software version readouts.
  - Read-only 2D/3D calibration map visualization (Driver Demand Torque, Gear Torque Limiter, Boost Target, Rail Target, Rev Limiter).
  - Virtualized Hex & ASCII dump viewer with offset navigation.
  - Byte-by-byte binary diff comparison engine between stock and modified files.
  - Read-only checksum integrity validator (32-bit Additive, Bosch/Siemens CRC-32).
  - Live CAN $\leftrightarrow$ Flash read-only correlation comparing calibrated flash ceilings against live observed CAN telemetry.
