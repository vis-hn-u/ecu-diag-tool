# Automotive ECU Engineering Platform — HARDWARE REQUIREMENTS & VCI TRANSITION SPECIFICATION

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  
**Classification:** Hardware Engineering & VCI Product Specification  

---

## 1. Executive Hardware Scope & Handoff Directive

With the software platform complete, hardened, and frozen, this document establishes the physical and electrical requirements for the next development domain: the **Professional Automotive Vehicle Communication Interface (VCI) & Bench Hardware**.

The software architecture is already capability-aware and implements all necessary interfaces (`ICanTransport`, `IDiagnosticTransport`, `IHilService`, `IAsamProtocolService`). The new physical hardware will directly unlock these software capabilities without modifying core domain logic.

```
+-----------------------------------------------------------------------------------+
|                        PROFESSIONAL AUTOMOTIVE VCI HARDWARE                       |
+-----------------------------------------------------------------------------------+
|  [Microcontroller / SoC Core]                                                     |
|   - NXP S32K3 / STM32H7 / Xilinx Zynq UltraScale+ dual-core ARM Cortex-M7/A53     |
|   - Hardware Cryptographic HSM / Secure Element (Secure Boot, Monotonic Counters)  |
|                                                                                   |
|  [Network Transceivers & Physical Layer (PHY)]                                    |
|   - 4x Isolated CAN FD Transceivers (ISO 11898-2:2016 with BRS up to 8 Mbps)      |
|   - 2x LIN Transceivers (ISO 17987 / SAE J2602)                                   |
|   - 1x K-Line / L-Line Transceiver (ISO 9141-2 / ISO 14230) with 5-baud wake-up    |
|   - 1x FlexRay Node (ISO 17458 Channel A + B dual-bus transceiver)                |
|   - 2x Automotive Ethernet PHYs (100BASE-T1 IEEE 802.3bw & 1000BASE-T1 802.3bp)   |
|                                                                                   |
|  [Host Connectivity & Timestamping]                                               |
|   - High-Speed USB 2.0 / USB 3.0 Type-C (FTDI FT4232H or native USB controller)   |
|   - Gigabit Ethernet (RJ45 / M12) for DoIP & XCP on Ethernet                      |
|   - Hardware Timer Engine providing sub-microsecond IEEE 1588 / PTP timestamps    |
|                                                                                   |
|  [Electrical Protection & Bench Controls]                                         |
|   - 2,500V RMS Galvanic Isolation across all vehicle buses and host interfaces    |
|   - ISO 7637-2 / ISO 16750-2 automotive electrical surge/transient protection    |
|   - Programmable Power Relays: Terminal 30 (Continuous 12/24V) & Terminal 15      |
|   - Precision Voltage & Current Monitoring (ADC sampling @ 1 kHz with 0.1% error) |
|   - 6-Channel Analog/Digital Stimulus & Harness Fault Injection Relays            |
+-----------------------------------------------------------------------------------+
```

---

## 2. Protocol & Capability Mapping to Platform Software

| Hardware Feature | Target Silicon / Transceiver | Software Consumer in Frozen Platform |
| :--- | :--- | :--- |
| **Multi-Channel CAN FD** | NXP TJA1443 / TI TCAN1044A | [`WaveshareCanFdTransport.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Transports/WaveshareCanFdTransport.cs) |
| **LIN Master/Slave** | Microchip MCP2003B | [`AutomotiveTransportAbstraction.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/AutomotiveTransportAbstraction.cs) |
| **K-Line / ISO 9141-2** | STMicroelectronics L9637D | [`AutomotiveTransportAbstraction.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/AutomotiveTransportAbstraction.cs) |
| **DoIP & 100BASE-T1** | Marvell 88Q2112 / NXP TJA1100 | [`AutomotiveTransportAbstraction.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/AutomotiveTransportAbstraction.cs) |
| **XCP on CAN / Eth** | Hardware DAQ Buffer Controller | [`AsamProtocolService.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Protocols/Asam/AsamProtocolService.cs) |
| **Terminal 30/15 Power**| Solid-State Power MOSFET Relays | [`HilService.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Hil/HilService.cs) |
| **Bench Fault Injection**| High-Voltage Relay Array | [`HilService.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/Hil/HilService.cs) |
| **Pass-Thru J2534** | Standard J2534-1 / J2534-2 DLL | [`AutomotiveTransportAbstraction.cs`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/backend/CanEngineeringPlatform.Core/Hardware/AutomotiveTransportAbstraction.cs) |

---

## 3. Physical Enclosure & Environmental Specifications

* **Enclosure:** CNC-machined anodized aluminum with IP65 silicone weather gaskets and integrated shock-absorbing rubber bumpers.
* **Operating Temperature:** `-40°C` to `+85°C` (Automotive Grade 1 / Industrial).
* **Connectors:** 
  * Vehicle Port: Standard OBD-II (SAE J1962) with heavy-duty Deutsch HD16-9-1939 adapter cable.
  * Bench Port: 26-pin high-density D-sub connector exposing power, relays, and all analog/digital lines.
  * Host Port: Industrial locking USB-C and sealed RJ45 Ethernet.
