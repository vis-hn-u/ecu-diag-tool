# Automotive ECU & Vehicle DBC Database Library

## Overview

A comprehensive collection of **16 production CANdb (.dbc) databases** has been integrated directly into the ECU Diagnostic Tool and CAN Engineering Platform. This covers the entire spectrum of Indian passenger, commercial, and utility vehicles across major ECU manufacturers:

- **Bosch**: EDC16, EDC17, MD1 / MG1 (BS6)
- **Magneti Marelli**: MJD 6F3 / 8F3 / 9DF / 9GF (Multijet / DDiS / Quadrajet)
- **Denso**: Maruti Suzuki K-Series Petrol & Toyota D-4D Diesel
- **Siemens / Continental**: SID305, SID307, PCR2.1, Simos
- **Indian OEM Architectures**: Tata Motors Passenger Fleet, Mahindra & Mahindra Platform
- **Heavy Commercial**: SAE J1939 (Tata Motors Trucks, Ashok Leyland, Eicher, BharatBenz)
- **Global Platforms in India**: Hyundai / Kia C-CAN, VW / Skoda PQ25 & MQB, Toyota Powertrain, Ford CGEA, MG Motor
- **Universal Fallback**: ISO 15765-4 OBD-II / UDS Broadcast

---

## 1. Complete Catalog of Integrated DBC Files

| File Key | Title | ECU Family | Target Vehicles | Key Live Signals |
|---|---|---|---|---|
| `magneti_marelli_mjd_ddis.dbc` | **Magneti Marelli MJD Multijet** | Marelli 6F3 / 8F3 / 9DF | Maruti Swift DDiS, Dzire, Ertiga, Baleno, Tata Vista/Manza, Fiat Punto | `EngineRPM`, `FuelRailPressure`, `BoostPressure`, `EngineCoolantTemp`, `VehicleSpeed` |
| `bosch_edc16_crde.dbc` | **Bosch EDC16C39** | Bosch EDC16 | Mahindra Scorpio 2.6/2.2 CRDe, Bolero CRDe, Tata Safari 2.2 Dicor, Indigo Dicor | `EngineRPM`, `CommonRailPressure`, `BoostPressureActual`, `MainInjectionQuantity`, `BatteryVoltage` |
| `bosch_edc17_mhawk_revotorq.dbc` | **Bosch EDC17 Platform** | Bosch EDC17C53 / C55 / C65 | Mahindra Thar CRDe / Scorpio mHawk, Tata Nexon/Tiago Revotorq, Maruti 1.3/1.6 DDiS, VW Polo 1.5 TDI | `EngineRPM`, `RailPressureActual`, `BoostPressureActual`, `DpfDifferentialPressure`, `DpfSootMass` |
| `bosch_md1_mg1_bs6.dbc` | **Bosch MD1 / MG1 BS6** | Bosch MD1CS018 / MG1 | Mahindra Scorpio-N, Thar BS6, XUV700 mHawk BS6, Tata Safari/Harrier 2.0 Kryotec BS6 | `EngineRPM`, `FuelRailPressure_Actual`, `DefAdBlueTankLevel`, `ScrCatalystTemp`, `DpfSootLoadPercent` |
| `denso_suzuki_toyota.dbc` | **Denso Powertrain** | Denso 32-bit ECU | Maruti Suzuki K-Series (K10B, K12M, K12N, K15B, K15C), Toyota Innova / Fortuner D-4D | `EngineRPM`, `VvtCamAngleIntake`, `ShortTermFuelTrim_STFT`, `LongTermFuelTrim_LTFT`, `CoolantTemp` |
| `siemens_continental_sid_pcr.dbc` | **Siemens & Continental EMS** | Continental SID305 / PCR2.1 | Renault Duster / Lodgy / Kiger 1.5 dCi, Nissan Terrano, VW Polo 1.6 TDI PCR2.1, Ford TDCi | `EngineRPM`, `RailPressureActual`, `TurboBoostPressure`, `InjectionQuantityMain`, `DpfPressureDelta` |
| `tata_motors_passenger.dbc` | **Tata Motors Passenger Network** | Tata Motors EMS & Body | Tata Safari, Harrier (2.0 Kryotec), Nexon (1.2T / 1.5D), Altroz, Tiago, Punch | `EngineRPM`, `DriveModeSelected` (City/Eco/Sport/Rough), `CommonRailPressure`, `VehicleSpeed` |
| `mahindra_passenger.dbc` | **Mahindra Passenger Platform** | Mahindra Powertrain & BCM | Mahindra Scorpio-N, Thar 4x4, XUV700, XUV300, Bolero Neo | `EngineRPM`, `FourWheelDriveMode` (2H/4H/4L), `RearDiffLockEngaged`, `FrontRightDoorOpen` |
| `sae_j1939_commercial.dbc` | **SAE J1939 Heavy Duty** | SAE J1939 29-bit CAN | Tata Prima / Signa, Ashok Leyland, Eicher Pro, BharatBenz | `Engine_Speed` (EEC1), `Driver_Demand_Torque`, `Wheel_Based_Vehicle_Speed`, `Fuel_Level` |
| `hyundai_kia_ccan.dbc` | **Hyundai & Kia C-CAN** | Hyundai-Kia C-CAN | Hyundai Creta, Venue, i20, Verna, Tucson; Kia Seltos, Sonet, Carens | `CR_Ems_EngSpd`, `WHL_SPD_FL`, `WHL_SPD_FR`, `SAS_Angle`, `CYL_PRES`, `ACCELERATOR_PEDAL` |
| `vw_skoda_pq25_edc17.dbc` | **VW & Skoda PQ25 / PQ35** | VW PQ Powertrain | Volkswagen Polo, Vento, Ameo; Skoda Rapid (1.2/1.5/1.6 TDI & TSI) | `MO_Drehzahl`, `MO_Kuehlmitteltemperatur`, `MO_Fahrpedalstellung`, `ESP_VL_Radgeschw` |
| `vw_skoda_mqb_india.dbc` | **VW & Skoda MQB India 2.0** | VW MQB-A0-IN | Volkswagen Taigun, Virtus; Skoda Kushaq, Slavia (1.0 TSI & 1.5 TSI) | `MO_Drehzahl`, `ESP_v_Signal`, `KBI_km_Stand`, `LWI_Lenkradwinkel`, `MO_Kuehlmitteltemp` |
| `toyota_innova_fortuner_pt.dbc` | **Toyota Ref Powertrain** | Toyota Ref PTCAN | Toyota Innova Crysta, Fortuner, Glanza, Urban Cruiser Hyryder | `ENGINE_RPM`, `VEHICLE_SPEED`, `STEER_ANGLE`, `GAS_PEDAL`, `BRAKE_PRESSURE` |
| `ford_india_cgea_pt.dbc` | **Ford India Powertrain** | Ford CGEA 1.2 PTCAN | Ford Figo, EcoSport, Aspire 1.5 TDCi & 1.5 Ti-VCT, Endeavour 2.0/3.2 Panther | `EngAout_N_Actl`, `Veh_V_ActlEng`, `ApedPos_Pc_ActlArb`, `BrkAct_B_Actl` |
| `mg_hector_astor.dbc` | **MG Motor India** | MG Powertrain & ADAS | MG Hector (1.5T Petrol / 2.0 Multijet Diesel), Astor, ZS EV | `Engine_Speed`, `Vehicle_Speed`, `Throttle_Position`, `Steering_Angle`, `Brake_Pedal_Status` |
| `universal_obd2_live_data.dbc` | **Universal ISO 15765-4 OBD-II** | ISO 15765-4 OBD-II | Any OBD-II compliant vehicle (All Indian BS3, BS4, BS6 vehicles) | `EngineRPM` (0x0C), `VehicleSpeed` (0x0D), `CoolantTemp` (0x05), `ThrottlePos` (0x11), `RailPressure` (0x23) |

---

## 2. Technical Architecture & Data Mapping Details

```mermaid
flowchart TD
    subgraph Hardware Transports
        NANO["Arduino Nano + MCP2515 (COM8 GVRET)"]
        ESP["ESP8266 + MCP2515 (COM14 USB Serial)"]
    end

    subgraph CAN Pipeline & Signal Engine
        PIPE["CanPipeline (Bounded Channel 50,000)"]
        PARSER["DbcParser (Vector CANdb Engine)"]
        ACTIVE["Active DBC: Live Signals State"]
    end

    subgraph DBC Library (16 Databases)
        CATALOG["/api/dbc/library (Metadata Catalog)"]
        LOAD["/api/dbc/load/{key} (One-Click Activation)"]
        BOSCH["Bosch EDC16 / EDC17 / MD1 BS6"]
        MARELLI["Magneti Marelli MJD"]
        DENSO["Denso K-Series / Toyota"]
        CONT["Continental SID / PCR2.1"]
        OEM["Tata / Mahindra / J1939"]
    end

    subgraph Frontend Diagnostics & Analysis
        UI["DBC Database Manager UI"]
        DECODER["Live Signal Display & Gauges"]
        TOPOLOGY["CAN Network Topology"]
    end

    NANO --> PIPE
    ESP --> PIPE
    CATALOG --> UI
    UI -- "Select & Activate" --> LOAD
    LOAD --> PARSER
    PARSER --> ACTIVE
    ACTIVE --> PIPE
    PIPE --> DECODER
    PIPE --> TOPOLOGY
```

---

## 3. Key Indian ECU Implementations

### Bosch EDC16C39 (`bosch_edc16_crde.dbc`)
- **Primary Applications**: Mahindra Scorpio CRDe 2.6 / 2.2, Bolero CRDe, Tata Safari 2.2 Dicor, Tata Indigo Dicor, early Maruti Swift DDiS Bosch variant.
- **CAN ID 0x280 (640)**: `EngineRPM` (bits 16\|16, 0.25 rpm), `EngineTorque` (0\|8, -100 Nm offset), `ThrottlePosition` (32\|8), `BrakeSwitch` (40\|1), `ClutchSwitch` (41\|1).
- **CAN ID 0x380 (896)**: `CommonRailPressure` (16\|16, 0.1 bar, 0–1800 bar), `EngineCoolantTemp` (0\|8, -40°C), `FuelTemperature` (8\|8, -40°C).
- **CAN ID 0x480 (1152)**: `BoostPressureActual` (0\|16, 0.1 kPa), `MainInjectionQuantity` (32\|16, 0.01 mg/hub), `EgrDutyCycle` (56\|8, 0.392%).
- **CAN ID 0x580 (1408)**: `MilLampStatus` (0\|1), `WaterInFuelWarning` (1\|1), `BatteryVoltage` (8\|8, 0.1 V), `FuelConsumptionRate` (24\|16, 0.05 l/h).

### Bosch EDC17 Platform (`bosch_edc17_mhawk_revotorq.dbc`)
- **Primary Applications**: Mahindra Thar CRDe / Scorpio mHawk 2.2, Tata Nexon / Tiago / Altroz 1.5 Revotorq, Maruti 1.3 / 1.6 DDiS, VW Polo 1.5 TDI (EDC17C64).
- **CAN ID 0x120 (288)**: `EngineRPM` (0\|16, 0.25 rpm), `ActualEngineTorque` (16\|16, -500 Nm offset), `AcceleratorPedal` (48\|8).
- **CAN ID 0x310 (784)**: `VehicleSpeed` (0\|16, 0.01 km/h), `WheelSpeedFL` (16\|16), `WheelSpeedFR` (32\|16), `BrakePedalActive` (48\|1).
- **CAN ID 0x318 (792)**: `RailPressureActual` (0\|16, 0.1 bar, up to 2200 bar), `RailPressureSetpoint` (16\|16), `BoostPressureActual` (32\|16, 0.1 kPa).
- **CAN ID 0x488 (1160)**: `DpfDifferentialPressure` (0\|16, 0.1 hPa), `DpfSootMass` (16\|8, 0.2 g), `ExhaustGasTemp_PreTurbine` (24\|16, -40°C), `ExhaustGasTemp_PreDpf` (40\|16).

### Bosch MD1 / MG1 BS6 Architecture (`bosch_md1_mg1_bs6.dbc`)
- **Primary Applications**: Mahindra Scorpio-N, Thar BS6, XUV700 mHawk 2.2 BS6 & mStallion 2.0 Petrol, Tata Safari / Harrier 2.0 Kryotec BS6, Tata 1.5 Revotorq BS6.
- **CAN ID 0x100 (256)**: `EngineRPM` (0\|16, 0.125 rpm, 0–8000), `ActualEngineTorque` (16\|16, 0.1 Nm), `DriverDemandedTorque` (32\|16).
- **CAN ID 0x300 (768)**: `FuelRailPressure_Actual` (0\|16, 0.1 bar, up to 2700 bar), `IntakeManifoldPressure_MAP` (32\|16, 0.1 kPa), `AirMassFlow_MAF` (48\|16, 0.1 kg/h).
- **CAN ID 0x400 (1024)**: `DefAdBlueTankLevel` (0\|8, 0.392%, 0–100%), `DefDosingRateActual` (8\|16, 0.1 mg/s), `ScrCatalystTemp` (24\|16, -40°C), `DpfDifferentialPressure` (40\|16, 0.1 mbar), `DpfSootLoadPercent` (56\|8, 0.392%).

### Denso Powertrain (`denso_suzuki_toyota.dbc`)
- **Primary Applications**: Maruti Suzuki Petrol K-Series (K10B Alto/WagonR, K12M/K12N Swift/Baleno/Dzire, K15B/K15C Brezza/Ertiga/Ciaz), Toyota Innova / Fortuner D-4D.
- **CAN ID 0x110 (272)**: `EngineRPM` (0\|16, 0.25 rpm), `ThrottlePosition` (16\|8), `AcceleratorPedal` (24\|8).
- **CAN ID 0x120 (288)**: `VvtCamAngleIntake` (0\|8, 0.5 deg, -50 to +50), `VvtCamAngleExhaust` (8\|8), `KnockRetardCyl1` (16\|8, 0.25 deg), `SparkAdvanceActual` (24\|8, 0.5 deg, -30 to +60).
- **CAN ID 0x320 (800)**: `ShortTermFuelTrim_STFT` (0\|8, 0.78125%, -100 to +99.2%), `LongTermFuelTrim_LTFT` (8\|8), `O2Sensor1Voltage` (16\|8, 0.005 V).

### Mahindra Passenger & Scorpio-N (`mahindra_passenger.dbc`)
- **Primary Applications**: Mahindra Scorpio-N, Thar 4x4 / RWD, XUV700, XUV300, Bolero Neo.
- **CAN ID 0x220 (544)**: `FourWheelDriveMode` (bits 16\|4: 0=2H, 1=4H, 2=4L, 3=Auto), `RearDiffLockEngaged` (20\|1), `HillDescentControlActive` (21\|1).
- **CAN ID 0x7ED (2029)**: Verified Scorpio-N BCM Door Status — `FrontRightDoorOpen` (bit 5, DID 0x037B), `FrontLeftDoorOpen` (bit 4), `RearRightDoorOpen` (bit 3), `RearLeftDoorOpen` (bit 2), `TailgateOpen` (bit 1), `BonnetOpen` (bit 0).

---

## 4. API Endpoints for DBC Management

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/dbc/active` | Returns active DBC metadata, version, node count, message list, and signals |
| `GET` | `/api/dbc/library` | Returns complete catalog of all 16 DBC databases with categories and live signal tags |
| `POST` | `/api/dbc/load/{key}` | Immediately parses and activates the requested DBC file into the live pipeline |
| `GET` | `/api/dbc/library/{key}/content` | Returns raw Vector DBC source code for inspection or export |
| `POST` | `/api/dbc/upload` | Upload and immediately activate any custom user-provided `.dbc` file |
| `POST` | `/api/dbc/reset-default` | Restores default Magneti Marelli MJD Swift DDiS DBC |
| `DELETE` | `/api/dbc/active` | Unloads active DBC |

---

## 5. Verification & Validation Summary

- **DBC Parser Test Suite**: 21 unit tests in `DbcLibraryTests.cs` — all 21 passed with 0 errors.
- **Live API Test**: Tested `/api/dbc/library` returning all 16 items; tested dynamic activation of `bosch_edc17_mhawk_revotorq` and restore to `magneti_marelli_mjd_ddis`.
- **Frontend Build**: `npm run build` completed in 4.09s with 0 errors.
- **Backend Service**: Daemon running live on `http://localhost:5000`.
