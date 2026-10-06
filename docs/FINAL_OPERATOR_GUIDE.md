# Automotive ECU Engineering Platform — FINAL OPERATOR & WORKSTATION MANUAL

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Getting Started: The One-Click Discovery Sequence

1. Launch the platform and navigate to **ECU Engineering** in the left sidebar.
2. In the top **Project Workspace Header**, click **"RUN 1-CLICK AUTOMATED SUITE"**.
3. The platform will automatically execute the end-to-end diagnostic workflow:
   * **Active Transport Discovery:** Verifies Waveshare, ESP8266, or Simulation bus link.
   * **ECU Identification:** Probes standard addresses (`0x7E0` / `0x7E8`), querying VIN, Part Numbers, and CalID.
   * **Protocol Discovery:** Probes UDS on ISO-TP, KWP2000, and standard OBD-II services.
   * **DTC Retrieval:** Queries active, pending, and confirmed trouble codes with full 8-bit status masks.
   * **Forensic Timeline Logging:** Logs each transition chronologically.

---

## 2. Investigating Diagnostic Trouble Codes (DTCs)

1. Select the **Diagnostic Engine** subtab.
2. Click **Read DTCs (All Masks)** to retrieve all stored codes.
3. Review status byte indicators:
   * `[TF]` Test Failed
   * `[TFTOC]` Test Failed This Operation Cycle
   * `[PDTC]` Pending DTC
   * `[CDTC]` Confirmed DTC
   * `[TNCSLC]` Test Not Completed Since Last Clear
   * `[WIR]` Warning Indicator Requested (Check Engine Light)
4. To clear DTCs: Enter engineering rationale in the confirmation modal and click **Confirm & Clear DTCs**.

---

## 3. CAN Reverse-Engineering Lab

1. Select the **CAN Reversing Lab** subtab.
2. The left panel ranks every active CAN ID by message frequency, jitter, and **Shannon Entropy**:
   * Low entropy (`< 1.0`): Static status flags or constant heartbeat values.
   * Medium entropy (`1.0 - 5.0`): Dynamic analog sensor signals (RPM, Speed, Torque).
   * High entropy (`> 6.5`): Cryptographic challenge/response or compressed flash data.
3. Select an ID to inspect the **Bit Transition Heatmap**, automatically extracted **Rolling Counters**, and **Trailing Checksums**.

---

## 4. Vehicle Digital Twin & HIL Bench

1. Select the **Digital Twin** subtab to simulate multi-ECU interactions without a physical vehicle.
2. Inject bus drops, modify sensor voltages, or trigger DTCs across ECM, TCM, ABS, and BCM nodes.
3. Switch to **HIL & Power Bench** to actuate physical relays for Terminal 30 and Terminal 15.
