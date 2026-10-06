# Automotive ECU Engineering Platform — FINAL DATA PROVENANCE SPECIFICATION

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  
**Classification:** Authoritative Technical Specification  

---

## 1. Provenance Architecture Philosophy

In automotive forensic engineering, warranty dispute arbitration, and functional safety compliance (ISO 26262), data without verifiable provenance is considered legally and technically inadmissible.

The Automotive ECU Engineering Platform implements a **Strict Immutable Cryptographic Provenance Model**:
1. **Raw Evidence Preservation:** Raw binary frame captures, UDS request/response exchanges, and binary flash dumps are stored unmodified.
2. **Cryptographic Sealing:** Every project and capture is sealed with SHA-256 checksums computed across all contained assets.
3. **Audit Trail Integrity:** User operations (DTC clear, calibration changes, resets) are logged to the unified timeline with operator IDs, timestamps, and justification strings.

```
+-----------------------------------------------------------------------------------+
|                        CRYPTOGRAPHIC PROVENANCE GRAPH                             |
+-----------------------------------------------------------------------------------+
|  [Raw CAN Frame Capture] (SHA-256: e3b0c442...)                                    |
|         │                                                                         |
|         ├───> [DBC Signal Decoder] (mqb_powertrain.dbc v2.1)                      |
|         │            │                                                            |
|         │            └───> [Decoded Physical Signals] (RPM, Speed, Boost)          |
|         │                                                                         |
|  [Raw Flash Binary] (SHA-256: 7d1a58e0...)                                        |
|         │                                                                         |
|         ├───> [Map Identification Engine]                                         |
|         │            │                                                            |
|         │            └───> [Calibration Workspace Cell Edits] (Delta Tracking)     |
|         │                                                                         |
|         └───> [Project Workspace Digest] (SHA-256 Composite Hash)                  |
+-----------------------------------------------------------------------------------+
```

---

## 2. Project State Provenance Computation

The `ProjectService` generates an authoritative SHA-256 state hash whenever a project is saved or exported:

```csharp
public static string ComputeProjectHash(EngineeringProject project)
{
    using var sha = SHA256.Create();
    var sb = new StringBuilder();
    sb.Append(project.Id).Append(project.Name).Append(project.CreatedUtc);
    sb.Append(project.Vehicle.Vin).Append(project.Vehicle.EngineCode);
    sb.Append(project.EcuTarget.HardwareNumber).Append(project.EcuTarget.SoftwareNumber);
    foreach (var evidence in project.EvidenceRecords.OrderBy(e => e.Id))
    {
        sb.Append(evidence.Id).Append(evidence.Sha256Hash);
    }
    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
    return Convert.ToHexString(sha.ComputeHash(bytes));
}
```

---

## 3. Forensic Timeline Preservation

The `UnifiedTimelineService` indexes all system events into a single sequence:
* Nanosecond hardware timestamps where provided by adapter.
* Fallback to high-resolution system clock (`DateTime.UtcNow`).
* Retains raw hexadecimal payloads for every diagnostic exchange alongside interpreted models.
* Exportable to standard CSV and JSON-L for external forensic review.
