# Automotive ECU Engineering Platform — FINAL PROTOCOL MATRIX

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Unified Diagnostic Services (UDS) — ISO 14229-1

### 1.1 Supported Service Identifiers (SID)

| SID | Sub-function / Description | Request Format | Expected Response | Retry Behavior |
| :---: | :--- | :--- | :--- | :--- |
| **0x10** | DiagnosticSessionControl | `[0x10, subFunction]` | `[0x50, subFunction, P2_ms, P2*_ms]` | SafeToRetry (Max 2) |
| **0x11** | ECUReset | `[0x11, resetType]` | `[0x51, resetType, powerDownDelay]` | DoNotRetry |
| **0x14** | ClearDiagnosticInformation | `[0x14, groupHigh, groupMid, groupLow]` | `[0x54]` | RequiresUserAction |
| **0x19** | ReadDTCInformation | `[0x19, subFunction, mask]` | `[0x59, subFunction, dtcData...]` | SafeToRetry |
| **0x22** | ReadDataByIdentifier | `[0x22, didHigh, didLow]` | `[0x62, didHigh, didLow, data...]` | SafeToRetry |
| **0x23** | ReadMemoryByAddress | `[0x23, addrFormat, addr..., size...]` | `[0x63, rawData...]` | DoNotRetry |
| **0x27** | SecurityAccess | `[0x27, level, key...]` | `[0x67, level, seed/ack...]` | RequiresUserAction |
| **0x2E** | WriteDataByIdentifier | `[0x2E, didHigh, didLow, data...]` | `[0x6E, didHigh, didLow]` | DoNotRetry |
| **0x31** | RoutineControl | `[0x31, subFunc, ridHigh, ridLow, opt...]`| `[0x71, subFunc, ridHigh, ridLow, stat]`| DoNotRetry |
| **0x34** | RequestDownload | `[0x34, format, addrFormat, addr..., sz]`| `[0x74, maxNumBytesFormat, maxNumBytes]` | DoNotRetry |
| **0x36** | TransferData | `[0x36, blockSequence, data...]` | `[0x76, blockSequence]` | SafeToRetry (NRC 0x78) |
| **0x37** | RequestTransferExit | `[0x37]` | `[0x77]` | DoNotRetry |
| **0x3E** | TesterPresent | `[0x3E, 0x00]` or `[0x3E, 0x80]` | `[0x7E, 0x00]` (if suppressBit=0) | Auto (every 2000ms) |
| **0x85** | ControlDTCSetting | `[0x85, subFunction]` | `[0xC5, subFunction]` | SafeToRetry |

### 1.2 Negative Response Code (NRC) Classification & Resolution

| NRC | Hex | Name | Platform Action / Safe Resolution |
| :---: | :---: | :--- | :--- |
| **16** | 0x10 | `GeneralReject` | Logged as error; aborts routine |
| **17** | 0x11 | `ServiceNotSupported` | Graceful degradation; falls back to legacy/alternative service |
| **18** | 0x12 | `SubFunctionNotSupported` | Flags service matrix capability as unsupported |
| **19** | 0x13 | `IncorrectMessageLengthOrInvalidFormat` | Throws validation error; prevents corrupt frame transmission |
| **34** | 0x22 | `ConditionsNotCorrect` | Operator advisory: Preconditions not met (e.g. Engine Running) |
| **35** | 0x24 | `RequestSequenceError` | State machine reset; transitions back to default session |
| **36** | 0x25 | `NoResponseFromSubnetComponent` | Network diagnosis: Target ECU subnet link down |
| **49** | 0x31 | `RequestOutOfRange` | Parameter boundary validation; warns operator of invalid PID/DID |
| **51** | 0x33 | `SecurityAccessDenied` | Safety guard: Prompts operator for required security seed/key |
| **53** | 0x35 | `InvalidKey` | Attestation lockout warning; operator alert |
| **54** | 0x36 | `ExceedNumberOfAttempts` | Security lockout delay enforced by system |
| **114**| 0x72 | `GeneralProgrammingFailure` | Triggers immediate 12-stage rollback & recovery routine |
| **120**| 0x78 | `RequestCorrectlyReceived-ResponsePending` | Automatically extends P2* timeout; continues waiting without fail |

---

## 2. ISO-TP (ISO 15765-2) Framing Specification

* **Single Frame (SF):** PCI Byte `[0x00 | DLC]`. Used for payloads <= 7 bytes (Classic CAN) or <= 63 bytes (CAN FD).
* **First Frame (FF):** PCI Bytes `[0x10 | (Length >> 8), Length & 0xFF]`. Initiates multi-frame segmented transfers.
* **Flow Control (FC):** PCI Bytes `[0x30 | FlowStatus, BlockSize, SeparationTimeMs]`.
  * `0x30`: Clear To Send (CTS)
  * `0x31`: Wait (WT)
  * `0x32`: Overflow (OVFLW)
* **Consecutive Frame (CF):** PCI Byte `[0x20 | (SequenceIndex & 0x0F)]`. Sequence numbers roll 0..15 continuously.

---

## 3. OBD-II / SAE J1979 Diagnostic Modes

* **Mode 01:** Current Powertrain Diagnostic Data (Engine Speed, Coolant Temp, MAP, MAF, Vehicle Speed, Spark Advance, Fuel Trims).
* **Mode 02:** Powertrain Freeze Frame Data.
* **Mode 03:** Emission-Related Diagnostic Trouble Codes.
* **Mode 04:** Clear / Reset Emission-Related Diagnostic Information.
* **Mode 07:** Emission-Related Diagnostic Trouble Codes Detected During Current or Last Driving Cycle (Pending DTCs).
* **Mode 09:** Vehicle Information (VIN, Calibration ID, Calibration Verification Numbers).
