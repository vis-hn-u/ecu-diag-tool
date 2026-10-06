# Automotive ECU Engineering Platform — FINAL DATABASE & PERSISTENCE MODEL

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Storage & Persistence Philosophy

The Automotive Engineering Platform utilizes an **immutable file-backed and structured document model** designed for forensic repeatability, transportability across test benches, and zero external database daemon dependencies.

```
Workspace Storage Root
├── projects/
│   ├── {project-id}.json             # Complete project workspace document
│   ├── {project-id}.hash             # SHA-256 integrity digest
│   └── attachments/                  # Raw binary dumps, logs, captures
├── dbc/
│   ├── standard_obd.dbc              # Built-in SAE J1979 signal dictionary
│   └── user_uploaded/*.dbc           # Vector DBC database definitions
├── asam/
│   ├── a2l/                          # ASAM MCD-2MC ECU database descriptions
│   └── odx/                          # ASAM MCD-2D diagnostic variants
├── flash_images/
│   ├── {image-hash}.bin              # Raw flash dumps
│   └── {image-hash}.manifest.json    # Read parameters, sector maps, entropy
└── logs/
    ├── sessions/                     # Canonical engineering run exports (.csv)
    └── traces/                       # Microsecond diagnostic exchange traces
```

---

## 2. Engineering Project Schema (`EngineeringProject`)

Each project encapsulates the full provenance of an engineering campaign:

```json
{
  "id": "uuid-v4",
  "name": "2024 VAG 2.0TSI ECU Bench Calibration & Validation",
  "description": "Baseline health audit, high-pressure fuel pump diagnostic, and map tuning",
  "createdBy": "Senior Lead Engineer",
  "createdUtc": "2026-09-26T20:00:00Z",
  "lastModifiedUtc": "2026-09-26T22:30:00Z",
  "projectHash": "3f25c79899e120...64-char-hex",
  "vehicle": {
    "vin": "WAUZZZ8V1GA000000",
    "make": "Audi",
    "model": "S3 2.0 TFSI Quattro",
    "year": 2024,
    "engineCode": "DJHA",
    "transmission": "DQ381 7-Speed DSG",
    "architecture": "MQB Evo"
  },
  "ecuTarget": {
    "ecuName": "ECM",
    "family": "Simos18.10",
    "hardwareNumber": "06K907425B",
    "softwareNumber": "SC800S5000000",
    "requestCanId": 2016,
    "responseCanId": 2024,
    "protocol": "ISO 14229 UDS on ISO-TP",
    "mcuFamily": "TriCore TC1791",
    "flashSizeBytes": 4194304
  },
  "hardware": {
    "preferredTransportId": "waveshare-usb-can-fd",
    "nominalBaudRate": 500000,
    "dataBaudRate": 2000000,
    "listenOnly": false,
    "serialPort": "COM3"
  },
  "associatedDbcFiles": ["mqb_powertrain.dbc"],
  "associatedA2lFiles": ["simos18_10_base.a2l"],
  "associatedOdxFiles": ["ev_ecm_vag.odx"],
  "notes": [],
  "evidenceRecords": []
}
```

---

## 3. Forensic Trace Buffer (`DiagnosticTraceService`)

* In-memory bounded circular ring buffer retaining up to 10,000 entries.
* Evicts oldest record automatically upon buffer overflow with zero reallocation spikes.
* Captures nanosecond timestamp, transport ID, direction (Tx/Rx), protocol identifier, and raw payload hex.
