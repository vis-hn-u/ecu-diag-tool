using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Manufacturer;

public enum ManufacturerOem
{
    Generic,
    MarutiMarelli, // Maruti Suzuki Swift Diesel (Magneti Marelli MJD)
    VAG,        // Volkswagen, Audi, Seat, Skoda, Porsche
    BMW,        // BMW, Mini, Rolls-Royce
    Ford,       // Ford, Lincoln
    GM,         // Chevrolet, Cadillac, GMC, Buick, Opel
    Toyota,     // Toyota, Lexus
    Mercedes    // Mercedes-Benz, AMG
}

public enum LiveDataIdentifierType
{
    UdsDid,         // Service 0x22 ReadDataByIdentifier (2-byte DID)
    KwpLocalId,     // Service 0x21 ReadDataByLocalIdentifier (1-byte or 2-byte LID)
    ObdPid,         // Service 0x01 / 0x02 Mode PID
    BroadcastCan    // Periodic Broadcast CAN Signal
}

public class ManufacturerLiveDataDefinition
{
    public string Id { get; set; } = string.Empty;
    public ManufacturerOem Oem { get; set; } = ManufacturerOem.Generic;
    public string Category { get; set; } = "Engine";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LiveDataIdentifierType IdentifierType { get; set; } = LiveDataIdentifierType.UdsDid;
    public uint Identifier { get; set; }
    public string IdentifierHex => IdentifierType == LiveDataIdentifierType.UdsDid 
        ? $"0x{Identifier:X4}" 
        : $"0x{Identifier:X2}";

    public int ByteOffset { get; set; } = 0;
    public int ByteLength { get; set; } = 2;
    public bool IsBigEndian { get; set; } = true;
    public bool IsSigned { get; set; } = false;
    public double Factor { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string Unit { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public string DecimalFormat { get; set; } = "F1";
    public Dictionary<int, string>? StateLabels { get; set; }
}

public class DecodedManufacturerValue
{
    public string DefinitionId { get; set; } = string.Empty;
    public ManufacturerOem Oem { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double RawValue { get; set; }
    public double ScaledValue { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? StateLabel { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public bool IsWarning { get; set; }
}
