import React, { useState } from 'react';
import { useCan, OperatingMode } from '../context/CanContext';
import { Wifi, WifiOff, FileCode, PlayCircle, Radio, Cpu, Settings, RefreshCw, X, HardDrive, Globe2, Usb, ChevronDown } from 'lucide-react';
import { api } from '../services/api';

interface HeaderProps {
  onNavigateToHardware?: () => void;
}

export const Header: React.FC<HeaderProps> = ({ onNavigateToHardware }) => {
  const {
    gatewayStatus,
    hardwareStatus,
    hardwareTransports,
    activeTransportId,
    selectTransport,
    replayStatus,
    activeDbc,
    fps,
    totalFrames,
    operatingMode,
    setOperatingMode,
    refreshGatewayStatus
  } = useCan();

  const [showHardwareDropdown, setShowHardwareDropdown] = useState(false);
  const [showConfigModal, setShowConfigModal] = useState(false);
  const [showTelemetryModal, setShowTelemetryModal] = useState(false);
  const [ipInput, setIpInput] = useState(gatewayStatus?.gatewayAddress || '192.168.4.1:8088');
  const [savingIp, setSavingIp] = useState(false);

  const esp = gatewayStatus?.espTelemetry;

  const handleSaveAddress = async () => {
    setSavingIp(true);
    try {
      await api.setGatewayAddress(ipInput);
      await refreshGatewayStatus();
      setShowConfigModal(false);
    } catch {
      // Ignore
    } finally {
      setSavingIp(false);
    }
  };

  const handlePollEsp = async () => {
    await api.pollGatewayStatus();
    await refreshGatewayStatus();
  };

  return (
    <header className="h-14 bg-can-panel border-b border-can-border flex items-center justify-between px-4 z-20 font-mono select-none">
      {/* Brand & Operating Modes */}
      <div className="flex items-center space-x-3">
        <div className="flex items-center space-x-3">
          <div className="w-8 h-8 rounded-lg bg-[#070D18] border border-sky-500/40 flex items-center justify-center shadow-md shadow-sky-950/60 overflow-hidden">
            <svg className="w-7 h-7" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient id="headerLogoCyan" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stopColor="#00F0FF" />
                  <stop offset="100%" stopColor="#0284C7" />
                </linearGradient>
                <linearGradient id="headerLogoAmber" x1="0%" y1="100%" x2="100%" y2="0%">
                  <stop offset="0%" stopColor="#F59E0B" />
                  <stop offset="100%" stopColor="#EF4444" />
                </linearGradient>
              </defs>
              {/* Tachometer / Telemetry Arc */}
              <path d="M 9 26 A 12.5 12.5 0 1 1 31 26" stroke="#1E293B" strokeWidth="2.5" strokeLinecap="round" />
              <path d="M 9 26 A 12.5 12.5 0 0 1 20 7.5" stroke="url(#headerLogoCyan)" strokeWidth="2.5" strokeLinecap="round" />
              <path d="M 20 7.5 A 12.5 12.5 0 0 1 29 22" stroke="url(#headerLogoAmber)" strokeWidth="2.5" strokeLinecap="round" />
              {/* Gauge Needle & Core */}
              <circle cx="20" cy="18" r="2.75" fill="#00F0FF" />
              <line x1="20" y1="18" x2="26" y2="11.5" stroke="#F59E0B" strokeWidth="1.75" strokeLinecap="round" />
              {/* Differential CAN Bus Waveform (CAN_H / CAN_L) */}
              <path d="M 5 31 L 12 31 L 15 27.5 L 21 27.5 L 24 31 L 35 31" stroke="#00F0FF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              <path d="M 5 35 L 12 35 L 15 37 L 21 37 L 24 35 L 35 35" stroke="#38BDF8" strokeWidth="1.2" strokeOpacity="0.7" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>
          <div>
            <div className="flex items-center space-x-1.5">
              <h1 className="text-sm font-bold text-white tracking-wide font-sans">APEX CAN</h1>
              <span className="text-[9px] px-1 py-0.2 bg-sky-500/20 text-sky-400 font-mono rounded border border-sky-500/30">RT-500</span>
            </div>
            <p className="text-[10px] text-slate-400 uppercase font-mono tracking-wider">ECU Engineering Platform</p>
          </div>
        </div>

        <div className="h-5 w-px bg-can-border mx-1" />

        {/* 3 Explicit Operating Modes */}
        <div className="flex items-center bg-can-bg rounded border border-can-border p-0.5 text-xs">
          <button
            onClick={() => setOperatingMode('Live Hardware')}
            className={`px-2.5 py-1 rounded font-semibold flex items-center space-x-1.5 transition-all ${
              operatingMode === 'Live Hardware'
                ? 'bg-emerald-600 text-white shadow'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Radio className="w-3 h-3" />
            <span>LIVE HARDWARE</span>
          </button>
          <button
            onClick={() => setOperatingMode('Offline Analysis')}
            className={`px-2.5 py-1 rounded font-semibold flex items-center space-x-1.5 transition-all ${
              operatingMode === 'Offline Analysis'
                ? 'bg-slate-700 text-white shadow'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <HardDrive className="w-3 h-3" />
            <span>OFFLINE ANALYSIS</span>
          </button>
          <button
            onClick={() => setOperatingMode('Simulation')}
            className={`px-2.5 py-1 rounded font-semibold flex items-center space-x-1.5 transition-all ${
              operatingMode === 'Simulation'
                ? 'bg-sky-600 text-white shadow'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <PlayCircle className="w-3 h-3" />
            <span>SIMULATION</span>
          </button>
        </div>
      </div>

      {/* Connectivity & Offline State Bar */}
      <div className="flex items-center space-x-5 text-xs">
        {/* Offline Status Indicators */}
        <div className="flex items-center space-x-3 bg-can-bg px-3 py-1 rounded border border-can-border">
          <div className="flex items-center space-x-1.5">
            <span className="w-2 h-2 rounded-full bg-emerald-400" />
            <span className="text-slate-400 text-[11px]">APP:</span>
            <span className="text-emerald-400 font-bold text-[11px]">LOCAL</span>
          </div>

          <div className="h-3 w-px bg-can-border" />

          <div className="flex items-center space-x-1.5">
            <span className={`w-2 h-2 rounded-full ${hardwareStatus?.isConnected ? 'bg-sky-400' : 'bg-slate-500'}`} />
            <span className="text-slate-400 text-[11px]">HW:</span>
            <span className={`font-bold text-[11px] ${hardwareStatus?.isConnected ? 'text-sky-400' : 'text-slate-400'}`}>
              {hardwareStatus?.isConnected ? 'CONNECTED' : 'OFFLINE'}
            </span>
          </div>

          <div className="h-3 w-px bg-can-border" />

          <div className="flex items-center space-x-1.5">
            <Globe2 className="w-3 h-3 text-slate-500" />
            <span className="text-slate-400 text-[11px]">INTERNET:</span>
            <span className="text-slate-300 text-[11px]">NOT REQUIRED</span>
          </div>
        </div>

        {/* Telemetry metrics */}
        <div className="flex items-center space-x-2">
          <span className="text-slate-400">FPS:</span>
          <span className="text-sky-400 font-bold">{fps}</span>
        </div>

        <div className="flex items-center space-x-2">
          <span className="text-slate-400">FRAMES:</span>
          <span className="text-slate-200">{totalFrames.toLocaleString()}</span>
        </div>

        <div className="flex items-center space-x-2">
          <span className="text-slate-400">DBC:</span>
          <span className={`flex items-center space-x-1 ${activeDbc?.isLoaded ? 'text-emerald-400' : 'text-slate-500'}`}>
            <FileCode className="w-3.5 h-3.5" />
            <span>{activeDbc?.isLoaded ? `${activeDbc.messageCount} msgs` : 'None'}</span>
          </span>
        </div>

        {/* Dynamic Hardware Transport Badge & Quick Switcher Dropdown */}
        <div className="relative">
          <button
            onClick={() => setShowHardwareDropdown(!showHardwareDropdown)}
            className={`flex items-center space-x-1.5 px-2.5 py-1 rounded border transition-colors ${
              hardwareStatus?.isConnected
                ? 'bg-sky-500/10 text-sky-300 border-sky-500/30 hover:bg-sky-500/20'
                : 'bg-slate-800 text-slate-400 border-can-border hover:bg-slate-700'
            }`}
            title="Active CAN Hardware Adapter (Click to switch or view adapters)"
          >
            {hardwareStatus?.transportId === 'waveshare-usb-can-fd' || hardwareStatus?.transportId === 'waveshare-canfd-b' ? (
              <Usb className="w-3.5 h-3.5 text-sky-400" />
            ) : hardwareStatus?.transportId === 'esp8266-usb-serial' ? (
              <Usb className="w-3.5 h-3.5 text-amber-400" />
            ) : hardwareStatus?.transportId === 'virtual-simulation' ? (
              <HardDrive className="w-3.5 h-3.5 text-purple-400" />
            ) : hardwareStatus?.transportId === 'elm327-adapter' ? (
              <Radio className="w-3.5 h-3.5 text-amber-400" />
            ) : (
              hardwareStatus?.isConnected ? <Wifi className="w-3.5 h-3.5 text-emerald-400" /> : <WifiOff className="w-3.5 h-3.5" />
            )}
            <span>
              {hardwareStatus?.transportId === 'waveshare-usb-can-fd' || hardwareStatus?.transportId === 'waveshare-canfd-b'
                ? 'Waveshare CAN-FD'
                : hardwareStatus?.transportId === 'esp8266-usb-serial'
                ? 'ESP8266 (USB Serial)'
                : hardwareStatus?.transportId === 'virtual-simulation'
                ? 'Virtual Sim'
                : hardwareStatus?.transportId === 'elm327-adapter'
                ? 'ELM327 OBD'
                : 'ESP8266 Gateway'}
            </span>
            <ChevronDown className="w-3 h-3 text-slate-400" />
          </button>

          {/* Quick Hardware Switcher Dropdown */}
          {showHardwareDropdown && (
            <div className="absolute right-0 mt-2 w-72 bg-can-panel border border-can-border rounded-lg shadow-2xl z-50 p-2 font-sans space-y-1">
              <div className="flex items-center justify-between px-2 py-1.5 border-b border-can-border text-[11px] font-bold text-slate-400 uppercase">
                <span>HARDWARE TRANSPORTS</span>
                <span className="font-mono text-[10px] text-sky-400">{hardwareTransports.length} Available</span>
              </div>
              <div className="max-h-60 overflow-y-auto space-y-1 py-1">
                {hardwareTransports.map(t => {
                  const isActive = activeTransportId === t.transportId || 
                    ((activeTransportId === 'waveshare-canfd-b' || activeTransportId === 'waveshare-usb-can-fd') && 
                     (t.transportId === 'waveshare-canfd-b' || t.transportId === 'waveshare-usb-can-fd'));
                  return (
                    <button
                      key={t.transportId}
                      onClick={async () => {
                        setShowHardwareDropdown(false);
                        await selectTransport(t.transportId);
                      }}
                      className={`w-full text-left p-2 rounded flex items-center justify-between text-xs transition-colors ${
                        isActive
                          ? 'bg-sky-600/20 border border-sky-500/40 text-white font-semibold'
                          : 'hover:bg-can-card text-slate-300'
                      }`}
                    >
                      <div className="flex items-center space-x-2 truncate">
                        {t.transportId.includes('waveshare') ? <Usb className="w-3.5 h-3.5 text-sky-400 flex-shrink-0" /> :
                         t.transportId.includes('elm') ? <Radio className="w-3.5 h-3.5 text-amber-400 flex-shrink-0" /> :
                         t.transportId.includes('sim') ? <HardDrive className="w-3.5 h-3.5 text-purple-400 flex-shrink-0" /> :
                         <Wifi className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" />}
                        <span className="truncate">{t.name}</span>
                      </div>
                      {isActive && <span className="text-[10px] text-emerald-400 font-mono font-bold">ACTIVE</span>}
                    </button>
                  );
                })}
              </div>
              <div className="border-t border-can-border pt-1">
                <button
                  onClick={() => {
                    setShowHardwareDropdown(false);
                    if (onNavigateToHardware) onNavigateToHardware();
                  }}
                  className="w-full text-center py-1 text-[11px] text-sky-400 hover:text-sky-300 font-bold"
                >
                  Open Full Hardware Manager →
                </button>
              </div>
            </div>
          )}
        </div>

        {/* Hardware Manager Navigation Button */}
        <button
          onClick={onNavigateToHardware}
          className="p-1.5 rounded hover:bg-can-card text-slate-400 hover:text-white transition-colors"
          title="Open Hardware Manager"
        >
          <Settings className="w-4 h-4" />
        </button>
      </div>

      {/* Gateway Address Modal */}
      {showConfigModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center font-sans">
          <div className="bg-can-panel border border-can-border rounded-lg shadow-xl w-96 p-5">
            <div className="flex items-center justify-between pb-3 border-b border-can-border">
              <h2 className="text-sm font-semibold text-white flex items-center space-x-2">
                <Settings className="w-4 h-4 text-sky-400" />
                <span>ESP8266 Gateway Connection</span>
              </h2>
              <button onClick={() => setShowConfigModal(false)} className="text-slate-400 hover:text-white">
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="mt-4 space-y-3">
              <div>
                <label className="text-xs text-slate-400 block mb-1 font-mono">Gateway WebSocket URL / IP:</label>
                <input
                  type="text"
                  value={ipInput}
                  onChange={e => setIpInput(e.target.value)}
                  className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-xs text-white font-mono focus:border-sky-500 focus:outline-none"
                  placeholder="192.168.4.1:8088"
                />
                <p className="text-[11px] text-slate-500 mt-1">Default AP: 192.168.4.1:8088 | Station: Local IP:8088</p>
              </div>
              <div className="flex justify-end space-x-2 pt-2">
                <button
                  onClick={() => setShowConfigModal(false)}
                  className="px-3 py-1.5 rounded text-xs text-slate-400 hover:text-white"
                >
                  Cancel
                </button>
                <button
                  onClick={handleSaveAddress}
                  disabled={savingIp}
                  className="px-4 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-semibold"
                >
                  {savingIp ? 'Connecting...' : 'Save & Connect'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ESP8266 Diagnostics Telemetry Modal */}
      {showTelemetryModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center font-sans">
          <div className="bg-can-panel border border-can-border rounded-lg shadow-xl w-[480px] p-5">
            <div className="flex items-center justify-between pb-3 border-b border-can-border">
              <h2 className="text-sm font-semibold text-white flex items-center space-x-2">
                <Cpu className="w-4 h-4 text-emerald-400" />
                <span>ESP8266 Local Diagnostics</span>
              </h2>
              <div className="flex items-center space-x-2">
                <button onClick={handlePollEsp} className="text-slate-400 hover:text-white" title="Refresh Status">
                  <RefreshCw className="w-3.5 h-3.5" />
                </button>
                <button onClick={() => setShowTelemetryModal(false)} className="text-slate-400 hover:text-white">
                  <X className="w-4 h-4" />
                </button>
              </div>
            </div>

            <div className="mt-4 grid grid-cols-2 gap-3 font-mono text-xs">
              <div className="bg-can-bg p-2.5 rounded border border-can-border">
                <span className="text-slate-500 text-[10px] block">CAN RX FRAMES</span>
                <span className="text-slate-200 font-bold text-sm">{esp?.rx_frames ?? 'N/A'}</span>
              </div>
              <div className="bg-can-bg p-2.5 rounded border border-can-border">
                <span className="text-slate-500 text-[10px] block">ESP RX FPS</span>
                <span className="text-sky-400 font-bold text-sm">{esp?.rx_fps ?? 'N/A'}</span>
              </div>
              <div className="bg-can-bg p-2.5 rounded border border-can-border">
                <span className="text-slate-500 text-[10px] block">DROPPED FRAMES</span>
                <span className={`font-bold text-sm ${esp?.dropped_frames ? 'text-rose-400' : 'text-slate-200'}`}>
                  {esp?.dropped_frames ?? 0}
                </span>
              </div>
              <div className="bg-can-bg p-2.5 rounded border border-can-border">
                <span className="text-slate-500 text-[10px] block">FREE HEAP</span>
                <span className="text-emerald-400 font-bold text-sm">{esp?.free_heap ? `${esp.free_heap} B` : 'N/A'}</span>
              </div>
              <div className="bg-can-bg p-2.5 rounded border border-can-border">
                <span className="text-slate-500 text-[10px] block">CAN ERROR FLAGS (EFLG)</span>
                <span className="text-slate-200 font-bold text-sm">0x{(esp?.eflg ?? 0).toString(16).toUpperCase()}</span>
              </div>
              <div className="bg-can-bg p-2.5 rounded border border-can-border">
                <span className="text-slate-500 text-[10px] block">TEC / REC</span>
                <span className="text-slate-200 font-bold text-sm">{esp?.tec ?? 0} / {esp?.rec ?? 0}</span>
              </div>
              <div className="bg-can-bg p-2.5 rounded border border-can-border">
                <span className="text-slate-500 text-[10px] block">QUEUE USAGE / PEAK</span>
                <span className="text-slate-200 font-bold text-sm">{esp?.queue_usage ?? 0} / {esp?.queue_high_water ?? 0}</span>
              </div>
              <div className="bg-can-bg p-2.5 rounded border border-can-border">
                <span className="text-slate-500 text-[10px] block">MAX LOOP LATENCY</span>
                <span className="text-slate-200 font-bold text-sm">{esp?.max_loop_latency_us ? `${esp.max_loop_latency_us} µs` : 'N/A'}</span>
              </div>
            </div>

            <div className="mt-4 pt-3 border-t border-can-border flex justify-between items-center text-[11px] text-slate-500 font-mono">
              <span>Target: {gatewayStatus?.gatewayAddress}</span>
              <button
                onClick={() => setShowTelemetryModal(false)}
                className="px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded font-sans"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </header>
  );
};
