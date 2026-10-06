#pragma once

#include <Arduino.h>

// Updated exclusively from application context, never from the CAN ISR.
struct GatewayStatistics {
    uint32_t rx_total_frames;
    uint32_t rx_frame_rate;
    uint32_t dropped_frames;
    uint32_t spi_read_errors;
    uint32_t can_errors;
    uint32_t websocket_failures;
    uint32_t ws_broadcast_count;
    uint32_t free_heap_bytes;
    uint32_t max_loop_latency_us;
    uint16_t queue_usage;
    uint16_t queue_high_water_mark;
    uint8_t can_error_flags;
    uint8_t tx_error_count;
    uint8_t rx_error_count;
    uint8_t connected_clients;
};

class StatsCollector {
public:
    StatsCollector();
    void recordFrameReceived();
    void recordFrameDropped();
    void recordSpiError();
    void recordCanError();
    void recordWsBroadcast();
    void recordWebSocketFailure();
    void recordLatency(uint32_t latency_us);
    void setQueueUsage(size_t usage);
    void setCanErrorState(uint8_t error_flags, uint8_t tx_error_count,
                          uint8_t rx_error_count);
    void setConnectedClients(uint8_t count);
    void tickOneSecond();
    const GatewayStatistics& getStats() const;
    size_t serializeStatsJson(char* out_buf, size_t max_len) const;

private:
    GatewayStatistics stats_;
    uint32_t last_frame_counter_;
    uint32_t last_tick_ms_;
};
