#pragma once

#include <Arduino.h>
#include <WebSocketsServer.h>
#include "can_frame.h"
#include "stats.h"

class WebSocketGateway {
public:
    explicit WebSocketGateway(uint16_t port);
    void begin(StatsCollector& stats);
    void tick();
    void broadcastFrame(const CanFrame& frame);

private:
    static void webSocketEvent(uint8_t client_id, WStype_t type,
                               uint8_t* payload, size_t length);
    void handleEvent(uint8_t client_id, WStype_t type,
                     uint8_t* payload, size_t length);
    void sendStatus(uint8_t client_id);

    static WebSocketGateway* instance_;
    WebSocketsServer server_;
    StatsCollector* stats_;
};
