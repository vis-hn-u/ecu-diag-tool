using System;

namespace CanEngineeringPlatform.Core.Diagnostics.Uds;

public static class UdsDecoder
{
    public static UdsResponse ParseResponse(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return new UdsResponse
            {
                IsPositive = false,
                Nrc = NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat,
                DecodedSummary = "Empty Response Payload"
            };
        }

        byte sid = payload[0];

        // Negative Response: 0x7F <RequestedSID> <NRC>
        if (sid == (byte)UdsServiceId.NegativeResponse)
        {
            if (payload.Length < 3)
            {
                return new UdsResponse
                {
                    IsPositive = false,
                    ResponseSid = sid,
                    Nrc = NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat,
                    DecodedSummary = "Truncated Negative Response"
                };
            }

            var reqSid = (UdsServiceId)payload[1];
            var nrc = (NegativeResponseCode)payload[2];

            return new UdsResponse
            {
                IsPositive = false,
                RequestedServiceId = reqSid,
                ResponseSid = sid,
                Nrc = nrc,
                Data = payload.Length > 3 ? payload[3..] : Array.Empty<byte>(),
                DecodedSummary = $"Negative Response: {reqSid} Rejected with NRC 0x{(byte)nrc:X2} ({NrcHelper.GetDescription(nrc)})"
            };
        }

        // Positive Response (Response SID = Request SID + 0x40)
        byte reqServiceId = (byte)(sid - 0x40);
        var service = (UdsServiceId)reqServiceId;

        var response = new UdsResponse
        {
            IsPositive = true,
            RequestedServiceId = service,
            ResponseSid = sid,
            Data = payload.Length > 1 ? payload[1..] : Array.Empty<byte>()
        };

        // Specific service decoding
        switch (service)
        {
            case UdsServiceId.DiagnosticSessionControl:
                if (payload.Length >= 2)
                {
                    byte sessionType = payload[1];
                    string sessionName = sessionType switch
                    {
                        0x01 => "Default Session",
                        0x02 => "Programming Session",
                        0x03 => "Extended Diagnostic Session",
                        0x04 => "Safety System Diagnostic Session",
                        _ => $"Custom Session (0x{sessionType:X2})"
                    };
                    response.DecodedSummary = $"Session Active: {sessionName}";
                }
                break;

            case UdsServiceId.EcuReset:
                if (payload.Length >= 2)
                {
                    byte resetType = payload[1];
                    string resetName = resetType switch
                    {
                        0x01 => "Hard Reset",
                        0x02 => "Key Off/On Reset",
                        0x03 => "Soft Reset",
                        _ => $"Reset Type (0x{resetType:X2})"
                    };
                    response.DecodedSummary = $"ECU Reset Acknowledged: {resetName}";
                }
                break;

            case UdsServiceId.ReadDataByIdentifier:
                if (payload.Length >= 3)
                {
                    ushort did = (ushort)((payload[1] << 8) | payload[2]);
                    response.DataIdentifier = did;
                    byte[] didData = payload.Length > 3 ? payload[3..] : Array.Empty<byte>();
                    response.Data = didData;
                    response.DecodedSummary = $"DID 0x{did:X4} Data ({didData.Length} bytes)";
                }
                break;

            case UdsServiceId.TesterPresent:
                response.DecodedSummary = "Tester Present Acknowledged (0x7E)";
                break;

            case UdsServiceId.ClearDiagnosticInformation:
                response.DecodedSummary = "Diagnostic Trouble Codes Cleared";
                break;

            default:
                response.DecodedSummary = $"Positive Response for Service {service} ({payload.Length} bytes)";
                break;
        }

        return response;
    }

    public static UdsRequest ParseRequest(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return new UdsRequest { ServiceId = (UdsServiceId)0 };
        }

        var req = new UdsRequest
        {
            ServiceId = (UdsServiceId)payload[0],
            Payload = payload
        };

        if (payload.Length > 1)
        {
            req.SubFunction = payload[1];
        }

        if (req.ServiceId == UdsServiceId.ReadDataByIdentifier && payload.Length >= 3)
        {
            req.DataIdentifier = (ushort)((payload[1] << 8) | payload[2]);
        }

        return req;
    }
}
