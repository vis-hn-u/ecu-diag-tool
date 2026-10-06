# Automotive ECU Engineering Platform — FINAL DEVELOPER & ENGINEERING EXTENSION GUIDE

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Adding a New ECU Family Profile

To register a new ECU profile in the platform:
1. Open `CanEngineeringPlatform.Core/EcuEngineering/Compatibility/EcuCompatibilityCatalog.cs`.
2. Instantiate an `EcuFamilyProfile`:

```csharp
var profile = new EcuFamilyProfile
{
    FamilyId = "SIEMENS_PCR2_1",
    Name = "Continental Simos PCR2.1",
    Manufacturer = "VAG",
    McuArchitecture = "TriCore TC1796",
    InternalFlashSizeBytes = 2097152,
    SupportsUds = true,
    SupportsKwp2000 = false,
    MemoryRegions = new List<MemoryRegionDefinition>
    {
        new("Bootloader", 0x80000000, 0x8001FFFF, MemoryAccessLevel.ReadOnly),
        new("Calibration", 0x80040000, 0x8007FFFF, MemoryAccessLevel.ReadWrite)
    }
};
RegisterProfile(profile);
```

3. Register associated DIDs and checksum algorithms.

---

## 2. Implementing a Custom Hardware Transport

1. Create a class implementing `ICanTransport` under `CanEngineeringPlatform.Core/Hardware/Transports/`.
2. Define `TransportCapabilities` (channel count, CAN-FD support, transmit flag, supported baud rates).
3. Connect incoming hardware streams to `OnFrameReceived?.Invoke(frame)`.
4. Register the new transport as a singleton in `CanEngineeringPlatform.Api/Program.cs` and inject into `HardwareManager`.

---

## 3. Extending the Knowledge Graph for AI Copilot

1. Use `IResearchKnowledgeService.AddDocumentAsync()` to index OEM technical service bulletins, pinout guides, or wiring schematics.
2. Link documents to ECU families and DTC codes via `AddGraphEdgeAsync("NODE_DTC_P0301", "DOC_TSB_1804", "ResolvedBy")`.
3. The AI Copilot will automatically include these citations when evaluating diagnostic queries.
