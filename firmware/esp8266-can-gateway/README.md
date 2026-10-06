# ESP8266 CAN gateway — Phase 1

This firmware is a receive-only CAN-to-WebSocket gateway. It starts the
MCP2515 in listen-only mode, so normal operation cannot transmit or acknowledge
CAN traffic. The high-rate stream is the 20-byte binary packet documented in
`include/transport_protocol.h`; WebSocket text is limited to the `status`
diagnostic request.

## First hardware test

1. Inspect the HW-184 crystal. This project is for a board marked `8.000` MHz.
   Do not use it with a 16 MHz module without adding and validating a 16 MHz
   timing table.
2. With power disconnected, wire D5/GPIO14 to SCK, D6/GPIO12 to MISO,
   D7/GPIO13 to MOSI, D2/GPIO4 to CS, and D1/GPIO5 to INT. Connect common
   ground plus CANH/CANL to a correctly terminated CAN bus. Never allow an
   HW-184 powered at 5 V to drive an ESP8266 input at 5 V; use a 3.3 V-safe
   module arrangement or level shifting.
3. Copy `include/wifi_credentials.example.h` to the ignored
   `include/wifi_credentials.h` and provide local station or AP credentials.
   The AP password must have at least eight characters.
4. Run `pio run -t upload`, then `pio device monitor`. Expect
   `MCP2515 listening`. It is normal for no frame traffic to appear until a
   CAN source is present.
5. Use a WebSocket client at `ws://<ESP-IP>:8088`. Each CAN frame is 20 bytes:
   `AA 55`, 32-bit little-endian timestamp, 32-bit little-endian CAN ID, DLC,
   flags, and eight payload bytes. Send text `status` for JSON diagnostics.
6. To test SPI/controller wiring before connecting a live bus, temporarily set
   `CAN_RUN_LOOPBACK_SELF_TEST` to `true`, upload, and confirm the serial
   `loopback self-test passed` message. It uses MCP2515 internal loopback only,
   then restores listen-only. Set it back to `false` for every normal build.

## Hardware verification checklist

- Check CNF registers with a logic analyzer or temporary debug readback:
  500 kbit/s at 8 MHz is CNF1=00, CNF2=90, CNF3=02.
- Send standard, extended, RTR, DLC 0, and DLC 8 frames from an independent
  CAN node. Check the binary packet IDs/flags and status counters.
- Burst at least two frames back-to-back and confirm both appear. RXB0 rollover
  into RXB1 is enabled and application code drains until no RX interrupt flag
  remains (or schedules the next bounded drain while INT stays low).
- Deliberately overload the receiver only on a safe bench bus. Confirm RX
  overflow changes `dropped_frames` and `can_errors`, with no ESP8266 reset.

## Software test commands

Run `pio run -e nodemcuv2` to build the firmware. The pure-logic Unity suite
is `pio test -e native`; it requires a host `gcc`/`g++` compiler. The embedded
Unity suite is `pio test -e nodemcuv2`, which requires an uploaded board and a
serial test port. Use `--without-uploading --without-testing` only to compile
that embedded test firmware when no board is connected.
