import React from 'react';
import { useCan } from '../context/CanContext';
import { 
  Radio, 
  Cpu, 
  Wifi, 
  Usb,
  Server, 
  GitCommit, 
  FileCode, 
  Activity, 
  Network, 
  ChevronRight,
  AlertCircle
} from 'lucide-react';

export const DataFlowMonitor: React.FC = () => {
  const { 
    gatewayStatus, 
    hardwareStatus,
    fps, 
    totalFrames, 
    activeDbc, 
    signals, 
    topology, 
    operatingMode, 
    connectionState,
    replayStatus 
  } = useCan();

  const isLiveMode = operatingMode === 'Live Hardware';
  const isSimMode = operatingMode === 'Simulation' || (replayStatus?.isPlaying ?? false);
  const isUsbEsp = hardwareStatus?.transportId === 'esp8266-usb-serial';
  const isHwConnected = (hardwareStatus?.isConnected || gatewayStatus?.isConnected) ?? false;
  const esp = gatewayStatus?.espTelemetry;

  // Real-time stage statuses
  const stages = [
    {
      id: 'canbus',
      name: 'CAN BUS',
      icon: Radio,
      status: isLiveMode && isHwConnected ? 'ACTIVE' : isSimMode ? 'SIMULATED' : 'IDLE',
      subtext: isLiveMode ? '500 kbit/s Active' : isSimMode ? 'Simulated Bus' : 'Bench Disconnected'
    },
    {
      id: 'esp',
      name: isUsbEsp ? 'ESP8266 (USB)' : 'ESP8266',
      icon: Cpu,
      status: isHwConnected ? 'ACTIVE' : isSimMode ? 'BYPASSED' : 'INACTIVE',
      subtext: isHwConnected ? (isUsbEsp ? `${hardwareStatus?.connectionDetails ?? 'COM8'} @ 921.6k` : `${esp?.free_heap ?? 0}B Heap`) : isSimMode ? 'Sim Gateway' : 'No Connection'
    },
    {
      id: 'ws',
      name: isUsbEsp ? 'USB SERIAL' : 'WEBSOCKET',
      icon: isUsbEsp ? Usb : Wifi,
      status: isHwConnected ? 'ACTIVE' : isSimMode ? 'INTERNAL' : 'IDLE',
      subtext: isHwConnected ? (isUsbEsp ? '921600 Baud 8N1' : `ws://${gatewayStatus?.gatewayAddress}`) : isSimMode ? 'Loopback Feed' : 'Offline'
    },
    {
      id: 'backend',
      name: 'BACKEND',
      icon: Server,
      status: connectionState === 'Connected' ? 'ACTIVE' : 'ERROR',
      subtext: connectionState === 'Connected' ? 'ASP.NET Core' : 'Connecting...'
    },
    {
      id: 'pipeline',
      name: 'CAN PIPELINE',
      icon: GitCommit,
      status: fps > 0 ? 'ACTIVE' : 'IDLE',
      subtext: `${fps} FPS (${totalFrames.toLocaleString()})`
    },
    {
      id: 'dbc',
      name: 'DBC DECODER',
      icon: FileCode,
      status: activeDbc?.isLoaded ? 'ACTIVE' : 'IDLE',
      subtext: activeDbc?.isLoaded ? `${activeDbc.messageCount} Messages` : 'No DBC'
    },
    {
      id: 'signals',
      name: 'LIVE SIGNALS',
      icon: Activity,
      status: Object.keys(signals).length > 0 ? 'ACTIVE' : 'IDLE',
      subtext: `${Object.keys(signals).length} Decoded`
    },
    {
      id: 'topology',
      name: 'TOPOLOGY',
      icon: Network,
      status: (topology?.activeNodeCount ?? 0) > 0 ? 'ACTIVE' : 'IDLE',
      subtext: `${topology?.activeNodeCount ?? 0} Active Nodes`
    }
  ];

  return (
    <div className="bg-can-panel border border-can-border rounded-lg p-3 font-mono shadow-md select-none">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center space-x-2">
          <span className="w-2 h-2 rounded-full bg-sky-400 animate-pulse" />
          <span className="text-[11px] font-bold text-white uppercase tracking-wider font-sans">
            REAL-TIME DATA FLOW PIPELINE
          </span>
        </div>
        <div className="flex items-center space-x-3 text-[10px] text-slate-400">
          <span>Mode: <strong className="text-white">{operatingMode}</strong></span>
          <span>Drops: <strong className={esp?.dropped_frames ? 'text-rose-400' : 'text-slate-300'}>{esp?.dropped_frames ?? 0}</strong></span>
          <span>Unknown IDs: <strong className="text-amber-400">{topology?.messages?.filter(m => !m.isKnown).length ?? 0}</strong></span>
        </div>
      </div>

      {/* Pipeline Stage Bar */}
      <div className="grid grid-cols-8 gap-1.5 items-center">
        {stages.map((stage, idx) => {
          const Icon = stage.icon;
          const isActive = stage.status === 'ACTIVE';
          const isSim = stage.status === 'SIMULATED' || stage.status === 'INTERNAL' || stage.status === 'BYPASSED';
          const isErr = stage.status === 'ERROR';

          return (
            <div key={stage.id} className="relative flex flex-col">
              <div
                className={`p-2 rounded border flex flex-col justify-between transition-all ${
                  isActive
                    ? 'bg-emerald-500/10 border-emerald-500/40 text-emerald-300 shadow-sm'
                    : isSim
                    ? 'bg-sky-500/10 border-sky-500/40 text-sky-300'
                    : isErr
                    ? 'bg-rose-500/10 border-rose-500/40 text-rose-300'
                    : 'bg-can-bg border-can-border text-slate-500'
                }`}
              >
                <div className="flex items-center justify-between mb-1">
                  <span className="text-[10px] font-bold truncate">{stage.name}</span>
                  <Icon className={`w-3.5 h-3.5 ${isActive ? 'text-emerald-400 animate-pulse' : isSim ? 'text-sky-400' : isErr ? 'text-rose-400' : 'text-slate-600'}`} />
                </div>
                <div className="flex items-center justify-between text-[9px]">
                  <span className={`px-1 py-0.2 rounded font-bold ${
                    isActive ? 'bg-emerald-500/20 text-emerald-300' : isSim ? 'bg-sky-500/20 text-sky-300' : isErr ? 'bg-rose-500/20 text-rose-300' : 'text-slate-500'
                  }`}>
                    {stage.status}
                  </span>
                  <span className="text-[9px] text-slate-400 truncate max-w-[65px]" title={stage.subtext}>
                    {stage.subtext}
                  </span>
                </div>
              </div>

              {idx < stages.length - 1 && (
                <div className="absolute -right-2.5 top-1/2 -translate-y-1/2 z-10 hidden xl:block text-slate-600">
                  <ChevronRight className="w-3.5 h-3.5" />
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};
