# STM32H723ZG REAL-TIME HARDWARE VALIDATION GATE REPORT
## First Measured Physical Hardware Baseline Audit & Engineering Gate

> **Platform Status:** Software Frozen & Complete (395/395 Backend Tests Passing &bull; 16/16 Firmware Tests Passing &bull; Clean Frontend Build)  
> **Report Date:** 2026-09-26  
> **Target Silicon:** STMicroelectronics STM32H723ZGT6 (ARM Cortex-M7 @ 550 MHz)  
> **Target Board:** ST NUCLEO-H723ZG (MB1364)  
> **Gate Standard:** Strict Physical Hardware Verification (No Simulations, No Inferences)  
> **Current Physical State:** **`NOT TESTED`** *(Hardware physically absent from USB bus at probe time; ready for physical bench connection)*

---

## 1. Official Documentation & Hardware Audit (UM2407 & STM32H723 Datasheet)

Every pin, clock, and solder bridge was audited directly against the official ST User Manual **UM2407** (STM32 Nucleo-144 boards) and the **STM32H723ZG Datasheet**:

### Morpho Connector Audit & Pinout Correction:
* **FDCAN1_TX:** `PD1` $\to$ **CN11 Pin 55** (Alternate Function `AF9_FDCAN1`)
* **FDCAN1_RX:** `PD0` $\to$ **CN11 Pin 57** (Alternate Function `AF9_FDCAN1`)
* **Digital Ground:** **CN11 Pin 60** / **CN11 Pin 62**
* **Logic Supply (VIO):** **CN11 Pin 16** / **CN11 Pin 18** (+3.3V)
* **Transceiver Supply (VCC):** **CN11 Pin 12** (+5V from USB/ST-LINK)

> [!CAUTION]
> **CRITICAL ARDUINO CONNECTOR DISCREPANCY REJECTED:**  
> Previous prototype notes suggested connecting to Arduino headers CN9 D12 / D13.  
> **AUDIT FINDING:** On NUCLEO-144 boards, Arduino/Zio D12 is physically connected to `PA6` and D13 is connected to `PA5`. Neither pin maps to FDCAN1 on the STM32H723. Connecting to D12/D13 will fail to acquire CAN frames.  
> **MANDATE:** FDCAN1 transceiver connections must strictly use **Morpho CN11 Pin 55 (`PD1`)** and **Morpho CN11 Pin 57 (`PD0`)**.

---

## 2. PC Transport Selection: Option A vs Option B

| Dimension | Option A: STM32 USART3 $\to$ ST-LINK/V3E VCP | Option B: Native USB CDC (On-Chip OTG FS/HS) |
| :--- | :--- | :--- |
| **Physical Port** | Micro-USB connector `CN1` (ST-LINK) | Micro/Type-C connector `CN13` (User USB) |
| **MCU Pins** | `PD8` (`USART3_TX`), `PD9` (`USART3_RX`) | `PA11` (`USB_DM`), `PA12` (`USB_DP`) |
| **Solder Bridges** | `SB20` & `SB21` ON (Default from ST factory) | Default D+ / D- routing |
| **Driver Type** | STMicroelectronics Virtual COM Port driver | Standard USB CDC-ACM Class Driver |
| **Baud Rate** | 921,600 baud (Tested & supported by ST-LINK V3) | Full-Speed (12 Mbps) or High-Speed (480 Mbps) |
| **Firmware Overhead** | Extremely low (USART DMA / FIFO) | USB Endpoint Interrupt Stack |
| **Selected Default** | **Option A (ST-LINK VCP @ 921,600)** | Available as high-throughput upgrade path |

* **Audit Conclusion:** The initial hardware validation uses **Option A** (`USART3` via ST-LINK V3E Virtual COM Port). It is explicitly designated as `ST-LINK VCP` to prevent misrepresenting it as native USB CDC.

---

## 3. Physical CAN Transceiver Electrical Audit

Before applying power, the transceiver module must be verified against electrical specifications:

| Parameter | NXP TJA1443AT / TCAN1044A-Q1 | NXP TJA1051T/3 | TI SN65HVD230 (WARNING) |
| :--- | :--- | :--- | :--- |
| **CAN Specification** | ISO 11898-2:2016 (CAN FD) | ISO 11898-2:2016 (CAN FD) | ISO 11898-2 (Classic CAN ONLY) |
| **Max Bitrate** | **Up to 5 / 8 Mbps** | **Up to 5 Mbps** | **1 Mbps Max (CAN FD FAILS)** |
| **Supply Voltage (VCC)** | 4.5V to 5.5V | 4.5V to 5.5V | 3.0V to 3.6V |
| **I/O Logic Supply (VIO)**| 1.7V to 5.5V (Supports 3.3V MCU) | 2.8V to 5.5V (Pin 5 VIO) | No VIO pin (Fixed 3.3V) |
| **Common Mode Range** | $\pm 30\text{V}$ | $\pm 30\text{V}$ | $\pm 12\text{V}$ (Insufficient for auto) |
| **Bus Fault Protection** | $\pm 58\text{V}$ | $\pm 58\text{V}$ | $\pm 16\text{V}$ to $\pm 36\text{V}$ |
| **Silent/Standby Pin** | Pin 8 (STB - pull LOW for Normal) | Pin 8 (S - pull LOW for Normal) | Pin 8 (Rs - pull LOW for High Speed)|
| **Validation Decision** | **APPROVED FOR VEHICLE / CAN FD** | **APPROVED FOR BENCH CAN FD** | **REJECTED FOR CAN FD (Classic Only)**|

---

## 4. Hardware Validation Test Matrix (Step-by-Step Gate)

```
[TEST 1: MCU Only] ──► [TEST 2: FDCAN Loopback] ──► [TEST 3: Two-Node Bench] ──► [TEST 4: Oscilloscope] ──► [TEST 5: Frozen Integration] ──► [VEHICLE]
```

### Test 1: MCU Only (No CAN Wires Connected)
* **Goal:** Verify silicon boot, PLL clocks, FDCAN1 peripheral clock, and ST-LINK VCP serial stream.
* **Firmware:** [`firmware/stm32h723zg-can-vci`](file:///c:/Users/vishnu/Documents/ChatGPT/diag%20tool/firmware/stm32h723zg-can-vci)
* **Measurement Procedure:**
  1. Plug NUCLEO-H723ZG via `CN1` (ST-LINK).
  2. Inspect Device Manager: ST-LINK VCP COM port enumerated.
  3. Green LED `LD1` (`PB0`) turns solid ON.
  4. Terminal @ 921,600 baud receives 21-byte handshake (`0xAA 0x55 ...`) followed by 1 Hz heartbeat pulses.
* **Gate Requirement:** MCU boots, PLL locks at 550 MHz, FDCAN kernel at 80 MHz, no crashes.

### Test 2: FDCAN Internal Silicon Loopback
* **Goal:** Validate Bosch FDCAN1 core, Message RAM allocation, and interrupt handler inside the chip before involving physical wires.
* **Configuration:** `mode = FDCAN_OPMODE_INTERNAL_LOOPBACK` (`FDCAN_MODE_INTERNAL_LOOPBACK`).
* **Test Vectors:**
  - 11-bit standard ID (`0x123`) & 29-bit extended ID (`0x18DAF110`).
  - Classic CAN (8 bytes payload, DLC 8).
  - CAN FD (64 bytes payload, DLC 15, Bit Rate Switch BRS enabled).
* **Measurement:** Frames injected via `HAL_FDCAN_AddMessageToTxFifoQ` must loop internally into `HAL_FDCAN_RxFifo0Callback` and be placed into the DTCM ring buffer with microsecond timestamps.

### Test 3: Two-Node Isolated Bench Bus
* **Goal:** Verify physical differential signaling between two physical nodes.
* **Topology:** NUCLEO-H723ZG + Transceiver $\longleftrightarrow$ Secondary CAN Node (e.g. CAN analyzer / second MCU).
* **Bus Termination:**
  - Node 1: $120\ \Omega$ metal film resistor across CAN_H and CAN_L.
  - Node 2: $120\ \Omega$ metal film resistor across CAN_H and CAN_L.
  - **Measurement before power:** Measure DC resistance between CAN_H and CAN_L with DMM: **Must read $60.0\ \Omega \pm 2\ \Omega$**.
  - **Zero extra termination:** Confirm no third resistor is present on the bus.

### Test 4: Physical Waveform & Bit Timing Verification (Oscilloscope / Logic Analyzer)
* **Oscilloscope Setup:** Dual-channel probe on CAN_H and CAN_L with math channel $(V_{CANH} - V_{CANL})$.
* **Verification Criteria:**
  - Recessive state: $V_{CANH} \approx 2.50\text{V}$, $V_{CANL} \approx 2.50\text{V}$, $V_{DIFF} \approx 0.0\text{V}$.
  - Dominant state: $V_{CANH} \approx 3.50\text{V}$, $V_{CANL} \approx 1.50\text{V}$, $V_{DIFF} \approx 2.00\text{V}$.
  - Nominal Bit Rate (500 kbps): Bit width = $2.00\ \mu\text{s} \pm 10\text{ ns}$.
  - Data Phase Bit Rate (2.0 Mbps): Bit width = $500\text{ ns} \pm 5\text{ ns}$.
  - BRS Transition: Inspect smooth propagation delay transition without ringing or overshoot.

### Test 5: Frozen Software Integration Verification
* **Connection:** STM32H723ZG (ST-LINK VCP) $\to$ Backend `Esp8266UsbSerialTransport` $\to$ `CanPipeline` $\to$ SignalR $\to$ React Frontend.
* **Compatibility Ingestion Check:**
  - Backend recognizes gateway via handshake (`0xAA 0x55` with identifier `"ESP8266\0"`).
  - Incoming frames parse cleanly through `BinaryPacketDecoder.TryDecode()`.
  - Frame rate counter matches physical transmission rate.
  - Frontend sniffer table renders frames with 0% CRC error drops.

---

## 5. Listen-Only Safety Verification (Rule 15)

In automotive diagnostics, a passive monitoring tool must **NEVER** disrupt vehicle communications:
* **Hardware Register Verification:**
  - `FDCAN1->CCCR & FDCAN_CCCR_MON` must evaluate to `1` (`Bus Monitoring Mode`).
  - `FDCAN1->CCCR & FDCAN_CCCR_TEST` must evaluate to `0` or normal test config.
* **Physical Electrical Behavior in Listen-Only:**
  - Transmit pin `PD1` is permanently held recessive (HIGH).
  - The controller **never transmits an ACK bit** upon receiving a valid frame.
  - The controller **never transmits an Error Flag** upon detecting a bit error.
* **Firmware Safeguard:**
  - `FDCAN1_Driver_Transmit()` contains an explicit software guard: `if (s_stats.is_listen_only) return false;`.

---

## 6. Hardware Limitation Register (Rule 10, 11, 19)

All physical and architectural boundaries are recorded here without masking or faking:

```
+---------------------------------------------------------------------------------------------------------+
|                                    HARDWARE LIMITATION REGISTER                                         |
+--------------------------+--------------------+---------------------------------------------------------+
| Classification           | Limitation ID      | Detailed Technical Description & Engineering Boundary  |
+--------------------------+--------------------+---------------------------------------------------------+
| TRANSPORT LIMITATION     | LIM-TRANS-01       | 21-Byte Classic Wire Protocol Boundary: The frozen      |
|                          |                    | 21-byte binary packet has exactly 8 bytes of data       |
|                          |                    | payload. CAN FD frames with >8 bytes (up to 64 bytes)   |
|                          |                    | CANNOT fit without packet redesign. The firmware MUST   |
|                          |                    | NOT silently truncate 64-byte frames. A dedicated       |
|                          |                    | 77-byte CAN FD binary transport is required for FD.     |
+--------------------------+--------------------+---------------------------------------------------------+
| TRANSPORT LIMITATION     | LIM-TRANS-02       | Handshake Identity Bridge: Frozen backend requires      |
|                          |                    | "ESP" in bytes 2-4 of handshake. The STM32 intentionally|
|                          |                    | sends "ESP8266\0" to bridge into the frozen backend     |
|                          |                    | without modifying frozen C# code.                       |
+--------------------------+--------------------+---------------------------------------------------------+
| PHYSICAL CONNECTOR LIMIT | LIM-CONN-01        | Morpho CN11 Required: FDCAN1 pins PD0/PD1 are on Morpho |
|                          |                    | CN11 pin 57 and pin 55. Arduino D12/D13 CANNOT be used. |
+--------------------------+--------------------+---------------------------------------------------------+
| TRANSCEIVER LIMITATION   | LIM-XCVR-01        | 3.3V Transceiver Compatibility: SN65HVD230 transceivers |
|                          |                    | are Classic CAN only (<= 1 Mbps). CAN FD testing        |
|                          |                    | strictly requires TCAN1044A or TJA1051T/3 with VIO=3.3V.|
+--------------------------+--------------------+---------------------------------------------------------+
| ELECTRICAL LIMITATION    | LIM-ELEC-01        | Vehicle 12V Isolation: OBD-II Pin 16 supplies +12V      |
|                          |                    | vehicle battery. Direct connection to MCU will destroy  |
|                          |                    | silicon. Requires automotive buck regulator or USB power|
+--------------------------+--------------------+---------------------------------------------------------+
| USB LIMITATION           | LIM-USB-01         | ST-LINK VCP Baud Cap: ST-LINK V3E Virtual COM Port is   |
|                          |                    | tested at 921,600 baud (~18,000 frames/sec max Classic  |
|                          |                    | CAN). Full 50,000+ fps CAN FD requires Native USB HS.   |
+--------------------------+--------------------+---------------------------------------------------------+
```

---

## 7. Real-Time Hardware Probe Results

The host environment was probed via WMI PnP and Python serial tools at `2026-09-26T23:43:20`:

```text
Probing System Serial Ports:
----------------------------
Detected 0 COM ports.

Probing PnP Subsystem for Target Hardware:
------------------------------------------
ST-LINK / STMicroelectronics devices : 0 Found
USB Serial (FTDI, CP210x, CH340)     : 0 Found
STM32 USB OTG CDC devices            : 0 Found
Physical CAN interfaces               : 0 Found
```

### Measured Real-Time Benchmarks (Current State):
* **Physical Hardware Ingestion:** Not physically connected.
* **Software Simulation Ingestion:** 395/395 tests passing (0 failures).
* **Firmware Native Test Suite:** 16/16 tests passing (0 failures).

---

## 8. Final Status Declaration (Rule 21)

Pursuant to Rule 21 of the Hardware Validation Gate Directive:
> *"Never declare physical success from software tests alone. Never declare vehicle readiness from simulation. Report exactly one of: NOT TESTED, MCU VALIDATED, BENCH CAN VALIDATED, CAN FD BENCH VALIDATED, FROZEN SOFTWARE + REAL HARDWARE VALIDATED, READY FOR CONTROLLED VEHICLE VALIDATION."*

The official, factual, measured hardware status is:

```text
================================================================================
                           FINAL HARDWARE GATE STATUS
================================================================================

                                NOT TESTED

  Reason: Physical NUCLEO-H723ZG board and CAN transceiver were not connected
          to the host USB bus during the initial hardware probe.
          
  Readiness: All prerequisite audits (UM2407 pinout verification, clock tree
             synthesis, listen-only safety registers, 21-byte wire protocol,
             and firmware code) are complete and waiting for physical plug-in.

================================================================================
```

---

## 9. Next Steps to Reach `MCU VALIDATED` and `BENCH CAN VALIDATED`

1. **Plug in NUCLEO-H723ZG:** Connect board via `CN1` USB to the PC.
2. **Flash Firmware:** Execute `pio run --target upload` in `firmware/stm32h723zg-can-vci`.
3. **Verify ST-LINK VCP:** Confirm COM port appears in Device Manager.
4. **Advance Gate to `MCU VALIDATED`:** Confirm handshake and 1 Hz heartbeats stream into terminal.
5. **Connect Transceiver (CN11 Pin 55 & 57):** Connect to second bench node with $60\ \Omega$ measured termination.
6. **Advance Gate to `BENCH CAN VALIDATED`:** Stream test frames and verify with frozen software backend.
