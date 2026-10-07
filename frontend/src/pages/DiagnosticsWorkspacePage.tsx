import React, { useState, useEffect } from 'react';
import { useCan } from '../context/CanContext';
import { api, downloadBlobAsFile } from '../services/api';
import { 
  DiagnosticExchange, 
  DiagnosticTroubleCode, 
  EcuIdentification, 
  ObdPidValue, 
  ObdPidDefinition, 
  VirtualEcu 
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
  Zap, Lock, ArrowRight, Sliders, Camera, HelpCircle, ExternalLink, Compass, Flame, Sparkles
} from 'lucide-react';

export const DiagnosticsWorkspacePage: React.FC = () => {
  const { operatingMode } = useCan();
  const [subTab, setSubTab] = useState<
    'trace' | 'obd' | 'uds' | 'dtc' | 'freeze-frame' | 'vag-blocks' | 'manufacturer' | 'ecuinfo' | 'simulator'
  >('trace');
  
  // Core Diagnostics Data state
  const [exchanges, setExchanges] = useState<DiagnosticExchange[]>([]);
  const [dtcs, setDtcs] = useState<DiagnosticTroubleCode[]>([]);
  const [ecuList, setEcuList] = useState<EcuIdentification[]>([]);
  const [obdValues, setObdValues] = useState<ObdPidValue[]>([]);
  const [obdDefs, setObdDefs] = useState<ObdPidDefinition[]>([]);
  const [simulatorStatus, setSimulatorStatus] = useState<{ isEnabled: boolean; virtualEcus: VirtualEcu[] } | null>(null);

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
    setIsSending(true);
    setDtcActionMessage(`Transmitting clear commands (UDS 0x14 FF FF FF & OBD-II Mode 04) to ${targetEcu}...`);
    try {
      const reqId = ecuCanIdMap[targetEcu] || 0x7E0;
      await api.clearDtcs(reqId, targetEcu, isSimulation);
      await new Promise(r => setTimeout(r, 600));
      const refreshed = await api.getActiveDtcs();
      setDtcs(refreshed);
      setDtcActionMessage(`Clear command acknowledged! Active fault codes: ${refreshed.length}`);
      await fetchAllDiagnosticsData();
    } catch {
      setDtcActionMessage('Clear DTC command rejected or communication error.');
    } finally {
      setIsSending(false);
      setTimeout(() => setDtcActionMessage(null), 5000);
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

        {/* VIEW 2: OBD-II DASHBOARD */}
        {subTab === 'obd' && (
          <div className="p-5 space-y-4 overflow-y-auto h-full">
            <div className="flex justify-between items-center border-b border-can-border pb-3 font-sans">
              <div>
                <h2 className="text-sm font-bold text-white uppercase">STANDARD OBD-II (SAE J1979 / ISO 15031-5) LIVE METRICS</h2>
                <p className="text-xs text-slate-400 mt-0.5">Continuous broadcast PID decoding via diagnostic transport</p>
              </div>
              <button
                onClick={() => handleSendCustomHex('01 00')}
                className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold"
              >
                Poll Supported PIDs
              </button>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-3 font-sans">
              {obdValues.map(v => (
                <div key={v.pid} className="bg-can-bg border border-can-border rounded-lg p-3.5 shadow flex flex-col justify-between">
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
            </div>
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
                  onClick={handleClearDtcs}
                  disabled={isSending || isScanningDtcs}
                  className="px-3.5 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-bold flex items-center space-x-1.5 shadow"
                  title="Purge NVRAM fault codes using UDS 0x14 FF FF FF and OBD-II Mode 04"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                  <span>{isSending ? 'Clearing Faults...' : 'Clear All Faults (0x14 + Mode 04)'}</span>
                </button>
              </div>
            </div>

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
                      <span className="text-lg font-bold text-rose-400 font-mono">{dtc.code}</span>
                      <span className="text-[10px] text-slate-400 bg-slate-800 px-2 py-0.5 rounded font-semibold">
                        ECU: {dtc.ecuName} (0x{dtc.canId.toString(16).toUpperCase()})
                      </span>
                    </div>
                    <span className="text-xs font-mono text-slate-500">Status: 0x{dtc.statusByte.toString(16).toUpperCase().padStart(2, '0')}</span>
                  </div>

                  <p className="text-xs text-slate-200 font-medium">{dtc.description}</p>

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
    </div>
  );
};
