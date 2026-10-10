import React, { useState, useEffect } from 'react';
import { useCan } from '../context/CanContext';
import { api, downloadBlobAsFile } from '../services/api';
import { 
  DiagnosticExchange, 
  DiagnosticTroubleCode, 
  DtcClearVerificationResult,
  EcuIdentification, 
  ObdPidValue, 
  ObdPidDefinition, 
  VirtualEcu,
  OperatingGridConfig,
  LiveOperatingGridSnapshot
} from '../types/diagnostics';
import { 
  DiagnosticFreezeFrame,
  VagMeasuringGroup,
  ManufacturerLiveDataDefinition,
  DecodedManufacturerValue,
  DtcMetadata,
  DtcCorrelationResult,
  ManufacturerOem
} from '../types/diagnosticsExtended';
import { 
  Wrench, Cpu, Activity, Send, Trash2, RotateCcw, ShieldAlert, CheckCircle2, 
  AlertTriangle, Layers, Gauge, FileText, Radio, Search, Key, RefreshCw,
  Zap, Lock, ArrowRight, Sliders, Camera, HelpCircle, ExternalLink, Compass, Flame, Sparkles, X,
  Download, Table
} from 'lucide-react';

export const DiagnosticsWorkspacePage: React.FC = () => {
  const { operatingMode } = useCan();
  const [subTab, setSubTab] = useState<
    'trace' | 'obd' | 'uds' | 'dtc' | 'freeze-frame' | 'vag-blocks' | 'manufacturer' | 'tuning-grid' | 'ecuinfo' | 'simulator'
  >('trace');
  
  // Core Diagnostics Data state
  const [exchanges, setExchanges] = useState<DiagnosticExchange[]>([]);
  const [dtcs, setDtcs] = useState<DiagnosticTroubleCode[]>([]);
  const [ecuList, setEcuList] = useState<EcuIdentification[]>([]);
  const [obdValues, setObdValues] = useState<ObdPidValue[]>([]);
  const [obdDefs, setObdDefs] = useState<ObdPidDefinition[]>([]);
  const [simulatorStatus, setSimulatorStatus] = useState<{ isEnabled: boolean; virtualEcus: VirtualEcu[] } | null>(null);

  // Automatic Mode 01 PID Discovery state
  const [isDiscoveringPids, setIsDiscoveringPids] = useState<boolean>(false);
  const [discoveredPids, setDiscoveredPids] = useState<ObdPidDefinition[]>([]);
  const [pidDiscoveryMessage, setPidDiscoveryMessage] = useState<string | null>(null);

  // Live Operating Envelope / Tuning Map state
  const [operatingGridId, setOperatingGridId] = useState<string>('rpm_vs_load_lambda');
  const [operatingGridConfigs, setOperatingGridConfigs] = useState<OperatingGridConfig[]>([]);
  const [gridSnapshot, setGridSnapshot] = useState<LiveOperatingGridSnapshot | null>(null);
  const [isGridPolling, setIsGridPolling] = useState<boolean>(true);
  const [gridActionMessage, setGridActionMessage] = useState<string | null>(null);

  // Extended Diagnostics Data state
  const [freezeFrames, setFreezeFrames] = useState<DiagnosticFreezeFrame[]>([]);
  const [selectedSnapshot, setSelectedSnapshot] = useState<DiagnosticFreezeFrame | null>(null);

  const [vagGroups, setVagGroups] = useState<VagMeasuringGroup[]>([]);
  const [selectedGroupNum, setSelectedGroupNum] = useState<number>(115);
  const [activeVagGroup, setActiveVagGroup] = useState<VagMeasuringGroup | null>(null);
  const [isVagPolling, setIsVagPolling] = useState<boolean>(false);

  const [selectedOem, setSelectedOem] = useState<ManufacturerOem>('MarutiMarelli');
  const [manufacturerDefs, setManufacturerDefs] = useState<ManufacturerLiveDataDefinition[]>([]);
  const [manufacturerSnapshot, setManufacturerSnapshot] = useState<DecodedManufacturerValue[]>([]);

  // DTC Deep-dive & Correlation state
  const [selectedDtcMeta, setSelectedDtcMeta] = useState<DtcMetadata | null>(null);
  const [selectedDtcCorrelation, setSelectedDtcCorrelation] = useState<DtcCorrelationResult | null>(null);
  const [isDtcModalOpen, setIsDtcModalOpen] = useState<boolean>(false);
  const [isClearModalOpen, setIsClearModalOpen] = useState<boolean>(false);
  const [clearVerificationResult, setClearVerificationResult] = useState<DtcClearVerificationResult | null>(null);

  // Request form state
  const [targetEcu, setTargetEcu] = useState<'ECM' | 'ABS' | 'BCM' | 'Broadcast'>('ECM');
  const [customHex, setCustomHex] = useState('22 F1 90');
  const [selectedDid, setSelectedDid] = useState('F190');
  const [selectedSession, setSelectedSession] = useState('01');
  const [selectedReset, setSelectedReset] = useState('01');
  const [isSending, setIsSending] = useState(false);
  const [isScanningDtcs, setIsScanningDtcs] = useState(false);
  const [dtcActionMessage, setDtcActionMessage] = useState<string | null>(null);
  const [isExportingDiagPdf, setIsExportingDiagPdf] = useState(false);
  const [isExportingHealthPdf, setIsExportingHealthPdf] = useState(false);

  const handleExportDiagnosticPdf = async () => {
    setIsExportingDiagPdf(true);
    try {
      const blob = await api.downloadDiagnosticReportPdf();
      downloadBlobAsFile(blob, `Diagnostic_Report_${new Date().toISOString().slice(0, 10)}.pdf`);
    } catch (e) {
      console.error('Failed to export diagnostic PDF:', e);
    } finally {
      setIsExportingDiagPdf(false);
    }
  };

  const handleExportHealthPdf = async () => {
    setIsExportingHealthPdf(true);
    try {
      const blob = await api.downloadVehicleHealthReportPdf();
      downloadBlobAsFile(blob, `Vehicle_Health_Audit_${new Date().toISOString().slice(0, 10)}.pdf`);
    } catch (e) {
      console.error('Failed to export vehicle health PDF:', e);
    } finally {
      setIsExportingHealthPdf(false);
    }
  };

  const isSimulation = operatingMode === 'Simulation' || operatingMode === 'Offline Analysis';

  const ecuCanIdMap: Record<string, number> = {
    'ECM': 0x7E0,
    'ABS': 0x7E1,
    'BCM': 0x7E2,
    'Broadcast': 0x7DF
  };

  const fetchAllDiagnosticsData = async () => {
    try {
      const [exc, dList, ecus, obd, sim, ffList, vGroups, mDefs] = await Promise.all([
        api.getRecentExchanges(150),
        api.getActiveDtcs(),
        api.getEcuIdentifications(),
        api.getObdLiveValues(),
        api.getSimulatorStatus(),
        api.getFreezeFrames(),
        api.getVagGroups(),
        api.getManufacturerDefinitions(selectedOem)
      ]);
      setExchanges(exc);
      setDtcs(dList);
      setEcuList(ecus);
      setObdValues(obd.values || []);
      setObdDefs(obd.supportedDefinitions || []);
      setSimulatorStatus(sim);
      setFreezeFrames(ffList);
      setVagGroups(vGroups);
      setManufacturerDefs(mDefs);
      if (ffList.length > 0 && !selectedSnapshot) setSelectedSnapshot(ffList[0]);
    } catch {
      // Ignore
    }
  };

  useEffect(() => {
    fetchAllDiagnosticsData();
    const interval = setInterval(fetchAllDiagnosticsData, 2000);
    return () => clearInterval(interval);
  }, [selectedOem]);

  // VAG Measuring Block Poller
  useEffect(() => {
    const fetchVag = async () => {
      try {
        const g = await api.readVagGroup(selectedGroupNum, 2800, 1.15, 78.0);
        setActiveVagGroup(g);
      } catch {
        // Ignore
      }
    };
    fetchVag();
    const interval = setInterval(fetchVag, 1500);
    return () => clearInterval(interval);
  }, [selectedGroupNum]);

  // Manufacturer Live Data Poller
  useEffect(() => {
    const fetchOemSnapshot = async () => {
      try {
        const snap = await api.getManufacturerSnapshot(selectedOem, 2800, 1.15, 78.0);
        setManufacturerSnapshot(snap);
      } catch {
        // Ignore
      }
    };
    fetchOemSnapshot();
    const interval = setInterval(fetchOemSnapshot, 1500);
    return () => clearInterval(interval);
  }, [selectedOem]);

  // Operating Grid Configs loader
  useEffect(() => {
    const fetchConfigs = async () => {
      try {
        const cfgs = await api.getOperatingGridConfigs();
        setOperatingGridConfigs(cfgs || []);
      } catch {
        // Ignore
      }
    };
    fetchConfigs();
  }, []);

  // Operating Grid Live Snapshot Poller
  useEffect(() => {
    if (subTab !== 'tuning-grid' || !isGridPolling) return;
    const fetchGrid = async () => {
      try {
        const snap = await api.getOperatingGrid(operatingGridId);
        setGridSnapshot(snap);
      } catch {
        // Ignore
      }
    };
    fetchGrid();
    const interval = setInterval(fetchGrid, 1000);
    return () => clearInterval(interval);
  }, [subTab, operatingGridId, isGridPolling]);

  const handleDiscoverSupportedPids = async () => {
    setIsDiscoveringPids(true);
    setPidDiscoveryMessage('Executing sequential Mode 01 bitmap discovery (0x00, 0x20, 0x40, 0x60, 0x80)...');
    try {
      const reqId = ecuCanIdMap[targetEcu] || 0x7DF;
      const pids = await api.discoverSupportedPids(reqId, isSimulation);
      setDiscoveredPids(pids || []);
      setObdDefs(pids || []);
      setPidDiscoveryMessage(`Discovery complete: Found ${pids?.length || 0} standardized Mode 01 PIDs supported by ${targetEcu}.`);
    } catch (err: any) {
      setPidDiscoveryMessage(`PID discovery failed: ${err.message || 'Communication error'}`);
    } finally {
      setIsDiscoveringPids(false);
      setTimeout(() => setPidDiscoveryMessage(null), 6000);
    }
  };

  const handleResetGrid = async () => {
    try {
      await api.resetOperatingGrid(operatingGridId);
      const snap = await api.getOperatingGrid(operatingGridId);
      setGridSnapshot(snap);
      setGridActionMessage(`Operating grid '${operatingGridId}' telemetry reset.`);
    } catch (e: any) {
      setGridActionMessage(`Reset failed: ${e.message}`);
    } finally {
      setTimeout(() => setGridActionMessage(null), 4000);
    }
  };

  const handleSendCustomHex = async (hexStr: string) => {
    setIsSending(true);
    try {
      const reqId = ecuCanIdMap[targetEcu] || 0x7E0;
      await api.sendDiagnosticRequest({
        requestCanId: reqId,
        ecuName: targetEcu,
        hexPayload: hexStr,
        isSimulation: isSimulation
      });
      await fetchAllDiagnosticsData();
    } catch {
      // Ignore
    } finally {
      setIsSending(false);
    }
  };

  const handleScanAllDtcs = async () => {
    setIsScanningDtcs(true);
    setDtcActionMessage('Scanning all ECUs across UDS 0x19, OBD Mode 03/07, and KWP2000...');
    try {
      const res = await api.scanAllDtcs(isSimulation);
      setDtcs(res);
      setDtcActionMessage(`Multi-protocol scan complete: ${res.length} active DTC(s) detected.`);
      await fetchAllDiagnosticsData();
    } catch {
      setDtcActionMessage('DTC scan failed or transport timeout.');
    } finally {
      setIsScanningDtcs(false);
      setTimeout(() => setDtcActionMessage(null), 5000);
    }
  };

  const handleClearDtcs = async () => {
    setIsClearModalOpen(false);
    setIsSending(true);
    setClearVerificationResult(null);
    setDtcActionMessage(`Executing 5-step verified DTC clear sequence on ${targetEcu} (UDS 0x14 FF FF FF & OBD-II Mode 04)...`);
    try {
      const reqId = ecuCanIdMap[targetEcu] || 0x7E0;
      const result = await api.clearDtcs(reqId, targetEcu, isSimulation);
      setClearVerificationResult(result);
      const refreshed = await api.getActiveDtcs();
      setDtcs(refreshed);
      setDtcActionMessage(result.message);
      await fetchAllDiagnosticsData();
    } catch (err: any) {
      setDtcActionMessage(`Clear DTC verification failed: ${err.message || 'Communication error'}`);
    } finally {
      setIsSending(false);
      setTimeout(() => setDtcActionMessage(null), 8000);
    }
  };

  const handleInspectDtc = async (code: string) => {
    try {
      const [meta, corr] = await Promise.all([
        api.getDtcMetadata(code),
        api.correlateDtc(code)
      ]);
      setSelectedDtcMeta(meta);
      setSelectedDtcCorrelation(corr);
      setIsDtcModalOpen(true);
    } catch {
      // Ignore
    }
  };

  const handleResetSimulator = async () => {
    await api.resetSimulator();
    await fetchAllDiagnosticsData();
  };

  const handleToggleFault = async (ecuName: string, dtcCode: string, active: boolean) => {
    await api.setSimulatorDtcFault(ecuName, dtcCode, active);
    await fetchAllDiagnosticsData();
  };

  return (
    <div className="flex flex-col h-full font-mono select-none overflow-hidden p-4 space-y-4">
      {/* Sub-Tab Navigation Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-2.5 flex flex-wrap items-center justify-between shadow-md gap-2">
        <div className="flex flex-wrap items-center gap-1">
          {[
            { id: 'trace', label: 'Live Trace', icon: Activity },
            { id: 'obd', label: 'OBD-II Dashboard', icon: Gauge },
            { id: 'uds', label: 'UDS Center', icon: Zap },
            { id: 'dtc', label: `DTCs (${dtcs.length})`, icon: AlertTriangle },
            { id: 'freeze-frame', label: `Freeze Frames (${freezeFrames.length})`, icon: Camera },
            { id: 'vag-blocks', label: 'VAG Blocks', icon: Layers },
            { id: 'manufacturer', label: 'Manufacturer Live', icon: Radio },
            { id: 'tuning-grid', label: 'Tuning Operating Grid', icon: Flame },
            { id: 'ecuinfo', label: 'ECU Inventory', icon: Cpu },
            { id: 'simulator', label: 'ECU Simulator', icon: Sliders }
          ].map(tab => {
            const Icon = tab.icon;
            const isActive = subTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setSubTab(tab.id as any)}
                className={`px-3 py-1.5 rounded text-xs font-sans font-semibold flex items-center space-x-1.5 transition-all ${
                  isActive
                    ? 'bg-sky-600 text-white shadow'
                    : 'text-slate-400 hover:text-white hover:bg-can-card'
                }`}
              >
                <Icon className="w-3.5 h-3.5" />
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* PDF Export Actions & Target ECU Selector */}
        <div className="flex items-center space-x-2 text-xs font-sans">
          <button
            onClick={handleExportDiagnosticPdf}
            disabled={isExportingDiagPdf}
            className="px-2.5 py-1 bg-sky-600/20 hover:bg-sky-600/30 text-sky-300 border border-sky-500/40 rounded flex items-center space-x-1.5 transition-all shadow-sm font-semibold"
            title="Download Professional Diagnostic Report PDF"
          >
            <FileText className="w-3.5 h-3.5 text-sky-400" />
            <span>{isExportingDiagPdf ? 'Generating...' : 'Diagnostic PDF'}</span>
          </button>
          <button
            onClick={handleExportHealthPdf}
            disabled={isExportingHealthPdf}
            className="px-2.5 py-1 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/40 rounded flex items-center space-x-1.5 transition-all shadow-sm font-semibold"
            title="Download Complete Vehicle Health Audit PDF"
          >
            <ShieldAlert className="w-3.5 h-3.5 text-emerald-400" />
            <span>{isExportingHealthPdf ? 'Generating...' : 'Health Audit PDF'}</span>
          </button>

          <span className="text-slate-400 pl-2 border-l border-can-border">Target ECU:</span>
          <select
            value={targetEcu}
            onChange={e => setTargetEcu(e.target.value as any)}
            className="bg-can-bg border border-can-border rounded px-2.5 py-1 text-xs text-white font-bold"
          >
            <option value="ECM">ECM (0x7E0 / 0x7E8)</option>
            <option value="ABS">ABS (0x7E1 / 0x7E9)</option>
            <option value="BCM">BCM (0x7E2 / 0x7EA)</option>
            <option value="Broadcast">Broadcast (0x7DF)</option>
          </select>
        </div>
      </div>

      {/* Main Sub-Tab View Container */}
      <div className="flex-1 bg-can-panel border border-can-border rounded-lg overflow-hidden shadow-xl flex flex-col">
        {/* VIEW 1: LIVE DIAGNOSTIC TRACE */}
        {subTab === 'trace' && (
          <div className="flex flex-col h-full overflow-hidden">
            <div className="p-3 border-b border-can-border flex justify-between items-center bg-can-panel text-xs font-sans">
              <span className="font-bold text-white uppercase tracking-wider">
                DIAGNOSTIC REQUEST / RESPONSE TRACE ({exchanges.length})
              </span>
              <button
                onClick={() => api.clearDiagnosticHistory().then(fetchAllDiagnosticsData)}
                className="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded text-[11px] border border-can-border flex items-center space-x-1"
              >
                <Trash2 className="w-3 h-3" />
                <span>Clear Trace</span>
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-2 space-y-1 text-xs">
              {exchanges.map(ex => (
                <div
                  key={ex.id}
                  className={`p-2.5 rounded border flex items-center justify-between font-mono ${
                    ex.isSuccess ? 'bg-can-bg border-can-border' : 'bg-rose-950/20 border-rose-800/40'
                  }`}
                >
                  <div className="flex items-center space-x-3">
                    <span className="text-slate-500 text-[10px]">{new Date(ex.timestampUtc).toLocaleTimeString()}</span>
                    <span className="px-1.5 py-0.5 bg-slate-800 text-sky-400 font-bold text-[10px] rounded">
                      {ex.ecuName}
                    </span>
                    <span className="text-slate-300 font-bold">{ex.serviceName}</span>
                    <span className="text-slate-400 text-[11px]">REQ: {ex.requestDataHex}</span>
                    {ex.responseDataHex && <span className="text-emerald-400 text-[11px]">RESP: {ex.responseDataHex}</span>}
                    {ex.nrcDescription && <span className="text-rose-400 text-[11px]">NRC: {ex.nrcDescription}</span>}
                  </div>
                  <span className="text-[10px] text-slate-500">{ex.responseTimeMs.toFixed(1)} ms</span>
                </div>
              ))}
              {exchanges.length === 0 && (
                <div className="text-center py-16 text-slate-500 font-sans text-xs">
                  No diagnostic requests logged yet. Use UDS or OBD tabs to transmit requests.
                </div>
              )}
            </div>
          </div>
        )}

        {/* VIEW 2: OBD-II DASHBOARD & PID DISCOVERY */}
        {subTab === 'obd' && (
          <div className="p-5 space-y-4 overflow-y-auto h-full">
            <div className="flex flex-wrap justify-between items-center border-b border-can-border pb-3 font-sans gap-3">
              <div>
                <h2 className="text-sm font-bold text-white uppercase">STANDARD OBD-II (SAE J1979 / ISO 15031-5) LIVE METRICS & PID DISCOVERY</h2>
                <p className="text-xs text-slate-400 mt-0.5">Continuous broadcast PID decoding and automated supported PID bitmap discovery</p>
              </div>
              <div className="flex items-center space-x-2">
                <button
                  onClick={handleDiscoverSupportedPids}
                  disabled={isDiscoveringPids}
                  className="px-3 py-1.5 bg-gradient-to-r from-sky-600 to-indigo-600 hover:from-sky-500 hover:to-indigo-500 text-white rounded text-xs font-bold flex items-center space-x-1.5 shadow"
                >
                  {isDiscoveringPids ? (
                    <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <Sparkles className="w-3.5 h-3.5 text-amber-300" />
                  )}
                  <span>{isDiscoveringPids ? 'Discovering Bitmaps...' : 'Auto-Discover PIDs (Mode 01)'}</span>
                </button>
                <button
                  onClick={() => handleSendCustomHex('01 00')}
                  className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded text-xs font-bold"
                >
                  Query Bitmap (01 00)
                </button>
              </div>
            </div>

            {/* PID Discovery Status Banner */}
            {pidDiscoveryMessage && (
              <div className="p-3 bg-sky-950/70 border border-sky-500/50 rounded-lg flex items-center space-x-2.5 text-xs text-sky-200 font-mono animate-fadeIn">
                <Sparkles className="w-4 h-4 text-sky-400 shrink-0" />
                <span>{pidDiscoveryMessage}</span>
              </div>
            )}

            {/* Live OBD Values Cards */}
            <div className="space-y-2">
              <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block font-sans">
                LIVE TELEMETRY PARAMETERS ({obdValues.length} MONITORED)
              </span>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3 font-sans">
                {obdValues.map(v => (
                  <div key={v.pid} className="bg-can-bg border border-can-border hover:border-sky-500/40 rounded-lg p-3.5 shadow flex flex-col justify-between">
                    <div className="flex justify-between text-xs text-slate-400 mb-1">
                      <span className="font-bold truncate">{v.name}</span>
                      <span className="font-mono text-[10px] text-sky-400">0x{v.pid.toString(16).toUpperCase().padStart(2, '0')}</span>
                    </div>
                    <div className="text-2xl font-bold text-white font-mono my-1">
                      {v.formattedValue} <span className="text-xs font-sans text-slate-400">{v.unit}</span>
                    </div>
                    <div className="text-[10px] text-slate-500 font-mono">PID {v.pid} • Mode 01</div>
                  </div>
                ))}
                {obdValues.length === 0 && (
                  <div className="col-span-4 text-center py-8 text-slate-500 text-xs">
                    No active OBD-II values broadcast yet. Click "Auto-Discover PIDs" or poll supported PIDs to begin.
                  </div>
                )}
              </div>
            </div>

            {/* Discovered / Supported PIDs Table */}
            {obdDefs.length > 0 && (
              <div className="bg-can-bg border border-can-border rounded-lg p-4 space-y-3 font-sans">
                <div className="flex justify-between items-center border-b border-can-border pb-2">
                  <div className="flex items-center space-x-2">
                    <Table className="w-4 h-4 text-sky-400" />
                    <span className="text-xs font-bold text-white uppercase">
                      STANDARDIZED OBD-II PID SPECIFICATION & BITMAP PROFILE ({obdDefs.length} DEFINED)
                    </span>
                  </div>
                  <span className="text-[10px] text-slate-400 font-mono">SAE J1979 / ISO 15031-5</span>
                </div>

                <div className="max-h-72 overflow-y-auto">
                  <table className="w-full text-left text-xs font-mono">
                    <thead className="text-[10px] uppercase text-slate-400 bg-slate-900 sticky top-0 border-b border-slate-800">
                      <tr>
                        <th className="py-2 px-3">PID</th>
                        <th className="py-2 px-3 font-sans">Signal / Parameter</th>
                        <th className="py-2 px-3 font-sans">Description</th>
                        <th className="py-2 px-3">Bytes</th>
                        <th className="py-2 px-3">Range & Unit</th>
                        <th className="py-2 px-3 text-right">Support Status</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/60 text-slate-300">
                      {obdDefs.map(def => {
                        const isDiscovered = discoveredPids.some(d => d.pid === def.pid);
                        return (
                          <tr key={def.pid} className="hover:bg-slate-800/40">
                            <td className="py-2 px-3 text-sky-400 font-bold">
                              0x{def.pid.toString(16).toUpperCase().padStart(2, '0')}
                            </td>
                            <td className="py-2 px-3 text-white font-sans font-semibold">
                              {def.name}
                            </td>
                            <td className="py-2 px-3 text-slate-400 font-sans text-[11px] truncate max-w-xs">
                              {def.description}
                            </td>
                            <td className="py-2 px-3 text-slate-400">
                              {def.returnBytes} B
                            </td>
                            <td className="py-2 px-3 text-slate-400 text-[11px]">
                              {def.minValue} .. {def.maxValue} {def.unit}
                            </td>
                            <td className="py-2 px-3 text-right">
                              <span className={`px-2 py-0.5 rounded text-[10px] font-bold uppercase ${
                                isDiscovered
                                  ? 'bg-emerald-950 text-emerald-300 border border-emerald-800/70'
                                  : 'bg-slate-800 text-slate-400 border border-slate-700/60'
                              }`}>
                                {isDiscovered ? 'ECU SUPPORTED' : 'SUPPORTED'}
                              </span>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        )}

        {/* VIEW 3: UDS COMMAND CENTER */}
        {subTab === 'uds' && (
          <div className="p-5 space-y-5 overflow-y-auto h-full font-sans">
            <div className="border-b border-can-border pb-3">
              <h2 className="text-sm font-bold text-white uppercase">UNIFIED DIAGNOSTIC SERVICES (ISO 14229-1)</h2>
              <p className="text-xs text-slate-400 mt-0.5">Execute UDS service requests over CAN ISO-TP or VAG TP2.0 transports</p>
            </div>

            <div className="grid grid-cols-2 gap-4">
              {/* Custom Hex Command */}
              <div className="bg-can-bg border border-can-border rounded-lg p-4 space-y-3 shadow">
                <span className="text-xs font-bold text-white uppercase block">Custom UDS Request (Hex)</span>
                <div className="flex space-x-2">
                  <input
                    type="text"
                    value={customHex}
                    onChange={e => setCustomHex(e.target.value)}
                    className="flex-1 bg-can-panel border border-can-border rounded px-3 py-1.5 text-white font-mono text-xs"
                    placeholder="e.g. 22 F1 90"
                  />
                  <button
                    onClick={() => handleSendCustomHex(customHex)}
                    disabled={isSending}
                    className="px-4 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold flex items-center space-x-1"
                  >
                    <Send className="w-3.5 h-3.5" />
                    <span>Send</span>
                  </button>
                </div>
              </div>

              {/* Service 0x22 Read DID */}
              <div className="bg-can-bg border border-can-border rounded-lg p-4 space-y-3 shadow">
                <span className="text-xs font-bold text-white uppercase block">0x22 Read Data By Identifier (DID)</span>
                <div className="flex space-x-2">
                  <select
                    value={selectedDid}
                    onChange={e => setSelectedDid(e.target.value)}
                    className="flex-1 bg-can-panel border border-can-border rounded px-3 py-1.5 text-xs text-white"
                  >
                    <option value="F190">0xF190 - Vehicle Identification Number (VIN)</option>
                    <option value="F18A">0xF18A - System Supplier Hardware Number</option>
                    <option value="F189">0xF189 - System Supplier Software Version</option>
                    <option value="F197">0xF197 - System Name / Engine Type</option>
                    <option value="F186">0xF186 - Active Diagnostic Session</option>
                    <option value="0100">0x0100 - Live Engine RPM (Dynamic)</option>
                    <option value="0101">0x0101 - Coolant Temp (Dynamic)</option>
                    <option value="0102">0x0102 - Battery Voltage (Dynamic)</option>
                  </select>
                  <button
                    onClick={() => handleSendCustomHex(`22 ${selectedDid.substring(0, 2)} ${selectedDid.substring(2)}`)}
                    className="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-bold"
                  >
                    Read
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* VIEW 4: DTC MANAGER & CORRELATION */}
        {subTab === 'dtc' && (
          <div className="p-5 space-y-5 overflow-y-auto h-full font-sans">
            <div className="flex items-center justify-between border-b border-can-border pb-3">
              <div>
                <h2 className="text-sm font-bold text-white uppercase">DIAGNOSTIC TROUBLE CODE (DTC) & CONTEXTUAL ANOMALY AUDIT</h2>
                <p className="text-xs text-slate-400 mt-0.5">Decoded fault codes with contextual sensor correlation and step-by-step diagnostic workflows</p>
              </div>
              <div className="flex items-center space-x-2">
                <button
                  onClick={handleScanAllDtcs}
                  disabled={isScanningDtcs || isSending}
                  className="px-3.5 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold flex items-center space-x-1.5 shadow"
                  title="Scan ECM, ABS, BCM across UDS 0x19 02 FF, OBD-II Mode 03/07, and KWP2000"
                >
                  <Search className="w-3.5 h-3.5" />
                  <span>{isScanningDtcs ? 'Scanning Protocols...' : 'Scan All DTCs (UDS + OBD)'}</span>
                </button>
                <button
                  onClick={() => setIsClearModalOpen(true)}
                  disabled={isSending || isScanningDtcs}
                  className="px-3.5 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-bold flex items-center space-x-1.5 shadow"
                  title="Purge NVRAM fault codes using verified UDS 0x14 FF FF FF and OBD-II Mode 04 sequence"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                  <span>{isSending ? 'Clearing Faults...' : 'Clear All Faults (0x14 + Mode 04)'}</span>
                </button>
              </div>
            </div>

            {/* DTC Clear Verification Result Banner */}
            {clearVerificationResult && (
              <div className={`p-4 rounded-lg border shadow-md font-sans space-y-2 animate-fadeIn ${
                clearVerificationResult.clearedSuccessfully
                  ? 'bg-emerald-950/40 border-emerald-500/60 text-emerald-200'
                  : clearVerificationResult.status === 'ConditionsNotCorrect'
                    ? 'bg-amber-950/40 border-amber-500/60 text-amber-200'
                    : 'bg-rose-950/40 border-rose-500/60 text-rose-200'
              }`}>
                <div className="flex items-center justify-between">
                  <div className="flex items-center space-x-2.5">
                    {clearVerificationResult.clearedSuccessfully ? (
                      <CheckCircle2 className="w-5 h-5 text-emerald-400 shrink-0" />
                    ) : (
                      <AlertTriangle className="w-5 h-5 text-amber-400 shrink-0" />
                    )}
                    <div>
                      <div className="font-bold text-sm text-white flex items-center space-x-2">
                        <span>
                          {clearVerificationResult.clearedSuccessfully ? 'DTC CLEAR VERIFIED' : 'CLEAR NOT COMPLETED'}
                        </span>
                        <span className={`px-2 py-0.5 text-[10px] rounded font-mono font-bold uppercase border ${
                          clearVerificationResult.clearedSuccessfully
                            ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40'
                            : 'bg-rose-500/20 text-rose-300 border-rose-500/40'
                        }`}>
                          {clearVerificationResult.status}
                        </span>
                      </div>
                      <p className="text-xs text-slate-300 mt-0.5">{clearVerificationResult.message}</p>
                    </div>
                  </div>
                  <button
                    onClick={() => setClearVerificationResult(null)}
                    className="p-1 hover:bg-slate-800 rounded text-slate-400 hover:text-white"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>

                <div className="grid grid-cols-3 gap-2 pt-2 border-t border-slate-700/60 text-xs font-mono">
                  <div>
                    <span className="text-slate-400">Pre-Clear Count:</span>{' '}
                    <strong className="text-white">{clearVerificationResult.preClearDtcCount}</strong>
                  </div>
                  <div>
                    <span className="text-slate-400">Post-Clear Count:</span>{' '}
                    <strong className={clearVerificationResult.postClearDtcCount === 0 ? 'text-emerald-400' : 'text-rose-400'}>
                      {clearVerificationResult.postClearDtcCount}
                    </strong>
                  </div>
                  <div>
                    <span className="text-slate-400">Erased Codes:</span>{' '}
                    <span className="text-emerald-300">
                      {clearVerificationResult.clearedCodes.length > 0 ? clearVerificationResult.clearedCodes.join(', ') : 'None'}
                    </span>
                  </div>
                </div>

                {clearVerificationResult.persistingCodes.length > 0 && (
                  <div className="text-xs text-rose-300 bg-rose-950/60 p-2 rounded border border-rose-800/80">
                    <strong>Persistent Faults:</strong> {clearVerificationResult.persistingCodes.join(', ')} — Hard fault still present in circuit/sensor.
                  </div>
                )}
                {clearVerificationResult.reTriggeredCodes.length > 0 && (
                  <div className="text-xs text-amber-300 bg-amber-950/60 p-2 rounded border border-amber-800/80">
                    <strong>Re-triggered Faults:</strong> {clearVerificationResult.reTriggeredCodes.join(', ')} — ECU immediately re-flagged fault cycle on self-test.
                  </div>
                )}
              </div>
            )}

            {/* DTC Action Notification Banner */}
            {dtcActionMessage && (
              <div className="p-3 bg-sky-950/60 border border-sky-500/40 rounded-lg flex items-center space-x-2.5 text-xs text-sky-300 font-mono animate-fadeIn">
                <Sparkles className="w-4 h-4 text-sky-400 shrink-0" />
                <span>{dtcActionMessage}</span>
              </div>
            )}

            {/* DTC Cards Grid */}
            <div className="grid grid-cols-2 gap-3">
              {dtcs.map(dtc => (
                <div key={`${dtc.ecuName}-${dtc.code}`} className="bg-can-bg border border-can-border hover:border-rose-500/40 rounded-lg p-4 shadow space-y-3">
                  <div className="flex justify-between items-start">
                    <div className="flex items-center space-x-2">
                      <span className="text-xl font-bold text-rose-400 font-mono">{dtc.code}</span>
                      <span className="text-[10px] text-slate-300 bg-slate-800 px-2 py-0.5 rounded font-mono font-bold">
                        ECU: {dtc.ecuName} ({dtc.ecuAddress || `0x${(dtc.responseCanId || dtc.canId).toString(16).toUpperCase()}`})
                      </span>
                      <span className="text-[10px] px-2 py-0.5 rounded font-mono font-bold uppercase bg-sky-950 text-sky-300 border border-sky-800/60">
                        {dtc.protocol || 'UDS'}
                      </span>
                      <span className={`text-[10px] px-2 py-0.5 rounded font-mono font-bold uppercase border ${
                        dtc.confidence === 'CONFIRMED' || dtc.isConfirmed
                          ? 'bg-rose-950 text-rose-300 border-rose-800/80'
                          : 'bg-amber-950 text-amber-300 border-amber-800/80'
                      }`}>
                        {dtc.confidence || (dtc.isConfirmed ? 'CONFIRMED' : 'PENDING')}
                      </span>
                    </div>
                    <span className="text-xs font-mono font-bold text-amber-400">
                      Status: 0x{dtc.statusByte.toString(16).toUpperCase().padStart(2, '0')}
                    </span>
                  </div>

                  <p className="text-xs text-slate-100 font-semibold">{dtc.description}</p>

                  {/* Complete CAN Forensic Telemetry Panel */}
                  <div className="p-2.5 bg-slate-950/70 border border-slate-800/80 rounded font-mono text-[11px] space-y-1.5">
                    <div className="grid grid-cols-2 gap-2 text-slate-400">
                      <div>
                        <span>Request ID:</span>{' '}
                        <strong className="text-sky-300">
                          0x{(dtc.requestCanId || (dtc.canId >= 0x7E8 ? dtc.canId - 8 : 0x7E0)).toString(16).toUpperCase()}
                        </strong>
                      </div>
                      <div>
                        <span>Response ID:</span>{' '}
                        <strong className="text-emerald-300">
                          0x{(dtc.responseCanId || dtc.canId).toString(16).toUpperCase()}
                        </strong>
                      </div>
                      <div>
                        <span>Service / Subfn:</span>{' '}
                        <strong className="text-white">
                          0x{(dtc.service || (dtc.protocol === 'OBD-II' ? 0x03 : 0x19)).toString(16).toUpperCase()}
                          {dtc.subfunction !== undefined && dtc.subfunction !== null ? ` / 0x${dtc.subfunction.toString(16).toUpperCase().padStart(2, '0')}` : ''}
                        </strong>
                      </div>
                      <div>
                        <span>Raw DTC Bytes:</span>{' '}
                        <strong className="text-amber-300">
                          {dtc.rawDtcBytesHex || dtc.code}
                        </strong>
                      </div>
                    </div>

                    {dtc.rawCompleteResponseHex && (
                      <div className="pt-1 border-t border-slate-800/60 truncate text-slate-400">
                        <span>Raw Frame:</span>{' '}
                        <span className="text-slate-300 select-all font-mono">
                          {dtc.rawCompleteResponseHex}
                        </span>
                      </div>
                    )}

                    <div className="flex justify-between items-center text-[10px] text-slate-500 pt-0.5">
                      <span>Source: {dtc.sourceDatabase || 'SAE J2012 / OEM Matrix'}</span>
                      {dtc.occurrenceCount !== undefined && dtc.occurrenceCount !== null && (
                        <span>Occurrences: {dtc.occurrenceCount}</span>
                      )}
                    </div>
                  </div>

                  <div className="flex flex-wrap gap-1.5 pt-1">
                    {dtc.activeStatusFlags?.map(flag => (
                      <span key={flag} className="px-2 py-0.5 bg-rose-500/10 text-rose-300 border border-rose-500/20 rounded text-[10px] font-semibold">
                        {flag}
                      </span>
                    ))}
                  </div>

                  <div className="pt-2 border-t border-can-border flex justify-end">
                    <button
                      onClick={() => handleInspectDtc(dtc.code)}
                      className="px-3 py-1 bg-purple-600 hover:bg-purple-500 text-white rounded text-xs font-bold flex items-center space-x-1 shadow"
                    >
                      <Sparkles className="w-3.5 h-3.5" />
                      <span>Deep Dive & Correlate</span>
                    </button>
                  </div>
                </div>
              ))}
              {dtcs.length === 0 && (
                <div className="col-span-2 text-center py-12 text-slate-500 text-xs">
                  <CheckCircle2 className="w-10 h-10 mx-auto mb-2 text-emerald-500" />
                  <p className="font-bold text-slate-300 text-sm">No Diagnostic Trouble Codes Active</p>
                  <p className="text-slate-500 mt-1">All monitored electronic control units report nominal system status.</p>
                </div>
              )}
            </div>
          </div>
        )}

        {/* VIEW 5: FREEZE FRAMES / EVENT SNAPSHOTS */}
        {subTab === 'freeze-frame' && (
          <div className="p-5 space-y-5 overflow-y-auto h-full font-sans">
            <div className="flex items-center justify-between border-b border-can-border pb-3">
              <div>
                <h2 className="text-sm font-bold text-white uppercase">FREEZE FRAME EVENT SNAPSHOTS (OBD MODE 02 & UDS 0x19 04)</h2>
                <p className="text-xs text-slate-400 mt-0.5">Operating sensor readings frozen at the microsecond an emissions fault was verified</p>
              </div>
              <button
                onClick={() => api.getFreezeFrames().then(setFreezeFrames)}
                className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold flex items-center space-x-1"
              >
                <RefreshCw className="w-3.5 h-3.5" />
                <span>Refresh Snapshots</span>
              </button>
            </div>

            <div className="grid grid-cols-12 gap-5">
              {/* Snapshot List */}
              <div className="col-span-4 space-y-2">
                <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">CAPTURED SNAPSHOT RECORDS</span>
                {freezeFrames.map(f => {
                  const isSelected = selectedSnapshot?.id === f.id;
                  return (
                    <button
                      key={f.id}
                      onClick={() => setSelectedSnapshot(f)}
                      className={`w-full text-left p-3 rounded-lg border transition-all ${
                        isSelected
                          ? 'bg-sky-600/20 border-sky-500 text-white shadow'
                          : 'bg-can-bg border-can-border text-slate-300 hover:bg-slate-800'
                      }`}
                    >
                      <div className="flex justify-between items-center">
                        <span className="text-sm font-mono font-bold text-rose-400">{f.dtcCode}</span>
                        <span className="text-[10px] font-mono text-slate-400">Rec #{f.recordNumber}</span>
                      </div>
                      <div className="text-[11px] text-slate-400 mt-1">{f.faultTriggerReason}</div>
                      <div className="text-[9px] text-slate-500 font-mono mt-1">{new Date(f.timestampUtc).toLocaleString()}</div>
                    </button>
                  );
                })}
              </div>

              {/* Snapshot Parameter Breakdown */}
              <div className="col-span-8 bg-can-bg border border-can-border rounded-lg p-4 shadow space-y-3">
                {selectedSnapshot ? (
                  <>
                    <div className="flex justify-between items-baseline border-b border-can-border pb-2">
                      <div>
                        <span className="text-base font-mono font-bold text-rose-400">{selectedSnapshot.dtcCode}</span>
                        <span className="text-xs text-slate-400 ml-2">Snapshot Record #{selectedSnapshot.recordNumber} ({selectedSnapshot.protocol})</span>
                      </div>
                      <span className="text-xs text-slate-400 font-mono">{new Date(selectedSnapshot.timestampUtc).toLocaleString()}</span>
                    </div>

                    <div className="grid grid-cols-3 gap-3 pt-1">
                      {selectedSnapshot.sensors.map(s => (
                        <div key={s.parameterId} className="bg-can-panel border border-can-border rounded p-3 text-xs">
                          <div className="text-slate-400 text-[10px] truncate mb-0.5">{s.name}</div>
                          <div className="text-xl font-bold font-mono text-white mt-1">
                            {s.formattedValue} <span className="text-[11px] font-sans text-slate-400">{s.unit}</span>
                          </div>
                          <div className="text-[9px] text-slate-500 font-mono mt-1">{s.parameterId} • {s.category}</div>
                        </div>
                      ))}
                    </div>
                  </>
                ) : (
                  <div className="py-16 text-center text-slate-500">Select a snapshot record on the left to inspect</div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* VIEW 6: VAG MEASURING BLOCKS */}
        {subTab === 'vag-blocks' && (
          <div className="p-5 space-y-5 overflow-y-auto h-full font-sans">
            <div className="flex flex-wrap items-center justify-between border-b border-can-border pb-3 gap-3">
              <div>
                <h2 className="text-sm font-bold text-white uppercase">VAG MEASURING BLOCKS (MESSWERTBLÖCKE 001 - 255)</h2>
                <p className="text-xs text-slate-400 mt-0.5">Classic Volkswagen/Audi 4-zone telemetry groups with standard VAG formula conversion</p>
              </div>

              <div className="flex items-center space-x-3 text-xs">
                {/* Popular Group Shortcuts */}
                <div className="flex items-center space-x-1 bg-can-bg border border-can-border rounded p-1">
                  {[1, 2, 3, 15, 20, 31, 115, 118, 120].map(gNum => (
                    <button
                      key={gNum}
                      onClick={() => setSelectedGroupNum(gNum)}
                      className={`px-2 py-1 rounded text-xs font-mono font-bold transition-all ${
                        selectedGroupNum === gNum ? 'bg-sky-600 text-white' : 'text-slate-400 hover:text-white'
                      }`}
                    >
                      {gNum.toString().padStart(3, '0')}
                    </button>
                  ))}
                </div>

                <div className="flex items-center space-x-1.5">
                  <span className="text-slate-400">Group:</span>
                  <input
                    type="number"
                    min="1"
                    max="255"
                    value={selectedGroupNum}
                    onChange={e => setSelectedGroupNum(Math.max(1, Math.min(255, parseInt(e.target.value) || 1)))}
                    className="w-16 bg-can-bg border border-can-border rounded px-2 py-1 text-white font-mono font-bold text-center"
                  />
                </div>
              </div>
            </div>

            {/* Active VAG Measuring Group Display */}
            {activeVagGroup ? (
              <div className="space-y-4">
                <div className="flex justify-between items-baseline bg-can-bg border border-can-border rounded-lg px-4 py-2.5">
                  <div className="flex items-center space-x-2">
                    <span className="text-base font-bold text-sky-400 font-mono">Group {activeVagGroup.groupNumber.toString().padStart(3, '0')}</span>
                    <span className="text-xs font-bold text-white">• {activeVagGroup.name}</span>
                  </div>
                  <span className="text-xs text-slate-400">{activeVagGroup.description}</span>
                </div>

                {/* 4 Large Display Zones */}
                <div className="grid grid-cols-4 gap-4">
                  {activeVagGroup.zones.map(z => (
                    <div
                      key={z.zoneNumber}
                      className="bg-can-bg border border-can-border rounded-xl p-5 shadow flex flex-col justify-between"
                      style={{ minHeight: '180px' }}
                    >
                      <div className="flex justify-between items-start text-xs border-b border-can-border pb-2">
                        <span className="font-bold text-slate-300">FIELD {z.zoneNumber}</span>
                        <span className="text-[10px] font-mono text-slate-500">{z.formulaType}</span>
                      </div>

                      <div className="my-auto text-center py-2">
                        <div className="text-3xl font-bold font-mono text-white tracking-tight">
                          {z.formattedValue}
                        </div>
                        <div className="text-xs font-bold text-sky-400 mt-1">{z.unit || 'State'}</div>
                      </div>

                      <div className="text-[11px] text-center font-semibold text-slate-400 pt-2 border-t border-slate-800">
                        {z.label}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <div className="py-16 text-center text-slate-500">Loading measuring block...</div>
            )}
          </div>
        )}

        {/* VIEW 7: MANUFACTURER LIVE DATA */}
        {subTab === 'manufacturer' && (
          <div className="p-5 space-y-5 overflow-y-auto h-full font-sans">
            <div className="flex flex-wrap items-center justify-between border-b border-can-border pb-3 gap-3">
              <div>
                <h2 className="text-sm font-bold text-white uppercase">MANUFACTURER EXTENDED LIVE-DATA DEFINITIONS</h2>
                <p className="text-xs text-slate-400 mt-0.5">OEM-specific data identifiers (DIDs) decoded with calibrated scaling factors and units</p>
              </div>

              {/* OEM Selector */}
              <div className="flex items-center space-x-2 text-xs">
                <span className="text-slate-400 font-bold">OEM BRAND:</span>
                <select
                  value={selectedOem}
                  onChange={e => setSelectedOem(e.target.value as any)}
                  className="bg-can-bg border border-can-border rounded px-3 py-1.5 text-white font-bold text-sky-400"
                >
                  <option value="MarutiMarelli">Maruti Suzuki Swift Diesel (Magneti Marelli MJD)</option>
                  <option value="VAG">VAG (VW, Audi, Seat, Skoda, Porsche)</option>
                  <option value="BMW">BMW / Mini</option>
                  <option value="Ford">Ford / Lincoln</option>
                  <option value="GM">General Motors (Chevy, Cadillac, GMC)</option>
                  <option value="Toyota">Toyota / Lexus</option>
                  <option value="Mercedes">Mercedes-Benz / AMG</option>
                </select>
              </div>
            </div>

            {/* Live Values Grid */}
            <div className="grid grid-cols-3 gap-4">
              {manufacturerSnapshot.map(m => (
                <div key={m.definitionId} className="bg-can-bg border border-can-border rounded-lg p-4 shadow flex flex-col justify-between">
                  <div className="flex justify-between items-start text-xs mb-1">
                    <span className="font-bold text-slate-300 truncate max-w-[180px]">{m.name}</span>
                    <span className="text-[10px] font-mono text-sky-400 bg-sky-950 px-1.5 py-0.5 rounded">{m.oem}</span>
                  </div>

                  <div className="my-2 flex items-baseline justify-center space-x-1.5 font-mono">
                    <span className="text-2xl font-bold text-white">{m.formattedValue}</span>
                    <span className="text-xs text-slate-400 font-sans">{m.unit}</span>
                  </div>

                  <div className="flex justify-between items-center text-[10px] font-mono text-slate-500 pt-2 border-t border-slate-800">
                    <span>{m.category}</span>
                    <span className={m.isWarning ? 'text-amber-400 font-bold' : 'text-emerald-400'}>
                      {m.isWarning ? 'OUT OF NOMINAL' : 'NOMINAL'}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* VIEW: LIVE OPERATING ENVELOPE / TUNING GRID */}
        {subTab === 'tuning-grid' && (
          <div className="p-5 space-y-5 overflow-y-auto h-full font-sans">
            {/* Header with Strict Engineering Disclaimer */}
            <div className="flex flex-wrap items-center justify-between border-b border-can-border pb-3 gap-3">
              <div>
                <div className="flex items-center space-x-2">
                  <h2 className="text-sm font-bold text-white uppercase tracking-wider">
                    LIVE CAN OPERATING ENVELOPE (TUNING & CALIBRATION ANALYSIS)
                  </h2>
                  <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold uppercase bg-amber-950 text-amber-300 border border-amber-800/80">
                    CAN TELEMETRY ONLY — NOT AN ECU FLASH MAP
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-0.5">
                  Multi-dimensional operating matrix accumulated in real time from canonical CAN signals. Unsampled cells are strictly rendered as NO DATA without interpolation.
                </p>
              </div>

              {/* Action Toolbar */}
              <div className="flex items-center space-x-2 text-xs">
                {/* Preset Selector */}
                <select
                  value={operatingGridId}
                  onChange={e => setOperatingGridId(e.target.value)}
                  className="bg-can-bg border border-can-border rounded px-3 py-1.5 text-white font-bold text-sky-400 font-mono"
                >
                  {operatingGridConfigs.length > 0 ? (
                    operatingGridConfigs.map(c => (
                      <option key={c.gridId} value={c.gridId}>
                        {c.name}
                      </option>
                    ))
                  ) : (
                    <>
                      <option value="rpm_vs_load_lambda">RPM × Load → Lambda</option>
                      <option value="rpm_vs_boost_railpressure">RPM × Boost → Rail Pressure</option>
                      <option value="rpm_vs_tps_torque">RPM × Throttle → Engine Torque</option>
                    </>
                  )}
                </select>

                <button
                  onClick={() => setIsGridPolling(!isGridPolling)}
                  className={`px-3 py-1.5 rounded text-xs font-bold flex items-center space-x-1 border ${
                    isGridPolling
                      ? 'bg-emerald-950/80 text-emerald-300 border-emerald-800'
                      : 'bg-slate-800 text-slate-400 border-slate-700'
                  }`}
                >
                  <Activity className="w-3.5 h-3.5" />
                  <span>{isGridPolling ? 'Live Streaming' : 'Paused'}</span>
                </button>

                <button
                  onClick={handleResetGrid}
                  className="px-3 py-1.5 bg-rose-950/70 hover:bg-rose-900 text-rose-300 border border-rose-800/80 rounded text-xs font-bold flex items-center space-x-1"
                >
                  <RotateCcw className="w-3.5 h-3.5" />
                  <span>Reset Grid</span>
                </button>

                <a
                  href={api.exportOperatingGridCsvUrl(operatingGridId)}
                  download={`${operatingGridId}_telemetry_grid.csv`}
                  className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold flex items-center space-x-1 shadow"
                >
                  <Download className="w-3.5 h-3.5" />
                  <span>Export CSV</span>
                </a>
              </div>
            </div>

            {/* Grid Action Message */}
            {gridActionMessage && (
              <div className="p-3 bg-sky-950/70 border border-sky-500/50 rounded-lg flex items-center space-x-2.5 text-xs text-sky-200 font-mono animate-fadeIn">
                <Sparkles className="w-4 h-4 text-sky-400 shrink-0" />
                <span>{gridActionMessage}</span>
              </div>
            )}

            {/* Metrics Overview Bar */}
            {gridSnapshot && (
              <div className="grid grid-cols-2 md:grid-cols-5 gap-3 font-mono">
                <div className="bg-can-bg border border-can-border rounded-lg p-3">
                  <div className="text-slate-400 text-[10px] uppercase font-sans">Total Samples Recorded</div>
                  <div className="text-xl font-bold text-sky-400 mt-1">
                    {gridSnapshot.totalSamples.toLocaleString()}
                  </div>
                  <div className="text-[10px] text-slate-500 font-sans mt-0.5">Welford variance tracking</div>
                </div>

                <div className="bg-can-bg border border-can-border rounded-lg p-3">
                  <div className="text-slate-400 text-[10px] uppercase font-sans">Active Operating Cells</div>
                  <div className="text-xl font-bold text-emerald-400 mt-1">
                    {gridSnapshot.activeCellCount} <span className="text-xs text-slate-400 font-normal">/ {(gridSnapshot.config.xBinEdges.length) * (gridSnapshot.config.yBinEdges.length)}</span>
                  </div>
                  <div className="text-[10px] text-slate-500 font-sans mt-0.5">
                    {(((gridSnapshot.activeCellCount / Math.max(1, (gridSnapshot.config.xBinEdges.length) * (gridSnapshot.config.yBinEdges.length)))) * 100).toFixed(1)}% coverage
                  </div>
                </div>

                <div className="bg-can-bg border border-can-border rounded-lg p-3">
                  <div className="text-slate-400 text-[10px] uppercase font-sans">X-Axis Parameter</div>
                  <div className="text-sm font-bold text-white mt-1 truncate">
                    {gridSnapshot.config.xSignalName}
                  </div>
                  <div className="text-[10px] text-slate-400 font-sans mt-0.5">
                    {gridSnapshot.config.xBinEdges[0]} .. {gridSnapshot.config.xBinEdges[gridSnapshot.config.xBinEdges.length - 1]}
                  </div>
                </div>

                <div className="bg-can-bg border border-can-border rounded-lg p-3">
                  <div className="text-slate-400 text-[10px] uppercase font-sans">Y-Axis Parameter</div>
                  <div className="text-sm font-bold text-white mt-1 truncate">
                    {gridSnapshot.config.ySignalName}
                  </div>
                  <div className="text-[10px] text-slate-400 font-sans mt-0.5">
                    {gridSnapshot.config.yBinEdges[0]} .. {gridSnapshot.config.yBinEdges[gridSnapshot.config.yBinEdges.length - 1]}
                  </div>
                </div>

                <div className="bg-can-bg border border-can-border rounded-lg p-3">
                  <div className="text-slate-400 text-[10px] uppercase font-sans">Target Observed Signal</div>
                  <div className="text-sm font-bold text-amber-300 mt-1 truncate">
                    {gridSnapshot.config.zSignalName} ({gridSnapshot.config.zUnit})
                  </div>
                  <div className="text-[10px] text-slate-500 font-sans mt-0.5">Canonical registry metric</div>
                </div>
              </div>
            )}

            {/* 2D Operating Grid Matrix Table */}
            {gridSnapshot ? (
              <div className="bg-can-bg border border-can-border rounded-lg p-4 space-y-3 font-sans">
                <div className="flex justify-between items-center border-b border-can-border pb-2">
                  <div className="flex items-center space-x-2">
                    <Flame className="w-4 h-4 text-amber-400" />
                    <span className="text-xs font-bold text-white uppercase">
                      {gridSnapshot.config.name}
                    </span>
                  </div>
                  <span className="text-[10px] font-mono text-slate-400">
                    Updated: {new Date(gridSnapshot.timestampUtc).toLocaleTimeString()}
                  </span>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full text-center text-xs font-mono border-collapse">
                    <thead>
                      <tr>
                        <th className="p-2 border border-slate-800 bg-slate-900 text-slate-400 text-[10px] uppercase sticky left-0 z-10 font-bold min-w-[100px]">
                          {gridSnapshot.config.ySignalName} ↓ \ {gridSnapshot.config.xSignalName} →
                        </th>
                        {gridSnapshot.config.xBinEdges.map((xVal, xi) => (
                          <th key={xi} className="p-2 border border-slate-800 bg-slate-900 text-sky-300 font-bold text-[11px] min-w-[85px]">
                            {xVal}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {gridSnapshot.config.yBinEdges.map((yVal, yi) => (
                        <tr key={yi}>
                          <th className="p-2 border border-slate-800 bg-slate-900 text-amber-300 font-bold text-[11px] sticky left-0 z-10 text-left">
                            {yVal}
                          </th>
                          {gridSnapshot.config.xBinEdges.map((_, xi) => {
                            const bucket = gridSnapshot.cells[yi]?.[xi];
                            const hasData = bucket && bucket.sampleCount > 0;
                            return (
                              <td
                                key={xi}
                                className={`p-2 border border-slate-800 transition-colors relative group ${
                                  hasData
                                    ? 'bg-slate-800/80 hover:bg-slate-700/80 text-white cursor-pointer'
                                    : 'bg-slate-950/60 text-slate-600'
                                }`}
                                title={
                                  hasData
                                    ? `X: ${bucket.xCenter}, Y: ${bucket.yCenter}\nMean: ${bucket.mean?.toFixed(3)}\nMin: ${bucket.min?.toFixed(3)}\nMax: ${bucket.max?.toFixed(3)}\nStdDev: ${bucket.stdDev?.toFixed(3)}\nSamples: ${bucket.sampleCount}`
                                    : 'NO DATA — Engine has not operated at this operating point'
                                }
                              >
                                {hasData ? (
                                  <div>
                                    <div className="font-bold text-xs text-emerald-300">
                                      {bucket.mean !== null && bucket.mean !== undefined ? bucket.mean.toFixed(2) : '—'}
                                    </div>
                                    <div className="text-[9px] text-slate-400 mt-0.5">
                                      N={bucket.sampleCount}
                                    </div>
                                  </div>
                                ) : (
                                  <div className="text-[10px] text-slate-600 font-sans tracking-wide">
                                    —
                                  </div>
                                )}
                              </td>
                            );
                          })}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Grid Legend & Forensic Assurance */}
                <div className="flex flex-wrap items-center justify-between text-[11px] text-slate-400 pt-2 border-t border-slate-800/80">
                  <div className="flex items-center space-x-4">
                    <span className="flex items-center space-x-1.5">
                      <span className="w-3 h-3 rounded-sm bg-slate-800 border border-slate-700 inline-block"></span>
                      <span>Active Cell (Mean ± StdDev)</span>
                    </span>
                    <span className="flex items-center space-x-1.5">
                      <span className="w-3 h-3 rounded-sm bg-slate-950 border border-slate-800 inline-block"></span>
                      <span>Unsampled Area (NO DATA)</span>
                    </span>
                  </div>
                  <span className="text-[10px] text-slate-500 font-mono">
                    Truthful Telemetry Assurance: Zero interpolation applied.
                  </span>
                </div>
              </div>
            ) : (
              <div className="py-20 text-center text-slate-500">
                <Flame className="w-8 h-8 mx-auto mb-2 opacity-40 animate-pulse" />
                <p>Loading operating envelope telemetry...</p>
              </div>
            )}
          </div>
        )}

        {/* VIEW 8: ECU IDENTIFICATION */}
        {subTab === 'ecuinfo' && (
          <div className="p-5 space-y-5 overflow-y-auto h-full font-sans">
            <div className="flex items-center justify-between border-b border-can-border pb-3">
              <div>
                <h2 className="text-sm font-bold text-white uppercase">ECU IDENTIFICATION & HARDWARE INVENTORY</h2>
                <p className="text-xs text-slate-400 mt-0.5">Read electronic serial numbers, VIN, hardware part numbers, and calibration versions.</p>
              </div>
              <button
                onClick={() => handleSendCustomHex('22 F1 90')}
                className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold"
              >
                Query VIN (0xF190)
              </button>
            </div>

            <div className="grid grid-cols-3 gap-4 font-mono">
              {ecuList.map(ecu => (
                <div key={ecu.ecuName} className="bg-can-bg border border-can-border rounded-lg p-4 shadow space-y-3 font-sans">
                  <div className="flex justify-between items-center border-b border-can-border pb-2">
                    <span className="text-sm font-bold text-white">{ecu.ecuName}</span>
                    <span className="text-[10px] text-sky-400 font-mono">
                      0x{ecu.requestCanId.toString(16).toUpperCase()} → 0x{ecu.responseCanId.toString(16).toUpperCase()}
                    </span>
                  </div>

                  <div className="space-y-2 text-xs">
                    <div>
                      <span className="text-slate-500 text-[10px] block font-bold">VIN:</span>
                      <span className="text-white font-mono font-bold">{ecu.vin || 'Not Available'}</span>
                    </div>
                    <div>
                      <span className="text-slate-500 text-[10px] block font-bold">HARDWARE NUMBER:</span>
                      <span className="text-slate-300 font-mono font-semibold">{ecu.hardwareNumber || 'Not Available'}</span>
                    </div>
                    <div>
                      <span className="text-slate-500 text-[10px] block font-bold">SOFTWARE VERSION:</span>
                      <span className="text-emerald-400 font-mono font-bold">{ecu.softwareVersion || 'Not Available'}</span>
                    </div>
                    <div>
                      <span className="text-slate-500 text-[10px] block font-bold">SYSTEM NAME:</span>
                      <span className="text-slate-300">{ecu.systemName || 'Not Available'}</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* VIEW 9: ECU SIMULATOR CONTROL PANEL */}
        {subTab === 'simulator' && (
          <div className="p-5 space-y-5 overflow-y-auto h-full font-sans">
            <div className="flex items-center justify-between border-b border-can-border pb-3">
              <div>
                <h2 className="text-sm font-bold text-white uppercase">VIRTUAL ECU SIMULATOR CONFIGURATION</h2>
                <p className="text-xs text-slate-400 mt-0.5">Simulate multi-frame ISO-TP responses, DID parameters, and fault DTC injection.</p>
              </div>
              <button
                onClick={handleResetSimulator}
                className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs border border-can-border flex items-center space-x-1.5"
              >
                <RotateCcw className="w-3.5 h-3.5" />
                <span>Reset Virtual ECUs</span>
              </button>
            </div>

            <div className="grid grid-cols-3 gap-4">
              {simulatorStatus?.virtualEcus?.map(ecu => (
                <div key={ecu.name} className="bg-can-bg border border-can-border rounded-lg p-4 shadow space-y-3">
                  <div className="flex justify-between items-center border-b border-can-border pb-2">
                    <span className="text-sm font-bold text-white">{ecu.name} Simulator</span>
                    <span className="text-[10px] text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded font-bold">ACTIVE</span>
                  </div>

                  <p className="text-xs text-slate-400">{ecu.description}</p>

                  {/* Fault Injection Controls */}
                  <div className="space-y-2 pt-2 border-t border-can-border">
                    <span className="text-[11px] text-slate-400 font-bold block uppercase">Inject Fault DTCs:</span>
                    {ecu.name === 'ECM' && (
                      <div className="flex space-x-2">
                        <button
                          onClick={() => handleToggleFault('ECM', 'P0300', true)}
                          className="flex-1 py-1 bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 rounded text-[11px] font-bold"
                        >
                          + P0300 Misfire
                        </button>
                        <button
                          onClick={() => handleToggleFault('ECM', 'P0087', true)}
                          className="flex-1 py-1 bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 rounded text-[11px] font-bold"
                        >
                          + P0087 Rail Low
                        </button>
                      </div>
                    )}
                    {ecu.name === 'ABS' && (
                      <button
                        onClick={() => handleToggleFault('ABS', 'C0035', true)}
                        className="w-full py-1 bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 rounded text-[11px] font-bold"
                      >
                        + C0035 Wheel Speed Fault
                      </button>
                    )}
                    {ecu.name === 'BCM' && (
                      <button
                        onClick={() => handleToggleFault('BCM', 'B1325', true)}
                        className="w-full py-1 bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 rounded text-[11px] font-bold"
                      >
                        + B1325 Voltage Low Fault
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* DTC Deep-Dive & Contextual Signal Correlation Modal */}
      {isDtcModalOpen && selectedDtcMeta && (
        <div className="fixed inset-0 z-50 bg-black/75 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-can-panel border border-can-border rounded-xl shadow-2xl max-w-3xl w-full max-h-[85vh] overflow-y-auto p-5 font-sans space-y-4">
            <div className="flex justify-between items-start border-b border-can-border pb-3">
              <div className="flex items-center space-x-3">
                <span className="text-2xl font-black font-mono text-rose-400">{selectedDtcMeta.code}</span>
                <div>
                  <h3 className="text-base font-bold text-white">{selectedDtcMeta.title}</h3>
                  <span className="text-xs text-slate-400">{selectedDtcMeta.category} • Severity: {selectedDtcMeta.severity}</span>
                </div>
              </div>
              <button
                onClick={() => setIsDtcModalOpen(false)}
                className="text-slate-400 hover:text-white px-2 py-1 bg-slate-800 rounded font-bold"
              >
                Close
              </button>
            </div>

            {/* Technical Explanation */}
            <div className="bg-can-bg border border-can-border rounded-lg p-3 text-xs space-y-1">
              <span className="font-bold text-slate-300 uppercase tracking-wide">TECHNICAL EXPLANATION</span>
              <p className="text-slate-300">{selectedDtcMeta.technicalExplanation}</p>
            </div>

            {/* Contextual Signal Correlation Report */}
            {selectedDtcCorrelation && (
              <div className="bg-purple-950/20 border border-purple-500/40 rounded-lg p-4 space-y-3">
                <div className="flex justify-between items-center text-xs">
                  <div className="flex items-center space-x-2 text-purple-400 font-bold">
                    <Sparkles className="w-4 h-4" />
                    <span>CONTEXTUAL SIGNAL ANOMALY RANKING</span>
                  </div>
                  <span className="text-[10px] font-mono text-purple-300">
                    Primary Suspect: <strong className="text-white">{selectedDtcCorrelation.topSuspectSignal}</strong>
                  </span>
                </div>

                <p className="text-xs text-purple-200 bg-purple-950/40 p-2.5 rounded border border-purple-800/40">
                  {selectedDtcCorrelation.rootCauseHypothesis}
                </p>

                {/* Anomalies Table */}
                <div className="overflow-x-auto">
                  <table className="w-full text-[11px] font-mono">
                    <thead>
                      <tr className="text-left text-slate-400 border-b border-purple-900/60">
                        <th className="pb-1.5 font-bold">SIGNAL NAME</th>
                        <th className="pb-1.5 font-bold text-center">BASELINE</th>
                        <th className="pb-1.5 font-bold text-center">AT FAULT</th>
                        <th className="pb-1.5 font-bold text-center">DEVIATION</th>
                        <th className="pb-1.5 font-bold text-center">Z-SCORE</th>
                        <th className="pb-1.5 font-bold text-right">CONFIDENCE</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-purple-900/30">
                      {selectedDtcCorrelation.anomalies.map(a => (
                        <tr key={a.signalName} className={a.isPrimarySuspect ? 'bg-purple-900/30 font-bold' : ''}>
                          <td className="py-1.5 text-white flex items-center space-x-1">
                            {a.isPrimarySuspect && <span className="text-rose-400 text-xs">★</span>}
                            <span>{a.signalName}</span>
                          </td>
                          <td className="py-1.5 text-center text-slate-400">{a.baselineMean} {a.unit}</td>
                          <td className="py-1.5 text-center text-white">{a.faultMomentValue} {a.unit}</td>
                          <td className={`py-1.5 text-center ${a.deviationPct < 0 ? 'text-rose-400' : 'text-amber-400'}`}>
                            {(a.deviationPct > 0 ? '+' : '') + a.deviationPct.toFixed(1)}%
                          </td>
                          <td className="py-1.5 text-center text-sky-400">{a.deviationZScore}σ</td>
                          <td className="py-1.5 text-right text-emerald-400">{(a.correlationConfidence * 100).toFixed(1)}%</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Likely Causes & Diagnostic Procedures */}
            <div className="grid grid-cols-2 gap-4 text-xs">
              <div className="bg-can-bg border border-can-border rounded-lg p-3 space-y-2">
                <span className="font-bold text-amber-400 uppercase tracking-wide block">LIKELY CAUSES</span>
                <ul className="list-disc list-inside text-slate-300 space-y-1">
                  {selectedDtcMeta.likelyCauses.map((c, i) => (
                    <li key={i}>{c}</li>
                  ))}
                </ul>
              </div>

              <div className="bg-can-bg border border-can-border rounded-lg p-3 space-y-2">
                <span className="font-bold text-sky-400 uppercase tracking-wide block">DIAGNOSTIC PROCEDURES</span>
                <ol className="list-decimal list-inside text-slate-300 space-y-1">
                  {selectedDtcMeta.diagnosticSteps.map((s, i) => (
                    <li key={i}>{s}</li>
                  ))}
                </ol>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Clear DTC Safety Confirmation Modal */}
      {isClearModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/80 backdrop-blur-xs flex items-center justify-center p-4 font-sans animate-fadeIn">
          <div className="bg-can-panel border border-rose-600/60 rounded-xl max-w-lg w-full p-5 shadow-2xl space-y-4">
            <div className="flex items-center justify-between border-b border-can-border pb-3">
              <div className="flex items-center space-x-2 text-rose-400">
                <AlertTriangle className="w-5 h-5" />
                <span className="font-bold text-sm tracking-wide text-white uppercase">Confirm ECU Fault Code Erase</span>
              </div>
              <button
                onClick={() => setIsClearModalOpen(false)}
                className="p-1 hover:bg-slate-800 text-slate-400 hover:text-white rounded"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-3 text-xs text-slate-300">
              <div className="p-3 bg-rose-950/30 border border-rose-500/40 rounded-lg space-y-1.5 text-rose-200">
                <div className="font-bold text-rose-100 flex items-center space-x-1.5">
                  <ShieldAlert className="w-4 h-4 text-rose-400" />
                  <span>Permanent NVRAM Diagnostic Erase Sequence</span>
                </div>
                <p className="text-[11px] leading-relaxed">
                  You are about to transmit diagnostic service <strong className="text-white font-mono">0x14 FF FF FF</strong> and <strong className="text-white font-mono">OBD-II Mode 04</strong> to <strong className="text-white">{targetEcu}</strong> (0x{(ecuCanIdMap[targetEcu] || 0x7E0).toString(16).toUpperCase()}).
                </p>
              </div>

              <div className="space-y-1.5">
                <span className="font-bold text-[11px] text-slate-400 uppercase tracking-wider block">ECU Safety Prerequisites:</span>
                <ul className="space-y-1 text-[11px] text-slate-300">
                  <li className="flex items-center space-x-2">
                    <span className="w-1.5 h-1.5 rounded-full bg-amber-400" />
                    <span>Ignition ON, Engine OFF (KOEO). Engine must not be rotating.</span>
                  </li>
                  <li className="flex items-center space-x-2">
                    <span className="w-1.5 h-1.5 rounded-full bg-amber-400" />
                    <span>Vehicle Speed must be exactly 0 km/h with parking brake set.</span>
                  </li>
                  <li className="flex items-center space-x-2">
                    <span className="w-1.5 h-1.5 rounded-full bg-amber-400" />
                    <span>Clearing freeze frames will reset OBD readiness monitors to incomplete.</span>
                  </li>
                </ul>
              </div>

              <div className="p-2.5 bg-slate-900 border border-slate-700 rounded text-[11px] text-slate-400">
                <strong className="text-sky-300">Automatic 5-Step Verification:</strong> The platform will take a pre-clear snapshot, transmit commands, catch NRC 0x22 (Conditions Not Correct), re-read active codes, and verify physical clearance.
              </div>
            </div>

            <div className="flex justify-end space-x-2 pt-2 border-t border-can-border">
              <button
                onClick={() => setIsClearModalOpen(false)}
                className="px-4 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded text-xs font-bold transition-all"
              >
                Cancel
              </button>
              <button
                onClick={handleClearDtcs}
                disabled={isSending}
                className="px-4 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-bold flex items-center space-x-1.5 shadow transition-all"
              >
                <Trash2 className="w-3.5 h-3.5" />
                <span>{isSending ? 'Transmitting...' : 'Confirm Clear & Verify'}</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
