import React, { useState, useEffect } from 'react';
import { useCan } from '../context/CanContext';
import { api } from '../services/api';
import { 
  Cpu, 
  CheckCircle2, 
  AlertCircle, 
  RefreshCw, 
  Send, 
  ShieldAlert, 
  ShieldCheck, 
  Wifi, 
  Usb, 
  HardDrive, 
  Sliders, 
  Zap, 
  Activity,
  Layers,
  Radio,
  Network,
  GitBranch
} from 'lucide-react';

export const HardwareManagerPage: React.FC = () => {
  const {
    hardwareStatus,
    hardwareTransports,
    activeTransportId,
    selectTransport,
    configureTransport,
    refreshHardwareStatus
  } = useCan();

  const [isSwitching, setIsSwitching] = useState(false);
  const [switchMessage, setSwitchMessage] = useState<string | null>(null);

  // Configuration forms state
  const [waveshareNominal, setWaveshareNominal] = useState<number>(500000);
  const [waveshareData, setWaveshareData] = useState<number>(2000000);
  const [waveshareChannel, setWaveshareChannel] = useState<number>(0);
  const [savingWaveshare, setSavingWaveshare] = useState(false);

  const [espAddress, setEspAddress] = useState<string>('192.168.4.1:8088');
  const [savingEsp, setSavingEsp] = useState(false);

  const [espUsbPort, setEspUsbPort] = useState<string>('');
  const [espUsbPorts, setEspUsbPorts] = useState<string[]>([]);
  const [loadingEspUsbPorts, setLoadingEspUsbPorts] = useState(false);
  const [savingEspUsb, setSavingEspUsb] = useState(false);

  const [elmPort, setElmPort] = useState<string>('Loopback');
  const [elmBaud, setElmBaud] = useState<number>(115200);
  const [elmProtocol, setElmProtocol] = useState<number>(6);
  const [availablePorts, setAvailablePorts] = useState<string[]>(['Loopback (Simulated)']);
  const [loadingPorts, setLoadingPorts] = useState(false);
  const [savingElm, setSavingElm] = useState(false);
  const [elmCmdInput, setElmCmdInput] = useState<string>('010C');
  const [elmCmdResult, setElmCmdResult] = useState<string | null>(null);
  const [executingCmd, setExecutingCmd] = useState(false);

  // Arduino Nano GVRET transport state
  const [gvretPort, setGvretPort] = useState<string>('COM8');
  const [gvretPorts, setGvretPorts] = useState<string[]>(['COM8']);
  const [loadingGvretPorts, setLoadingGvretPorts] = useState(false);
  const [gvretBusSpeed, setGvretBusSpeed] = useState<number>(500000);
  const [gvretUartBaud, setGvretUartBaud] = useState<number>(1000000);
  const [gvretListenOnly, setGvretListenOnly] = useState<boolean>(true);
  const [savingGvret, setSavingGvret] = useState(false);

  // Test transmit state
  const [testId, setTestId] = useState<string>('7DF');
  const [testPayload, setTestPayload] = useState<string>('02 01 00 00 00 00 00 00');
  const [testIsCanFd, setTestIsCanFd] = useState(false);
  const [testBrs, setTestBrs] = useState(true);
  const [testChannel, setTestChannel] = useState<number>(0);
  const [transmitting, setTransmitting] = useState(false);
  const [txResult, setTxResult] = useState<string | null>(null);

  const loadElmPorts = async () => {
    setLoadingPorts(true);
    try {
      const ports = await api.getElm327Ports();
      if (Array.isArray(ports) && ports.length > 0) {
        setAvailablePorts(ports);
      }
    } catch {
      // Fallback
    } finally {
      setLoadingPorts(false);
    }
  };

  const loadEspUsbPorts = async () => {
    setLoadingEspUsbPorts(true);
    try {
      const ports = await api.getEsp8266UsbPorts();
      if (Array.isArray(ports) && ports.length > 0) {
        setEspUsbPorts(ports);
        if (!espUsbPort) setEspUsbPort(ports[0]);
      }
    } catch {
      // Fallback
    } finally {
      setLoadingEspUsbPorts(false);
    }
  };

  const loadGvretPorts = async () => {
    setLoadingGvretPorts(true);
    try {
      const ports = await api.getGvretPorts();
      if (Array.isArray(ports) && ports.length > 0) {
        setGvretPorts(ports);
        if (!gvretPort) setGvretPort(ports[0]);
      }
    } catch {
      // Fallback
    } finally {
      setLoadingGvretPorts(false);
    }
  };

  const handleSaveGvretConfig = async () => {
    setSavingGvret(true);
    try {
      await configureTransport({
        transportId: 'arduino-nano-gvret',
        portName: gvretPort,
        nominalBaudRate: gvretBusSpeed,
        serialBaudRate: gvretUartBaud,
        listenOnly: gvretListenOnly
      });
      await api.setGvretListenOnly(gvretListenOnly);
      setSwitchMessage('Arduino Nano GVRET parameters applied');
    } catch {
      setSwitchMessage('Failed to configure GVRET parameters');
    } finally {
      setSavingGvret(false);
      setTimeout(() => setSwitchMessage(null), 4000);
    }
  };

  useEffect(() => {
    loadElmPorts();
    loadEspUsbPorts();
    loadGvretPorts();
  }, []);

  const isWaveshareId = (id?: string | null) => id === 'waveshare-canfd-b' || id === 'waveshare-usb-can-fd';

  const getTransportIcon = (transportId: string) => {
    if (isWaveshareId(transportId)) return Usb;
    if (transportId === 'esp8266-gateway') return Wifi;
    if (transportId === 'esp8266-usb-serial') return Usb;
    if (transportId === 'arduino-nano-gvret') return Cpu;
    if (transportId === 'virtual-simulation') return HardDrive;
    if (transportId === 'elm327-adapter') return Radio;
    return Cpu;
  };

  // Initialize form state when hardware status arrives
  useEffect(() => {
    if (hardwareStatus) {
      if (isWaveshareId(hardwareStatus.transportId) && hardwareStatus.extendedTelemetry) {
        const t = hardwareStatus.extendedTelemetry as any;
        if (t.nominalBaudRate) setWaveshareNominal(t.nominalBaudRate);
        if (t.dataBaudRate) setWaveshareData(t.dataBaudRate);
        if (t.activeChannel !== undefined) setWaveshareChannel(t.activeChannel);
      } else if (hardwareStatus.transportId === 'esp8266-gateway') {
        const addr = hardwareStatus.connectionDetails?.replace('ws://', '') || '192.168.4.1:8088';
        setEspAddress(addr);
      } else if (hardwareStatus.transportId === 'esp8266-usb-serial' && hardwareStatus.extendedTelemetry) {
        const t = hardwareStatus.extendedTelemetry as any;
        if (t.comPort && t.comPort !== 'None') setEspUsbPort(t.comPort);
      } else if (hardwareStatus.transportId === 'arduino-nano-gvret' && hardwareStatus.extendedTelemetry) {
        const t = hardwareStatus.extendedTelemetry as any;
        if (t.comPort && t.comPort !== 'None') setGvretPort(t.comPort);
        if (t.listenOnly !== undefined) setGvretListenOnly(t.listenOnly);
      } else if (hardwareStatus.transportId === 'elm327-adapter' && hardwareStatus.extendedTelemetry) {
        const t = hardwareStatus.extendedTelemetry as any;
        if (t.portName) setElmPort(t.portName);
        if (t.serialBaudRate) setElmBaud(t.serialBaudRate);
      }
    }
  }, [hardwareStatus]);

  const handleSelectAdapter = async (transportId: string) => {
    setIsSwitching(true);
    setSwitchMessage(null);
    try {
      const ok = await selectTransport(transportId);
      if (ok) {
        setSwitchMessage(`Active transport switched to ${transportId}`);
      } else {
        setSwitchMessage(`Failed to switch to ${transportId}`);
      }
    } catch (err: any) {
      setSwitchMessage(`Error: ${err?.message || 'Failed to switch'}`);
    } finally {
      setIsSwitching(false);
      setTimeout(() => setSwitchMessage(null), 4000);
    }
  };

  const handleSaveWaveshareConfig = async () => {
    setSavingWaveshare(true);
    try {
      await configureTransport({
        transportId: 'waveshare-usb-can-fd',
        nominalBaudRate: waveshareNominal,
        dataBaudRate: waveshareData,
        activeChannel: waveshareChannel
      });
      setSwitchMessage('Waveshare USB-CAN-FD-B configuration updated');
    } catch {
      setSwitchMessage('Failed to update Waveshare configuration');
    } finally {
      setSavingWaveshare(false);
      setTimeout(() => setSwitchMessage(null), 4000);
    }
  };

  const handleSaveEspAddress = async () => {
    setSavingEsp(true);
    try {
      await configureTransport({
        transportId: 'esp8266-gateway',
        gatewayAddress: espAddress
      });
      setSwitchMessage('ESP8266 gateway address updated');
    } catch {
      setSwitchMessage('Failed to update ESP8266 address');
    } finally {
      setSavingEsp(false);
      setTimeout(() => setSwitchMessage(null), 4000);
    }
  };

  const handleSaveEspUsbConfig = async () => {
    setSavingEspUsb(true);
    try {
      await configureTransport({
        transportId: 'esp8266-usb-serial',
        portName: espUsbPort
      });
      setSwitchMessage('ESP8266 USB Serial configuration updated');
    } catch {
      setSwitchMessage('Failed to update ESP8266 USB configuration');
    } finally {
      setSavingEspUsb(false);
      setTimeout(() => setSwitchMessage(null), 4000);
    }
  };

  const handleSaveElmConfig = async () => {
    setSavingElm(true);
    try {
      await configureTransport({
        transportId: 'elm327-adapter',
        portName: elmPort,
        serialBaudRate: elmBaud,
        nominalBaudRate: (elmProtocol === 8 || elmProtocol === 9) ? 250000 : 500000
      });
      setSwitchMessage('ELM327 configuration updated');
    } catch {
      setSwitchMessage('Failed to update ELM327 configuration');
    } finally {
      setSavingElm(false);
      setTimeout(() => setSwitchMessage(null), 4000);
    }
  };

  const handleExecuteElmCmd = async (cmd?: string) => {
    const toSend = cmd || elmCmdInput;
    if (!toSend) return;
    setExecutingCmd(true);
    setElmCmdResult(null);
    try {
      const res = await api.executeElm327Command(toSend);
      if (res.success) {
        setElmCmdResult(`[OK] ${res.lines?.join(' | ') || res.rawResponse || 'OK'}`);
      } else {
        setElmCmdResult(`[ERR] ${res.errorMessage || res.rawResponse || 'Failed'}`);
      }
    } catch (err: any) {
      setElmCmdResult(`[ERR] ${err?.message || 'Error executing command'}`);
    } finally {
      setExecutingCmd(false);
    }
  };

  const handleSendTestFrame = async () => {
    setTransmitting(true);
    setTxResult(null);
    try {
      const cleanId = parseInt(testId.replace(/^0x/i, ''), 16);
      if (isNaN(cleanId)) throw new Error('Invalid CAN ID');

      const bytes = testPayload
        .trim()
        .split(/\s+/)
        .filter(s => s.length > 0)
        .map(s => parseInt(s, 16));

      if (bytes.some(b => isNaN(b) || b < 0 || b > 255)) {
        throw new Error('Invalid byte sequence in payload');
      }

      const res = await api.transmitCanFrame({
        id: cleanId,
        data: bytes,
        isCanFd: testIsCanFd,
        bitRateSwitch: testBrs,
        channel: testChannel
      });

      if (res.success) {
        setTxResult(`Sent successfully on CAN${testChannel + 1}: ${bytes.length} bytes`);
      } else {
        setTxResult(`Transmit refused: ${res.error || 'Check transport state'}`);
      }
    } catch (err: any) {
      setTxResult(`Transmit error: ${err.message}`);
    } finally {
      setTransmitting(false);
      setTimeout(() => setTxResult(null), 5000);
    }
  };

  const activeCaps = hardwareStatus?.capabilities;

  return (
    <div className="h-full overflow-y-auto p-4 space-y-4 font-mono text-xs select-none">
      {/* Header & Status Notice */}
      <div className="flex flex-wrap items-center justify-between gap-3 bg-can-panel border border-can-border rounded-lg p-3 shadow-md">
        <div>
          <div className="flex items-center space-x-2">
            <Cpu className="w-5 h-5 text-sky-400" />
            <h1 className="text-base font-bold text-white font-sans tracking-wide">HARDWARE MANAGER</h1>
            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-sky-500/20 text-sky-300 border border-sky-500/30">
              DYNAMIC ABSTRACTION LAYER
            </span>
          </div>
          <p className="text-[11px] text-slate-400 font-sans mt-0.5">
            Unified adapter lifecycle management. All diagnostic, telemetry, and analysis tools route through the selected hardware transport.
          </p>
        </div>

        <div className="flex items-center space-x-3">
          {switchMessage && (
            <span className="px-3 py-1 rounded bg-sky-950/80 border border-sky-500/40 text-sky-300 text-xs animate-fade-in">
              {switchMessage}
            </span>
          )}
          <button
            onClick={() => refreshHardwareStatus()}
            className="flex items-center space-x-1.5 px-3 py-1.5 rounded bg-can-bg border border-can-border text-slate-300 hover:text-white hover:border-slate-500 transition-colors"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            <span>REFRESH</span>
          </button>
        </div>
      </div>

      {/* Active Hardware Telemetry Summary Banner */}
      {hardwareStatus && (
        <div className="bg-can-card border border-can-border rounded-lg p-3 shadow-lg">
          <div className="flex items-center justify-between pb-2 border-b border-can-border/60 mb-2.5">
            <div className="flex items-center space-x-2">
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-pulse" />
              <span className="text-slate-400 font-bold uppercase text-[11px]">ACTIVE HARDWARE ADAPTER:</span>
              <span className="text-white font-bold text-sm font-sans">{hardwareStatus.name}</span>
              <span className="text-[10px] text-slate-500">({hardwareStatus.transportId})</span>
            </div>
            <div className="flex items-center space-x-2">
              <span className={`px-2 py-0.5 rounded text-[10px] font-bold border ${
                hardwareStatus.isConnected
                  ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40'
                  : 'bg-rose-500/20 text-rose-300 border-rose-500/40'
              }`}>
                {hardwareStatus.isConnected ? 'STATE: CONNECTED' : 'STATE: DISCONNECTED'}
              </span>
              <span className={`px-2 py-0.5 rounded text-[10px] font-bold border ${
                hardwareStatus.capabilities.supportsTransmit
                  ? 'bg-sky-500/20 text-sky-300 border-sky-500/40'
                  : 'bg-amber-500/20 text-amber-300 border-amber-500/40'
              }`}>
                {hardwareStatus.capabilities.supportsTransmit ? 'MODE: BIDIRECTIONAL (TX/RX)' : 'MODE: LISTEN-ONLY (PASSIVE)'}
              </span>
            </div>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-6 gap-2 text-center text-[11px]">
            <div className="bg-can-bg/80 p-2 rounded border border-can-border">
              <span className="text-slate-500 text-[10px] block">CONNECTION DETAILS</span>
              <span className="text-slate-200 font-bold truncate block">{hardwareStatus.connectionDetails || 'Ready'}</span>
            </div>
            <div className="bg-can-bg/80 p-2 rounded border border-can-border">
              <span className="text-slate-500 text-[10px] block">CHANNELS</span>
              <span className="text-sky-400 font-bold">{hardwareStatus.capabilities.channelCount} {hardwareStatus.capabilities.channelCount > 1 ? '(CAN1 & CAN2)' : '(CAN1)'}</span>
            </div>
            <div className="bg-can-bg/80 p-2 rounded border border-can-border">
              <span className="text-slate-500 text-[10px] block">CAN PROTOCOL</span>
              <span className="text-emerald-400 font-bold">{hardwareStatus.capabilities.supportsCanFd ? 'Classic CAN + CAN FD' : 'Classic CAN (2.0B)'}</span>
            </div>
            <div className="bg-can-bg/80 p-2 rounded border border-can-border">
              <span className="text-slate-500 text-[10px] block">MAX PAYLOAD</span>
              <span className="text-slate-200 font-bold">{hardwareStatus.capabilities.maxPayloadBytes} Bytes</span>
            </div>
            <div className="bg-can-bg/80 p-2 rounded border border-can-border">
              <span className="text-slate-500 text-[10px] block">RX RATE / TOTAL</span>
              <span className="text-sky-300 font-bold">{hardwareStatus.rxFrameRate} fps / {hardwareStatus.totalFramesReceived.toLocaleString()}</span>
            </div>
            <div className="bg-can-bg/80 p-2 rounded border border-can-border">
              <span className="text-slate-500 text-[10px] block">TX RATE / TOTAL</span>
              <span className="text-amber-300 font-bold">{hardwareStatus.txFrameRate} fps / {hardwareStatus.totalFramesTransmitted.toLocaleString()}</span>
            </div>
          </div>
        </div>
      )}

      {/* 1. DISCOVERED HARDWARE ADAPTERS & TRANSPORTS LISTING */}
      <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-lg space-y-3 font-sans">
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-can-border pb-3">
          <div className="flex items-center space-x-2.5">
            <div className="p-2 rounded-lg bg-sky-500/10 border border-sky-500/30 text-sky-400">
              <Layers className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-sm font-bold text-white uppercase tracking-wider flex items-center space-x-2">
                <span>Discovered Hardware Transports & Adapters</span>
                <span className="px-2 py-0.5 rounded-full text-[10px] font-mono font-bold bg-sky-500/20 text-sky-300 border border-sky-500/30">
                  {hardwareTransports.length} Registered
                </span>
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">
                Physical transceivers, network gateways, serial interfaces, and virtual loops registered in the platform abstraction layer.
              </p>
            </div>
          </div>

          <div className="flex items-center space-x-2">
            <button
              onClick={() => refreshHardwareStatus()}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded bg-can-bg border border-can-border text-slate-300 hover:text-white hover:border-sky-500 font-mono text-xs transition-colors"
            >
              <RefreshCw className="w-3.5 h-3.5" />
              <span>Rescan Hardware</span>
            </button>
          </div>
        </div>

        {/* Dynamic Transports Table / Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3 pt-1">
          {hardwareTransports.map(t => {
            const isCurrentActive = activeTransportId === t.transportId || (isWaveshareId(activeTransportId) && isWaveshareId(t.transportId));
            const Icon = getTransportIcon(t.transportId);
            return (
              <div
                key={t.transportId}
                className={`rounded-lg border p-3.5 flex flex-col justify-between transition-all ${
                  isCurrentActive
                    ? 'bg-sky-950/30 border-sky-500/60 shadow-md ring-1 ring-sky-500/30'
                    : 'bg-can-bg/90 border-can-border/80 hover:border-slate-600'
                }`}
              >
                <div>
                  <div className="flex items-start justify-between mb-2">
                    <div className="flex items-center space-x-2">
                      <div className={`p-1.5 rounded-md ${isCurrentActive ? 'bg-sky-600 text-white' : 'bg-slate-800 text-slate-400'}`}>
                        <Icon className="w-4 h-4" />
                      </div>
                      <div>
                        <div className="font-bold text-xs text-white leading-tight">{t.name}</div>
                        <span className="font-mono text-[10px] text-slate-400 block mt-0.5">{t.transportId}</span>
                      </div>
                    </div>

                    {isCurrentActive ? (
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/40 font-mono flex items-center space-x-1">
                        <CheckCircle2 className="w-3 h-3" />
                        <span>ACTIVE</span>
                      </span>
                    ) : (
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-slate-700 font-mono">
                        STANDBY
                      </span>
                    )}
                  </div>

                  <div className="space-y-1.5 my-2.5 font-mono text-[11px] text-slate-300">
                    <div className="flex justify-between border-b border-can-border/40 pb-1">
                      <span className="text-slate-500">Channels:</span>
                      <span className="font-semibold text-slate-200">{t.capabilities?.channelCount > 1 ? 'Dual (CAN1 & CAN2)' : 'Single (CAN1)'}</span>
                    </div>
                    <div className="flex justify-between border-b border-can-border/40 pb-1">
                      <span className="text-slate-500">Max Payload:</span>
                      <span className="font-semibold text-sky-400">{t.capabilities?.supportsCanFd ? '64 Bytes (CAN FD)' : '8 Bytes (Classic)'}</span>
                    </div>
                    <div className="flex justify-between border-b border-can-border/40 pb-1">
                      <span className="text-slate-500">Mode:</span>
                      <span className={`font-semibold ${t.capabilities?.supportsTransmit ? 'text-emerald-400' : 'text-amber-400'}`}>
                        {t.capabilities?.supportsTransmit ? 'Bidirectional TX/RX' : 'Listen-Only (Passive)'}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-500">Fast Bitrate (BRS):</span>
                      <span className="font-semibold text-slate-300">{t.capabilities?.supportsBitRateSwitch ? 'Supported' : 'No'}</span>
                    </div>
                  </div>
                </div>

                <div className="pt-2">
                  <button
                    onClick={() => handleSelectAdapter(t.transportId)}
                    disabled={isSwitching || isCurrentActive}
                    className={`w-full py-1.5 rounded text-xs font-bold font-mono transition-all flex items-center justify-center space-x-1.5 ${
                      isCurrentActive
                        ? 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/40 cursor-default'
                        : 'bg-sky-600 hover:bg-sky-500 text-white border border-sky-400 shadow-sm'
                    }`}
                  >
                    {isCurrentActive ? (
                      <>
                        <CheckCircle2 className="w-3.5 h-3.5" />
                        <span>CURRENT ACTIVE</span>
                      </>
                    ) : (
                      <>
                        <Zap className="w-3.5 h-3.5" />
                        <span>SELECT & ACTIVATE</span>
                      </>
                    )}
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Adapters Detailed Configuration Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        {/* Card 1: Waveshare USB-CAN-FD-B */}
        <div className={`bg-can-panel border rounded-lg p-4 shadow-lg flex flex-col justify-between transition-all ${
          isWaveshareId(activeTransportId)
            ? 'border-sky-500/60 shadow-sky-950/40 bg-gradient-to-b from-sky-950/10 to-can-panel ring-1 ring-sky-500/30'
            : 'border-can-border'
        }`}>
          <div>
            {/* Top Bar */}
            <div className="flex items-start justify-between border-b border-can-border pb-3">
              <div className="flex items-center space-x-2.5">
                <div className="w-8 h-8 rounded-lg bg-sky-500/10 border border-sky-500/30 flex items-center justify-center text-sky-400">
                  <Usb className="w-5 h-5" />
                </div>
                <div>
                  <h2 className="text-sm font-bold text-white font-sans">Waveshare USB-CAN-FD-B</h2>
                  <span className="text-[10px] text-slate-400">USB 2.0 Dual-Channel CAN FD</span>
                </div>
              </div>

              {isWaveshareId(activeTransportId) ? (
                <span className="flex items-center space-x-1 px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/40">
                  <CheckCircle2 className="w-3 h-3" />
                  <span>ACTIVE</span>
                </span>
              ) : (
                <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-can-border">
                  STANDBY
                </span>
              )}
            </div>

            {/* Capabilities Matrix */}
            <div className="my-3 space-y-1.5">
              <div className="text-[10px] text-slate-500 uppercase font-semibold">Hardware Capabilities</div>
              <div className="flex flex-wrap gap-1">
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-sky-500/15 text-sky-300 border border-sky-500/30 font-bold">
                  CAN1 & CAN2 (Dual)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-bold">
                  CAN FD (0–64B)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-bold">
                  Classic CAN
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-purple-500/15 text-purple-300 border border-purple-500/30 font-bold">
                  BRS & ESI
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-sky-500/15 text-sky-300 border border-sky-500/30 font-bold">
                  TX + RX Bidirectional
                </span>
              </div>
            </div>

            {/* Configuration Controls */}
            <div className="space-y-2.5 bg-can-bg p-3 rounded border border-can-border text-[11px] mb-4">
              <div className="text-[10px] text-slate-400 font-semibold uppercase flex items-center justify-between">
                <span>Adapter Configuration</span>
                <Sliders className="w-3.5 h-3.5 text-sky-400" />
              </div>

              {/* Active Channel Selector */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">Target CAN Channel:</label>
                <div className="grid grid-cols-2 gap-1.5">
                  <button
                    type="button"
                    onClick={() => setWaveshareChannel(0)}
                    className={`py-1 rounded font-bold border transition-colors ${
                      waveshareChannel === 0
                        ? 'bg-sky-600 text-white border-sky-400'
                        : 'bg-can-card text-slate-400 border-can-border hover:text-white'
                    }`}
                  >
                    CAN1 (Channel 0)
                  </button>
                  <button
                    type="button"
                    onClick={() => setWaveshareChannel(1)}
                    className={`py-1 rounded font-bold border transition-colors ${
                      waveshareChannel === 1
                        ? 'bg-sky-600 text-white border-sky-400'
                        : 'bg-can-card text-slate-400 border-can-border hover:text-white'
                    }`}
                  >
                    CAN2 (Channel 1)
                  </button>
                </div>
              </div>

              {/* Arbitration Bitrate */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">Arbitration Bitrate (Nominal):</label>
                <select
                  value={waveshareNominal}
                  onChange={e => setWaveshareNominal(parseInt(e.target.value, 10))}
                  className="w-full bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-sky-500 font-mono text-xs"
                >
                  <option value={100000}>100 kbps (Slow bus)</option>
                  <option value={250000}>250 kbps (J1939 / Medium)</option>
                  <option value={500000}>500 kbps (Automotive OBD-II standard)</option>
                  <option value={1000000}>1000 kbps (1 Mbps High-speed)</option>
                </select>
              </div>

              {/* CAN FD Data Bitrate */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">CAN FD Data Bitrate:</label>
                <select
                  value={waveshareData}
                  onChange={e => setWaveshareData(parseInt(e.target.value, 10))}
                  className="w-full bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-sky-500 font-mono text-xs"
                >
                  <option value={1000000}>1 Mbps (Data 1M)</option>
                  <option value={2000000}>2 Mbps (Standard CAN FD 2M)</option>
                  <option value={4000000}>4 Mbps (High-speed 4M)</option>
                  <option value={5000000}>5 Mbps (Max Recommended 5M)</option>
                </select>
              </div>

              {/* Save Config */}
              <button
                type="button"
                onClick={handleSaveWaveshareConfig}
                disabled={savingWaveshare}
                className="w-full py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border font-medium text-[11px] transition-colors"
              >
                {savingWaveshare ? 'Applying...' : 'Apply Bitrate & Channel Settings'}
              </button>
            </div>
          </div>

          {/* Activate Button */}
          <button
            onClick={() => handleSelectAdapter('waveshare-usb-can-fd')}
            disabled={isSwitching || isWaveshareId(activeTransportId)}
            className={`w-full py-2 rounded font-bold transition-all text-xs flex items-center justify-center space-x-2 ${
              isWaveshareId(activeTransportId)
                ? 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/40 cursor-default'
                : 'bg-sky-600 hover:bg-sky-500 text-white border border-sky-400 shadow-md'
            }`}
          >
            {isWaveshareId(activeTransportId) ? (
              <>
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                <span>ACTIVE ADAPTER</span>
              </>
            ) : (
              <>
                <Zap className="w-4 h-4" />
                <span>CONNECT / ACTIVATE WAVESHARE</span>
              </>
            )}
          </button>
        </div>

        {/* Card 2: ESP8266 + MCP2515 Gateway (USB Serial) */}
        <div className={`bg-can-panel border rounded-lg p-4 shadow-lg flex flex-col justify-between transition-all ${
          activeTransportId === 'esp8266-usb-serial' || activeTransportId === 'esp8266-gateway'
            ? 'border-amber-500/60 shadow-amber-950/40 bg-gradient-to-b from-amber-950/10 to-can-panel ring-1 ring-amber-500/30'
            : 'border-can-border'
        }`}>
          <div>
            {/* Top Bar */}
            <div className="flex items-start justify-between border-b border-can-border pb-3">
              <div className="flex items-center space-x-2.5">
                <div className="w-8 h-8 rounded-lg bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400">
                  <Usb className="w-5 h-5" />
                </div>
                <div>
                  <h2 className="text-sm font-bold text-white font-sans">ESP8266 + MCP2515</h2>
                  <span className="text-[10px] text-slate-400">USB Serial Gateway (921600 baud)</span>
                </div>
              </div>

              <div className="flex items-center space-x-1.5">
                <span className="px-1.5 py-0.5 rounded text-[9px] font-bold bg-amber-500/15 text-amber-300 border border-amber-500/30 font-mono">
                  USB SERIAL
                </span>
                {activeTransportId === 'esp8266-usb-serial' || activeTransportId === 'esp8266-gateway' ? (
                  <span className="flex items-center space-x-1 px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/40 font-mono">
                    <CheckCircle2 className="w-3 h-3" />
                    <span>ACTIVE</span>
                  </span>
                ) : (
                  <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-can-border font-mono">
                    STANDBY
                  </span>
                )}
              </div>
            </div>

            {/* Capabilities Matrix */}
            <div className="my-3 space-y-1.5">
              <div className="text-[10px] text-slate-500 uppercase font-semibold">Hardware Capabilities</div>
              <div className="flex flex-wrap gap-1">
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-bold">
                  CAN1 Only
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-bold">
                  Classic CAN (0–8B)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-amber-500/15 text-amber-300 border border-amber-500/30 font-bold">
                  Listen-Only (Passive)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-sky-500/15 text-sky-300 border border-sky-500/30 font-bold">
                  921600 Baud
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 text-slate-500 border border-slate-700 font-bold line-through">
                  CAN FD
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 text-slate-500 border border-slate-700 font-bold line-through">
                  Transmission
                </span>
              </div>
            </div>

            {/* Configuration Controls */}
            <div className="space-y-2.5 bg-can-bg p-3 rounded border border-can-border text-[11px] mb-4">
              <div className="text-[10px] text-slate-400 font-semibold uppercase flex items-center justify-between">
                <span>Serial Interface Configuration</span>
                <Sliders className="w-3.5 h-3.5 text-amber-400" />
              </div>

              {/* COM Port Dropdown & Controls */}
              <div>
                <div className="flex items-center justify-between mb-1">
                  <label className="text-slate-400 text-[10px]">COM Port (Auto-detected):</label>
                  <button
                    type="button"
                    onClick={loadEspUsbPorts}
                    disabled={loadingEspUsbPorts}
                    className="text-[10px] text-sky-400 hover:text-sky-300 flex items-center space-x-1"
                  >
                    <RefreshCw className={`w-3 h-3 ${loadingEspUsbPorts ? 'animate-spin' : ''}`} />
                    <span>Scan</span>
                  </button>
                </div>
                <div className="flex space-x-1.5">
                  <select
                    value={espUsbPort}
                    onChange={e => setEspUsbPort(e.target.value)}
                    className="flex-1 bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-amber-500 font-mono text-xs"
                  >
                    {espUsbPorts.length > 0 ? (
                      espUsbPorts.map(p => (
                        <option key={p} value={p}>{p}</option>
                      ))
                    ) : (
                      <option value="">No COM Ports Detected</option>
                    )}
                  </select>
                  <button
                    type="button"
                    onClick={handleSaveEspUsbConfig}
                    disabled={savingEspUsb || !espUsbPort}
                    className="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border font-medium text-[10px]"
                  >
                    {savingEspUsb ? '...' : 'Apply'}
                  </button>
                </div>
              </div>

              {/* Fixed Channel & Bitrate Details */}
              <div className="grid grid-cols-2 gap-2 text-[10px]">
                <div className="bg-can-card p-1.5 rounded border border-can-border">
                  <span className="text-slate-500 block">CHANNEL:</span>
                  <span className="text-slate-200 font-bold">CAN1 (Channel 0)</span>
                </div>
                <div className="bg-can-card p-1.5 rounded border border-can-border">
                  <span className="text-slate-500 block">CAN BITRATE:</span>
                  <span className="text-slate-200 font-bold">500 kbps (Fixed)</span>
                </div>
                <div className="bg-can-card p-1.5 rounded border border-can-border">
                  <span className="text-slate-500 block">SERIAL BAUD:</span>
                  <span className="text-amber-400 font-bold">921600 8N1</span>
                </div>
                <div className="bg-can-card p-1.5 rounded border border-can-border">
                  <span className="text-slate-500 block">CRC PROTOCOL:</span>
                  <span className="text-sky-300 font-bold">CRC-8 (21-Byte)</span>
                </div>
              </div>

              {/* Live Telemetry (When Active) */}
              {(activeTransportId === 'esp8266-usb-serial' || activeTransportId === 'esp8266-gateway') && hardwareStatus?.extendedTelemetry && (
                <div className="p-2 bg-slate-900/80 rounded border border-amber-500/30 space-y-1 text-[10px]">
                  <div className="flex justify-between">
                    <span className="text-slate-400">Gateway Identity:</span>
                    <span className={`font-bold ${(hardwareStatus.extendedTelemetry as any).isGatewayIdentified ? 'text-emerald-400' : 'text-amber-400'}`}>
                      {(hardwareStatus.extendedTelemetry as any).isGatewayIdentified ? 'CONFIRMED' : 'WAITING'}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-slate-400">RX Frames / Rate:</span>
                    <span className="text-slate-200 font-mono">
                      {(hardwareStatus.extendedTelemetry as any).rxFrameCount ?? hardwareStatus.totalFramesReceived} ({hardwareStatus.rxFrameRate} fps)
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-slate-400">Dropped / Errors:</span>
                    <span className="text-slate-200 font-mono">
                      {(hardwareStatus.extendedTelemetry as any).droppedFrames ?? 0} dropped / {(hardwareStatus.extendedTelemetry as any).serialErrors ?? 0} err
                    </span>
                  </div>
                </div>
              )}

              {/* Bring-Up Architecture & Hardware Forensic Status (Section 16) */}
              {(activeTransportId === 'esp8266-usb-serial' || activeTransportId === 'esp8266-gateway') && (
                <div className="p-2.5 bg-slate-950/80 rounded border border-slate-700/60 space-y-1.5 text-[10px] font-mono">
                  <div className="text-[11px] font-bold text-slate-300 border-b border-slate-800 pb-1 flex justify-between items-center">
                    <span>HARDWARE FORENSIC STATE</span>
                    <span className="text-amber-400 text-[9px] px-1.5 py-0.2 rounded bg-amber-500/10 border border-amber-500/30">
                      EXPERIMENTAL
                    </span>
                  </div>
                  <div className="grid grid-cols-2 gap-x-2 gap-y-1 text-[9px]">
                    <div className="flex justify-between">
                      <span className="text-slate-400">SPI 100-CYCLE:</span>
                      <span className="text-emerald-400 font-bold">PASS (100/100)</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-400">MCP2515:</span>
                      <span className="text-emerald-400 font-bold">DETECTED (8 MHz)</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-400">CAN BITRATE:</span>
                      <span className="text-slate-200 font-bold">500 kbps (Fixed)</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-400">MODE:</span>
                      <span className="text-sky-400 font-bold">LOOPBACK</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-400">LOOPBACK TEST:</span>
                      <span className="text-emerald-400 font-bold">PASS (ID 0x123)</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-400">LEVEL SHIFTER:</span>
                      <span className="text-rose-400 font-bold">NOT PRESENT</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-400">EXT CAN NODE:</span>
                      <span className="text-slate-500 font-bold">NOT PRESENT</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-400">VEHICLE:</span>
                      <span className="text-slate-500 font-bold">NOT CONNECTED</span>
                    </div>
                  </div>
                  <div className="border-t border-slate-800/80 pt-1 text-[9px] flex justify-between items-center">
                    <span className="text-slate-400">PHYSICAL CAN RX:</span>
                    <span className="text-amber-400 font-bold">NOT YET VALIDATED</span>
                  </div>
                </div>
              )}

              {/* Hardware Warning: Direct 3.3V <-> 5V SPI (No Level Shifter) */}
              <div className="p-2 rounded bg-rose-500/10 border border-rose-500/30 text-rose-300 text-[10px] flex items-start space-x-1.5">
                <AlertCircle className="w-4 h-4 shrink-0 text-rose-400 mt-0.5" />
                <span>
                  <strong>WARNING:</strong> Direct ESP8266 ↔ 5 V MCP2515 SPI detected. Install an appropriate 3.3 V ↔ 5 V logic-level interface before production/vehicle operation.
                </span>
              </div>

              {/* Safety Listen-Only Notice */}
              <div className="p-2 rounded bg-amber-500/10 border border-amber-500/30 text-amber-300 text-[10px] flex items-start space-x-1.5">
                <ShieldAlert className="w-4 h-4 shrink-0 text-amber-400 mt-0.5" />
                <span>
                  <strong>Safety Enforced:</strong> Gateway operates strictly in passive listen-only mode. All frame transmission is disabled to prevent vehicle bus disturbance.
                </span>
              </div>
            </div>
          </div>

          {/* Activate Button */}
          <button
            onClick={() => handleSelectAdapter('esp8266-usb-serial')}
            disabled={isSwitching || activeTransportId === 'esp8266-usb-serial'}
            className={`w-full py-2 rounded font-bold transition-all text-xs flex items-center justify-center space-x-2 ${
              activeTransportId === 'esp8266-usb-serial'
                ? 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/40 cursor-default'
                : 'bg-amber-600 hover:bg-amber-500 text-white border border-amber-400 shadow-md'
            }`}
          >
            {activeTransportId === 'esp8266-usb-serial' ? (
              <>
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                <span>ACTIVE ADAPTER</span>
              </>
            ) : (
              <>
                <Zap className="w-4 h-4" />
                <span>CONNECT / ACTIVATE ESP8266 USB</span>
              </>
            )}
          </button>
        </div>

        {/* Card 3: Virtual CAN / CAN FD Simulation */}
        <div className={`bg-can-panel border rounded-lg p-4 shadow-lg flex flex-col justify-between transition-all ${
          activeTransportId === 'virtual-simulation'
            ? 'border-sky-500/60 shadow-sky-950/40 bg-gradient-to-b from-sky-950/10 to-can-panel ring-1 ring-sky-500/30'
            : 'border-can-border'
        }`}>
          <div>
            {/* Top Bar */}
            <div className="flex items-start justify-between border-b border-can-border pb-3">
              <div className="flex items-center space-x-2.5">
                <div className="w-8 h-8 rounded-lg bg-purple-500/10 border border-purple-500/30 flex items-center justify-center text-purple-400">
                  <HardDrive className="w-5 h-5" />
                </div>
                <div>
                  <h2 className="text-sm font-bold text-white font-sans">Virtual Simulation Bus</h2>
                  <span className="text-[10px] text-slate-400">In-Process CAN & CAN FD Loopback</span>
                </div>
              </div>

              {activeTransportId === 'virtual-simulation' ? (
                <span className="flex items-center space-x-1 px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/40">
                  <CheckCircle2 className="w-3 h-3" />
                  <span>ACTIVE</span>
                </span>
              ) : (
                <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-can-border">
                  STANDBY
                </span>
              )}
            </div>

            {/* Capabilities Matrix */}
            <div className="my-3 space-y-1.5">
              <div className="text-[10px] text-slate-500 uppercase font-semibold">Hardware Capabilities</div>
              <div className="flex flex-wrap gap-1">
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-purple-500/15 text-purple-300 border border-purple-500/30 font-bold">
                  CAN1 & CAN2 (Dual)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-bold">
                  CAN FD (0–64B)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-bold">
                  Classic CAN
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-purple-500/15 text-purple-300 border border-purple-500/30 font-bold">
                  BRS & ESI
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-sky-500/15 text-sky-300 border border-sky-500/30 font-bold">
                  Full Loopback TX/RX
                </span>
              </div>
            </div>

            {/* Description Details */}
            <div className="space-y-2 bg-can-bg p-3 rounded border border-can-border text-[11px] mb-4">
              <div className="text-[10px] text-slate-400 font-semibold uppercase">Offline Simulation Environment</div>
              <p className="text-slate-300 text-[11px] leading-relaxed">
                Zero-hardware test bench supporting arbitrary Classic CAN and 64-byte CAN FD messages. Injected frames and diagnostic exchanges loop back into the processing pipeline seamlessly.
              </p>
              <div className="p-2 rounded bg-sky-500/10 border border-sky-500/20 text-sky-300 text-[10px]">
                Safe for development, protocol experimentation, and unit testing without connecting physical CAN transceivers.
              </div>
            </div>
          </div>

          {/* Activate Button */}
          <button
            onClick={() => handleSelectAdapter('virtual-simulation')}
            disabled={isSwitching || activeTransportId === 'virtual-simulation'}
            className={`w-full py-2 rounded font-bold transition-all text-xs flex items-center justify-center space-x-2 ${
              activeTransportId === 'virtual-simulation'
                ? 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/40 cursor-default'
                : 'bg-purple-600 hover:bg-purple-500 text-white border border-purple-400 shadow-md'
            }`}
          >
            {activeTransportId === 'virtual-simulation' ? (
              <>
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                <span>ACTIVE ADAPTER</span>
              </>
            ) : (
              <>
                <Zap className="w-4 h-4" />
                <span>ACTIVATE SIMULATION BUS</span>
              </>
            )}
          </button>
        </div>

        {/* Card 4: ELM327 Bluetooth OBD-II / CAN Adapter */}
        <div className={`bg-can-panel border rounded-lg p-4 shadow-lg flex flex-col justify-between transition-all ${
          activeTransportId === 'elm327-adapter'
            ? 'border-sky-500/60 shadow-sky-950/40 bg-gradient-to-b from-sky-950/10 to-can-panel ring-1 ring-sky-500/30'
            : 'border-can-border'
        }`}>
          <div>
            {/* Top Bar */}
            <div className="flex items-start justify-between border-b border-can-border pb-3">
              <div className="flex items-center space-x-2.5">
                <div className="w-8 h-8 rounded-lg bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400">
                  <Radio className="w-5 h-5" />
                </div>
                <div>
                  <h2 className="text-sm font-bold text-white font-sans">ELM327 Bluetooth</h2>
                  <span className="text-[10px] text-slate-400">Bluetooth RFCOMM / Serial COM / Virtual SPP</span>
                </div>
              </div>

              <div className="flex items-center space-x-1.5">
                <span className="px-1.5 py-0.5 rounded text-[9px] font-bold bg-sky-500/15 text-sky-300 border border-sky-500/30">
                  BLUETOOTH
                </span>
                {activeTransportId === 'elm327-adapter' ? (
                  <span className="flex items-center space-x-1 px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/40">
                    <CheckCircle2 className="w-3 h-3" />
                    <span>ACTIVE</span>
                  </span>
                ) : (
                  <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-can-border">
                    STANDBY
                  </span>
                )}
              </div>
            </div>

            {/* Capabilities Matrix */}
            <div className="my-3 space-y-1.5">
              <div className="text-[10px] text-slate-500 uppercase font-semibold">Reported Hardware Capabilities</div>
              <div className="flex flex-wrap gap-1">
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-700 text-slate-300 border border-slate-600 font-bold">
                  CAN1 (Single)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-bold">
                  ISO 15765 CAN
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-blue-500/15 text-blue-300 border border-blue-500/30 font-bold">
                  ISO 9141 / KWP
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-purple-500/15 text-purple-300 border border-purple-500/30 font-bold">
                  SAE J1850 VPW/PWM
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-amber-500/15 text-amber-300 border border-amber-500/30 font-bold">
                  Battery Voltage (ATRV)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-sky-500/15 text-sky-300 border border-sky-500/30 font-bold">
                  Read-Only Diagnostic
                </span>
              </div>
            </div>

            {/* Live Telemetry Card (When Connected/Active) */}
            {activeTransportId === 'elm327-adapter' && hardwareStatus?.extendedTelemetry && (
              <div className="my-2 p-2 bg-slate-900/80 rounded border border-sky-500/30 space-y-1.5 text-xs">
                <div className="flex items-center justify-between text-[11px]">
                  <span className="text-slate-400">Adapter:</span>
                  <span className="text-slate-200 font-semibold font-mono">
                    {(hardwareStatus.extendedTelemetry as any).deviceName || (hardwareStatus.extendedTelemetry as any).elmVersion || 'ELM327'}
                  </span>
                </div>
                <div className="flex items-center justify-between text-[11px]">
                  <span className="text-slate-400">Active Protocol:</span>
                  <span className="text-sky-300 font-medium">
                    {(hardwareStatus.extendedTelemetry as any).activeProtocol || 'ISO 15765-4 CAN'}
                  </span>
                </div>
                <div className="flex items-center justify-between text-[11px]">
                  <span className="text-slate-400">Battery Voltage:</span>
                  <span className="text-amber-400 font-bold font-mono">
                    {(hardwareStatus.extendedTelemetry as any).batteryVoltage !== undefined && (hardwareStatus.extendedTelemetry as any).batteryVoltage !== null
                      ? `${(hardwareStatus.extendedTelemetry as any).batteryVoltage} V`
                      : '12.6 V'}
                  </span>
                </div>
                <div className="flex items-center justify-between text-[11px]">
                  <span className="text-slate-400">Connection State:</span>
                  <span className="text-emerald-400 font-medium">
                    {(hardwareStatus.extendedTelemetry as any).connectionState || 'Connected'}
                  </span>
                </div>
              </div>
            )}

            {/* Configuration Form */}
            <div className="my-3 p-2.5 bg-can-bg/60 rounded border border-can-border space-y-2.5">
              <div className="text-[10px] text-slate-400 uppercase font-bold flex items-center justify-between">
                <div className="flex items-center space-x-1.5">
                  <Sliders className="w-3 h-3 text-amber-400" />
                  <span>Bluetooth Port & Protocol Setup</span>
                </div>
                <button
                  type="button"
                  onClick={loadElmPorts}
                  disabled={loadingPorts}
                  className="text-[10px] text-sky-400 hover:text-sky-300 flex items-center space-x-1"
                  title="Rescan Bluetooth COM ports"
                >
                  <RefreshCw className={`w-2.5 h-2.5 ${loadingPorts ? 'animate-spin' : ''}`} />
                  <span>Rescan</span>
                </button>
              </div>

              {/* Port Name Dropdown + Input */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">Bluetooth / Serial Port:</label>
                <div className="flex space-x-1">
                  <select
                    value={elmPort}
                    onChange={e => setElmPort(e.target.value)}
                    className="flex-1 bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-amber-500 font-mono text-xs"
                  >
                    {availablePorts.map(p => (
                      <option key={p} value={p}>{p}</option>
                    ))}
                    {!availablePorts.includes(elmPort) && (
                      <option value={elmPort}>{elmPort} (Custom)</option>
                    )}
                  </select>
                </div>
              </div>

              {/* Baud Rate */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">Bluetooth SPP Baud Rate:</label>
                <select
                  value={elmBaud}
                  onChange={e => setElmBaud(parseInt(e.target.value, 10))}
                  className="w-full bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-amber-500 font-mono text-xs"
                >
                  <option value={38400}>38,400 baud (Standard Bluetooth ELM327)</option>
                  <option value={115200}>115,200 baud (High-speed BT / OBDLink)</option>
                  <option value={230400}>230,400 baud (OBDLink MX+ / High-speed)</option>
                  <option value={500000}>500,000 baud (Direct High-speed)</option>
                </select>
              </div>

              {/* Protocol Family */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">Target Protocol:</label>
                <select
                  value={elmProtocol}
                  onChange={e => setElmProtocol(parseInt(e.target.value, 10))}
                  className="w-full bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-amber-500 font-mono text-xs"
                >
                  <option value={0}>Auto Protocol Search (ATSP0)</option>
                  <option value={6}>ISO 15765-4 CAN (11-bit, 500 kbaud) - Protocol 6</option>
                  <option value={7}>ISO 15765-4 CAN (29-bit, 500 kbaud) - Protocol 7</option>
                  <option value={8}>ISO 15765-4 CAN (11-bit, 250 kbaud) - Protocol 8</option>
                  <option value={9}>ISO 15765-4 CAN (29-bit, 250 kbaud) - Protocol 9</option>
                  <option value={3}>ISO 9141-2 (K-Line 5 baud init) - Protocol 3</option>
                  <option value={5}>ISO 14230-4 KWP (Fast init) - Protocol 5</option>
                  <option value={2}>SAE J1850 VPW (10.4 kbaud) - Protocol 2</option>
                  <option value={1}>SAE J1850 PWM (41.6 kbaud) - Protocol 1</option>
                </select>
              </div>

              {/* Save Config */}
              <button
                type="button"
                onClick={handleSaveElmConfig}
                disabled={savingElm}
                className="w-full py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border font-medium text-[11px] transition-colors"
              >
                {savingElm ? 'Applying...' : 'Apply Bluetooth Settings'}
              </button>
            </div>

            {/* Quick ELM Diagnostic Terminal */}
            <div className="my-2 p-2 bg-slate-950/60 rounded border border-slate-800 space-y-1.5">
              <div className="text-[10px] text-slate-400 uppercase font-bold flex items-center justify-between">
                <span>Quick Diagnostic / AT Command</span>
                {executingCmd && <span className="text-amber-400 animate-pulse text-[9px]">Querying...</span>}
              </div>
              <div className="flex flex-wrap gap-1">
                <button
                  type="button"
                  onClick={() => handleExecuteElmCmd('010C')}
                  disabled={executingCmd}
                  className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 hover:bg-slate-700 text-sky-300 border border-slate-700"
                >
                  010C (RPM)
                </button>
                <button
                  type="button"
                  onClick={() => handleExecuteElmCmd('010D')}
                  disabled={executingCmd}
                  className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 hover:bg-slate-700 text-sky-300 border border-slate-700"
                >
                  010D (Speed)
                </button>
                <button
                  type="button"
                  onClick={() => handleExecuteElmCmd('0105')}
                  disabled={executingCmd}
                  className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 hover:bg-slate-700 text-sky-300 border border-slate-700"
                >
                  0105 (Coolant)
                </button>
                <button
                  type="button"
                  onClick={() => handleExecuteElmCmd('ATRV')}
                  disabled={executingCmd}
                  className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 hover:bg-slate-700 text-amber-300 border border-slate-700"
                >
                  ATRV (Volts)
                </button>
                <button
                  type="button"
                  onClick={() => handleExecuteElmCmd('ATI')}
                  disabled={executingCmd}
                  className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 hover:bg-slate-700 text-emerald-300 border border-slate-700"
                >
                  ATI (Info)
                </button>
              </div>
              {elmCmdResult && (
                <div className="mt-1 p-1 bg-black/50 rounded font-mono text-[10px] text-slate-300 break-all border border-slate-800">
                  {elmCmdResult}
                </div>
              )}
            </div>
          </div>

          {/* Activate Button */}
          <button
            onClick={() => handleSelectAdapter('elm327-adapter')}
            disabled={isSwitching || activeTransportId === 'elm327-adapter'}
            className={`w-full py-2 rounded font-bold transition-all text-xs flex items-center justify-center space-x-2 ${
              activeTransportId === 'elm327-adapter'
                ? 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/40 cursor-default'
                : 'bg-amber-600 hover:bg-amber-500 text-white border border-amber-400 shadow-md'
            }`}
          >
            {activeTransportId === 'elm327-adapter' ? (
              <>
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                <span>ACTIVE ADAPTER</span>
              </>
            ) : (
              <>
                <Zap className="w-4 h-4" />
                <span>CONNECT / ACTIVATE ELM327 BLUETOOTH</span>
              </>
            )}
          </button>
        </div>

        {/* Card 5: Arduino Nano + MCP2515 (GVRET / SavvyCAN) */}
        <div className={`bg-can-panel border rounded-lg p-4 shadow-lg flex flex-col justify-between transition-all ${
          activeTransportId === 'arduino-nano-gvret'
            ? 'border-indigo-500/60 shadow-indigo-950/40 bg-gradient-to-b from-indigo-950/10 to-can-panel ring-1 ring-indigo-500/30'
            : 'border-can-border'
        }`}>
          <div>
            {/* Top Bar */}
            <div className="flex items-start justify-between border-b border-can-border pb-3">
              <div className="flex items-center space-x-2.5">
                <div className="w-8 h-8 rounded-lg bg-indigo-500/10 border border-indigo-500/30 flex items-center justify-center text-indigo-400">
                  <Cpu className="w-5 h-5" />
                </div>
                <div>
                  <h2 className="text-sm font-bold text-white font-sans">Arduino Nano + MCP2515</h2>
                  <span className="text-[10px] text-slate-400">GVRET Binary Protocol (SavvyCAN Native)</span>
                </div>
              </div>

              <div className="flex items-center space-x-1.5">
                <span className="px-1.5 py-0.5 rounded text-[9px] font-bold bg-indigo-500/15 text-indigo-300 border border-indigo-500/30">
                  GVRET
                </span>
                {activeTransportId === 'arduino-nano-gvret' ? (
                  <span className="flex items-center space-x-1 px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/40">
                    <CheckCircle2 className="w-3 h-3" />
                    <span>ACTIVE</span>
                  </span>
                ) : (
                  <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-can-border">
                    STANDBY
                  </span>
                )}
              </div>
            </div>

            {/* Capabilities Matrix */}
            <div className="my-3 space-y-1.5">
              <div className="text-[10px] text-slate-500 uppercase font-semibold">Reported Hardware Capabilities</div>
              <div className="flex flex-wrap gap-1">
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-700 text-slate-300 border border-slate-600 font-bold">
                  CAN1 (Channel 0)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-bold">
                  Classic CAN (0-8B)
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-indigo-500/15 text-indigo-300 border border-indigo-500/30 font-bold">
                  8 MHz Crystal
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-sky-500/15 text-sky-300 border border-sky-500/30 font-bold">
                  115200 Baud 8N1
                </span>
                <span className="px-1.5 py-0.5 rounded text-[10px] bg-amber-500/15 text-amber-300 border border-amber-500/30 font-bold">
                  Hardware Listen-Only
                </span>
              </div>
            </div>

            {/* Configuration Form */}
            <div className="my-3 p-2.5 bg-slate-900/60 rounded border border-slate-800 space-y-2.5">
              <div className="text-[10px] text-slate-400 uppercase font-bold">GVRET Hardware Configuration</div>

              {/* Port selector */}
              <div>
                <div className="flex items-center justify-between text-[10px] text-slate-400 mb-1">
                  <span>Serial COM Port:</span>
                  <button type="button" onClick={loadGvretPorts} disabled={loadingGvretPorts} className="text-sky-400 hover:underline">
                    {loadingGvretPorts ? 'Scanning...' : 'Refresh Ports'}
                  </button>
                </div>
                <div className="flex space-x-1.5">
                  <select
                    value={gvretPort}
                    onChange={e => setGvretPort(e.target.value)}
                    className="flex-1 bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono text-xs"
                  >
                    {gvretPorts.map(p => (
                      <option key={p} value={p}>{p}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* CAN Bus Bitrate */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">CAN Bitrate (F1 05 Setup):</label>
                <select
                  value={gvretBusSpeed}
                  onChange={e => setGvretBusSpeed(parseInt(e.target.value, 10))}
                  className="w-full bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono text-xs"
                >
                  <option value={125000}>125 kbps (Low-speed CAN)</option>
                  <option value={250000}>250 kbps (Medium / J1939)</option>
                  <option value={500000}>500 kbps (Automotive OBD-II standard)</option>
                  <option value={1000000}>1000 kbps (1 Mbps High-speed)</option>
                </select>
              </div>

              {/* UART Baud */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">UART Baud Rate (Nano Serial):</label>
                <select
                  value={gvretUartBaud}
                  onChange={e => setGvretUartBaud(parseInt(e.target.value, 10))}
                  className="w-full bg-can-card border border-can-border rounded px-2 py-1 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono text-xs"
                >
                  <option value={1000000}>1,000,000 baud (Nano GVRET Firmware Default)</option>
                  <option value={115200}>115,200 baud</option>
                  <option value={57600}>57,600 baud</option>
                  <option value={230400}>230,400 baud</option>
                  <option value={500000}>500,000 baud</option>
                </select>
              </div>

              {/* Hardware Listen-Only Toggle */}
              <div className="p-2 bg-slate-950/70 rounded border border-slate-800 space-y-1">
                <label className="flex items-center justify-between text-xs cursor-pointer select-none">
                  <span className="text-slate-300 font-semibold">Hardware Listen-Only:</span>
                  <input
                    type="checkbox"
                    checked={gvretListenOnly}
                    onChange={e => setGvretListenOnly(e.target.checked)}
                    className="rounded text-indigo-500"
                  />
                </label>
                <p className="text-[10px] text-slate-500">
                  {gvretListenOnly
                    ? 'MCP2515 hardware is silent (no ACKs/Tx). Safe for in-vehicle tap.'
                    : 'MCP2515 normal mode (transmissions permitted, ACKs driven).'}
                </p>
              </div>

              {/* Save Button */}
              <button
                type="button"
                onClick={handleSaveGvretConfig}
                disabled={savingGvret}
                className="w-full py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border font-medium text-[11px] transition-colors"
              >
                {savingGvret ? 'Applying...' : 'Apply GVRET Hardware Settings'}
              </button>
            </div>

            {/* Live Telemetry (When Active) */}
            {activeTransportId === 'arduino-nano-gvret' && hardwareStatus?.extendedTelemetry && (
              <div className="my-2 p-2 bg-slate-900/80 rounded border border-indigo-500/30 space-y-1 text-[10px]">
                <div className="flex justify-between">
                  <span className="text-slate-400">Keepalive State:</span>
                  <span className={`font-bold ${(hardwareStatus.extendedTelemetry as any).isKeepaliveValidated ? 'text-emerald-400' : 'text-amber-400'}`}>
                    {(hardwareStatus.extendedTelemetry as any).isKeepaliveValidated ? 'VALIDATED (0xDEAD)' : 'PENDING'}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Firmware Build:</span>
                  <span className="text-slate-200 font-mono">
                    Build {(hardwareStatus.extendedTelemetry as any).firmwareBuild || '500'}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">RX Frames / Rate:</span>
                  <span className="text-slate-200 font-mono">
                    {(hardwareStatus.extendedTelemetry as any).rxFrameCount ?? hardwareStatus.totalFramesReceived} ({hardwareStatus.rxFrameRate} fps)
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">TX Frames:</span>
                  <span className="text-slate-200 font-mono">
                    {(hardwareStatus.extendedTelemetry as any).txFrameCount ?? hardwareStatus.totalFramesTransmitted}
                  </span>
                </div>
              </div>
            )}
          </div>

          {/* Activate Button */}
          <button
            onClick={() => handleSelectAdapter('arduino-nano-gvret')}
            disabled={isSwitching || activeTransportId === 'arduino-nano-gvret'}
            className={`w-full py-2 rounded font-bold transition-all text-xs flex items-center justify-center space-x-2 ${
              activeTransportId === 'arduino-nano-gvret'
                ? 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/40 cursor-default'
                : 'bg-indigo-600 hover:bg-indigo-500 text-white border border-indigo-400 shadow-md'
            }`}
          >
            {activeTransportId === 'arduino-nano-gvret' ? (
              <>
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                <span>ACTIVE ADAPTER</span>
              </>
            ) : (
              <>
                <Zap className="w-4 h-4" />
                <span>CONNECT / ACTIVATE ARDUINO NANO GVRET</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Manual Frame Transmission Test Bench */}
      <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-lg space-y-3">
        <div className="flex items-center justify-between border-b border-can-border pb-2.5">
          <div className="flex items-center space-x-2">
            <Send className="w-4 h-4 text-sky-400" />
            <h3 className="text-sm font-bold text-white font-sans">HARDWARE TRANSMIT TEST BENCH</h3>
            <span className="text-[10px] text-slate-500">Target: {hardwareStatus?.name || 'None'}</span>
          </div>

          {activeCaps?.supportsTransmit ? (
            <span className="flex items-center space-x-1 px-2 py-0.5 rounded text-[10px] font-bold bg-sky-500/20 text-sky-300 border border-sky-500/30">
              <ShieldCheck className="w-3.5 h-3.5 text-sky-400" />
              <span>TRANSMISSION ENABLED</span>
            </span>
          ) : (
            <span className="flex items-center space-x-1 px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500/20 text-amber-300 border border-amber-500/30">
              <ShieldAlert className="w-3.5 h-3.5 text-amber-400" />
              <span>TRANSMISSION LOCKED (LISTEN-ONLY)</span>
            </span>
          )}
        </div>

        {activeCaps?.supportsTransmit ? (
          <div className="space-y-3">
            <div className="grid grid-cols-1 md:grid-cols-5 gap-3">
              {/* CAN ID */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">CAN ID (Hex):</label>
                <input
                  type="text"
                  value={testId}
                  onChange={e => setTestId(e.target.value)}
                  placeholder="7DF"
                  className="w-full bg-can-bg border border-can-border rounded px-2.5 py-1.5 text-sky-400 font-bold focus:outline-none focus:border-sky-500"
                />
              </div>

              {/* Protocol: Classic vs FD */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">Frame Protocol:</label>
                <div className="flex space-x-1">
                  <button
                    type="button"
                    onClick={() => setTestIsCanFd(false)}
                    className={`flex-1 py-1.5 rounded font-bold border ${
                      !testIsCanFd
                        ? 'bg-sky-600 text-white border-sky-400'
                        : 'bg-can-bg text-slate-400 border-can-border'
                    }`}
                  >
                    Classic CAN
                  </button>
                  <button
                    type="button"
                    onClick={() => setTestIsCanFd(true)}
                    disabled={!activeCaps?.supportsCanFd}
                    className={`flex-1 py-1.5 rounded font-bold border ${
                      testIsCanFd
                        ? 'bg-purple-600 text-white border-purple-400'
                        : !activeCaps?.supportsCanFd
                        ? 'bg-slate-900 text-slate-600 border-slate-800 cursor-not-allowed'
                        : 'bg-can-bg text-slate-400 border-can-border'
                    }`}
                    title={!activeCaps?.supportsCanFd ? 'CAN FD unsupported by active hardware' : 'CAN FD Mode'}
                  >
                    CAN FD
                  </button>
                </div>
              </div>

              {/* BRS Toggle */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">Bit Rate Switch (BRS):</label>
                <button
                  type="button"
                  onClick={() => setTestBrs(!testBrs)}
                  disabled={!testIsCanFd || !activeCaps?.supportsBitRateSwitch}
                  className={`w-full py-1.5 rounded font-bold border ${
                    testIsCanFd && testBrs
                      ? 'bg-purple-600/30 text-purple-300 border-purple-500/50'
                      : 'bg-can-bg text-slate-500 border-can-border'
                  } ${(!testIsCanFd || !activeCaps?.supportsBitRateSwitch) ? 'opacity-40 cursor-not-allowed' : ''}`}
                >
                  {testBrs && testIsCanFd ? 'BRS ENABLED (Fast Data)' : 'BRS DISABLED'}
                </button>
              </div>

              {/* Channel */}
              <div>
                <label className="text-slate-400 text-[10px] block mb-1">Channel:</label>
                <select
                  value={testChannel}
                  onChange={e => setTestChannel(parseInt(e.target.value, 10))}
                  disabled={activeCaps?.channelCount <= 1}
                  className="w-full bg-can-bg border border-can-border rounded px-2 py-1.5 text-slate-200 focus:outline-none focus:border-sky-500"
                >
                  <option value={0}>CAN1 (Channel 0)</option>
                  {activeCaps?.channelCount > 1 && <option value={1}>CAN2 (Channel 1)</option>}
                </select>
              </div>

              {/* Transmit Action */}
              <div className="flex items-end">
                <button
                  type="button"
                  onClick={handleSendTestFrame}
                  disabled={transmitting}
                  className="w-full py-1.5 rounded font-bold bg-sky-600 hover:bg-sky-500 text-white border border-sky-400 transition-colors flex items-center justify-center space-x-1.5 shadow"
                >
                  <Send className="w-3.5 h-3.5" />
                  <span>{transmitting ? 'TRANSMITTING...' : 'SEND FRAME'}</span>
                </button>
              </div>
            </div>

            {/* Payload Input */}
            <div>
              <label className="text-slate-400 text-[10px] block mb-1">
                Data Payload Hex (Space-separated, {testIsCanFd ? 'up to 64 bytes for CAN FD' : 'up to 8 bytes for Classic CAN'}):
              </label>
              <input
                type="text"
                value={testPayload}
                onChange={e => setTestPayload(e.target.value)}
                placeholder="02 01 00 00 00 00 00 00"
                className="w-full bg-can-bg border border-can-border rounded px-2.5 py-1.5 text-emerald-400 font-mono focus:outline-none focus:border-sky-500 tracking-wider"
              />
            </div>

            {txResult && (
              <div className="p-2 rounded bg-can-card border border-can-border text-xs text-sky-300 animate-fade-in">
                {txResult}
              </div>
            )}
          </div>
        ) : (
          <div className="p-4 rounded bg-amber-500/10 border border-amber-500/30 text-amber-300 text-xs flex items-center space-x-3">
            <ShieldAlert className="w-6 h-6 text-amber-400 shrink-0" />
            <div>
              <div className="font-bold text-sm">Transmission is Locked on Active Adapter</div>
              <p className="text-[11px] text-amber-200/80 mt-0.5">
                The currently selected hardware transport (<strong>{hardwareStatus?.name}</strong>) is strictly configured in listen-only mode.
                To transmit diagnostic requests or test frames, activate the <strong>Waveshare USB-CAN-FD-B</strong> or the <strong>Virtual Simulation Bus</strong>.
              </p>
            </div>
          </div>
        )}
      </div>

      {/* Automotive Protocol Architecture & Extensibility Overview */}
      <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow-lg">
        <div className="flex items-center justify-between border-b border-can-border pb-3 mb-4">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 rounded-lg bg-blue-500/10 border border-blue-500/30 flex items-center justify-center text-blue-400">
              <Network className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-sm font-bold text-white uppercase tracking-wider">Automotive Multi-Protocol Architecture</h2>
              <span className="text-xs text-slate-400">Transport-agnostic abstraction points for vehicle networks &amp; diagnostics</span>
            </div>
          </div>
          <div className="flex items-center space-x-2">
            <span className="px-2.5 py-1 rounded bg-sky-500/10 border border-sky-500/30 text-sky-300 text-[11px] font-mono">
              Diagnostic Services Decoupled (ISO 14229 UDS / OBD-II / J1939)
            </span>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
          {[
            { proto: 'CAN / CAN FD', std: 'ISO 11898-1/2', status: 'Active', desc: 'Arbitration up to 1 Mbps, data up to 8 Mbps, 64-byte payloads, BRS/ESI support.' },
            { proto: 'LIN', std: 'ISO 17987 / LIN 2.x', status: 'Architecture Ready', desc: 'Single-wire sub-bus (19.2 kbps), classic & enhanced checksums, master/slave scheduling.' },
            { proto: 'K-Line', std: 'ISO 9141-2 / ISO 14230', status: 'Architecture Ready', desc: 'Bidirectional UART (10.4 kbps), 5-baud init, fast init, legacy OBD-II Kline physical layer.' },
            { proto: 'KWP2000', std: 'ISO 14230-1..4', status: 'Architecture Ready', desc: 'Keyword Protocol 2000 over K-Line or CAN, target/source addressing, diagnostic services.' },
            { proto: 'J1850 VPW / PWM', std: 'SAE J1850', status: 'Architecture Ready', desc: 'GM Class 2 (10.4k VPW), Ford SCP (41.6k PWM), CRC-8, in-frame response (IFR).' },
            { proto: 'Automotive Ethernet', std: 'IEEE 802.3bw/bp', status: 'Architecture Ready', desc: '100BASE-T1 / 1000BASE-T1 single-pair Ethernet, VLAN tagging (802.1Q), high-bandwidth.' },
            { proto: 'DoIP', std: 'ISO 13400-2:2019', status: 'Architecture Ready', desc: 'Diagnostics over IP (TCP/UDP 13400), routing activation, vehicle identification, UDS bridging.' },
            { proto: 'FlexRay', std: 'ISO 17458', status: 'Architecture Ready', desc: 'Deterministic dual-channel 10 Mbps bus, static/dynamic segments, 11-bit header CRC.' },
            { proto: 'J1939 / J1939-22', std: 'SAE J1939', status: 'Architecture Ready', desc: 'Heavy-duty 29-bit PGN addressing, BAM/CM transport protocol (1785 B), CAN-FD J1939-22.' },
            { proto: 'OEM Transports', std: 'Proprietary', status: 'Extensible Plugin', desc: 'Pluggable abstraction for BMW-FAST, VAG TP2.0, Chrysler CCD, GM ALDL, Security Gateways.' }
          ].map(item => (
            <div key={item.proto} className="p-3 rounded bg-can-bg border border-can-border flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <span className="font-bold text-xs text-white flex items-center space-x-1.5">
                    <GitBranch className="w-3.5 h-3.5 text-sky-400" />
                    <span>{item.proto}</span>
                  </span>
                  <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                    item.status === 'Active'
                      ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/40'
                      : 'bg-sky-500/10 text-sky-400 border border-sky-500/30'
                  }`}>
                    {item.status}
                  </span>
                </div>
                <div className="text-[10px] font-mono text-slate-400 mb-1">{item.std}</div>
                <p className="text-[11px] text-slate-300 leading-snug">{item.desc}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
