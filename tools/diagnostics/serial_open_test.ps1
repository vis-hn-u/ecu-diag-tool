# Diagnostic Tool: Serial Port Open Test
# Tests repeated open, close, and re-open on candidate COM ports.

$ports = [System.IO.Ports.SerialPort]::GetPortNames()
Write-Host "=== COM Port Enumeration ==="
Write-Host "Found $($ports.Count) ports: $($ports -join ', ')"

$targetPort = "COM8"
if ($ports -contains $targetPort) {
    Write-Host "`nTesting Target Port: $targetPort at 921600 baud (8N1)"
    $successCount = 0
    $failureCount = 0

    for ($i = 1; $i -le 10; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $sp = New-Object System.IO.Ports.SerialPort($targetPort, 921600, [System.IO.Ports.Parity]::None, 8, [System.IO.Ports.StopBits]::One)
        $sp.ReadTimeout = 500
        $sp.WriteTimeout = 500

        try {
            $sp.Open()
            $sw.Stop()
            if ($sp.IsOpen) {
                Write-Host "Cycle $i/10: SUCCESS - Opened in $($sw.ElapsedMilliseconds) ms"
                $successCount++
            } else {
                Write-Host "Cycle $i/10: FAILED - IsOpen was false"
                $failureCount++
            }
        } catch {
            $sw.Stop()
            Write-Host "Cycle $i/10: EXCEPTION - $($_.Exception.Message) ($($_.Exception.GetType().Name))"
            $failureCount++
        } finally {
            if ($sp.IsOpen) {
                $sp.Close()
            }
            $sp.Dispose()
        }
        Start-Sleep -Milliseconds 100
    }

    Write-Host "`n=== Summary ==="
    Write-Host "Success: $successCount / 10"
    Write-Host "Failure: $failureCount / 10"
} else {
    Write-Host "Target port $targetPort not found among enumerated ports."
}
