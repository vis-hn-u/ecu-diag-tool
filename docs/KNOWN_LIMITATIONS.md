# Automotive ECU Engineering Platform — KNOWN PHYSICAL & ARCHITECTURAL LIMITATIONS

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Physical Hardware Limitations

As established by the architectural perfection directive, all domain engineering software layers are complete; the only constraints are physical hardware characteristics:

### 1.1 ESP8266 + MCP2515 Gateway
* **Receive-Only Architecture:** Physical transmission is permanently disabled in firmware and hardware configuration. Cannot transmit UDS requests, clear DTCs, or perform active ECU identification. Used strictly as a passive, non-intrusive sniffer.
* **Payload Limitation:** Classic CAN only (DLC <= 8 bytes). Does not support 64-byte ISO CAN-FD frames.
* **8 MHz Crystal Baud Rate:** On 8.000 MHz crystals, baud rates of 1,000,000 bps are mathematically impossible with valid bit timing quanta (0% error). The firmware safely rejects 1 Mbps configuration requests.

### 1.2 ELM327 / OBDLink Adapters
* **ASCII Serial Bandwidth:** Because frames are converted to ASCII strings over a 115,200 to 500,000 baud serial UART, total throughput is physically capped at approximately 80 to 200 frames/sec. Not suitable for whole-bus 100% load logging.
* **CAN FD Unsupported:** Standard ELM327 chipsets cannot process ISO CAN-FD frames.

### 1.3 Target ECU Microcontroller Protections
* **Secured Bootloaders:** ECUs with locked internal bootloaders (e.g. Infineon TriCore TC179x with OTP passwords or HSM enabled) will return NRC `0x33` (SecurityAccessDenied) or NRC `0x35` (InvalidKey) when accessing protected memory regions without valid OEM cryptographic keys.
