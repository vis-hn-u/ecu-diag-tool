# Engine Simulation & Telemetry Model (Phase 4 Specification)

## 1. Physical Principles & Relationships

The platform implements a deterministic 4-stroke internal combustion engine model with turbocharger dynamics:

- **Manifold Absolute Pressure (MAP)**:
  - At idle/deceleration: Intake vacuum $\approx 32\text{ kPa}$.
  - At cruise: Naturally aspirated throttle modulation ($32\text{ kPa} \dots 101.3\text{ kPa}$).
  - At full load / boost: Turbocharger boost pressure increases up to $P_{\text{max}} = 1.35\text{ bar}$ ($236.3\text{ kPa}$ absolute) above spool threshold ($2200\text{ RPM}$).
- **Volumetric Efficiency (VE)**:
  $$\text{VE}(\text{RPM}) = \text{VE}_{\text{peak}} \cdot \exp\left(-0.5 \left(\frac{\text{RPM} - \text{RPM}_{\text{peak}}}{\sigma}\right)^2\right)$$
- **Mass Airflow (MAF)**:
  $$\text{MAF (g/s)} = \left(\frac{\text{RPM}}{120}\right) \cdot V_{\text{disp}} \cdot \rho_{\text{air}} \cdot \left(\frac{\text{MAP}}{P_{\text{atm}}}\right) \cdot \text{VE}$$
- **Engine Load %**: Actual cylinder air charge relative to naturally aspirated standard conditions.
- **Ignition Timing Advance**: Dynamic lookup based on RPM advance curve and load retardation to prevent spark knock.

---

## 2. Power & Torque Equations

$$\text{Power (kW)} = \frac{\text{Torque (Nm)} \times \text{RPM}}{9548.8}$$
$$\text{Power (HP)} = \frac{\text{Torque (Nm)} \times \text{RPM}}{7120.8} \approx \text{Power (kW)} \times 1.34102$$
$$\text{Torque (lb-ft)} = \text{Torque (Nm)} \times 0.737562$$

---

## 3. CAN Stream Integration

When simulation is running, `EngineCanBridgeService` streams real-time frames into `ICanPipeline` at 20 Hz:
- `0x280 EngineData`: RPM, Engine Torque, Throttle %, Coolant Temp, Engine Load %, Boost, AFR.
- `0x390 VehicleDynamics`: Vehicle Speed, Selected Gear.
- `0x7E0 ECM Diagnostic Target`: Automatically updates live DIDs for UDS Service 0x22 and OBD-II Mode 01 PIDs.
