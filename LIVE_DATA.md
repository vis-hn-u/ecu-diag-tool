# Live ECU Data & Runtime Telemetry (Phase 5 Specification)

## 1. Domain A: Real-Time Vehicle / ECU Data Path

```
REAL VEHICLE → CANH/CANL → Transceiver → MCP2515 → ESP8266 (Listen-Only) → WebSocket → ASP.NET Core → CanPipeline → DBC / Signal Decoder → LiveEcuDataService → React Engineering UI
```

---

## 2. Automotive Parameter Categorization

Parameters observed on the bus or decoded via DBC / OBD-II / UDS are classified into domain categories:

- **Engine**: Engine RPM, calculated engine load, throttle position, coolant temperature, oil temperature, intake air temperature, manifold absolute pressure, MAF airflow, fuel rail pressure, lambda/AFR, spark advance, torque requests, actual torque.
- **Air / Turbo**: Boost pressure, boost target, turbo actuator position, VGT/VNT duty cycle, wastegate position, charge-air temp.
- **Fuel**: Fuel rail pressure, high-pressure target, injection quantity, injection duration, start of injection (SOI).
- **EGR**: EGR position, EGR commanded target, EGR estimated mass flow, EGR temperature.
- **DPF**: Differential pressure, exhaust gas temperature before/after filter, soot load %, ash mass, regeneration status.
- **SCR / AdBlue**: AdBlue tank level %, dosing rate, pump pressure, SCR catalyst efficiency, NOx upstream / downstream ppm.
- **Thermal**: Exhaust Gas Temperature (EGT), coolant protection state, oil protection state.
- **Vehicle**: Vehicle road speed, four-wheel speeds, selected gear, brake switch, clutch switch, battery terminal voltage.
- **Diagnostics**: Malfunction Indicator Lamp (MIL), active DTC count, diagnostic session state.

---

## 3. Data Provenance & Safety

Every runtime parameter displays its explicit provenance:
- `DBC`: Decoded from an active Vector DBC message descriptor.
- `OBSERVED CAN`: Raw observed frame without DBC mapping.
- `DIAGNOSTIC RESPONSE`: Polled via OBD-II Mode 01 or UDS Service 0x22 DID.
- `SIMULATION`: Synthetic vehicle dynamics stream.
- `UNKNOWN`: Unclassified traffic.

---

## 4. Requested vs Actual Runtime Comparison

Where matching signal pairs exist in the project, real-time comparison cards compute:
$$\text{Error Delta} = \text{Actual Value} - \text{Target Value}$$
$$\text{Error Percent} = \left(\frac{\text{Error Delta}}{\text{Target Value}}\right) \times 100\%$$
- Requested Torque vs Actual Engine Torque
- Boost Target vs Measured Boost Pressure
- Rail Pressure Target vs Measured Common Rail Pressure
