#include <Arduino.h>
#include <unity.h>

#include "can_frame.h"
#include "mcp2515_defs.h"
#include "ring_buffer.h"
#include "transport_protocol.h"

// PlatformIO test builds do not link normal application sources by default.
#include "../../src/transport_protocol.cpp"

void test_can_frame_validation() {
    CanFrame standard = {};
    standard.id = 0x7FF;
    standard.dlc = 8;
    TEST_ASSERT_TRUE(isValidCanFrame(standard));

    standard.id = 0x800;
    TEST_ASSERT_FALSE(isValidCanFrame(standard));

    CanFrame extended = {};
    extended.id = 0x1FFFFFFF;
    extended.dlc = 8;
    extended.flags = CAN_FLAG_EXTENDED;
    TEST_ASSERT_TRUE(isValidCanFrame(extended));
    extended.id = 0x20000000;
    TEST_ASSERT_FALSE(isValidCanFrame(extended));
}

void test_binary_packet_is_explicit_little_endian() {
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
                                0xBE, 0xEF};
    TEST_ASSERT_EQUAL_UINT8_ARRAY(expected, packet.bytes, sizeof(expected));
    TEST_ASSERT_EQUAL_UINT8(0, packet.bytes[19]);
}

void test_ring_buffer_capacity_and_overflow() {
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
    CanFrame unused = {};
    TEST_ASSERT_FALSE(ring.pop(unused));
}

void test_8mhz_bit_timing_table() {
    const McpBitTiming bit125 = mcpBitTimingFor(CanBitRate::CAN_125KBPS,
                                                 McpOscillatorFreq::OSC_8MHZ);
    TEST_ASSERT_TRUE(bit125.supported);
    TEST_ASSERT_EQUAL_HEX8(0x01, bit125.cnf1);
    TEST_ASSERT_EQUAL_HEX8(0xB1, bit125.cnf2);
    TEST_ASSERT_EQUAL_HEX8(0x05, bit125.cnf3);

    const McpBitTiming bit250 = mcpBitTimingFor(CanBitRate::CAN_250KBPS,
                                                 McpOscillatorFreq::OSC_8MHZ);
    TEST_ASSERT_EQUAL_HEX8(0x00, bit250.cnf1);
    TEST_ASSERT_EQUAL_HEX8(0xB1, bit250.cnf2);
    TEST_ASSERT_EQUAL_HEX8(0x05, bit250.cnf3);

    const McpBitTiming bit500 = mcpBitTimingFor(CanBitRate::CAN_500KBPS,
                                                 McpOscillatorFreq::OSC_8MHZ);
    TEST_ASSERT_EQUAL_HEX8(0x00, bit500.cnf1);
    TEST_ASSERT_EQUAL_HEX8(0x90, bit500.cnf2);
    TEST_ASSERT_EQUAL_HEX8(0x02, bit500.cnf3);

    TEST_ASSERT_FALSE(mcpBitTimingFor(CanBitRate::CAN_1000KBPS,
                                       McpOscillatorFreq::OSC_8MHZ).supported);
}

void setup() {
    delay(2000);
    UNITY_BEGIN();
    RUN_TEST(test_can_frame_validation);
    RUN_TEST(test_binary_packet_is_explicit_little_endian);
    RUN_TEST(test_ring_buffer_capacity_and_overflow);
    RUN_TEST(test_8mhz_bit_timing_table);
    UNITY_END();
}

void loop() {
}
