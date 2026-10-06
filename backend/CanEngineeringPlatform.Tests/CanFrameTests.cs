using CanEngineeringPlatform.Core.Models;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class CanFrameTests
{
    [Fact]
    public void StandardId_ValidRange_IsValidTrue()
    {
        var frame = new CanFrame(0, CanFrame.StandardIdMax, 8, 0, new byte[8]);
        Assert.True(frame.IsValid());
        Assert.False(frame.IsExtended);
    }

    [Fact]
    public void StandardId_Above7FF_IsValidFalse()
    {
        var frame = new CanFrame(0, 0x800, 8, 0, new byte[8]);
        Assert.False(frame.IsValid());
    }

    [Fact]
    public void ExtendedId_ValidRange_IsValidTrue()
    {
        var frame = new CanFrame(0, CanFrame.ExtendedIdMax, 8, CanFrame.FlagExtended, new byte[8]);
        Assert.True(frame.IsValid());
        Assert.True(frame.IsExtended);
    }

    [Fact]
    public void ExtendedId_Above1FFFFFFF_IsValidFalse()
    {
        var frame = new CanFrame(0, 0x20000000, 8, CanFrame.FlagExtended, new byte[8]);
        Assert.False(frame.IsValid());
    }

    [Fact]
    public void Dlc_ZeroAndEight_Valid()
    {
        var frame0 = new CanFrame(0, 0x100, 0, 0, Array.Empty<byte>());
        Assert.True(frame0.IsValid());
        Assert.Equal(0, frame0.Dlc);

        var frame8 = new CanFrame(0, 0x100, 8, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.True(frame8.IsValid());
        Assert.Equal(8, frame8.Dlc);
        Assert.Equal("01 02 03 04 05 06 07 08", frame8.ToHexString());
    }
}
