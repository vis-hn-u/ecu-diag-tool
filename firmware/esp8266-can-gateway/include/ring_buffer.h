#pragma once

#include <cstddef>
#include <cstdint>
#include "can_frame.h"
#include "config.h"

// The ISR never accesses this object. The application loop performs both the
// MCP2515 read/push producer step and the WebSocket pop consumer step, so no
// volatile indices or lock-free publication assumptions are required.
class CanRingBuffer {
public:
    static constexpr size_t kSlotCount = CAN_RING_BUFFER_SIZE;
    static constexpr size_t kUsableCapacity = kSlotCount - 1;
    static constexpr size_t kStorageBytes = kSlotCount * sizeof(CanFrame);

    CanRingBuffer() : head_(0), tail_(0), dropped_count_(0), high_water_mark_(0), buffer_{} {}

    bool push(const CanFrame& frame) {
        const size_t next_head = (head_ + 1) & (kSlotCount - 1);
        if (next_head == tail_) {
            ++dropped_count_;
            return false;
        }
        buffer_[head_] = frame;
        head_ = next_head;
        const size_t usage = available();
        if (usage > high_water_mark_) {
            high_water_mark_ = usage;
        }
        return true;
    }

    bool pop(CanFrame& frame) {
        if (head_ == tail_) {
            return false;
        }
        frame = buffer_[tail_];
        tail_ = (tail_ + 1) & (kSlotCount - 1);
        return true;
    }

    size_t available() const {
        return (head_ - tail_) & (kSlotCount - 1);
    }

    uint32_t droppedCount() const { return dropped_count_; }
    size_t highWaterMark() const { return high_water_mark_; }

private:
    static_assert(kSlotCount >= 2, "Ring needs one sentinel slot");
    static_assert((kSlotCount & (kSlotCount - 1)) == 0,
                  "CAN_RING_BUFFER_SIZE must be a power of two");

    size_t head_;
    size_t tail_;
    uint32_t dropped_count_;
    size_t high_water_mark_;
    CanFrame buffer_[kSlotCount];
};

static_assert(CanRingBuffer::kUsableCapacity == 127,
              "One slot is reserved to distinguish full from empty");
#if defined(ARDUINO_ARCH_ESP8266)
static_assert(sizeof(size_t) == 4, "Phase 1 targets 32-bit ESP8266");
static_assert(CanRingBuffer::kStorageBytes == 2560,
              "128 CanFrame slots must consume 2,560 bytes");
static_assert(sizeof(CanRingBuffer) == 2576,
              "Update documented ESP8266 ring-buffer memory usage");
#endif
