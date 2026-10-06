# STM32H723ZG Professional Automotive CAN FD / DoIP VCI Firmware

## Target Board: STMicroelectronics NUCLEO-H723ZG
- **MCU:** STM32H723ZGT6 (ARM Cortex-M7 with DP-FPU @ 550 MHz)
- **Flash:** 1 MB Dual-Bank Flash
- **RAM:** 564 KB SRAM (64 KB ITCM, 128 KB DTCM, 128 KB AXI SRAM)
- **CAN Subsystem:** 3x Bosch FDCAN Controllers with 10 KB Message RAM
- **Uplink:** Native USB High-Speed (480 Mbps) / Virtual COM Port @ 921,600 baud

---

## 1. Hardware Pinout & Wiring Diagram (ST Official UM2407 Verified)

Connect your CAN FD Transceiver (e.g., **TI TCAN1044A**, **NXP TJA1051T/3**, or **TI SN65HVD230**) to the **NUCLEO-H723ZG Morpho Headers (CN11)**:

```
+-----------------------------------------------------------------------------------------------+
|               NUCLEO-H723ZG TO CAN TRANSCEIVER & OBD-II CONNECTIONS (CN11)                    |
+----------------------+--------------------+--------------------+------------------------------+
| NUCLEO-H723ZG Pin    | Morpho Connector   | CAN Transceiver    | OBD-II Vehicle Connector Pin |
+----------------------+--------------------+--------------------+------------------------------+
| PD1 (FDCAN1_TX)      | CN11 Pin 55        | Transceiver TXD    | —                            |
| PD0 (FDCAN1_RX)      | CN11 Pin 57        | Transceiver RXD    | —                            |
| GND                  | CN11 Pin 60 / 62   | Transceiver GND    | Pin 4 (Chassis) & Pin 5 (Sig)|
| +3.3V (VIO / Logic)  | CN11 Pin 16 / 18   | Transceiver VIO    | —                            |
| +5V (VCC Transceiver)| CN11 Pin 12        | Transceiver VCC    | —                            |
| —                    | —                  | Transceiver CAN_H  | Pin 6 (CAN High)             |
| —                    | —                  | Transceiver CAN_L  | Pin 14 (CAN Low)             |
+----------------------+--------------------+--------------------+------------------------------+
```

> [!CAUTION]
> **CRITICAL PINOUT AUDIT (UM2407):**  
> **DO NOT USE CN9 D12 / CN9 D13 FOR FDCAN1.**  
> On NUCLEO-144 boards, Arduino/Zio connector D12 is wired to `PA6` and D13 is wired to `PA5`. These pins have no connection to `PD0` or `PD1`.  
> FDCAN1 lines `PD0` and `PD1` are physically located exclusively on **Morpho Connector CN11 pin 57 (`PD0`)** and **CN11 pin 55 (`PD1`)**.

### Bus Termination:
* If connecting to an active vehicle OBD-II port: **DO NOT install a 120-ohm termination resistor** (the vehicle bus already has 60 ohms termination).
* If bench testing with a standalone ECU on a desk: **Install a 120-ohm resistor between CAN_H and CAN_L** at both physical ends of the two-node bus.

---

## 2. Compiling and Flashing

### Option A: Using PlatformIO (Command Line or VSCode)
1. Plug the NUCLEO-H723ZG into your PC via the **ST-LINK USB connector** (CN1).
2. Open terminal in this folder:
   ```bash
   cd "firmware/stm32h723zg-can-vci"
   pio run --target upload
   ```
3. The onboard ST-LINK V3 will flash the binary and reboot the STM32H723ZG.
4. Green LED (LD1) will turn solid ON, indicating successful FDCAN1 initialization.

### Option B: Using STM32CubeIDE / STM32CubeProgrammer
1. Open the project in STM32CubeIDE.
2. Build in Release mode.
3. Click **Run -> Debug** or flash the generated `.elf` using **STM32CubeProgrammer**.

---

## 3. Platform Integration & Verification

1. Start the backend:
   ```powershell
   cd backend\CanEngineeringPlatform.Api
   dotnet run --configuration Release
   ```
2. Open the frontend at `http://localhost:5173`.
3. Navigate to **Hardware Manager**.
4. Select the **ESP8266 / STM32 USB Serial Gateway** card.
5. In the COM port dropdown, select the **STMicroelectronics Virtual COM Port**.
6. Click **CONNECT / ACTIVATE**.
7. The gateway will complete the 21-byte handshake and immediately begin streaming microsecond-timestamped CAN frames into the Reactive CAN Pipeline!
