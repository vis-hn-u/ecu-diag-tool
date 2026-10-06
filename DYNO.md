# Dyno Analysis & Vehicle Performance (Phase 4 Specification)

## 1. Dyno Pull Segmentation

The Dyno subsystem segments Wide-Open Throttle (WOT) engine pulls from $\text{StartRPM}$ ($2000\text{ RPM}$) up to $\text{EndRPM}$ ($6800\text{ RPM}$).

### Peak Detection
- **Peak Torque**: Maximum torque value and the exact engine RPM where it occurs.
- **Peak Power**: Maximum power (HP / kW) and the exact engine RPM where it occurs.

### Explicit Labeling
- `SIMULATED`: Generated from deterministic physical engine model.
- `MEASURED`: Ingested from physical dynamometer sensors or vehicle CAN log.
- `CALCULATED`: Derived from vehicle mass, wheel torque, and gear acceleration.

---

## 2. Vehicle Performance Estimation

Derived from vehicle curb weight ($1450\text{ kg}$) and effective wheel power:
- $0 - 60\text{ mph}$ ($0 - 100\text{ km/h}$) acceleration time.
- $60 - 100\text{ km/h}$ and $80 - 120\text{ km/h}$ rolling acceleration intervals.
- $1/4\text{ mile}$ elapsed time and trap speed.
