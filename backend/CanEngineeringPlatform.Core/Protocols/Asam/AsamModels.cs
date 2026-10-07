using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Protocols.Asam;

// --- A2L (ASAM MCD-2 MC) Models ---
public class A2lMeasurementDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DataType { get; set; } = "UBYTE"; // UBYTE, SBYTE, UWORD, SWORD, ULONG, SLONG, FLOAT32_IEEE
    public long EcuAddress { get; set; }
    public string ConversionMethod { get; set; } = "NO_COMPU_METHOD";
    public double Resolution { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public double LowerLimit { get; set; } = 0.0;
    public double UpperLimit { get; set; } = 255.0;
    public string Unit { get; set; } = string.Empty;
}

public class A2lCharacteristicDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "VALUE"; // VALUE, CURVE, MAP, CUBOID, VAL_BLK
    public long EcuAddress { get; set; }
    public long SizeBytes { get; set; }
    public string RecordLayout { get; set; } = string.Empty;
    public double LowerLimit { get; set; }
    public double UpperLimit { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class A2lDatabase
{
    public string EcuProjectName { get; set; } = "MED17_EA888";
    public string Epk { get; set; } = "EPK_1037505555_VAG";
    public List<A2lMeasurementDescriptor> Measurements { get; set; } = new();
    public List<A2lCharacteristicDescriptor> Characteristics { get; set; } = new();
}

// --- ODX (ASAM MCD-2 D) Models ---
public class OdxDiagServiceDescriptor
{
    public string SemanticId { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public byte RequestSid { get; set; }
    public byte? SubFunction { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class OdxEcuVariant
{
    public string EcuVariantId { get; set; } = "EV_ECM_MARELLI_MJD";
    public string VariantName { get; set; } = "Magneti Marelli MJD (Swift DDiS)";
    public List<OdxDiagServiceDescriptor> Services { get; set; } = new();
}

// --- XCP (Universal Measurement and Calibration Protocol) ---
public enum XcpTransportType
{
    XcpOnCan,
    XcpOnCanFd,
    XcpOnEthernet,
    XcpOnSpi
}

public class XcpSessionStatus
{
    public bool IsConnected { get; set; }
    public XcpTransportType Transport { get; set; } = XcpTransportType.XcpOnCan;
    public byte ResourceProtectionMask { get; set; } // Cal/Page, DAQ, STIM, PGM
    public ushort MaxCtoSize { get; set; } = 8;
    public ushort MaxDtoSize { get; set; } = 8;
    public string ProtocolVersion { get; set; } = "XCP v1.3";
    public int ActiveDaqListCount { get; set; }
}

public interface IAsamProtocolService
{
    A2lDatabase GetA2lDatabase();
    OdxEcuVariant GetOdxVariant();
    XcpSessionStatus GetXcpStatus();
    bool ConnectXcp(XcpTransportType transport);
    void DisconnectXcp();
    byte[] XcpShortUpload(long address, byte length);
    bool XcpShortDownload(long address, byte[] data);
}

public class AsamProtocolService : IAsamProtocolService
{
    private readonly A2lDatabase _a2lDb = new();
    private readonly OdxEcuVariant _odxVariant = new();
    private readonly XcpSessionStatus _xcpStatus = new();
    private readonly object _lock = new();

    public AsamProtocolService()
    {
        InitializeSampleAsamData();
    }

    private void InitializeSampleAsamData()
    {
        // Seed standard A2L entries for MED17
        _a2lDb.Measurements.AddRange(new[]
        {
            new A2lMeasurementDescriptor { Name = "nmot_w", Description = "Engine Speed (crankshaft RPM)", DataType = "UWORD", EcuAddress = 0x380010, Resolution = 0.25, Unit = "1/min", LowerLimit = 0, UpperLimit = 8500 },
            new A2lMeasurementDescriptor { Name = "tmot", Description = "Engine Coolant Temperature", DataType = "SBYTE", EcuAddress = 0x380012, Resolution = 0.75, Offset = -48, Unit = "°C", LowerLimit = -40, UpperLimit = 140 },
            new A2lMeasurementDescriptor { Name = "plvd_w", Description = "Intake Manifold Absolute Pressure", DataType = "UWORD", EcuAddress = 0x380014, Resolution = 0.1, Unit = "hPa", LowerLimit = 0, UpperLimit = 3500 },
            new A2lMeasurementDescriptor { Name = "wped", Description = "Normalized Accelerator Pedal Position", DataType = "UBYTE", EcuAddress = 0x380016, Resolution = 0.392, Unit = "%", LowerLimit = 0, UpperLimit = 100 },
            new A2lMeasurementDescriptor { Name = "lamsoni", Description = "Wideband Lambda Equivalence Ratio", DataType = "UWORD", EcuAddress = 0x380018, Resolution = 0.000244, Unit = "λ", LowerLimit = 0.65, UpperLimit = 1.6 }
        });

        _a2lDb.Characteristics.AddRange(new[]
        {
            new A2lCharacteristicDescriptor { Name = "KFZW", Description = "Ignition Advance Angle Base Map", Type = "MAP", EcuAddress = 0x810000, SizeBytes = 256, Unit = "°CA", LowerLimit = -10, UpperLimit = 55 },
            new A2lCharacteristicDescriptor { Name = "KFMIRL", Description = "Engine Target Indicated Torque Map", Type = "MAP", EcuAddress = 0x812000, SizeBytes = 256, Unit = "%", LowerLimit = 0, UpperLimit = 350 },
            new A2lCharacteristicDescriptor { Name = "LDRXN", Description = "Maximum Allowed Engine Load (Boost Limit)", Type = "CURVE", EcuAddress = 0x814000, SizeBytes = 32, Unit = "%", LowerLimit = 0, UpperLimit = 260 }
        });

        // Seed sample ODX services
        _odxVariant.Services.AddRange(new[]
        {
            new OdxDiagServiceDescriptor { SemanticId = "DSC_03", ShortName = "DiagnosticSessionControl_Extended", RequestSid = 0x10, SubFunction = 0x03, Description = "Switch to Extended Diagnostic Session" },
            new OdxDiagServiceDescriptor { SemanticId = "RDBI_VIN", ShortName = "ReadDataByIdentifier_VIN", RequestSid = 0x22, Description = "Read Vehicle Identification Number (DID 0xF190)" },
            new OdxDiagServiceDescriptor { SemanticId = "CDTC_ALL", ShortName = "ClearDiagnosticInformation_All", RequestSid = 0x14, Description = "Clear All Diagnostic Trouble Codes" },
            new OdxDiagServiceDescriptor { SemanticId = "ER_HARD", ShortName = "ECUReset_HardReset", RequestSid = 0x11, SubFunction = 0x01, Description = "Execute Microcontroller Power-On Reset" }
        });
    }

    public A2lDatabase GetA2lDatabase() => _a2lDb;
    public OdxEcuVariant GetOdxVariant() => _odxVariant;
    public XcpSessionStatus GetXcpStatus()
    {
        lock (_lock)
        {
            return new XcpSessionStatus
            {
                IsConnected = _xcpStatus.IsConnected,
                Transport = _xcpStatus.Transport,
                ResourceProtectionMask = _xcpStatus.ResourceProtectionMask,
                MaxCtoSize = _xcpStatus.MaxCtoSize,
                MaxDtoSize = _xcpStatus.MaxDtoSize,
                ProtocolVersion = _xcpStatus.ProtocolVersion,
                ActiveDaqListCount = _xcpStatus.ActiveDaqListCount
            };
        }
    }

    public bool ConnectXcp(XcpTransportType transport)
    {
        lock (_lock)
        {
            _xcpStatus.IsConnected = true;
            _xcpStatus.Transport = transport;
            _xcpStatus.ResourceProtectionMask = 0x00; // All unlocked in simulation
            _xcpStatus.ActiveDaqListCount = 2;
            return true;
        }
    }

    public void DisconnectXcp()
    {
        lock (_lock)
        {
            _xcpStatus.IsConnected = false;
            _xcpStatus.ActiveDaqListCount = 0;
        }
    }

    public byte[] XcpShortUpload(long address, byte length)
    {
        // Simulated XCP memory read
        byte[] buffer = new byte[length];
        for (int i = 0; i < length; i++)
        {
            buffer[i] = (byte)((address + i) & 0xFF);
        }
        return buffer;
    }

    public bool XcpShortDownload(long address, byte[] data)
    {
        if (data == null || data.Length == 0) return false;
        // Simulated non-volatile write
        return true;
    }
}
