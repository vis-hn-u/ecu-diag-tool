# Automotive CAN / ECU Engineering Platform — System Architecture

This document describes the complete multi-tier architecture for the CAN Engineering Platform across **Phase 1 (Hardware Gateway)** and **Phase 2 (Offline-First CAN Engineering Platform)**.

---

## 1. High-Level System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       REAL CAN BUS                          │
│                (ISO 11898-2 High-Speed 500k)                │
└─────────────────────────────┬───────────────────────────────┘
                              │ CAN_H / CAN_L
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ Phase 1: ESP8266 + MCP2515 CAN Gateway (Listen-Only Mode)   │
│  - MCP2515 CAN Controller (8.000 MHz crystal)               │
│  - Level-shifted SPI (HSPI: D5/D6/D7/D2/D1)                 │
│  - Microsecond timestamping & 128-slot circular FIFO buffer │
│  - Zero-allocation ISR (event flag only)                    │
│  - Fixed 20-byte Little-Endian binary packet framing        │
└─────────────────────────────┬───────────────────────────────┘
                              │
                              │ 20-byte Binary WebSocket Stream (ws://<ESP-IP>:8088)
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ Phase 2: ASP.NET Core Local Backend (http://localhost:5000) │
│                                                             │
│   [ GatewayClientService ] ──► [ BinaryPacketDecoder ]      │
│                                           │                 │
│                                           ▼                 │
│                                 [ Authoritative Pipeline ]  │
│                                           │                 │
│           ┌───────────────────────────────┼─────────────┐   │
│           ▼                               ▼             ▼   │
│   [ DbcDecoderService ]          [ TopologyEngine ] [ Logger]
│           │                               │             │   │
│           ▼                               ▼             ▼   │
│   [ Decoded Signals ]            [ Network Topology ] [CSV] │
│           │                               │             │   │
│           └───────────────────────────────┼─────────────┘   │
│                                           ▼                 │
│                             [ SignalR Hub: /hubs/can ]      │
│                                           │                 │
│                             [ Static SPA File Provider ]    │
└─────────────────────────────────────┬───────────────────────┘
                                      │ Local WebSockets / SignalR / Static Assets
                                      ▼
┌─────────────────────────────────────────────────────────────┐
│ Phase 2: Progressive Web App (PWA) / Desktop Engineering UI │
│  - Service Worker (sw.js) for 100% offline shell caching    │
│  - Web App Manifest (manifest.webmanifest) for install      │
│  - Native System Fonts & Local CSS/JS (Zero CDN reliance)   │
│  - 3 Operating Modes: LIVE HARDWARE | OFFLINE | SIMULATION  │
│  - CAN Analyzer (Virtualized stream, search, byte inspector)│
│  - Live Data Dashboard (Physical values, ECU grouping, pin) │
│  - Real-Time Topology (Interactive graph, signal tracing)   │
│  - DBC Manager (Local Vector CANdb .dbc import & validation)│
│  - Session Logger (Capture to buffer & 1-click CSV export)  │
│  - Simulation Replay (Speed control, timeline seeking)      │
│  - Signal Oscilloscope (Multi-trace real-time graphs)       │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Offline-First Principles & Guarantees

1. **Zero Internet Requirement**:
   - The application has zero cloud authentication, zero external API dependencies, zero remote font stylesheets, and zero analytics/telemetry scripts.
   - All assets (JavaScript, CSS, fonts, icons) are bundled locally.
2. **Deterministic Offline Startup**:
   - The workstation can boot and run without any active network interface.
   - When opened, the Service Worker serves the cached app shell immediately.
3. **Local Storage Strategy**:
   - Lightweight UI preferences (pinned signals, active mode, filter parameters) persist in browser `localStorage`.
   - Authoritative high-rate CAN traffic is processed in memory and exported as local CSV files.
4. **Safety in Replay**:
   - Simulation replay routes exclusively through the software ingestion pipeline without transmitting frames onto physical vehicle buses.
