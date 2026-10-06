# Automotive ECU Engineering Platform — FINAL ARCHITECTURE SPECIFICATION

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  
**Classification:** Professional Engineering System Architecture  

---

## 1. Executive System Overview

The Automotive ECU Engineering Platform is a comprehensive, production-grade workstation designed to integrate automotive network engineering, multi-protocol ECU diagnostics, vehicle digital twin simulation, hardware-in-the-loop (HIL) stimulus, ASAM standards (A2L / ODX / XCP), safe 12-stage flash programming, reverse-engineering intelligence, forensic timelines, and evidence-grounded AI copilot assistance into a unified engineering environment.

```
+---------------------------------------------------------------------------------------+
|                               REACT 18 + TYPESCRIPT FRONTEND                          |
|  - Forensic Timeline        - CAN Reverse Lab         - Digital Twin Multi-ECU        |
|  - HIL Power & Fault Bench  - ASAM MCD-2MC / XCP      - 12-Stage Safe Flasher         |
|  - UDS / OBD-II Diagnostics - ECU Health & Normalizer - AI Copilot & Knowledge Graph  |
+-------------------------------------------+-------------------------------------------+
                                            | REST + SignalR (WebSocket)
+-------------------------------------------v-------------------------------------------+
|                           ASPNET CORE 8.0 MODULAR BACKEND                             |
|  - Project & Provenance Engine (SHA-256)        - Unified Chronological Timeline     |
|  - Reverse-Engineering Heuristic Engine         - Multi-Node Vehicle Digital Twin    |
|  - HIL Test Fixture Stimulus Engine             - ASAM (A2L / ODX / XCP v1.3) Engine |
|  - 12-Stage Safe Programming State Machine      - Vector Knowledge Graph & Copilot   |
|  - Diagnostic Session & NRC Resolver            - Plausibility & OEM Normalizer      |
+-------------------------------------------+-------------------------------------------+
                                            | Hardware Abstraction Layer (ICanTransport)
+-------------------------------------------v-------------------------------------------+
|                          HARDWARE TRANSPORTS & INTEGRATION                            |
|  - Waveshare USB-CAN-FD (Dual Ch, 64B, BRS)      - ESP8266 + MCP2515 (USB Serial)     |
|  - ESP8266 + MCP2515 (WiFi SoftAP WebSocket)     - ELM327 / OBDLink (UART / Bluetooth)|
|  - Deterministic Automotive Simulation Bus       - HIL Power Supply & Harness Relay   |
+---------------------------------------------------------------------------------------+
```

---

## 2. Core Architectural Pillars

### 2.1 Authoritative CAN Data Pipeline (`ICanPipeline`)
All incoming and outgoing frames pass through a centralized, high-throughput lock-free buffer system utilizing `System.Threading.Channels.Channel<CanFrame>` configured with bounded capacities and backpressure guarantees. Downstream consumers (CAN Analyzer, DBC Signal Decoder, Logger, Reverse-Engineering Lab, Digital Twin, Diagnostic Sessions) subscribe concurrently without blocking frame ingest.

### 2.2 Physical Hardware vs Software Abstraction
Hardware is treated strictly as an I/O transport medium:
* **Waveshare USB-CAN-FD-B:** Full dual-channel bidirectional CAN 2.0A/B and ISO CAN-FD support up to 64 bytes payload with Bit Rate Switching (BRS) up to 5 Mbps.
* **ESP8266 + MCP2515 Gateway:** Operates strictly in **Listen-Only (Passive)** hardware mode. Can never inject, acknowledge, or disrupt vehicle traffic. Operates over 921,600 baud binary USB Serial or Wi-Fi WebSocket.
* **ELM327 / OBDLink:** Multi-protocol legacy serial bridge mapping standard OBD PIDs and legacy ISO 9141-2 / ISO 14230-4 K-Line frames into the canonical data model.
* **Simulation Transport:** Zero-dependency loopback transport with sub-millisecond deterministic frame scheduling.

### 2.3 Evidence-Grounded Diagnostic Safety Model
Every state-modifying action (DTC Clear, ECU Reset, Routine Execution, Flashing) implements:
1. Two-man rule equivalent: Explicit cryptographic or operator confirmation.
2. Safety pre-condition validation (Engine Off, Terminal 15 active, Voltage between 12.8V - 14.4V).
3. Non-destructive query fallback (Sessions drop automatically to Default Session upon disconnect or timeout).

---

## 3. Subsystem Breakdown

1. **Project & Provenance:** Stores projects as immutable JSON graphs signed with SHA-256 digest calculated across all vehicle metadata, calibration maps, raw evidence files, and operator notes.
2. **CAN Reverse Engineering Lab:** Shannon entropy calculation per CAN ID, bit transition heatmaps, bit-stride delta tracking, rolling counter extraction (modulo 4, 8, 16), and trailing checksum detection (CRC-8, Sum8, Xor8).
3. **Vehicle Digital Twin:** Real-time multi-ECU network simulation (ECM, TCM, ABS, BCM, EPS, Gateway, ADAS, BMS) simulating 11-bit and 29-bit CAN IDs, signal cross-talk, bus drops, and DTC fault injection.
4. **HIL Bench Architecture:** Virtual and physical bench fixture controlling Terminal 30 (constant battery), Terminal 15 (ignition relay), and 6 analog/digital channels supporting open-circuit, short-to-ground, and short-to-battery fault injection.
5. **ASAM Integration:** MCD-2MC (A2L) characteristics and measurements parser, ODX ECU variant diagnostics descriptor, and XCP on CAN protocol engine.
6. **12-Stage Flashing State Machine:** Strict sequential transitions: Disconnected -> Connected -> Identified -> Diagnostic -> EngineeringAuthorized -> ProgrammingReady -> Programming -> Verifying -> Complete.
7. **Automotive Research Engine & Knowledge Graph:** Graph structure connecting ECU nodes, diagnostic codes, OEM technical service bulletins, and ASAM descriptors with vector search.
8. **AI Copilot:** Evidence-grounded reasoning assistant with confidence scoring, source citation, alternative explanation generation, and physical safety advisories.
