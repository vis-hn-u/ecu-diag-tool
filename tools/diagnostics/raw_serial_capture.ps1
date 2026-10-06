# Diagnostic Tool: Raw Serial Capture
# Bypasses frontend and backend completely, captures raw bytes from COM8 for 10 seconds.

param(
    [string]$PortName = "COM8",
    [int]$BaudRate = 921600,
    [int]$DurationSeconds = 10
)

# Standard CRC-8 (poly 0x07, init 0x00)
function Get-Crc8([byte[]]$bytes, [int]$length) {
    [byte]$crc = 0x00
    for ($i = 0; $i -lt $length; $i++) {
        $crc = [byte]($crc -bxor $bytes[$i])
        for ($bit = 0; $bit -lt 8; $bit++) {
            if (($crc -band 0x80) -ne 0) {
                $crc = [byte]((($crc -shl 1) -bxor 0x07) -band 0xFF)
            } else {
                $crc = [byte](($crc -shl 1) -band 0xFF)
            }
        }
    }
    return $crc
}

Write-Host "Opening $PortName at $BaudRate baud for $DurationSeconds seconds capture..."
$sp = New-Object System.IO.Ports.SerialPort($PortName, $BaudRate, [System.IO.Ports.Parity]::None, 8, [System.IO.Ports.StopBits]::One)
$sp.ReadTimeout = 200

$allBytes = [System.Collections.Generic.List[byte]]::new()

try {
    $sp.Open()
    Write-Host "Port opened successfully. Capturing raw bytes..."
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $buf = New-Object byte[] 256

    while ($sw.Elapsed.TotalSeconds -lt $DurationSeconds) {
        if ($sp.BytesToRead -gt 0) {
            try {
                $read = $sp.Read($buf, 0, [Math]::Min($sp.BytesToRead, $buf.Length))
                if ($read -gt 0) {
                    for ($i = 0; $i -lt $read; $i++) {
                        $allBytes.Add($buf[$i])
                    }
                }
            } catch [TimeoutException] {}
        } else {
            Start-Sleep -Milliseconds 10
        }
    }
    $sw.Stop()
} finally {
    if ($sp.IsOpen) { $sp.Close() }
    $sp.Dispose()
}

$totalBytes = $allBytes.Count
$elapsedSec = $sw.Elapsed.TotalSeconds
$bytesPerSec = if ($elapsedSec -gt 0) { [Math]::Round($totalBytes / $elapsedSec, 2) } else { 0 }

Write-Host "`n=== RAW SERIAL CAPTURE REPORT ==="
Write-Host "Duration: $([Math]::Round($elapsedSec, 2)) seconds"
Write-Host "Total Bytes Received: $totalBytes"
Write-Host "Throughput: $bytesPerSec bytes/sec"

if ($totalBytes -eq 0) {
    Write-Host "NO BYTES RECEIVED from $PortName. Device may be in reset or not sending."
    exit
}

# Hex dump first 128 bytes
Write-Host "`n--- Hex Dump (First $([Math]::Min(128, $totalBytes)) bytes) ---"
$hexDump = ""
$asciiDump = ""
for ($i = 0; $i -lt [Math]::Min(128, $totalBytes); $i++) {
    $b = $allBytes[$i]
    $hexDump += "{0:X2} " -f $b
    if ($b -ge 32 -and $b -le 126) {
        $asciiDump += [char]$b
    } else {
        $asciiDump += "."
    }
    if (($i + 1) % 16 -eq 0) {
        Write-Host ("{0:D4}: {1,-48} | {2}" -f ($i - 15), $hexDump, $asciiDump)
        $hexDump = ""
        $asciiDump = ""
    }
}
if ($hexDump -ne "") {
    Write-Host ("{0:D4}: {1,-48} | {2}" -f ($i - ($i % 16)), $hexDump, $asciiDump)
}

# Scan for 21-byte frames starting with 0xAA 0x55
Write-Host "`n--- 21-Byte Frame Detection ---"
$frameCount = 0
$handshakeCount = 0
$heartbeatCount = 0
$canFrameCount = 0
$crcErrors = 0

$i = 0
while ($i -le ($totalBytes - 21)) {
    if ($allBytes[$i] -eq 0xAA -and $allBytes[$i+1] -eq 0x55) {
        $frame = $allBytes.GetRange($i, 21).ToArray()
        $expectedCrc = Get-Crc8 $frame 20
        $actualCrc = $frame[20]

        $crcOk = ($expectedCrc -eq $actualCrc)
        if (-not $crcOk) {
            $crcErrors++
            Write-Host ("Frame at offset {0}: CRC MISMATCH! Expected 0x{1:X2}, got 0x{2:X2}" -f $i, $expectedCrc, $actualCrc)
        } else {
            $frameCount++
            $dlc = $frame[10]
            $msgType = $frame[11]

            if ($dlc -eq 0xFF -and $msgType -eq 0x02) {
                $handshakeCount++
                $idStr = [System.Text.Encoding]::ASCII.GetString($frame, 2, 8)
                Write-Host ("Frame at {0}: HANDSHAKE from '{1}', Version: {2}, CRC: 0x{3:X2} (VALID)" -f $i, $idStr, $frame[12], $actualCrc)
            } elseif ($dlc -eq 0xFF -and $msgType -eq 0x03) {
                $heartbeatCount++
                $mcpReady = $frame[13]
                $canstat = $frame[14]
                $eflg = $frame[15]
                $rec = $frame[16]
                $tec = $frame[17]
                $baudCode = $frame[18]
                Write-Host ("Frame at {0}: HEARTBEAT: McpReady={1}, CANSTAT=0x{2:X2}, EFLG=0x{3:X2}, REC={4}, TEC={5}, BaudCode={6}, CRC: 0x{7:X2} (VALID)" -f $i, $mcpReady, $canstat, $eflg, $rec, $tec, $baudCode, $actualCrc)
            } elseif ($dlc -le 8) {
                $canFrameCount++
                $canId = [BitConverter]::ToUInt32($frame, 6)
                $dataHex = [BitConverter]::ToString($frame, 12, 8)
                Write-Host ("Frame at {0}: CAN FRAME ID=0x{1:X}, DLC={2}, Data={3}, CRC: 0x{4:X2} (VALID)" -f $i, $canId, $dlc, $dataHex, $actualCrc)
            } else {
                Write-Host ("Frame at {0}: UNKNOWN PACKET DLC=0x{1:X2}, Type=0x{2:X2}" -f $i, $dlc, $msgType)
            }
        }
        $i += 21
    } else {
        $i++
    }
}

Write-Host "`n=== FRAME SUMMARY ==="
Write-Host "Total Valid Frames: $frameCount"
Write-Host "Handshake Frames:   $handshakeCount"
Write-Host "Heartbeat Frames:   $heartbeatCount"
Write-Host "CAN Frames:         $canFrameCount"
Write-Host "CRC Mismatches:     $crcErrors"
