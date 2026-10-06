#pragma once

#include <Arduino.h>

class WifiService {
public:
    WifiService();
    void begin();
    void tick();
    bool connected() const;
    bool accessPointActive() const;

private:
    void startStation();
    void startAccessPoint();

    uint32_t station_started_ms_;
    uint32_t last_reconnect_ms_;
    bool station_configured_;
    bool ap_active_;
};
