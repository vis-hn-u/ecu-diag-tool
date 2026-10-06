namespace CanEngineeringPlatform.Core.EcuEngineering.Compatibility;

/// <summary>
/// Major automotive microcontroller architectures found in production engine and vehicle control units.
/// </summary>
public enum McuArchitecture
{
    InfineonTriCore_TC17xx,
    InfineonTriCore_AURIX_TC2xx,
    InfineonTriCore_AURIX_TC3xx,
    NxpPowerPc_MPC55xx,
    NxpPowerPc_MPC56xx,
    NxpS32G_S32K,
    Renesas_RH850,
    Renesas_SuperH_SH705x,
    STMicroelectronics_ST10_C167,
    ArmCortexM_Automotive,
    ArmCortexR_Automotive,
    Generic_Automotive_Mcu
}
