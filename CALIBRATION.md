# ECU Calibration Studio (Phase 4 Specification)

## 1. Calibration Architecture

Calibration tables represent the multidimensional control maps governing engine operation:

| Table ID | Table Name | Dimensions | Units | Range |
| :--- | :--- | :--- | :--- | :--- |
| `spark_timing_main` | Ignition Timing Advance | $10 \times 16$ (Load vs RPM) | ° BTDC | $0.0 \dots 45.0$ |
| `ve_fuel_main` | Volumetric Efficiency (VE) | $10 \times 14$ (Load vs RPM) | % | $20.0 \dots 130.0$ |
| `target_lambda_main`| Target Air-Fuel Lambda | $7 \times 7$ (Load vs RPM) | λ | $0.70 \dots 1.15$ |
| `boost_target_main` | Boost Pressure Target | $5 \times 11$ (TPS vs RPM) | bar | $0.0 \dots 2.5$ |
| `torque_limiter_gear`| Maximum Torque Limiter | $6 \times 7$ (Gear vs RPM) | Nm | $100.0 \dots 600.0$ |

---

## 2. Interactive Calibration Tools

- **2D/3D Heatmap Canvas**: Renders dynamic color gradients across matrix cells with active engine operating point crosshairs.
- **Diff Mode**: Compares working copy against stock calibration with cell delta badges:
  - `UNCHANGED`: Matching stock value.
  - `INCREASED`: Positive offset ($\Delta > 0$).
  - `DECREASED`: Negative offset ($\Delta < 0$).
- **Bulk Adjustments**: Apply $+X$ numeric offsets or $\times Y\%$ scaling multipliers across selection.
- **Undo / Redo & Audit History**: Every edit is recorded with previous value, new value, operation, and UTC timestamp.

---

## 3. Calibration Safety Constraints

- All calibration editing operates strictly on the in-memory working copy, offline project files, or simulated engine instances.
- Zero unrestricted CAN writes or flashing commands exist in this tier.
