using System;
using System.Linq;
using CanEngineeringPlatform.Core.Calibration;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class CalibrationWorkspaceTests
{
    [Fact]
    public void CalibrationService_StockInit_LoadsAllStandardMaps()
    {
        var cal = new CalibrationService();
        var proj = cal.GetProject();

        Assert.NotNull(proj);
        Assert.Equal(5, proj.Tables.Count);

        var spark = cal.GetTable("spark_timing_main");
        Assert.NotNull(spark);
        Assert.Equal(10, spark.Data.Length); // 10 Load breakpoints
        Assert.Equal(16, spark.Data[0].Length); // 16 RPM breakpoints
    }

    [Fact]
    public void CalibrationService_CellEditAndDiff_UpdatesCorrectly()
    {
        var cal = new CalibrationService();
        var spark = cal.GetTable("spark_timing_main")!;

        double originalVal = spark.Data[2][3];
        bool updated = cal.UpdateCell("spark_timing_main", 3, 2, originalVal + 4.0);

        Assert.True(updated);
        Assert.Equal(originalVal + 4.0, spark.Data[2][3]);

        var diffs = cal.ComputeDiff("spark_timing_main");
        var modifiedCell = diffs.First(d => d.XIndex == 3 && d.YIndex == 2);

        Assert.Equal("INCREASED", modifiedCell.State);
        Assert.Equal(4.0, modifiedCell.AbsoluteDiff);
    }

    [Fact]
    public void CalibrationService_UndoRedo_RestoresPreviousValues()
    {
        var cal = new CalibrationService();
        var spark = cal.GetTable("spark_timing_main")!;

        double originalVal = spark.Data[0][0];
        cal.UpdateCell("spark_timing_main", 0, 0, 25.0);
        Assert.Equal(25.0, spark.Data[0][0]);

        bool undoSuccess = cal.Undo("spark_timing_main");
        Assert.True(undoSuccess);
        Assert.Equal(originalVal, spark.Data[0][0]);

        bool redoSuccess = cal.Redo("spark_timing_main");
        Assert.True(redoSuccess);
        Assert.Equal(25.0, spark.Data[0][0]);
    }

    [Fact]
    public void CalibrationService_BulkOffsetAndScale_AppliesToAllCells()
    {
        var cal = new CalibrationService();
        var spark = cal.GetTable("spark_timing_main")!;

        double cell00 = spark.Data[0][0];
        cal.BulkOffset("spark_timing_main", 2.0);

        Assert.Equal(cell00 + 2.0, spark.Data[0][0]);
    }
}
