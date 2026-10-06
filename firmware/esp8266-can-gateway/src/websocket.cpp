#include "websocket.h"

#include <string.h>
#include "transport_protocol.h"

WebSocketGateway* WebSocketGateway::instance_ = nullptr;

WebSocketGateway::WebSocketGateway(uint16_t port) : server_(port), stats_(nullptr) {}

void WebSocketGateway::begin(StatsCollector& stats) {
    instance_ = this;
    stats_ = &stats;
    server_.begin();
    server_.onEvent(WebSocketGateway::webSocketEvent);
}

void WebSocketGateway::tick() {
    server_.loop();
    if (stats_ != nullptr) {
        const int clients = server_.connectedClients();
        stats_->setConnectedClients(clients > 255 ? 255 : static_cast<uint8_t>(clients));
    }
}

void WebSocketGateway::broadcastFrame(const CanFrame& frame) {
    if (stats_ == nullptr) {
        return;
    }
    BinaryCanPacket packet = {};
    if (!TransportProtocol::serializeBinary(frame, packet)) {
        stats_->recordWebSocketFailure();
        return;
    }
    if (server_.connectedClients() == 0) {
        stats_->setConnectedClients(0);
        return;
    }
    if (server_.broadcastBIN(packet.bytes, sizeof(packet.bytes))) {
        stats_->recordWsBroadcast();
    } else {
        stats_->recordWebSocketFailure();
    }
}

void WebSocketGateway::webSocketEvent(uint8_t client_id, WStype_t type,
                                      uint8_t* payload, size_t length) {
    if (instance_ != nullptr) {
        instance_->handleEvent(client_id, type, payload, length);
    }
}

void WebSocketGateway::handleEvent(uint8_t client_id, WStype_t type,
                                   uint8_t* payload, size_t length) {
    if (type == WStype_CONNECTED) {
        sendStatus(client_id);
        return;
    }
    if (type == WStype_TEXT && length == 6 && memcmp(payload, "status", 6) == 0) {
        sendStatus(client_id);
        return;
    }
    if (type == WStype_TEXT && stats_ != nullptr) {
        // Phase 1 exposes diagnostics only; unsupported text is not a config
        // or CAN-transmit command and is counted as a WebSocket failure.
        stats_->recordWebSocketFailure();
    }
}

void WebSocketGateway::sendStatus(uint8_t client_id) {
    if (stats_ == nullptr) {
        return;
    }
    char message[384];
    const size_t length = stats_->serializeStatsJson(message, sizeof(message));
    if (length == 0 || !server_.sendTXT(client_id, message, length)) {
        stats_->recordWebSocketFailure();
    }
}
