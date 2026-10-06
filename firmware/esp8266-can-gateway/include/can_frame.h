#pragma once

#include <cstdint>
#include <type_traits>

// In-memory representation only. It is deliberately not packed: naturally
// aligned uint32_t fields are safe on ESP8266 and transport writes bytes
// explicitly. Semantic fields use 18 bytes; the ESP8266 ABI pads to 20.
struct CanFrame {
    uint32_t timestamp_us;
    uint32_t id;
    uint8_t dlc;
    uint8_t flags;
    uint8_t data[8];
};

constexpr uint8_t CAN_FLAG_EXTENDED = 0x01;
constexpr uint8_t CAN_FLAG_RTR = 0x02;
constexpr uint8_t CAN_FLAG_MASK = CAN_FLAG_EXTENDED | CAN_FLAG_RTR;
constexpr uint32_t CAN_STANDARD_ID_MAX = 0x7FFUL;
constexpr uint32_t CAN_EXTENDED_ID_MAX = 0x1FFFFFFFUL;

static_assert(sizeof(uint32_t) == 4, "CAN transport requires 32-bit integers");
static_assert(sizeof(CanFrame) == 20,
              "Update RAM accounting when CanFrame changes");
static_assert(alignof(CanFrame) >= alignof(uint32_t),
              "CanFrame uint32_t fields must remain naturally aligned");
static_assert(std::is_trivially_copyable<CanFrame>::value,
              "CanFrame must be safe to copy into the ring buffer");

inline bool isExtendedFrame(const CanFrame& frame) {
    return (frame.flags & CAN_FLAG_EXTENDED) != 0;
}

inline bool isRtrFrame(const CanFrame& frame) {
    return (frame.flags & CAN_FLAG_RTR) != 0;
}

constexpr bool isValidCanId(uint32_t id, bool extended) {
    return id <= (extended ? CAN_EXTENDED_ID_MAX : CAN_STANDARD_ID_MAX);
}

inline bool isValidCanFrame(const CanFrame& frame) {
    return frame.dlc <= 8 &&
           (frame.flags & ~CAN_FLAG_MASK) == 0 &&
           isValidCanId(frame.id, isExtendedFrame(frame));
}
