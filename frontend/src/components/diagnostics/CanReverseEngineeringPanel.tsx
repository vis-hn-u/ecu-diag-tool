import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { CanIdDeepMetrics, DiscoveredSignalCandidate } from '../../types';
import { 
  Binary, 
  RefreshCw, 
  Activity, 
  Search, 
  ShieldCheck, 
  Layers, 
  Sliders, 
  Zap, 
  TrendingUp, 
  Hash, 
  Sparkles,
  CheckCircle2,
  AlertTriangle
} from 'lucide-react';

export const CanReverseEngineeringPanel: React.FC = () => {
  const [metrics, setMetrics] = useState<CanIdDeepMetrics[]>([]);
  const [signals, setSignals] = useState<DiscoveredSignalCandidate[]>([]);
  const [selectedCanId, setSelectedCanId] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [baselineSet, setBaselineSet] = useState(false);
  const [activeTab, setActiveTab] = useState<'metrics' | 'signals' | 'entropy'>('metrics');

  useEffect(() => {
    loadData();
    const interval = setInterval(loadData, 2000);
    return () => clearInterval(interval);
  }, []);

  const loadData = async () => {
    try {
      const [m, s] = await Promise.all([
        api.getReverseEngineeringMetrics(),
        api.getDiscoveredSignals()
      ]);
      setMetrics(m);
      setSignals(s);
      if (m.length > 0 && selectedCanId === null) {
        setSelectedCanId(m[0].canId);
      }
    } catch {
      // ignore in offline/unconnected
    }
  };

  const handleSetBaseline = async () => {
    try {
      await api.setReverseEngineeringBaseline();
      setBaselineSet(true);
      setTimeout(() => setBaselineSet(false), 3000);
    } catch { }
  };

  const selectedMetric = metrics.find(m => m.canId === selectedCanId);

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-200 font-mono text-xs overflow-hidden">
      {/* Top Action Bar */}
      <div className="bg-[#101726] border-b border-slate-800 p-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center space-x-2">
          <div className="p-1.5 bg-sky-500/10 border border-sky-500/30 rounded text-sky-400">
            <Binary className="w-4 h-4" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-white tracking-wide">CAN REVERSE-ENGINEERING LAB</h2>
            <p className="text-[11px] text-slate-400">Deep telemetry timing, Shannon payload entropy, bitmasks & signal discovery</p>
          </div>
        </div>

        <div className="flex items-center space-x-2">
          <button
            onClick={handleSetBaseline}
            className={`px-3 py-1.5 rounded border transition-all text-xs font-semibold ${
              baselineSet
                ? 'bg-emerald-600/20 text-emerald-400 border-emerald-500/40'
                : 'bg-slate-800 hover:bg-slate-700 text-slate-200 border-slate-700'
            }`}
          >
            {baselineSet ? '✓ Baseline Snapshot Saved' : 'Set Baseline Capture'}
          </button>

          <button
            onClick={() => api.resetReverseEngineering().then(loadData)}
            className="px-3 py-1.5 bg-rose-950/40 hover:bg-rose-900/60 text-rose-300 border border-rose-800/40 rounded text-xs"
          >
            Reset Metrics
          </button>
        </div>
      </div>

      {/* Main Tabs */}
      <div className="flex border-b border-slate-800 bg-[#0D121F] px-4 space-x-4">
        <button
          onClick={() => setActiveTab('metrics')}
          className={`py-2 text-xs font-semibold border-b-2 transition-all ${
            activeTab === 'metrics'
              ? 'text-sky-400 border-sky-500'
              : 'text-slate-400 border-transparent hover:text-slate-200'
          }`}
        >
          CAN ID Deep Metrics ({metrics.length})
        </button>
        <button
          onClick={() => setActiveTab('signals')}
          className={`py-2 text-xs font-semibold border-b-2 transition-all ${
            activeTab === 'signals'
              ? 'text-sky-400 border-sky-500'
              : 'text-slate-400 border-transparent hover:text-slate-200'
          }`}
        >
          Discovered Signal Candidates ({signals.length})
        </button>
      </div>

      {/* Content Area */}
      <div className="flex-1 flex overflow-hidden">
        {activeTab === 'metrics' && (
          <>
            {/* ID Table */}
            <div className="w-1/2 border-r border-slate-800 overflow-y-auto p-3">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 text-[11px]">
                    <th className="p-2">ID</th>
                    <th className="p-2">RATE</th>
                    <th className="p-2">PERIOD</th>
                    <th className="p-2">JITTER</th>
                    <th className="p-2">ENTROPY</th>
                    <th className="p-2">COUNT</th>
                  </tr>
                </thead>
                <tbody>
                  {metrics.map(m => {
                    const isSelected = m.canId === selectedCanId;
                    return (
                      <tr
                        key={m.canId}
                        onClick={() => setSelectedCanId(m.canId)}
                        className={`border-b border-slate-900 cursor-pointer transition-colors ${
                          isSelected ? 'bg-sky-950/40 text-sky-300' : 'hover:bg-slate-900/40 text-slate-300'
                        }`}
                      >
                        <td className="p-2 font-bold font-mono text-white">{m.formattedCanId}</td>
                        <td className="p-2">{m.frequencyHz.toFixed(1)} Hz</td>
                        <td className="p-2">{m.periodMs.toFixed(1)} ms</td>
                        <td className="p-2 text-amber-400">±{m.jitterMs.toFixed(2)} ms</td>
                        <td className="p-2">
                          <span className={`px-1.5 py-0.5 rounded text-[10px] ${
                            m.payloadEntropy > 2.0 ? 'bg-purple-950 text-purple-300 border border-purple-800' : 'bg-slate-800 text-slate-400'
                          }`}>
                            {m.payloadEntropy.toFixed(2)} bits
                          </span>
                        </td>
                        <td className="p-2 text-slate-400">{m.messageCount}</td>
                      </tr>
                    );
                  })}
                  {metrics.length === 0 && (
                    <tr>
                      <td colSpan={6} className="p-6 text-center text-slate-500">
                        No active CAN traffic observed. Connect hardware or start simulation.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            {/* Detail View for Selected ID */}
            <div className="w-1/2 p-4 overflow-y-auto bg-[#0C101A]">
              {selectedMetric ? (
                <div className="space-y-4">
                  <div className="flex items-center justify-between border-b border-slate-800 pb-2">
                    <div>
                      <h3 className="text-base font-bold text-white font-mono">{selectedMetric.formattedCanId} Deep Inspector</h3>
                      <p className="text-[11px] text-slate-400">DLC: {selectedMetric.dlc} • Messages: {selectedMetric.messageCount}</p>
                    </div>
                    <div className="text-right">
                      <div className="text-emerald-400 font-bold">{selectedMetric.frequencyHz.toFixed(1)} Hz</div>
                      <div className="text-[10px] text-slate-500">Interval: {selectedMetric.minIntervalMs.toFixed(1)} - {selectedMetric.maxIntervalMs.toFixed(1)} ms</div>
                    </div>
                  </div>

                  {/* Byte Change Frequency Grid */}
                  <div>
                    <h4 className="text-xs font-semibold text-slate-300 mb-1.5 flex items-center space-x-1">
                      <Sliders className="w-3 h-3 text-sky-400" />
                      <span>Byte Change Activity (Heatmap)</span>
                    </h4>
                    <div className="grid grid-cols-8 gap-1.5 text-center">
                      {selectedMetric.byteChangeCounts.map((count, idx) => (
                        <div key={idx} className="bg-slate-900 border border-slate-800 p-2 rounded">
                          <div className="text-[10px] text-slate-500">B{idx}</div>
                          <div className="text-xs font-bold text-sky-300">{count}</div>
                          <div className="text-[9px] text-slate-400 font-mono mt-0.5">
                            {selectedMetric.lastPayload[idx] !== undefined
                              ? `0x${selectedMetric.lastPayload[idx].toString(16).padStart(2, '0').toUpperCase()}`
                              : '--'}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* Counter & Checksum Candidates */}
                  <div className="grid grid-cols-2 gap-3">
                    <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg">
                      <div className="flex items-center space-x-1.5 text-sky-400 font-semibold mb-1">
                        <Hash className="w-3.5 h-3.5" />
                        <span>Counter Candidates</span>
                      </div>
                      {selectedMetric.counterCandidates.length > 0 ? (
                        selectedMetric.counterCandidates.map((c, i) => (
                          <div key={i} className="text-[11px] text-slate-300 mt-1">
                            Bit {c.startBit} (Len {c.bitLength}) • Modulo {c.modulo} • Conf: {(c.monotonicityConfidence * 100).toFixed(0)}%
                          </div>
                        ))
                      ) : (
                        <div className="text-[11px] text-slate-500">No rolling counter detected.</div>
                      )}
                    </div>

                    <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg">
                      <div className="flex items-center space-x-1.5 text-indigo-400 font-semibold mb-1">
                        <ShieldCheck className="w-3.5 h-3.5" />
                        <span>Checksum Candidates</span>
                      </div>
                      {selectedMetric.checksumCandidates.length > 0 ? (
                        selectedMetric.checksumCandidates.map((c, i) => (
                          <div key={i} className="text-[11px] text-slate-300 mt-1">
                            Byte {c.byteIndex} • Algorithm: {c.algorithm} • Match: {(c.matchRatio * 100).toFixed(0)}%
                          </div>
                        ))
                      ) : (
                        <div className="text-[11px] text-slate-500">No trailing checksum detected.</div>
                      )}
                    </div>
                  </div>
                </div>
              ) : (
                <div className="p-8 text-center text-slate-500">Select a CAN ID from the left to inspect detailed metrics.</div>
              )}
            </div>
          </>
        )}

        {activeTab === 'signals' && (
          <div className="flex-1 p-4 overflow-y-auto">
            <table className="w-full text-left border-collapse font-mono text-xs">
              <thead>
                <tr className="border-b border-slate-800 text-slate-400 text-[11px]">
                  <th className="p-2">SIGNAL NAME</th>
                  <th className="p-2">PHYSICAL CATEGORY</th>
                  <th className="p-2">START BIT</th>
                  <th className="p-2">LENGTH</th>
                  <th className="p-2">PLAUSIBLE FACTOR</th>
                  <th className="p-2">UNIT</th>
                  <th className="p-2">CONFIDENCE</th>
                  <th className="p-2">RATIONALE</th>
                </tr>
              </thead>
              <tbody>
                {signals.map((s, idx) => (
                  <tr key={idx} className="border-b border-slate-900 hover:bg-slate-900/40">
                    <td className="p-2 font-bold text-sky-400">{s.name}</td>
                    <td className="p-2 text-white">{s.physicalCategory}</td>
                    <td className="p-2">{s.startBit}</td>
                    <td className="p-2">{s.bitLength} bits</td>
                    <td className="p-2">{s.plausibleFactor}</td>
                    <td className="p-2 text-indigo-300">{s.unit || '--'}</td>
                    <td className="p-2">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                        s.confidence === 'Verified'
                          ? 'bg-emerald-950 text-emerald-300 border border-emerald-800'
                          : s.confidence === 'Probable'
                          ? 'bg-sky-950 text-sky-300 border border-sky-800'
                          : 'bg-amber-950 text-amber-300 border border-amber-800'
                      }`}>
                        {s.confidence}
                      </span>
                    </td>
                    <td className="p-2 text-slate-400 text-[11px]">{s.rationale}</td>
                  </tr>
                ))}
                {signals.length === 0 && (
                  <tr>
                    <td colSpan={8} className="p-6 text-center text-slate-500">
                      No signal candidates discovered yet. Ingest high-rate CAN traffic to populate.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
