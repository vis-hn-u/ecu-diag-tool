$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "     Waveshare USB-CAN-FD-B Physical Hardware Validator         " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. PnP / USB Detection
Write-Host "`n[Step 1] Scanning USB Bus for Physical Device..." -ForegroundColor Yellow
$pnpDevices = Get-PnpDevice -PresentOnly | Where-Object { 
    $_.InstanceId -match "0483" -or 
    $_.FriendlyName -match "CAN" -or 
    $_.Class -in @("WinUsbDevice", "Ports") -and ($_.FriendlyName -match "CAN|STM32|USB") 
}

if ($pnpDevices) {
    Write-Host "[SUCCESS] Detected PnP Device:" -ForegroundColor Green
    $pnpDevices | Format-Table FriendlyName, InstanceId, Status -AutoSize | Out-String | Write-Host
} else {
    Write-Host "[NOTICE] No matching PnP device currently enumerated on USB bus." -ForegroundColor Yellow
}

# 2. DLL Verification
$dllPath = (Resolve-Path "backend\CanEngineeringPlatform.Core\runtimes\win-x64\native\ControlCANFD.dll").Path
Write-Host "`n[Step 2] Verifying Native Driver DLL at: $dllPath" -ForegroundColor Yellow
if (Test-Path $dllPath) {
    $dllBytes = [System.IO.File]::ReadAllBytes($dllPath)
    Write-Host "[SUCCESS] ControlCANFD.dll verified ($($dllBytes.Length) bytes, x64 architecture)." -ForegroundColor Green
} else {
    Write-Host "[ERROR] ControlCANFD.dll not found." -ForegroundColor Red
    exit 1
}

# Ensure DLL directory is in PATH and loaded
$dllDir = [System.IO.Path]::GetDirectoryName($dllPath)
$env:PATH = "$dllDir;$env:PATH"
[System.Runtime.InteropServices.NativeLibrary]::Load($dllPath) | Out-Null

# 3. Add Type
if (-not ([System.Management.Automation.PSTypeName]'WaveshareNativeValidator').Type) {
    $csharpCode = Get-Content "WaveshareNativeValidator.cs" -Raw
    Add-Type -TypeDefinition $csharpCode
}

# 4. Probe Hardware Open
Write-Host "`n[Step 3] Calling ZCAN_OpenDevice(deviceType: 41, deviceIndex: 0)..." -ForegroundColor Yellow
$devHandle = [WaveshareNativeValidator]::ZCAN_OpenDevice(41, 0, 0)

if ($devHandle -eq [IntPtr]::Zero -or $devHandle -eq (New-Object IntPtr (-1))) {
    Write-Host "[RESULT] Physical Hardware NOT Connected (Handle = 0)." -ForegroundColor Red
    Write-Host "         Please connect the Waveshare USB-CAN-FD-B to an active USB port." -ForegroundColor Yellow
    Write-Host "         For bench loopback testing, bridge CAN1_H <-> CAN2_H, CAN1_L <-> CAN2_L," -ForegroundColor Yellow
    Write-Host "         and set the onboard 120 ohm termination switches to ON." -ForegroundColor Yellow
} else {
    Write-Host "[SUCCESS] Hardware Detected! Device Handle: 0x$($devHandle.ToInt64().ToString('X'))" -ForegroundColor Green

    # Query device info
    $info = New-Object WaveshareNativeValidator+ZCAN_DEVICE_INFO
    $ret = [WaveshareNativeValidator]::ZCAN_GetDeviceInf($devHandle, [ref]$info)
    if ($ret -eq 1) {
        $serial = [System.Text.Encoding]::ASCII.GetString($info.str_Serial_Num).TrimEnd([char]0, ' ')
        Write-Host "         Hardware Version: 0x$($info.hw_Version.ToString('X4'))" -ForegroundColor Green
        Write-Host "         Firmware Version: 0x$($info.fw_Version.ToString('X4'))" -ForegroundColor Green
        Write-Host "         Serial Number   : $serial" -ForegroundColor Green
        Write-Host "         CAN Channels    : $($info.can_Num)" -ForegroundColor Green
    }

    # Initialize CAN1 and CAN2
    Write-Host "`n[Step 4] Initializing CAN1 & CAN2 (500k nominal / 2M data, 120 ohm resistance)..." -ForegroundColor Yellow
    $chHandles = @( [IntPtr]::Zero, [IntPtr]::Zero )
    for ($ch = 0; $ch -lt 2; $ch++) {
        [WaveshareNativeValidator]::ZCAN_SetAbitBaud($devHandle, $ch, 500000)
        [WaveshareNativeValidator]::ZCAN_SetDbitBaud($devHandle, $ch, 2000000)
        [WaveshareNativeValidator]::ZCAN_SetResistanceEnable($devHandle, $ch, 1)

        $cfg = New-Object WaveshareNativeValidator+ZCAN_CHANNEL_INIT_CONFIG
        $cfg.can_type = 1 # CAN FD
        $cfg.acc_mask = 0xFFFFFFFF
        $cfg.filter = 1
        $cfg.mode = 0

        $chHandles[$ch] = [WaveshareNativeValidator]::ZCAN_InitCAN($devHandle, $ch, [ref]$cfg)
        if ($chHandles[$ch] -ne [IntPtr]::Zero) {
            [WaveshareNativeValidator]::ZCAN_ClearFilter($chHandles[$ch])
            [WaveshareNativeValidator]::ZCAN_AckFilter($chHandles[$ch])
            [WaveshareNativeValidator]::ZCAN_StartCAN($chHandles[$ch])
            Write-Host "         Channel $($ch + 1) Initialized and Started (Handle: 0x$($chHandles[$ch].ToInt64().ToString('X')))" -ForegroundColor Green
        }
    }

    # Teardown
    for ($ch = 0; $ch -lt 2; $ch++) {
        if ($chHandles[$ch] -ne [IntPtr]::Zero) {
            [WaveshareNativeValidator]::ZCAN_ResetCAN($chHandles[$ch])
        }
    }
    [WaveshareNativeValidator]::ZCAN_CloseDevice($devHandle)
    Write-Host "`n[Step 5] Device handles cleanly released." -ForegroundColor Green
}