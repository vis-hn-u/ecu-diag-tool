import React, { useState, useEffect, useRef, useMemo } from 'react';
import { 
  Binary, 
  Play, 
  Pause, 
  Trash2, 
  Filter, 
  Send, 
  ShieldAlert, 
  ShieldCheck, 
  Download, 
  Activity, 
  Layers, 
  Search, 
  RefreshCw,
  Cpu,
  ArrowDownUp,
  Clock,
  Flame,
  CheckCircle2
} from 'lucide-react';
import { useCan } from '../context/CanContext';
import { api } from '../services/api';
import { CanFrame } from '../types';

interface GroupedFrame {
  id: number;
  isExtended: boolean;
  dlc: number;
  data: number[];
  prevData?: number[];
  changedMask: boolean[];
  count: number;
  lastTimestampUs: number;
  periodMs: number;
  channel: number;
}

export const SavvyCanAnalysisPage: React.FC = () => {
  const { frames, connectionState, hardwareStatus } = useCan();
  const isConnected = connectionState === 'Connected' && (hardwareStatus?.isConnected ?? false);
  const activeTransport = hardwareStatus;
  const transmitFrame = api.transmitCanFrame;

  // View tabs
  type SavvySubTab = 'monitor' | 'datagrid' | 'sniffer' | 'sender';
  const [subTab, setSubTab] = useState<SavvySubTab>('monitor');

  // Streaming control
  const [isPaused, setIsPaused] = useState(false);
  const [autoScroll, setAutoScroll] = useState(true);
  const [searchFilter, setSearchFilter] = useState('');
  const [idFilterMin, setIdFilterMin] = useState('');
  const [idFilterMax, setIdFilterMax] = useState('');
  const [busFilter, setBusFilter] = useState<string>('all');

  // Buffer for raw monitor table (capped to 2,000 frames)
  const [displayFrames, setDisplayFrames] = useState<CanFrame[]>([]);
  const tableBottomRef = useRef<HTMLDivElement>(null);

  // Grouped state for Sniffer & Data Grid
  const [groupedMap, setGroupedMap] = useState<Map<number, GroupedFrame>>(new Map());
  const [frozenMap, setFrozenMap] = useState<Map<number, number[]>>(new Map());
  const [selectedGridId, setSelectedGridId] = useState<number | null>(null);

  // Frame sender state
  const [txId, setTxId] = useState('7DF');
  const [txExtended, setTxExtended] = useState(false);
  const [txDlc, setTxDlc] = useState(8);
  const [txData, setTxData] = useState('02 01 00 00 00 00 00 00');
  const [txIntervalMs, setTxIntervalMs] = useState(100);
  const [isPeriodicTx, setIsPeriodicTx] = useState(false);
  const [txStatusMsg, setTxStatusMsg] = useState<string | null>(null);

  // Hardware listen-only state from telemetry
  const isListenOnly = useMemo(() => {
    if (activeTransport?.extendedTelemetry) {
      const tele = activeTransport.extendedTelemetry as any;
      if (typeof tele.listenOnly === 'boolean') return tele.listenOnly;
      if (typeof tele.ListenOnly === 'boolean') return tele.ListenOnly;
    }
    return !activeTransport?.capabilities?.supportsTransmit;
  }, [activeTransport]);

  const [togglingListenOnly, setTogglingListenOnly] = useState(false);

  // Throttled frame ingestion
  useEffect(() => {
    if (isPaused || frames.length === 0) return;

    const latest = frames[frames.length - 1];

    // Update raw display frames (sliding window of 1,000)
    setDisplayFrames(prev => {
      const next = [...prev, latest];
      return next.length > 1000 ? next.slice(next.length - 1000) : next;
    });

    // Update grouped map
    setGroupedMap(prev => {
      const next = new Map(prev);
      const existing = next.get(latest.id);
      const nowUs = latest.timestampUs || Date.now() * 1000;
      const periodMs = existing && existing.lastTimestampUs > 0 
        ? Math.round((nowUs - existing.lastTimestampUs) / 1000) 
        : 0;

      const changedMask = [false, false, false, false, false, false, false, false];
      if (existing) {
        for (let i = 0; i < Math.min(latest.dlc, 8); i++) {
          if (existing.data[i] !== (latest.data[i] ?? 0)) {
            changedMask[i] = true;
          }
        }
      }

      next.set(latest.id, {
        id: latest.id,
        isExtended: latest.isExtended,
        dlc: latest.dlc,
        data: [...latest.data],
        prevData: existing ? [...existing.data] : undefined,
        changedMask,
        count: (existing?.count || 0) + 1,
        lastTimestampUs: nowUs,
        periodMs: periodMs > 0 ? periodMs : (existing?.periodMs || 0),
        channel: latest.channel ?? 0
      });
      return next;
    });
  }, [frames, isPaused]);

  // Auto-scroll
  useEffect(() => {
    if (autoScroll && subTab === 'monitor') {
      tableBottomRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [displayFrames, autoScroll, subTab]);

  // Periodic transmitter timer
  useEffect(() => {
    if (!isPeriodicTx) return;
    const interval = setInterval(() => {
      handleSendFrame();
    }, Math.max(20, txIntervalMs));
    return () => clearInterval(interval);
  }, [isPeriodicTx, txIntervalMs, txId, txExtended, txDlc, txData]);

  const handleToggleListenOnly = async () => {
    setTogglingListenOnly(true);
    try {
      await api.setGvretListenOnly(!isListenOnly);
      setTxStatusMsg(`Hardware mode switched: ${!isListenOnly ? 'LISTEN-ONLY' : 'TRANSMIT ARMED'}`);
    } catch {
      setTxStatusMsg('Failed to toggle hardware listen-only mode');
    } finally {
      setTogglingListenOnly(false);
      setTimeout(() => setTxStatusMsg(null), 3000);
    }
  };

  const handleSendFrame = async () => {
    if (isListenOnly) {
      setTxStatusMsg('ERROR: Hardware is armed in LISTEN-ONLY mode. Disable listen-only to transmit.');
      setIsPeriodicTx(false);
      return;
    }

    try {
      const idNum = parseInt(txId.trim(), 16);
      if (isNaN(idNum)) {
        setTxStatusMsg('Invalid CAN ID');
        return;
      }

      const bytes = txData.trim().split(/\s+/).map(h => parseInt(h, 16)).filter(n => !isNaN(n));
      while (bytes.length < txDlc) bytes.push(0);

      const frame: CanFrame = {
        timestampUs: Date.now() * 1000,
        id: idNum,
        dlc: txDlc,
        flags: txExtended ? 0x01 : 0x00,
        data: bytes.slice(0, txDlc),
        isExtended: txExtended,
        isRtr: false,
        direction: 'Tx',
        channel: 0,
        sourceId: 'SavvyCAN-Sender'
      };

      await transmitFrame(frame);
      setTxStatusMsg(`Sent Frame 0x${idNum.toString(16).toUpperCase()}`);
    } catch (err: any) {
      setTxStatusMsg(`TX Error: ${err.message || 'Failed to send'}`);
      setIsPeriodicTx(false);
    }
  };

  const handleFreezeBaseline = () => {
    const snap = new Map<number, number[]>();
    groupedMap.forEach((g, id) => {
      snap.set(id, [...g.data]);
    });
    setFrozenMap(snap);
  };

  const handleClearBaseline = () => {
    setFrozenMap(new Map());
  };

  const handleExportCsv = () => {
    const rows = [
      ['Timestamp_us', 'ID_Hex', 'Type', 'Bus', 'DLC', 'D0', 'D1', 'D2', 'D3', 'D4', 'D5', 'D6', 'D7']
    ];
    displayFrames.forEach(f => {
      const d = f.data || [];
      rows.push([
        f.timestampUs.toString(),
        '0x' + f.id.toString(16).toUpperCase(),
        f.isExtended ? 'Extended' : 'Standard',
        (f.channel ?? 0).toString(),
        f.dlc.toString(),
        ...(d.map(b => b.toString(16).padStart(2, '0').toUpperCase())),
        ...(Array(Math.max(0, 8 - d.length)).fill(''))
      ]);
    });

    const csvContent = 'data:text/csv;charset=utf-8,' + rows.map(r => r.join(',')).join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `savvycan_capture_${Date.now()}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  // Filtered frames for Monitor
  const filteredFrames = useMemo(() => {
    return displayFrames.filter(f => {
      const idHex = f.id.toString(16).toUpperCase();
      if (searchFilter && !idHex.includes(searchFilter.toUpperCase())) return false;
      if (busFilter !== 'all' && (f.channel ?? 0).toString() !== busFilter) return false;
      if (idFilterMin) {
        const minVal = parseInt(idFilterMin, 16);
        if (!isNaN(minVal) && f.id < minVal) return false;
      }
      if (idFilterMax) {
        const maxVal = parseInt(idFilterMax, 16);
        if (!isNaN(maxVal) && f.id > maxVal) return false;
      }
      return true;
    });
  }, [displayFrames, searchFilter, busFilter, idFilterMin, idFilterMax]);

  // Grouped list for Sniffer
  const groupedList = useMemo(() => {
    return Array.from(groupedMap.values()).sort((a, b) => a.id - b.id);
  }, [groupedMap]);

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-100 overflow-hidden font-sans">
      {/* Top Header & Telemetry Bar */}
      <div className="p-3 bg-can-panel border-b border-can-border flex flex-wrap items-center justify-between gap-3 shadow-md">
        <div className="flex items-center space-x-3">
          <div className="w-9 h-9 rounded-lg bg-indigo-500/15 border border-indigo-500/30 flex items-center justify-center text-indigo-400 shadow-sm">
            <Binary className="w-5 h-5" />
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <h1 className="text-sm font-bold text-white tracking-wide font-sans">SAVVYCAN ANALYSIS SUITE</h1>
              <span className="px-1.5 py-0.2 rounded text-[10px] font-bold bg-indigo-500/20 text-indigo-300 border border-indigo-500/40 font-mono">
                GVRET ENGINE
              </span>
            </div>
            <div className="flex items-center space-x-3 text-[11px] text-slate-400 font-mono">
              <span>Adapter: <span className="text-slate-200">{activeTransport?.name || 'None'}</span></span>
              <span>Port: <span className="text-sky-300 font-bold">{activeTransport?.connectionDetails || 'COM8'}</span></span>
              <span>Bus: <span className="text-emerald-400 font-bold">500 kbps (8 MHz)</span></span>
            </div>
          </div>
        </div>

        {/* Listen-Only Safety Pill & Quick Action Controls */}
        <div className="flex items-center space-x-2">
          {/* Hardware Mode Button */}
          <button
            onClick={handleToggleListenOnly}
            disabled={togglingListenOnly}
            className={`flex items-center space-x-1.5 px-2.5 py-1 rounded text-xs font-bold border transition-all ${
              isListenOnly
                ? 'bg-amber-500/15 text-amber-300 border-amber-500/40 hover:bg-amber-500/25'
                : 'bg-emerald-500/15 text-emerald-300 border-emerald-500/40 hover:bg-emerald-500/25'
            }`}
          >
            {isListenOnly ? (
              <>
                <ShieldAlert className="w-3.5 h-3.5 text-amber-400" />
                <span>LISTEN-ONLY (SAFE)</span>
              </>
            ) : (
              <>
                <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
                <span>TRANSMIT ARMED</span>
              </>
            )}
          </button>

          {/* Pause / Resume */}
          <button
            onClick={() => setIsPaused(!isPaused)}
            className={`flex items-center space-x-1 px-2.5 py-1 rounded text-xs font-semibold border ${
              isPaused 
                ? 'bg-emerald-600/20 text-emerald-400 border-emerald-500/40 hover:bg-emerald-600/30' 
                : 'bg-slate-800 text-slate-300 border-slate-700 hover:bg-slate-700'
            }`}
          >
            {isPaused ? <Play className="w-3.5 h-3.5" /> : <Pause className="w-3.5 h-3.5" />}
            <span>{isPaused ? 'Resume' : 'Pause'}</span>
          </button>

          {/* Clear Buffer */}
          <button
            onClick={() => {
              setDisplayFrames([]);
              setGroupedMap(new Map());
            }}
            className="flex items-center space-x-1 px-2.5 py-1 rounded text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700"
          >
            <Trash2 className="w-3.5 h-3.5 text-rose-400" />
            <span>Clear</span>
          </button>

          {/* CSV Export */}
          <button
            onClick={handleExportCsv}
            disabled={displayFrames.length === 0}
            className="flex items-center space-x-1 px-2.5 py-1 rounded text-xs font-semibold bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 border border-indigo-500/40 disabled:opacity-50"
          >
            <Download className="w-3.5 h-3.5" />
            <span>Export CSV</span>
          </button>
        </div>
      </div>

      {/* Sub-Navigation Tabs */}
      <div className="bg-can-panel/60 border-b border-can-border px-3 py-1 flex items-center justify-between">
        <div className="flex space-x-1 text-xs">
          <button
            onClick={() => setSubTab('monitor')}
            className={`px-3 py-1 rounded font-medium transition-all ${
              subTab === 'monitor' 
                ? 'bg-indigo-600/20 text-indigo-300 border border-indigo-500/40 font-bold' 
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            CAN Monitor
          </button>
          <button
            onClick={() => setSubTab('datagrid')}
            className={`px-3 py-1 rounded font-medium transition-all ${
              subTab === 'datagrid' 
                ? 'bg-indigo-600/20 text-indigo-300 border border-indigo-500/40 font-bold' 
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            Byte Heat Map (Data Grid)
          </button>
          <button
            onClick={() => setSubTab('sniffer')}
            className={`px-3 py-1 rounded font-medium transition-all ${
              subTab === 'sniffer' 
                ? 'bg-indigo-600/20 text-indigo-300 border border-indigo-500/40 font-bold' 
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            Reverse Engineering Sniffer
          </button>
          <button
            onClick={() => setSubTab('sender')}
            className={`px-3 py-1 rounded font-medium transition-all ${
              subTab === 'sender' 
                ? 'bg-indigo-600/20 text-indigo-300 border border-indigo-500/40 font-bold' 
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            Frame Sender
          </button>
        </div>

        {/* Global Stats */}
        <div className="flex items-center space-x-3 text-[11px] font-mono text-slate-400">
          <span>Frames: <strong className="text-white">{displayFrames.length}</strong></span>
          <span>Unique IDs: <strong className="text-sky-300">{groupedMap.size}</strong></span>
          <span>Status: <strong className={isConnected ? 'text-emerald-400' : 'text-slate-500'}>{isConnected ? 'LIVE' : 'DISCONNECTED'}</strong></span>
        </div>
      </div>

      {/* Main Tab Views */}
      <div className="flex-1 overflow-hidden p-3">
        {/* TAB 1: CAN MONITOR */}
        {subTab === 'monitor' && (
          <div className="h-full flex flex-col bg-can-panel border border-can-border rounded-lg overflow-hidden shadow-lg">
            {/* Filter Toolbar */}
            <div className="p-2 border-b border-can-border bg-slate-950/40 flex flex-wrap items-center justify-between gap-2 text-xs">
              <div className="flex items-center space-x-2">
                <div className="relative">
                  <Search className="w-3.5 h-3.5 absolute left-2 top-2 text-slate-500" />
                  <input
                    type="text"
                    value={searchFilter}
                    onChange={e => setSearchFilter(e.target.value)}
                    placeholder="Search ID (Hex)..."
                    className="pl-7 pr-2 py-1 bg-can-card border border-can-border rounded text-slate-200 font-mono text-xs w-36 focus:outline-none focus:border-indigo-500"
                  />
                </div>

                <div className="flex items-center space-x-1 text-slate-400 font-mono text-[11px]">
                  <span>ID:</span>
                  <input
                    type="text"
                    value={idFilterMin}
                    onChange={e => setIdFilterMin(e.target.value)}
                    placeholder="Min"
                    className="w-14 px-1 py-1 bg-can-card border border-can-border rounded text-slate-200 text-xs font-mono"
                  />
                  <span>-</span>
                  <input
                    type="text"
                    value={idFilterMax}
                    onChange={e => setIdFilterMax(e.target.value)}
                    placeholder="Max"
                    className="w-14 px-1 py-1 bg-can-card border border-can-border rounded text-slate-200 text-xs font-mono"
                  />
                </div>

                <select
                  value={busFilter}
                  onChange={e => setBusFilter(e.target.value)}
                  className="bg-can-card border border-can-border rounded px-2 py-1 text-slate-300 text-xs font-mono"
                >
                  <option value="all">All Buses</option>
                  <option value="0">Bus 0</option>
                  <option value="1">Bus 1</option>
                </select>
              </div>

              <div className="flex items-center space-x-3 text-xs">
                <label className="flex items-center space-x-1.5 cursor-pointer text-slate-300 select-none">
                  <input
                    type="checkbox"
                    checked={autoScroll}
                    onChange={e => setAutoScroll(e.target.checked)}
                    className="rounded bg-can-card border-can-border text-indigo-500"
                  />
                  <span>Auto-scroll</span>
                </label>
              </div>
            </div>

            {/* Virtualized Frame Table */}
            <div className="flex-1 overflow-auto font-mono text-xs">
              <table className="w-full text-left border-collapse">
                <thead className="sticky top-0 bg-slate-900 border-b border-can-border text-slate-400 text-[11px] select-none z-10">
                  <tr>
                    <th className="p-2 w-24">TIME (US)</th>
                    <th className="p-2 w-20">CAN ID</th>
                    <th className="p-2 w-16">TYPE</th>
                    <th className="p-2 w-12">BUS</th>
                    <th className="p-2 w-12">DLC</th>
                    <th className="p-2">DATA (HEX)</th>
                    <th className="p-2 w-28">ASCII</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60">
                  {filteredFrames.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="p-8 text-center text-slate-500 font-sans">
                        No frames matching filters or awaiting incoming bus traffic on COM8...
                      </td>
                    </tr>
                  ) : (
                    filteredFrames.map((f, idx) => {
                      const idHex = '0x' + f.id.toString(16).toUpperCase().padStart(f.isExtended ? 8 : 3, '0');
                      const dataBytes = f.data || [];
                      const ascii = dataBytes.map(b => (b >= 32 && b <= 126 ? String.fromCharCode(b) : '.')).join('');

                      return (
                        <tr key={idx} className="hover:bg-indigo-950/20 transition-colors">
                          <td className="p-1.5 text-slate-400 text-[11px]">{f.timestampUs}</td>
                          <td className="p-1.5 font-bold text-sky-400">{idHex}</td>
                          <td className="p-1.5 text-slate-400 text-[10px]">
                            {f.isExtended ? <span className="text-purple-400">EXT</span> : 'STD'}
                          </td>
                          <td className="p-1.5 text-slate-300">{f.channel ?? 0}</td>
                          <td className="p-1.5 text-amber-400 font-bold">{f.dlc}</td>
                          <td className="p-1.5 text-emerald-300 tracking-wider">
                            {dataBytes.map((b, bIdx) => (
                              <span key={bIdx} className="mr-1.5 inline-block">
                                {b.toString(16).padStart(2, '0').toUpperCase()}
                              </span>
                            ))}
                          </td>
                          <td className="p-1.5 text-slate-400 text-[11px] truncate">{ascii}</td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
              <div ref={tableBottomRef} />
            </div>
          </div>
        )}

        {/* TAB 2: SAVVYCAN BYTE HEAT MAP (DATA GRID) */}
        {subTab === 'datagrid' && (
          <div className="h-full flex flex-col md:flex-row gap-3 overflow-hidden">
            {/* ID Selector List */}
            <div className="w-full md:w-64 bg-can-panel border border-can-border rounded-lg flex flex-col overflow-hidden">
              <div className="p-2 border-b border-can-border bg-slate-900 text-xs font-bold text-slate-300">
                ACTIVE CAN IDs ({groupedList.length})
              </div>
              <div className="flex-1 overflow-auto divide-y divide-slate-800 text-xs font-mono">
                {groupedList.map(g => {
                  const idHex = '0x' + g.id.toString(16).toUpperCase().padStart(g.isExtended ? 8 : 3, '0');
                  const isSelected = selectedGridId === g.id;
                  return (
                    <button
                      key={g.id}
                      onClick={() => setSelectedGridId(g.id)}
                      className={`w-full p-2 flex items-center justify-between text-left transition-colors ${
                        isSelected 
                          ? 'bg-indigo-600/30 text-indigo-300 font-bold border-l-2 border-indigo-400' 
                          : 'hover:bg-slate-800/60 text-slate-300'
                      }`}
                    >
                      <span>{idHex}</span>
                      <span className="text-[10px] text-slate-500">{g.count} msgs</span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Heat Map Visualization Panel */}
            <div className="flex-1 bg-can-panel border border-can-border rounded-lg p-4 flex flex-col overflow-auto">
              {selectedGridId === null ? (
                <div className="flex-1 flex items-center justify-center text-slate-500 font-sans text-xs">
                  Select a CAN ID from the left panel to inspect real-time byte & bit activity
                </div>
              ) : (
                (() => {
                  const item = groupedMap.get(selectedGridId);
                  if (!item) return null;
                  const idHex = '0x' + item.id.toString(16).toUpperCase();

                  return (
                    <div className="space-y-4">
                      <div className="flex items-center justify-between border-b border-can-border pb-2">
                        <div>
                          <h2 className="text-base font-bold text-white font-mono">{idHex}</h2>
                          <span className="text-xs text-slate-400">
                            Period: ~{item.periodMs} ms | Total Frames: {item.count} | Bus {item.channel}
                          </span>
                        </div>
                        <span className="px-2 py-0.5 rounded text-xs bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 font-bold">
                          DLC: {item.dlc}
                        </span>
                      </div>

                      {/* 8-Byte Grid */}
                      <div className="grid grid-cols-4 md:grid-cols-8 gap-3 font-mono">
                        {Array.from({ length: 8 }).map((_, byteIdx) => {
                          const val = item.data[byteIdx] ?? 0;
                          const changed = item.changedMask[byteIdx];
                          const hexVal = val.toString(16).padStart(2, '0').toUpperCase();
                          const binVal = val.toString(2).padStart(8, '0');

                          return (
                            <div
                              key={byteIdx}
                              className={`p-3 rounded-lg border flex flex-col items-center justify-center space-y-1.5 transition-all ${
                                byteIdx >= item.dlc
                                  ? 'bg-slate-900/40 border-slate-800/40 text-slate-600'
                                  : changed
                                  ? 'bg-amber-500/20 border-amber-500/50 shadow-md shadow-amber-950/20 ring-1 ring-amber-400'
                                  : 'bg-can-card border-can-border text-slate-200'
                              }`}
                            >
                              <span className="text-[10px] text-slate-500 font-bold">BYTE {byteIdx}</span>
                              <span className={`text-xl font-bold ${changed ? 'text-amber-300 animate-pulse' : 'text-emerald-400'}`}>
                                {hexVal}
                              </span>
                              <span className="text-[10px] tracking-wider text-slate-400 font-mono">
                                {binVal}
                              </span>
                              <span className="text-[9px] text-slate-500">
                                Dec: {val}
                              </span>
                            </div>
                          );
                        })}
                      </div>

                      <div className="p-3 bg-slate-900/60 rounded border border-slate-800 text-xs text-slate-400 space-y-1">
                        <div className="font-bold text-slate-300">SavvyCAN Bit Activity Legend:</div>
                        <div className="flex items-center space-x-4 text-[11px]">
                          <span className="flex items-center space-x-1">
                            <span className="w-2.5 h-2.5 rounded bg-emerald-500 inline-block" />
                            <span>Static Payload Byte</span>
                          </span>
                          <span className="flex items-center space-x-1">
                            <span className="w-2.5 h-2.5 rounded bg-amber-400 inline-block" />
                            <span>Actively Mutating / Toggling Byte</span>
                          </span>
                          <span className="flex items-center space-x-1">
                            <span className="w-2.5 h-2.5 rounded bg-slate-700 inline-block" />
                            <span>Unused DLC Bytes</span>
                          </span>
                        </div>
                      </div>
                    </div>
                  );
                })()
              )}
            </div>
          </div>
        )}

        {/* TAB 3: REVERSE ENGINEERING SNIFFER */}
        {subTab === 'sniffer' && (
          <div className="h-full flex flex-col bg-can-panel border border-can-border rounded-lg overflow-hidden shadow-lg">
            {/* Sniffer Controls */}
            <div className="p-3 border-b border-can-border bg-slate-950/40 flex items-center justify-between text-xs">
              <div className="flex items-center space-x-2">
                <button
                  onClick={handleFreezeBaseline}
                  className="px-3 py-1.5 rounded font-bold bg-sky-600 hover:bg-sky-500 text-white border border-sky-400 shadow-sm"
                >
                  Freeze Baseline Snapshot
                </button>
                <button
                  onClick={handleClearBaseline}
                  disabled={frozenMap.size === 0}
                  className="px-3 py-1.5 rounded font-bold bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 disabled:opacity-50"
                >
                  Clear Baseline
                </button>
                <span className="text-slate-400 font-mono text-[11px] ml-2">
                  {frozenMap.size > 0 ? `Baseline Frozen (${frozenMap.size} IDs)` : 'No baseline snapshot active'}
                </span>
              </div>

              <span className="text-[11px] text-slate-400 font-mono">
                Highlights bytes that changed since the snapshot was taken
              </span>
            </div>

            {/* Grouped Sniffer Table */}
            <div className="flex-1 overflow-auto font-mono text-xs">
              <table className="w-full text-left border-collapse">
                <thead className="sticky top-0 bg-slate-900 border-b border-can-border text-slate-400 text-[11px] select-none">
                  <tr>
                    <th className="p-2 w-24">CAN ID</th>
                    <th className="p-2 w-16">BUS</th>
                    <th className="p-2 w-16">PERIOD</th>
                    <th className="p-2 w-16">COUNT</th>
                    <th className="p-2">CURRENT DATA (HEX)</th>
                    <th className="p-2">BASELINE SNAPSHOT</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800">
                  {groupedList.map(g => {
                    const idHex = '0x' + g.id.toString(16).toUpperCase().padStart(g.isExtended ? 8 : 3, '0');
                    const frozen = frozenMap.get(g.id);

                    return (
                      <tr key={g.id} className="hover:bg-indigo-950/20">
                        <td className="p-2 font-bold text-sky-400">{idHex}</td>
                        <td className="p-2 text-slate-400">{g.channel}</td>
                        <td className="p-2 text-slate-300">{g.periodMs} ms</td>
                        <td className="p-2 text-slate-500">{g.count}</td>
                        <td className="p-2">
                          <div className="flex space-x-1.5">
                            {g.data.map((b, idx) => {
                              const isDiff = frozen && frozen[idx] !== b;
                              return (
                                <span
                                  key={idx}
                                  className={`px-1 rounded text-xs ${
                                    isDiff 
                                      ? 'bg-amber-500 text-slate-950 font-bold animate-pulse' 
                                      : 'text-emerald-400'
                                  }`}
                                >
                                  {b.toString(16).padStart(2, '0').toUpperCase()}
                                </span>
                              );
                            })}
                          </div>
                        </td>
                        <td className="p-2 text-slate-500">
                          {frozen ? (
                            <div className="flex space-x-1.5">
                              {frozen.map((b, idx) => (
                                <span key={idx}>
                                  {b.toString(16).padStart(2, '0').toUpperCase()}
                                </span>
                              ))}
                            </div>
                          ) : (
                            <span className="italic text-slate-600">None</span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* TAB 4: FRAME SENDER */}
        {subTab === 'sender' && (
          <div className="h-full flex flex-col md:flex-row gap-4 overflow-hidden">
            {/* Sender Configuration Form */}
            <div className="w-full md:w-96 bg-can-panel border border-can-border rounded-lg p-4 space-y-4 overflow-auto">
              <div className="flex items-center space-x-2 border-b border-can-border pb-2.5">
                <Send className="w-4 h-4 text-indigo-400" />
                <h3 className="text-sm font-bold text-white">ARBITRATED FRAME SENDER</h3>
              </div>

              {/* Listen-Only Safety Warning */}
              {isListenOnly && (
                <div className="p-3 bg-amber-500/15 border border-amber-500/40 rounded-lg text-xs space-y-2">
                  <div className="flex items-center space-x-1.5 text-amber-300 font-bold">
                    <ShieldAlert className="w-4 h-4 text-amber-400" />
                    <span>TRANSMISSION LOCKED</span>
                  </div>
                  <p className="text-slate-300 text-[11px]">
                    Hardware is currently locked in Listen-Only mode to protect vehicle ECUs. To transmit frames, switch hardware mode to Normal.
                  </p>
                  <button
                    onClick={handleToggleListenOnly}
                    disabled={togglingListenOnly}
                    className="w-full py-1 rounded bg-amber-600 hover:bg-amber-500 text-white font-bold text-xs shadow"
                  >
                    Arm Hardware for Transmission
                  </button>
                </div>
              )}

              {/* Status Banner */}
              {txStatusMsg && (
                <div className="p-2 bg-slate-900 rounded border border-slate-700 text-xs text-sky-300 font-mono">
                  {txStatusMsg}
                </div>
              )}

              {/* CAN ID */}
              <div>
                <label className="text-slate-400 text-xs block mb-1">CAN ID (Hex):</label>
                <div className="flex space-x-2">
                  <input
                    type="text"
                    value={txId}
                    onChange={e => setTxId(e.target.value)}
                    placeholder="7DF"
                    className="flex-1 bg-can-card border border-can-border rounded px-2.5 py-1.5 text-white font-mono text-xs focus:outline-none focus:border-indigo-500"
                  />
                  <label className="flex items-center space-x-1.5 px-2.5 py-1 bg-can-card border border-can-border rounded text-xs cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={txExtended}
                      onChange={e => setTxExtended(e.target.checked)}
                      className="rounded"
                    />
                    <span>Extended (29-bit)</span>
                  </label>
                </div>
              </div>

              {/* DLC */}
              <div>
                <label className="text-slate-400 text-xs block mb-1">DLC (Data Length):</label>
                <input
                  type="number"
                  min={0}
                  max={8}
                  value={txDlc}
                  onChange={e => setTxDlc(parseInt(e.target.value, 10))}
                  className="w-full bg-can-card border border-can-border rounded px-2.5 py-1.5 text-white font-mono text-xs focus:outline-none focus:border-indigo-500"
                />
              </div>

              {/* Data Payload */}
              <div>
                <label className="text-slate-400 text-xs block mb-1">Payload (Hex Bytes separated by spaces):</label>
                <input
                  type="text"
                  value={txData}
                  onChange={e => setTxData(e.target.value)}
                  placeholder="02 01 00 00 00 00 00 00"
                  className="w-full bg-can-card border border-can-border rounded px-2.5 py-1.5 text-emerald-400 font-mono text-xs focus:outline-none focus:border-indigo-500"
                />
              </div>

              {/* Periodic Transmit Controls */}
              <div className="p-3 bg-slate-900/60 rounded border border-slate-800 space-y-2">
                <label className="flex items-center justify-between text-xs text-slate-300 cursor-pointer">
                  <span>Periodic Loop Mode:</span>
                  <input
                    type="checkbox"
                    checked={isPeriodicTx}
                    onChange={e => setIsPeriodicTx(e.target.checked)}
                    disabled={isListenOnly}
                    className="rounded text-indigo-500"
                  />
                </label>
                {isPeriodicTx && (
                  <div className="flex items-center space-x-2 text-xs">
                    <span className="text-slate-400">Interval:</span>
                    <input
                      type="number"
                      min={20}
                      step={10}
                      value={txIntervalMs}
                      onChange={e => setTxIntervalMs(parseInt(e.target.value, 10))}
                      className="w-20 bg-can-card border border-can-border rounded px-2 py-1 text-white font-mono text-xs"
                    />
                    <span className="text-slate-400">ms</span>
                  </div>
                )}
              </div>

              {/* Action Button */}
              <button
                type="button"
                onClick={handleSendFrame}
                disabled={isListenOnly}
                className="w-full py-2.5 rounded font-bold text-xs bg-indigo-600 hover:bg-indigo-500 text-white border border-indigo-400 shadow-md transition-all disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center space-x-2"
              >
                <Send className="w-4 h-4" />
                <span>{isPeriodicTx ? 'TRANSMITTING PERIODICALLY...' : 'TRANSMIT SINGLE FRAME'}</span>
              </button>
            </div>

            {/* Presets & Standard Request Patterns */}
            <div className="flex-1 bg-can-panel border border-can-border rounded-lg p-4 space-y-3 overflow-auto">
              <h3 className="text-xs font-bold text-slate-300 uppercase tracking-wide">Standard Automotive Query Templates</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs font-mono">
                <button
                  type="button"
                  onClick={() => {
                    setTxId('7DF');
                    setTxExtended(false);
                    setTxDlc(8);
                    setTxData('02 01 00 00 00 00 00 00');
                  }}
                  className="p-2.5 rounded bg-can-card hover:bg-slate-800 border border-can-border text-left space-y-1 transition-colors"
                >
                  <div className="font-bold text-sky-400">OBD-II Supported PIDs (01 00)</div>
                  <div className="text-[11px] text-slate-400">ID: 0x7DF | 02 01 00 00 00 00 00 00</div>
                </button>

                <button
                  type="button"
                  onClick={() => {
                    setTxId('7DF');
                    setTxExtended(false);
                    setTxDlc(8);
                    setTxData('02 01 0C 00 00 00 00 00');
                  }}
                  className="p-2.5 rounded bg-can-card hover:bg-slate-800 border border-can-border text-left space-y-1 transition-colors"
                >
                  <div className="font-bold text-sky-400">OBD-II Engine RPM (01 0C)</div>
                  <div className="text-[11px] text-slate-400">ID: 0x7DF | 02 01 0C 00 00 00 00 00</div>
                </button>

                <button
                  type="button"
                  onClick={() => {
                    setTxId('7DF');
                    setTxExtended(false);
                    setTxDlc(8);
                    setTxData('01 03 00 00 00 00 00 00');
                  }}
                  className="p-2.5 rounded bg-can-card hover:bg-slate-800 border border-can-border text-left space-y-1 transition-colors"
                >
                  <div className="font-bold text-amber-400">Read Stored DTCs (Mode 03)</div>
                  <div className="text-[11px] text-slate-400">ID: 0x7DF | 01 03 00 00 00 00 00 00</div>
                </button>

                <button
                  type="button"
                  onClick={() => {
                    setTxId('7DF');
                    setTxExtended(false);
                    setTxDlc(8);
                    setTxData('02 09 02 00 00 00 00 00');
                  }}
                  className="p-2.5 rounded bg-can-card hover:bg-slate-800 border border-can-border text-left space-y-1 transition-colors"
                >
                  <div className="font-bold text-emerald-400">Read Vehicle VIN (Mode 09 02)</div>
                  <div className="text-[11px] text-slate-400">ID: 0x7DF | 02 09 02 00 00 00 00 00</div>
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
