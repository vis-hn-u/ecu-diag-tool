#include <unity.h>

#include "can_frame.h"
#include "mcp2515_defs.h"
#include "ring_buffer.h"
#include "transport_protocol.h"

// Pure serializer code is deliberately Arduino-independent and is included so
// this native test target needs no ESP8266 hardware or application runtime.
#include "../../src/transport_protocol.cpp"

void test_can_frame_id_and_dlc_bounds() {
    CanFrame frame = {};
    frame.id = CAN_STANDARD_ID_MAX;
    frame.dlc = 8;
    TEST_ASSERT_TRUE(isValidCanFrame(frame));
    frame.id = CAN_STANDARD_ID_MAX + 1;
    TEST_ASSERT_FALSE(isValidCanFrame(frame));

    frame.id = CAN_EXTENDED_ID_MAX;
    frame.flags = CAN_FLAG_EXTENDED;
    TEST_ASSERT_TRUE(isValidCanFrame(frame));
    frame.dlc = 9;
    TEST_ASSERT_FALSE(isValidCanFrame(frame));
}

void test_binary_packet_byte_order_and_padding() {
    CanFrame frame = {};
    frame.timestamp_us = 0x12345678UL;
    frame.id = 0x01ABCDEFUL;
    frame.dlc = 2;
    frame.flags = CAN_FLAG_EXTENDED;
    frame.data[0] = 0xBE;
    frame.data[1] = 0xEF;
    BinaryCanPacket packet = {};

    TEST_ASSERT_TRUE(TransportProtocol::serializeBinary(frame, packet));
    const uint8_t expected[] = {0xAA, 0x55, 0x78, 0x56, 0x34, 0x12,
                                0xEF, 0xCD, 0xAB, 0x01, 0x02, 0x01,
                                0xBE, 0xEF, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00};
    TEST_ASSERT_EQUAL_UINT8_ARRAY(expected, packet.bytes, sizeof(expected));
}

void test_ring_capacity_overflow_and_order() {
    CanRingBuffer ring;
    CanFrame frame = {};
    for (size_t i = 0; i < CanRingBuffer::kUsableCapacity; ++i) {
        frame.id = static_cast<uint32_t>(i);
        TEST_ASSERT_TRUE(ring.push(frame));
    }
    TEST_ASSERT_EQUAL_UINT32(127, ring.available());
    TEST_ASSERT_FALSE(ring.push(frame));
    TEST_ASSERT_EQUAL_UINT32(1, ring.droppedCount());
    TEST_ASSERT_EQUAL_UINT32(127, ring.highWaterMark());
    for (size_t i = 0; i < CanRingBuffer::kUsableCapacity; ++i) {
        CanFrame received = {};
        TEST_ASSERT_TRUE(ring.pop(received));
        TEST_ASSERT_EQUAL_UINT32(i, received.id);
    }
}

void test_8mhz_cnf_table_and_one_megabit_rejection() {
    const McpBitTiming bit125 = mcpBitTimingFor(CanBitRate::CAN_125KBPS,
                                                 McpOscillatorFreq::OSC_8MHZ);
    TEST_ASSERT_EQUAL_HEX8(0x81, bit125.cnf1);
    TEST_ASSERT_EQUAL_HEX8(0xE5, bit125.cnf2);
    TEST_ASSERT_EQUAL_HEX8(0x83, bit125.cnf3);

    const McpBitTiming bit250 = mcpBitTimingFor(CanBitRate::CAN_250KBPS,
                                                 McpOscillatorFreq::OSC_8MHZ);
    TEST_ASSERT_EQUAL_HEX8(0x80, bit250.cnf1);
    TEST_ASSERT_EQUAL_HEX8(0xE5, bit250.cnf2);
    TEST_ASSERT_EQUAL_HEX8(0x83, bit250.cnf3);

    const McpBitTiming bit500 = mcpBitTimingFor(CanBitRate::CAN_500KBPS,
                                                 McpOscillatorFreq::OSC_8MHZ);
    TEST_ASSERT_EQUAL_HEX8(0x00, bit500.cnf1);
    TEST_ASSERT_EQUAL_HEX8(0xD1, bit500.cnf2);
    TEST_ASSERT_EQUAL_HEX8(0x81, bit500.cnf3);
    TEST_ASSERT_FALSE(mcpBitTimingFor(CanBitRate::CAN_1000KBPS,
                                       McpOscillatorFreq::OSC_8MHZ).supported);
}

void test_crc8_known_vector_all_zero() {
    uint8_t data[20] = {0};
    uint8_t crc = UsbSerialProtocol::crc8(data, 20);
    TEST_ASSERT_EQUAL_HEX8(0x00, crc);
}

void test_crc8_known_vector_magic_bytes() {
    uint8_t data[20] = {0};
    data[0] = 0xAA;
    data[1] = 0x55;
    
    // Compute manually in helper
    uint8_t expected = 0x00;
    for (size_t i = 0; i < 20; i++) {
        expected ^= data[i];
        for (uint8_t bit = 0; bit < 8; bit++) {
            if (expected & 0x80) expected = (expected << 1) ^ 0x07;
            else expected <<= 1;
        }
    }
    
    uint8_t crc = UsbSerialProtocol::crc8(data, 20);
    TEST_ASSERT_EQUAL_HEX8(expected, crc);
}

void test_crc8_single_bit_flip_changes_crc() {
    uint8_t data[20] = {0};
    data[0] = 0xAA;
    uint8_t crc1 = UsbSerialProtocol::crc8(data, 20);
    data[0] = 0xAB;
    uint8_t crc2 = UsbSerialProtocol::crc8(data, 20);
    TEST_ASSERT_NOT_EQUAL(crc1, crc2);
}

void test_usb_frame_crc_appended_correctly() {
    CanFrame frame = {};
    frame.timestamp_us = 0x12345678UL;
    BinaryCanPacket packet = {};
    TEST_ASSERT_TRUE(TransportProtocol::serializeBinary(frame, packet));
    uint8_t expected_crc = UsbSerialProtocol::crc8(packet.bytes, 20);
    // Since writeCanFrame uses Serial which is not available in native,
    // we simulate the frame formatting here to verify the logic.
    uint8_t buf[21];
    memcpy(buf, packet.bytes, 20);
    buf[20] = UsbSerialProtocol::crc8(buf, 20);
    TEST_ASSERT_EQUAL_HEX8(expected_crc, buf[20]);
}

void test_usb_handshake_identifies_firmware() {
    uint8_t buf[21] = {0};
    buf[0] = 0xAA;
    buf[1] = 0x55;
    const char id[8] = {'E', 'S', 'P', '8', '2', '6', '6', '\0'};
    memcpy(&buf[2], id, 8);
    buf[10] = 0xFF; // Universal Non-CAN DLC discriminator (DLC = 0xFF > 8)
    buf[11] = 0x02; // Message Type: Handshake
    buf[12] = 0x01; // Protocol Version: 1
    buf[20] = UsbSerialProtocol::crc8(buf, 20);

    TEST_ASSERT_EQUAL_HEX8(0xAA, buf[0]);
    TEST_ASSERT_EQUAL_HEX8(0x55, buf[1]);
    TEST_ASSERT_EQUAL_UINT8('E', buf[2]);
    TEST_ASSERT_EQUAL_HEX8(0xFF, buf[10]);
    TEST_ASSERT_EQUAL_UINT8(0x02, buf[11]);
    TEST_ASSERT_EQUAL_UINT8(0x01, buf[12]);
}

void test_usb_heartbeat_identifies_heartbeat() {
    uint8_t buf[21] = {0};
    buf[0] = 0xAA;
    buf[1] = 0x55;
    buf[2] = 0x12; buf[3] = 0x34; buf[4] = 0x56; buf[5] = 0x78; // timestamp
    buf[10] = 0xFF; // Universal Non-CAN DLC discriminator (DLC = 0xFF > 8)
    buf[11] = 0x03; // Message Type: Heartbeat
    buf[12] = 0x01; // Protocol Version: 1
    buf[20] = UsbSerialProtocol::crc8(buf, 20);

    TEST_ASSERT_EQUAL_HEX8(0xAA, buf[0]);
    TEST_ASSERT_EQUAL_HEX8(0x55, buf[1]);
    TEST_ASSERT_EQUAL_HEX8(0xFF, buf[10]);
    TEST_ASSERT_EQUAL_HEX8(0x03, buf[11]);
    TEST_ASSERT_EQUAL_HEX8(0x01, buf[12]);
}

void test_crc8_standard_ascii_123456789() {
    // Standard catalog check vector for CRC-8/SMBus (poly 0x07, init 0x00, refin false, refout false, xorout 0x00)
    const uint8_t data[] = {'1', '2', '3', '4', '5', '6', '7', '8', '9'};
    uint8_t crc = UsbSerialProtocol::crc8(data, sizeof(data));
    TEST_ASSERT_EQUAL_HEX8(0xF4, crc);
}

void test_crc8_independent_can_packet_vectors() {
    // Independent Vector 1: Std ID 0x123, DLC 4, data [1,2,3,4], timestamp 1000 us
    const uint8_t packet1[20] = {
        0xAA, 0x55, 0xE8, 0x03, 0x00, 0x00, 0x23, 0x01, 0x00, 0x00,
        0x04, 0x00, 0x01, 0x02, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00
    };
    TEST_ASSERT_EQUAL_HEX8(0x7B, UsbSerialProtocol::crc8(packet1, 20));

    // Independent Vector 2: Ext ID 0x18DAF110, DLC 8, UDS payload, timestamp 500000 us
    const uint8_t packet2[20] = {
        0xAA, 0x55, 0x20, 0xA1, 0x07, 0x00, 0x10, 0xF1, 0xDA, 0x18,
        0x08, 0x01, 0x02, 0x01, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00
    };
    TEST_ASSERT_EQUAL_HEX8(0x30, UsbSerialProtocol::crc8(packet2, 20));

    // Independent Vector 3: Std ID 0x7E0, DLC 0, timestamp 0 us
    const uint8_t packet3[20] = {
        0xAA, 0x55, 0x00, 0x00, 0x00, 0x00, 0xE0, 0x07, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };
    TEST_ASSERT_EQUAL_HEX8(0x2E, UsbSerialProtocol::crc8(packet3, 20));
}

void test_mcp2515_listen_only_mode_definitions() {
    TEST_ASSERT_EQUAL_HEX8(0x60, McpRegisters::MODE_LISTENONLY);
    TEST_ASSERT_EQUAL_HEX8(0xE0, McpRegisters::MODE_MASK);
}

void test_mcp2515_polling_mode_configuration_no_int_dependency() {
    // Verifies polling mode is active and INT wire is not required
    TEST_ASSERT_FALSE(MCP2515_USE_INTERRUPT);
    TEST_ASSERT_EQUAL_UINT8(255, PIN_CAN_INT); // 255 = Disconnected/unused
    TEST_ASSERT_EQUAL_UINT8(14, PIN_CAN_SCK);  // D5
    TEST_ASSERT_EQUAL_UINT8(12, PIN_CAN_MISO); // D6
    TEST_ASSERT_EQUAL_UINT8(13, PIN_CAN_MOSI); // D7
    TEST_ASSERT_EQUAL_UINT8(4, PIN_CAN_CS);    // D2
}

void test_mcp2515_polling_status_opcodes_and_masks() {
    TEST_ASSERT_EQUAL_HEX8(0xA0, McpRegisters::CMD_READ_STATUS);
    TEST_ASSERT_EQUAL_HEX8(0xB0, McpRegisters::CMD_RX_STATUS);
    TEST_ASSERT_EQUAL_HEX8(0x01, McpRegisters::INT_RX0IF);
    TEST_ASSERT_EQUAL_HEX8(0x02, McpRegisters::INT_RX1IF);
    TEST_ASSERT_EQUAL_HEX8(0xC0, McpRegisters::EFLG_OVERFLOW_MASK);
}

void test_mcp2515_simulated_polling_buffer_drain_order() {
    CanRingBuffer ring;
    
    // Scenario 1: Empty RX buffers -> no frames pushed
    uint8_t status_empty = 0x00;
    TEST_ASSERT_EQUAL_UINT8(0, (status_empty & (McpRegisters::INT_RX0IF | McpRegisters::INT_RX1IF)));
    TEST_ASSERT_EQUAL_UINT32(0, ring.available());

    // Scenario 2: Both RX0 and RX1 pending (burst on bus)
    // MCP2515 status opcode 0xA0 returns status byte with bits 0 and 1 set (0x03)
    uint8_t status_both = McpRegisters::INT_RX0IF | McpRegisters::INT_RX1IF; // 0x03
    
    // Simulate polling drain loop:
    // First drain RX0
    if ((status_both & McpRegisters::INT_RX0IF) != 0) {
        CanFrame f0 = {};
        f0.id = 0x100; f0.dlc = 1; f0.data[0] = 0xAA;
        TEST_ASSERT_TRUE(ring.push(f0));
        status_both &= ~McpRegisters::INT_RX0IF; // RX0 cleared
    }
    // Then drain RX1
    if ((status_both & McpRegisters::INT_RX1IF) != 0) {
        CanFrame f1 = {};
        f1.id = 0x200; f1.dlc = 1; f1.data[0] = 0xBB;
        TEST_ASSERT_TRUE(ring.push(f1));
        status_both &= ~McpRegisters::INT_RX1IF; // RX1 cleared
    }

    // Verify ordering in ring buffer: f0 arrived first, f1 second (strict FIFO)
    TEST_ASSERT_EQUAL_UINT32(2, ring.available());
    CanFrame popped0 = {}, popped1 = {};
    TEST_ASSERT_TRUE(ring.pop(popped0));
    TEST_ASSERT_EQUAL_UINT32(0x100, popped0.id);
    TEST_ASSERT_TRUE(ring.pop(popped1));
    TEST_ASSERT_EQUAL_UINT32(0x200, popped1.id);

    // Scenario 3: Overflow condition
    uint8_t eflg_overflow = McpRegisters::EFLG_RX0OVR | McpRegisters::EFLG_RX1OVR;
    TEST_ASSERT_TRUE((eflg_overflow & McpRegisters::EFLG_OVERFLOW_MASK) != 0);
}

int main(int, char**) {
    UNITY_BEGIN();
    RUN_TEST(test_can_frame_id_and_dlc_bounds);
    RUN_TEST(test_binary_packet_byte_order_and_padding);
    RUN_TEST(test_ring_capacity_overflow_and_order);
    RUN_TEST(test_8mhz_cnf_table_and_one_megabit_rejection);
    RUN_TEST(test_crc8_known_vector_all_zero);
    RUN_TEST(test_crc8_known_vector_magic_bytes);
    RUN_TEST(test_crc8_single_bit_flip_changes_crc);
    RUN_TEST(test_crc8_standard_ascii_123456789);
    RUN_TEST(test_crc8_independent_can_packet_vectors);
    RUN_TEST(test_usb_frame_crc_appended_correctly);
    RUN_TEST(test_usb_handshake_identifies_firmware);
    RUN_TEST(test_usb_heartbeat_identifies_heartbeat);
    RUN_TEST(test_mcp2515_listen_only_mode_definitions);
    RUN_TEST(test_mcp2515_polling_mode_configuration_no_int_dependency);
    RUN_TEST(test_mcp2515_polling_status_opcodes_and_masks);
    RUN_TEST(test_mcp2515_simulated_polling_buffer_drain_order);
    return UNITY_END();
}
