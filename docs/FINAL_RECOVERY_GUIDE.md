# Automotive ECU Engineering Platform — FINAL RECOVERY & BENCH DISASTER GUIDE

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Safety First Principle

**NEVER disconnect bench power during active flash write stages.** If a write failure occurs, the target microcontroller often retains the factory secondary bootloader in RAM or a protected boot sector. Disconnecting power converts a recoverable session into a bench BDM/JTAG recovery requirement.

---

## 2. Flash Session Recovery Procedure

When the 12-Stage Flasher transitions to `RecoveryRequired` (e.g. following communication timeout or NRC `0x72` GeneralProgrammingFailure):

```
+-----------------------------------------------------------------------------------+
|                           FLASH RECOVERY STATE MACHINE                            |
+-----------------------------------------------------------------------------------+
|  Step 1: DO NOT POWER OFF TERMINAL 30 OR TERMINAL 15                              |
|  Step 2: Inspect Supply Voltage (Confirm >= 13.5V via HIL / Bench Meter)          |
|  Step 3: Open 'Safe 12-Stage Flashing' -> Select 'Initiate Bootloader Recovery'   |
|  Step 4: Transmit UDS 0x10 0x02 (ProgrammingSession) via Waveshare Active Ch     |
|  Step 5: Verify Secondary Bootloader Echo (0x50 0x02)                             |
|  Step 6: Reflash Original Stock Backup Image (Calculated Original SHA-256)        |
|  Step 7: Perform Checksum Verification (Routine 0x0202)                            |
|  Step 8: Transmit ECU Hard Reset (0x11 0x01) -> Confirm Default Session (0x10 0x01)|
+-----------------------------------------------------------------------------------+
```

---

## 3. Communication Lockout (NRC 0x36 / 0x37)

If security access fails and triggers NRC `0x36` (Exceed Number of Attempts):
1. Maintain Terminal 30 and Terminal 15 active.
2. Maintain `TesterPresent` (`0x3E 0x80`) every 2.0 seconds to keep the diagnostic timer alive.
3. Observe required OEM penalty timer (typically 10 to 15 minutes for Bosch/Continental ECUs).
4. Do NOT attempt additional keys until the timer expires.

---

## 4. Hardware Fault Isolation

* **Bus Off Error:** Verify 120-ohm termination resistors across CAN_H and CAN_L. Resistance measured between pins with power disconnected must equal 60 ohms (two 120-ohm resistors in parallel).
* **Ground Loop / Noise:** Ensure galvanic isolation on the USB-CAN adapter is intact and bench ground is tied to vehicle chassis/ECU ground pin.
