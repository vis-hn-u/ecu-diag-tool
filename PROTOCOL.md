# Automotive CAN Platform — Protocol & API Specifications

---

## 1. 20-Byte Phase 1 Binary CAN Packet Specification

High-rate CAN frames streamed from the ESP8266 Gateway over WebSocket (`ws://<ESP-IP>:8088`) use a fixed 20-byte binary packet structure:

| Byte Offset | Field Name | Type | Description |
| :--- | :--- | :--- | :--- |
| `0x00` | Sync Byte 0 | `uint8` | Fixed magic byte: `0xAA` |
| `0x01` | Sync Byte 1 | `uint8` | Fixed magic byte: `0x55` |
| `0x02 - 0x05` | Timestamp | `uint32` (LE) | Microsecond timestamp ($\mu\text{s}$) from MCU hardware clock |
| `0x06 - 0x09` | CAN ID | `uint32` (LE) | Standard 11-bit ($\le 0x7\text{FF}$) or Extended 29-bit ($\le 0x1\text{FFFFFFF}$) |
| `0x0A` | DLC | `uint8` | Data Length Code ($0 \dots 8$) |
| `0x0B` | Flags | `uint8` | `0x01` = Extended Frame, `0x02` = RTR Frame |
| `0x0C - 0x13` | Data Payload | `uint8[8]` | 8 data bytes (zero-padded if $\text{DLC} < 8$) |

### Packet Validation Rules:
- Buffer size must equal 20 bytes.
- Bytes 0-1 must match `0xAA 0x55`.
- DLC must be $\le 8$.
- If `Flags & 0x01 == 0` (Standard): $\text{ID} \le 0x7\text{FF}$.
- If `Flags & 0x01 != 0` (Extended): $\text{ID} \le 0x1\text{FFFFFFF}$.

---

## 2. ESP8266 Diagnostic Status JSON

Sent in response to the ASCII text command `"status"`:

```json
{
  "rx_frames": 128450,
  "rx_fps": 450,
  "dropped_frames": 0,
  "spi_errors": 0,
  "can_errors": 0,
  "queue_usage": 2,
  "queue_high_water": 38,
  "ws_failures": 0,
  "ws_broadcasts": 128450,
  "eflg": 0,
  "tec": 0,
  "rec": 0,
  "free_heap": 28416,
  "max_loop_latency_us": 840,
  "clients": 1
}
```

---

## 3. SignalR Hub Contract (`/hubs/can`)

### Client Invocations:
- `RequestInitialState()`: Fetches current network topology snapshot, replay status, and gateway status.

### Server Events (Broadcasts to Clients):
- `ReceiveFrameBatch(CanFrameBatch batch)`: Emitted every 25ms or 20 frames. Contains raw `CanFrame[]` and `DecodedSignal[]`.
- `ReceiveTopologyUpdate(NetworkTopologyState topology)`: Emitted every 250ms with active node frequencies and signal edge states.
- `ReceiveReplayStatus(ReplayStatus status)`: Emitted during simulation replay with frame position and progress percentage.
- `ReceiveGatewayStatus(GatewayStatus status)`: Emitted with ESP8266 connection telemetry.

---

## 4. REST API Reference

| Endpoint | Method | Description |
| :--- | :--- | :--- |
| `/api/gateway/status` | `GET` | Get connection status and ESP8266 metrics |
| `/api/gateway/address` | `POST` | Update ESP8266 WebSocket target IP/hostname |
| `/api/gateway/poll-status` | `POST` | Force diagnostic status query from ESP8266 |
| `/api/dbc/active` | `GET` | Get active DBC database metadata and signals |
| `/api/dbc/upload` | `POST` | Upload and parse Vector CANdb `.dbc` file |
| `/api/dbc/load-sample` | `POST` | Load built-in automotive sample DBC |
| `/api/dbc/active` | `DELETE`| Clear active DBC database |
| `/api/log/current` | `GET` | Get current active logging session status |
| `/api/log/sessions` | `GET` | List all recorded CAN sessions |
| `/api/log/start` | `POST` | Start recording session (`{ name: string }`) |
| `/api/log/pause` | `POST` | Pause recording session |
| `/api/log/resume` | `POST` | Resume recording session |
| `/api/log/stop` | `POST` | Stop recording and finalize session |
| `/api/log/export/{id}` | `GET` | Download session as CSV dataset |
| `/api/replay/status` | `GET` | Get current simulation player status |
| `/api/replay/load-session/{id}` | `POST` | Load recorded session into simulation player |
| `/api/replay/upload-csv` | `POST` | Upload CSV log for simulation playback |
| `/api/replay/generate-sample` | `POST` | Generate realistic 30s vehicle driving cycle |
| `/api/replay/play` | `POST` | Start simulation playback |
| `/api/replay/pause` | `POST` | Pause simulation playback |
| `/api/replay/stop` | `POST` | Stop simulation playback |
| `/api/replay/seek?index={n}` | `POST` | Seek to specific frame index |
| `/api/replay/speed?multiplier={m}`| `POST` | Set playback speed multiplier ($0.1\times \dots 10.0\times$) |
| `/api/topology` | `GET` | Get live network topology graph snapshot |
| `/api/topology/mode` | `POST` | Set topology mode (`Live`, `Paused`, `Simulation`) |
| `/api/topology/reset` | `POST` | Reset observed node/message statistics |
