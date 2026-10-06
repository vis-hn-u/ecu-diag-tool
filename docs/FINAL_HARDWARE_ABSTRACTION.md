# Automotive ECU Engineering Platform — FINAL HARDWARE ABSTRACTION LAYER (HAL)

**Document Version:** 5.0.0-FREEZE  
**Release Tag:** `RELEASE-v5.0-ENTERPRISE-FINAL`  

---

## 1. Unified Interface Definition (`ICanTransport`)

Every hardware adapter implements the clean, asynchronous `ICanTransport` interface:

```csharp
public interface ICanTransport : IAsyncDisposable
{
    string TransportId { get; }
    string Name { get; }
    TransportType Type { get; }
    bool IsConnected { get; }
    TransportCapabilities Capabilities { get; }
    
    event Action<CanFrame>? OnFrameReceived;
    event Action<TransportStatus>? OnStatusChanged;
    
    ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default);
    TransportStatus GetStatus();
}
```

---

## 2. Hardware Transports Implementation Details

### 2.1 Waveshare USB-CAN-FD-B (Dual Channel)
* **Architecture:** STM32G4-based high-speed USB device with two isolated CAN transceivers.
* **Protocol Framing:** 64-byte USB packets containing framing headers, microsecond hardware timestamps, CAN arbitration IDs, DLC flag masks (BRS, ESI, RTR, EXT), and variable payload bytes up to 64 bytes.
* **Channel Independence:** Channel 0 (CAN1) and Channel 1 (CAN2) operate completely asynchronously with independent baud configuration and pipeline routing.

### 2.2 ESP8266 + MCP2515 Gateway (USB Serial & Wi-Fi)
* **Oscillator Clocking:** Validated for 8.000 MHz and 16.000 MHz crystals. Configures CNF1, CNF2, CNF3 registers for exact phase segments (Propagation Delay, Phase Seg 1, Phase Seg 2).
* **Hardware Mode Enforced:** Configured exclusively with `REQOP = 0x60` (Listen-Only Mode). The MCP2515 physical transmit buffer register (`TXB0CTRL`) is never armed.
* **USB Serial Protocol:**
  * Baud rate: 921,600 bps (8 data bits, no parity, 1 stop bit).
  * 21-byte framing: `[0xAA, 0x55, 18-byte payload, CRC-8]`.
  * CRC-8 polynomial: `0x07` (`x^8 + x^2 + x + 1`), initial value `0x00`, unreflected.

### 2.3 ELM327 / OBDLink Multi-Protocol Bridge
* Communicates via AT command protocol (`ATZ`, `ATE0`, `ATH1`, `ATSP0`, `ATSP6`, `ATSP7`).
* Automatically formats standard 11-bit (`0x7DF`, `0x7E0`) and 29-bit (`0x18DB33F1`) CAN frames into hexadecimal lines, parsing hex responses back into native `CanFrame` structs.

---

## 3. Bus Synchronization & Failure Recovery

1. **Hardware Loss Detection:** Transports emit `OnStatusChanged` within 100ms of USB disconnect.
2. **Buffer Flush:** Transmit channels discard queued frames upon disconnect to prevent stale transmission upon reconnection.
3. **Resynchronization:** The USB Serial state machine features hunt states (`HuntSync1`, `HuntSync2`, `Collect`) ensuring immediate re-locking onto `0xAA 0x55` byte streams even through heavy line noise.
