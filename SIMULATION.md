# Automotive Engineering Simulation (Phase 4 Specification)

## 1. Closed-Loop Simulation Pipeline

The platform establishes an integrated closed-loop software testing environment:

```
[ ECU Calibration Studio ] (Working Map Modifications)
            ↓
[ Deterministic Engine Simulator ] (Physical relationships, IMEP, VE, MAP)
            ↓
[ Virtual ECU Simulator ] (ECM 0x7E0, ABS 0x7E1, BCM 0x7E2)
            ↓
[ Authoritative CanPipeline ] (CanFrame bounded channels)
            ├──► [ DBC Decoder & Live Telemetry ]
            ├──► [ Network Topology Analyzer ]
            ├──► [ Session Logger & CSV Export ]
            └──► [ Dyno Analysis & Performance Graphs ]
```

---

## 2. Operating Modes & Boundary Enforcement

- **LIVE HARDWARE**: Direct communication with physical ESP8266 + MCP2515 hardware gateway.
- **OFFLINE ANALYSIS**: Analysis of recorded log files, DBC files, and saved calibration files without hardware connection.
- **SIMULATION**: Synthetic vehicle physics engine generating realistic CAN frames and diagnostic responses.
