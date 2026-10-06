using System;
using System.Text;

namespace CanEngineeringPlatform.Core.Diagnostics.EcuInfo;

public static class EcuInfoDecoder
{
    public static void ApplyDidResponse(EcuIdentification ecu, ushort did, byte[] data)
    {
        if (data == null || data.Length == 0) return;

        string ascii = Encoding.ASCII.GetString(data).Trim('\0', ' ', '\r', '\n');
        string hex = BitConverter.ToString(data).Replace("-", " ");

        switch (did)
        {
            case 0xF190: // VIN
                ecu.Vin = ascii;
                ecu.VinStatus = DataAvailability.Supported;
                break;

            case 0xF191: // Vehicle Manufacturer ECU Hardware Number
            case 0xF18A: // System Supplier ECU Hardware Number
                ecu.HardwareNumber = ascii.Length >= 4 ? ascii : hex;
                ecu.HardwareNumberStatus = DataAvailability.Supported;
                break;

            case 0xF188: // Vehicle Manufacturer ECU Software Number
                ecu.SoftwareNumber = ascii.Length >= 4 ? ascii : hex;
                ecu.SoftwareNumberStatus = DataAvailability.Supported;
                break;

            case 0xF189: // System Supplier ECU Software Version Number
                ecu.SoftwareVersion = ascii.Length >= 2 ? ascii : hex;
                ecu.SoftwareVersionStatus = DataAvailability.Supported;
                break;

            case 0xF180: // Boot Software Identification
                ecu.BootVersion = ascii.Length >= 2 ? ascii : hex;
                ecu.BootVersionStatus = DataAvailability.Supported;
                break;

            case 0xF18C: // ECU Serial Number
                ecu.SerialNumber = ascii.Length >= 4 ? ascii : hex;
                ecu.SerialNumberStatus = DataAvailability.Supported;
                break;

            case 0xF197: // System Name
                ecu.SystemName = ascii;
                ecu.SystemNameStatus = DataAvailability.Supported;
                break;

            case 0xF186: // Active Diagnostic Session
                if (data.Length >= 1)
                {
                    ecu.ActiveSession = data[0] switch
                    {
                        0x01 => "Default Session (0x01)",
                        0x02 => "Programming Session (0x02)",
                        0x03 => "Extended Session (0x03)",
                        0x04 => "Safety System Session (0x04)",
                        _ => $"Session 0x{data[0]:X2}"
                    };
                }
                break;

            default:
                ecu.CustomDids[did] = ascii.Length >= 2 && IsPrintable(ascii) ? ascii : hex;
                break;
        }

        ecu.LastQueriedUtc = DateTime.UtcNow;
    }

    private static bool IsPrintable(string text)
    {
        foreach (char c in text)
        {
            if (c < 32 || c > 126) return false;
        }
        return true;
    }
}
