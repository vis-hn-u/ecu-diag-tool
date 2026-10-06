using System;
using System.Text;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class UdsAndObdDiagnosticsTests
{
    [Fact]
    public void UdsDecoder_PositiveSessionControlResponse_ParsesCorrectly()
    {
        // 0x50 0x03 0x00 0x32 0x01 0xF4 (Extended Session, P2=50ms, P2*=5000ms)
        byte[] payload = new byte[] { 0x50, 0x03, 0x00, 0x32, 0x01, 0xF4 };
        var resp = UdsDecoder.ParseResponse(payload);

        Assert.True(resp.IsPositive);
        Assert.Equal(UdsServiceId.DiagnosticSessionControl, resp.RequestedServiceId);
        Assert.Equal(0x50, resp.ResponseSid);
        Assert.Contains("Extended Diagnostic Session", resp.DecodedSummary);
    }

    [Fact]
    public void UdsDecoder_NegativeResponse_DecodesNrcProperly()
    {
        // 0x7F 0x22 0x31 (ReadDataByIdentifier Rejected with NRC 0x31 RequestOutOfRange)
        byte[] payload = new byte[] { 0x7F, 0x22, 0x31 };
        var resp = UdsDecoder.ParseResponse(payload);

        Assert.False(resp.IsPositive);
        Assert.Equal(UdsServiceId.ReadDataByIdentifier, resp.RequestedServiceId);
        Assert.Equal(NegativeResponseCode.RequestOutOfRange, resp.Nrc);
        Assert.Equal("Request Out Of Range", resp.NrcDescription);
        Assert.Contains("0x31", resp.DecodedSummary);
    }

    [Fact]
    public void ObdDecoder_Mode01_EngineSpeedAndCoolant_DecodesAccurately()
    {
        // Mode 01 PID 0x0C (Engine RPM = 2000 rpm -> Raw = 8000 = 0x1F40)
        byte[] rpmPayload = new byte[] { 0x41, 0x0C, 0x1F, 0x40 };
        var rpmResp = ObdDecoder.ParseResponse(rpmPayload);

        Assert.True(rpmResp.IsPositive);
        Assert.Equal(ObdMode.CurrentData, rpmResp.Mode);
        Assert.Equal((byte)0x0C, rpmResp.Pid!.Value);
        Assert.Single(rpmResp.PidValues);
        Assert.Equal(2000.0, rpmResp.PidValues[0].Value);
        Assert.Equal("rpm", rpmResp.PidValues[0].Unit);

        // Mode 01 PID 0x05 (Coolant Temp = 88 C -> Raw = 128 = 0x80)
        byte[] tempPayload = new byte[] { 0x41, 0x05, 0x80 };
        var tempResp = ObdDecoder.ParseResponse(tempPayload);

        Assert.True(tempResp.IsPositive);
        Assert.Equal(88.0, tempResp.PidValues[0].Value);
        Assert.Equal("°C", tempResp.PidValues[0].Unit);
    }

    [Fact]
    public void ObdDecoder_Mode03_StoredDtcList_DecodesStandardCodes()
    {
        // 0x43 P0300 (0x03 0x00) and P0115 (0x01 0x15)
        byte[] dtcPayload = new byte[] { 0x43, 0x03, 0x00, 0x01, 0x15 };
        var resp = ObdDecoder.ParseResponse(dtcPayload);

        Assert.True(resp.IsPositive);
        Assert.Equal(ObdMode.StoredDTCs, resp.Mode);
        Assert.Equal(2, resp.DtcCodes.Count);
        Assert.Equal("P0300", resp.DtcCodes[0]);
        Assert.Equal("P0115", resp.DtcCodes[1]);
    }

    [Fact]
    public void DtcDecoder_UdsService0x19_DecodesStatusByteAndFlags()
    {
        // 0x59 0x02 0xFF P0300(0x03, 0x00, 0x00, status 0x2F = Confirmed+Pending+TestFailed)
        byte[] payload = new byte[] { 0x59, 0x02, 0xFF, 0x03, 0x00, 0x00, 0x2F };
        var dtcs = DtcDecoder.ParseUds0x19Response(payload, "ECM", 0x7E8);

        Assert.Single(dtcs);
        var dtc = dtcs[0];
        Assert.Equal("P0300", dtc.Code);
        Assert.True(dtc.IsConfirmed);
        Assert.True(dtc.IsPending);
        Assert.True(dtc.IsActive);
        Assert.Contains("Random/Multiple Cylinder Misfire Detected", dtc.Description);
    }
}
