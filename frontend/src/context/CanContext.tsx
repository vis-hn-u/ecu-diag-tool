import React, { createContext, useContext, useEffect, useState, useRef, ReactNode } from 'react';
import * as signalR from '@microsoft/signalr';
import { CanFrame, DecodedSignal, CanFrameBatch, NetworkTopologyState, GatewayStatus, HardwareStatus, HardwareTransportInfo, ReplayStatus, DbcDatabaseInfo } from '../types';
import { api, FALLBACK_HARDWARE_TRANSPORTS } from '../services/api';

export type OperatingMode = 'Live Hardware' | 'Offline Analysis' | 'Simulation';

interface CanContextType {
  frames: CanFrame[];
  signals: Record<string, DecodedSignal>;
  signalHistory: Record<string, { timestamp: number; value: number }[]>;
  topology: NetworkTopologyState | null;
  gatewayStatus: GatewayStatus | null;
  hardwareStatus: HardwareStatus | null;
  hardwareTransports: HardwareTransportInfo[];
  activeTransportId: string | null;
  selectTransport: (transportId: string) => Promise<boolean>;
  configureTransport: (config: {
    transportId: string;
    nominalBaudRate?: number;
    dataBaudRate?: number;
    activeChannel?: number;
    gatewayAddress?: string;
    portName?: string;
    serialBaudRate?: number;
    listenOnly?: boolean;
  }) => Promise<boolean>;
  refreshHardwareStatus: () => Promise<void>;
  replayStatus: ReplayStatus | null;
  activeDbc: DbcDatabaseInfo | null;
  fps: number;
  totalFrames: number;
  isStreaming: boolean;
  setIsStreaming: (val: boolean) => void;
  selectedFrame: CanFrame | null;
  setSelectedFrame: (frame: CanFrame | null) => void;
  pinnedSignals: string[];
  togglePinSignal: (name: string) => void;
  clearFrames: () => void;
  refreshDbc: () => Promise<void>;
  refreshGatewayStatus: () => Promise<void>;
  connectionState: 'Connected' | 'Connecting' | 'Disconnected';
  operatingMode: OperatingMode;
  setOperatingMode: (mode: OperatingMode) => void;
}

const CanContext = createContext<CanContextType | undefined>(undefined);

const MAX_FRAMES = 2500;
const MAX_SIGNAL_HISTORY = 120;

const STORAGE_KEY_PINNED = 'can_platform_pinned_signals';
const STORAGE_KEY_MODE = 'can_platform_operating_mode';

export const CanProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [frames, setFrames] = useState<CanFrame[]>([]);
  const [signals, setSignals] = useState<Record<string, DecodedSignal>>({});
  const [signalHistory, setSignalHistory] = useState<Record<string, { timestamp: number; value: number }[]>>({});
  const [topology, setTopology] = useState<NetworkTopologyState | null>(null);
  const [gatewayStatus, setGatewayStatus] = useState<GatewayStatus | null>(null);
  const [hardwareStatus, setHardwareStatus] = useState<HardwareStatus | null>(null);
  const [hardwareTransports, setHardwareTransports] = useState<HardwareTransportInfo[]>(FALLBACK_HARDWARE_TRANSPORTS);
  const [replayStatus, setReplayStatus] = useState<ReplayStatus | null>(null);
  const [activeDbc, setActiveDbc] = useState<DbcDatabaseInfo | null>(null);
  const [fps, setFps] = useState<number>(0);
  const [totalFrames, setTotalFrames] = useState<number>(0);
  const [isStreaming, setIsStreaming] = useState<boolean>(true);
  const [selectedFrame, setSelectedFrame] = useState<CanFrame | null>(null);
  const [connectionState, setConnectionState] = useState<'Connected' | 'Connecting' | 'Disconnected'>('Connecting');

  // Load persisted user preferences
  const [pinnedSignals, setPinnedSignals] = useState<string[]>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY_PINNED);
      return saved ? JSON.parse(saved) : ['EngineSpeed', 'VehicleSpeed', 'ThrottlePosition'];
    } catch {
      return ['EngineSpeed', 'VehicleSpeed', 'ThrottlePosition'];
    }
  });

  const [operatingMode, setOperatingModeState] = useState<OperatingMode>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY_MODE);
      return (saved as OperatingMode) || 'Live Hardware';
    } catch {
      return 'Live Hardware';
    }
  });

  const isStreamingRef = useRef(isStreaming);
  isStreamingRef.current = isStreaming;

  const setOperatingMode = (mode: OperatingMode) => {
    setOperatingModeState(mode);
    try {
      localStorage.setItem(STORAGE_KEY_MODE, mode);
    } catch {}

    if (mode === 'Simulation') {
      api.setTopologyMode('Simulation').catch(() => {});
    } else if (mode === 'Offline Analysis') {
      api.setTopologyMode('Paused').catch(() => {});
    } else {
      api.setTopologyMode('Live').catch(() => {});
    }
  };

  const refreshDbc = async () => {
    try {
      const data = await api.getActiveDbc();
      setActiveDbc(data);
    } catch {
      // Offline fallback
    }
  };

  const refreshGatewayStatus = async () => {
    try {
      const data = await api.getGatewayStatus();
      setGatewayStatus(data);
    } catch {
      // Offline fallback
    }
  };

  const refreshHardwareStatus = async () => {
    try {
      const [statusRes, transportsRes] = await Promise.allSettled([
        api.getHardwareStatus(),
        api.getHardwareTransports()
      ]);
      if (statusRes.status === 'fulfilled' && statusRes.value) {
        setHardwareStatus(statusRes.value);
      }
      if (transportsRes.status === 'fulfilled' && Array.isArray(transportsRes.value) && transportsRes.value.length > 0) {
        setHardwareTransports(transportsRes.value);
      }
    } catch {
      // Offline fallback
    }
  };

  const selectTransport = async (transportId: string): Promise<boolean> => {
    try {
      const res = await api.selectHardwareTransport(transportId);
      await refreshHardwareStatus();
      return !!res;
    } catch {
      return false;
    }
  };

  const configureTransport = async (config: {
    transportId: string;
    nominalBaudRate?: number;
    dataBaudRate?: number;
    activeChannel?: number;
    gatewayAddress?: string;
    portName?: string;
    serialBaudRate?: number;
  }): Promise<boolean> => {
    try {
      const updated = await api.configureHardwareTransport(config);
      setHardwareStatus(updated);
      await refreshHardwareStatus();
      return true;
    } catch {
      return false;
    }
  };

  const clearFrames = () => {
    setFrames([]);
    setSelectedFrame(null);
  };

  const togglePinSignal = (name: string) => {
    setPinnedSignals(prev => {
      const next = prev.includes(name) ? prev.filter(s => s !== name) : [...prev, name];
      try {
        localStorage.setItem(STORAGE_KEY_PINNED, JSON.stringify(next));
      } catch {}
      return next;
    });
  };

  useEffect(() => {
    refreshDbc();
    refreshGatewayStatus();
    refreshHardwareStatus();

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/can')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('ReceiveFrameBatch', (batch: CanFrameBatch) => {
      if (batch.frameRate !== undefined) {
        setFps(batch.frameRate);
      }

      setTotalFrames(prev => prev + batch.frames.length);

      if (isStreamingRef.current && batch.frames.length > 0) {
        setFrames(prev => {
          const next = [...prev, ...batch.frames];
          return next.length > MAX_FRAMES ? next.slice(next.length - MAX_FRAMES) : next;
        });
      }

      if (batch.signals && batch.signals.length > 0) {
        setSignals(prev => {
          const next = { ...prev };
          for (const s of batch.signals) {
            next[s.signalName] = s;
          }
          return next;
        });

        setSignalHistory(prev => {
          const next = { ...prev };
          const now = Date.now();
          for (const s of batch.signals) {
            const history = next[s.signalName] ? [...next[s.signalName]] : [];
            history.push({ timestamp: now, value: s.engineeringValue });
            if (history.length > MAX_SIGNAL_HISTORY) {
              history.shift();
            }
            next[s.signalName] = history;
          }
          return next;
        });
      }
    });

    connection.on('ReceiveTopologyUpdate', (top: NetworkTopologyState) => {
      setTopology(top);
    });

    connection.on('ReceiveReplayStatus', (status: ReplayStatus) => {
      setReplayStatus(status);
      if (status.isPlaying) {
        setOperatingModeState('Simulation');
      }
    });

    connection.on('ReceiveGatewayStatus', (status: GatewayStatus) => {
      setGatewayStatus(status);
    });

    connection.on('ReceiveHardwareStatus', (status: HardwareStatus) => {
      setHardwareStatus(status);
    });

    connection.onreconnecting(() => setConnectionState('Connecting'));
    connection.onreconnected(() => setConnectionState('Connected'));
    connection.onclose(() => setConnectionState('Disconnected'));

    connection.start()
      .then(() => {
        setConnectionState('Connected');
        connection.invoke('RequestInitialState').catch(() => {});
      })
      .catch(() => setConnectionState('Disconnected'));

    return () => {
      connection.stop();
    };
  }, []);

  return (
    <CanContext.Provider
      value={{
        frames,
        signals,
        signalHistory,
        topology,
        gatewayStatus,
        hardwareStatus,
        hardwareTransports,
        activeTransportId: hardwareStatus?.transportId || null,
        selectTransport,
        configureTransport,
        refreshHardwareStatus,
        replayStatus,
        activeDbc,
        fps,
        totalFrames,
        isStreaming,
        setIsStreaming,
        selectedFrame,
        setSelectedFrame,
        pinnedSignals,
        togglePinSignal,
        clearFrames,
        refreshDbc,
        refreshGatewayStatus,
        connectionState,
        operatingMode,
        setOperatingMode
      }}
    >
      {children}
    </CanContext.Provider>
  );
};

export const useCan = () => {
  const context = useContext(CanContext);
  if (!context) {
    throw new Error('useCan must be used within a CanProvider');
  }
  return context;
};
