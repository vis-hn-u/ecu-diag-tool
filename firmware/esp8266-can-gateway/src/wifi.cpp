#include "wifi.h"

#include <ESP8266WiFi.h>
#include <string.h>
#include "config.h"

WifiService::WifiService()
    : station_started_ms_(0),
      last_reconnect_ms_(0),
      station_configured_(false),
      ap_active_(false) {}

void WifiService::begin() {
    station_configured_ = WIFI_SSID[0] != '\0';
    ap_active_ = false;
    WiFi.persistent(false);
    WiFi.setAutoReconnect(true);
    if (station_configured_) {
        startStation();
    } else {
        startAccessPoint();
        if (!ap_active_) {
            WiFi.mode(WIFI_OFF);
        }
    }
}

void WifiService::tick() {
    if (!station_configured_ || WiFi.status() == WL_CONNECTED) {
        return;
    }

    const uint32_t now = millis();
    if (!ap_active_ && now - station_started_ms_ >= WIFI_CONNECT_TIMEOUT_MS) {
        startAccessPoint();
    }
    if (now - last_reconnect_ms_ >= WIFI_RECONNECT_INTERVAL_MS) {
        startStation();
    }
}

bool WifiService::connected() const {
    return WiFi.status() == WL_CONNECTED;
}

bool WifiService::accessPointActive() const {
    return ap_active_;
}

void WifiService::startStation() {
    WiFi.mode(ap_active_ ? WIFI_AP_STA : WIFI_STA);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    station_started_ms_ = millis();
    last_reconnect_ms_ = station_started_ms_;
}

void WifiService::startAccessPoint() {
    // No implicit/open AP: local credentials must be explicitly configured.
    if (AP_SSID[0] == '\0' || strlen(AP_PASSWORD) < 8) {
        return;
    }
    WiFi.mode(station_configured_ ? WIFI_AP_STA : WIFI_AP);
    IPAddress local_ip(192, 168, 4, 1);
    IPAddress gateway(192, 168, 4, 1);
    IPAddress subnet(255, 255, 255, 0);
    WiFi.softAPConfig(local_ip, gateway, subnet);
    ap_active_ = WiFi.softAP(AP_SSID, AP_PASSWORD, 1, 0, 4);
}
