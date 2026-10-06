using System;
using System.Linq;
using System.Reflection;
using CanEngineeringPlatform.Core.Calibration;
using CanEngineeringPlatform.Core.FlashAnalysis;
using CanEngineeringPlatform.Core.Hardware.Transports;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class SafetyReadonlyValidationTests
{
    private static readonly string[] ForbiddenStems = 
    { 
        "Write", "Flash", "SaveModified", "EditCell", "Patch", 
        "Bypass", "Stage1", "Stage2", "Stage3", "Tune", 
        "Immo", "Immobilizer", "UnlockSecurity", "SeedKey" 
    };

    [Fact]
    public void FlashAnalyzerService_ContainsNoWriteOrFlashMethods()
    {
        ValidateServiceMethods(typeof(FlashAnalyzerService));
    }

    [Fact]
    public void EcuDefinitionPackService_ContainsNoWriteOrFlashMethods()
    {
        ValidateServiceMethods(typeof(EcuDefinitionPackService));
    }

    [Fact]
    public void CalibrationService_ContainsNoWriteOrFlashMethods()
    {
        ValidateServiceMethods(typeof(CalibrationService));
    }

    [Fact]
    public void Elm327Transport_ContainsNoWriteOrFlashMethods()
    {
        ValidateServiceMethods(typeof(Elm327Transport));
    }

    [Fact]
    public void EcuDefinitionPacks_AllMaps_EnforceStrictReadOnlyFlag()
    {
        var service = new EcuDefinitionPackService();
        var packs = service.GetAllPacks();

        Assert.NotEmpty(packs);

        foreach (var pack in packs)
        {
            Assert.NotEmpty(pack.Maps);

            foreach (var map in pack.Maps)
            {
                Assert.True(map.IsReadOnly, $"Safety violation: Map '{map.Name}' in pack '{pack.Name}' must have IsReadOnly=true.");
            }
        }
    }

    private static void ValidateServiceMethods(Type serviceType)
    {
        var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            // Skip property getters/setters unless dangerous
            if (method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")))
            {
                continue;
            }

            foreach (var forbidden in ForbiddenStems)
            {
                Assert.False(
                    method.Name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"Safety violation: Method '{method.Name}' on '{serviceType.Name}' violates read-only constraint (contains forbidden stem '{forbidden}').");
            }
        }
    }
}
