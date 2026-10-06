# Automotive ECU Engineering Platform — FINAL SECURITY ARCHITECTURE

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Safety & Security Principles

Automotive software engineering systems interface with physical vehicles where accidental bus disruption or incorrect memory modifications can cause immobilization, component damage, or physical injury. This platform adheres to the **Automotive Zero-Trust & Physical Safety Framework**:

```
+-----------------------------------------------------------------------------------+
|                        PHYSICAL AUTOMOTIVE SAFETY LAYER                           |
+-----------------------------------------------------------------------------------+
|  1. Listen-Only Physical Hardware Gate (ESP8266 + MCP2515)                        |
|  2. Operator Physical Attestation (Two-man rule for DTC Clear, Reset, Flash)     |
|  3. Pre-Condition Electrical Interlock (Terminal 15 Active, 12.8V - 14.4V Supply)  |
|  4. Automatic Diagnostic Session Fallback (Heartbeat Drop -> Default Session)     |
|  5. Dual-Hash Verification (Original Flash vs Modified Flash vs Sector Bounds)   |
|  6. Non-Destructive Memory Query Safeguards (Restricted to Calibration/App Sectors)|
+-----------------------------------------------------------------------------------+
```

---

## 2. Operator Authorization Interlocks

All intrusive actions require explicit authorization records containing:
* `UserConfirmed`: Must be explicitly set to `true`.
* `ConfirmationReason`: Mandatory non-empty string stating engineering justification.
* Failure to provide confirmation immediately triggers `UnauthorizedAccessException` before any CAN frame is formed or dispatched.

---

## 3. Flash Programming Safety State Machine

Reflashing an ECU is guarded by a rigid **12-stage state machine**:
1. Check bench supply voltage (`>= 13.0V` required).
2. Read & verify original ECU identification (Part No, Hardware No, Software No).
3. Compute SHA-256 hash of existing flash image backup.
4. Verify cryptographic hash of new calibration binary.
5. Verify flash sector boundary alignment (never write outside calibrated bounds).
6. Request Diagnostic Extended Session (`0x10 0x03`).
7. Request Security Access (`0x27 0x01` / `0x27 0x02`).
8. Disable Normal Communication (`0x28 0x03`).
9. Disable DTC Setting (`0x85 0x02`).
10. Erase target flash sectors (`0x31 0x01`).
11. Flash blocks in sequence (`0x34` -> `0x36` -> `0x37`) with transfer checksum verification.
12. Final Checksum Attestation (`0x31 0x01 0x02 0x02`) and ECU Reset (`0x11 0x01`).

If any stage fails or returns an NRC (e.g., NRC `0x72`), the engine enters `RecoveryRequired` state, preventing subsequent write actions until a verified recovery sequence is authorized.
