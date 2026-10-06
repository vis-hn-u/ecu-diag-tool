#include "stats.h"

#include <string.h>

StatsCollector::StatsCollector() : last_frame_counter_(0), last_tick_ms_(0) {
    memset(&stats_, 0, sizeof(stats_));
    stats_.free_heap_bytes = ESP.getFreeHeap();
}

void StatsCollector::recordFrameReceived() { ++stats_.rx_total_frames; }
void StatsCollector::recordFrameDropped() { ++stats_.dropped_frames; }
void StatsCollector::recordSpiError() { ++stats_.spi_read_errors; }
void StatsCollector::recordCanError() { ++stats_.can_errors; }
void StatsCollector::recordWsBroadcast() { ++stats_.ws_broadcast_count; }
void StatsCollector::recordWebSocketFailure() { ++stats_.websocket_failures; }

void StatsCollector::recordLatency(uint32_t latency_us) {
    if (latency_us > stats_.max_loop_latency_us) {
        stats_.max_loop_latency_us = latency_us;
    }
}

void StatsCollector::setQueueUsage(size_t usage) {
    const uint16_t bounded = usage > 0xFFFFU ? 0xFFFFU : static_cast<uint16_t>(usage);
    stats_.queue_usage = bounded;
    if (bounded > stats_.queue_high_water_mark) {
        stats_.queue_high_water_mark = bounded;
    }
}

void StatsCollector::setCanErrorState(uint8_t error_flags, uint8_t tx_error_count,
                                      uint8_t rx_error_count) {
    stats_.can_error_flags = error_flags;
    stats_.tx_error_count = tx_error_count;
    stats_.rx_error_count = rx_error_count;
}

void StatsCollector::setConnectedClients(uint8_t count) {
    stats_.connected_clients = count;
}

void StatsCollector::tickOneSecond() {
    const uint32_t now = millis();
    if (now - last_tick_ms_ >= 1000U) {
        stats_.rx_frame_rate = stats_.rx_total_frames - last_frame_counter_;
        last_frame_counter_ = stats_.rx_total_frames;
        stats_.free_heap_bytes = ESP.getFreeHeap();
        last_tick_ms_ = now;
    }
}

const GatewayStatistics& StatsCollector::getStats() const {
    return stats_;
}

size_t StatsCollector::serializeStatsJson(char* out_buf, size_t max_len) const {
    if (out_buf == nullptr || max_len == 0) {
        return 0;
    }

    const int written = snprintf(
        out_buf, max_len,
        "{\"rx_frames\":%lu,\"rx_fps\":%lu,\"dropped_frames\":%lu,"
        "\"spi_errors\":%lu,\"can_errors\":%lu,\"queue_usage\":%u,"
        "\"queue_high_water\":%u,\"ws_failures\":%lu,\"ws_broadcasts\":%lu,"
        "\"eflg\":%u,\"tec\":%u,\"rec\":%u,\"free_heap\":%lu,"
        "\"max_loop_latency_us\":%lu,\"clients\":%u}",
        static_cast<unsigned long>(stats_.rx_total_frames),
        static_cast<unsigned long>(stats_.rx_frame_rate),
        static_cast<unsigned long>(stats_.dropped_frames),
        static_cast<unsigned long>(stats_.spi_read_errors),
        static_cast<unsigned long>(stats_.can_errors),
        static_cast<unsigned int>(stats_.queue_usage),
        static_cast<unsigned int>(stats_.queue_high_water_mark),
        static_cast<unsigned long>(stats_.websocket_failures),
        static_cast<unsigned long>(stats_.ws_broadcast_count),
        static_cast<unsigned int>(stats_.can_error_flags),
        static_cast<unsigned int>(stats_.tx_error_count),
        static_cast<unsigned int>(stats_.rx_error_count),
        static_cast<unsigned long>(stats_.free_heap_bytes),
        static_cast<unsigned long>(stats_.max_loop_latency_us),
        static_cast<unsigned int>(stats_.connected_clients));
    return written >= 0 && static_cast<size_t>(written) < max_len
               ? static_cast<size_t>(written)
               : 0;
}
