# Automotive ECU Engineering Platform — FINAL DEPLOYMENT GUIDE

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. System Requirements & Prerequisites

* **Operating System:** Windows 10/11 (64-bit), Windows Server 2022, or Ubuntu 22.04/24.04 LTS (x86_64).
* **Runtime Environments:**
  * Microsoft .NET 8.0 Runtime / SDK (`8.0.30+`).
  * Node.js LTS (`v20.x+`) & npm.
  * PlatformIO Core CLI (for firmware flashing).
* **Physical Hardware Interfaces:**
  * Waveshare USB-CAN-FD-B (Requires CH343 / STM32 VCP Driver).
  * ESP8266 + MCP2515 Gateway (CP2102 or CH340 USB-UART Driver).
  * OBDLink SX / EX / ELM327 USB Adapter.

---

## 2. Production Build & Deployment Steps

### 2.1 Backend Build & Publish
```powershell
cd "backend"
dotnet publish CanEngineeringPlatform.Api/CanEngineeringPlatform.Api.csproj `
  --configuration Release `
  --output "C:\Deploy\CanEngineeringPlatform\backend" `
  --self-contained false
```

### 2.2 Frontend Build & Static Serving
```powershell
cd "frontend"
npm ci
npm run build
# Copy dist contents to backend wwwroot or static reverse proxy
Copy-Item -Recurse -Force "dist\*" "C:\Deploy\CanEngineeringPlatform\backend\wwwroot\"
```

### 2.3 ESP8266 Gateway Firmware Flashing
```powershell
cd "firmware\esp8266-can-gateway"
pio run --target upload
```

---

## 3. Running as a Production Service

On Windows, use NSSM or Windows Services:
```powershell
nssm install CanEngineeringPlatform "C:\Deploy\CanEngineeringPlatform\backend\CanEngineeringPlatform.Api.exe"
nssm set CanEngineeringPlatform AppDirectory "C:\Deploy\CanEngineeringPlatform\backend"
nssm set CanEngineeringPlatform AppParameters "--urls=http://0.0.0.0:5000"
nssm start CanEngineeringPlatform
```

Access the unified engineering workstation at `http://localhost:5000`.
