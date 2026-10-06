import React, { useState, useEffect } from 'react';
import { api } from '../services/api';
import { useCan } from '../context/CanContext';
import { LiveParameter, RequestedVsActualPair } from '../types/flash';
import { 
  Gauge, 
  Search, 
  Pin, 
  Filter, 
  RotateCcw, 
  Download, 
  Radio, 
  Activity, 
  ArrowRightLeft, 
  ShieldCheck, 
  Layers,
  Flame,
  Wind,
  Zap,
  Thermometer,
  Truck,
  Wrench,
  CheckCircle2
} from 'lucide-react';

const CATEGORIES = [
  'All',
  'Engine',
  'AirTurbo',
  'Fuel',
  'Egr',
  'Dpf',
  'ScrAdBlue',
  'Nox',
  'Thermal',
  'Vehicle',
  'Transmission',
  'Diagnostics',
  'Other'
];

export const LiveEcuDataPage: React.FC = () => {
  const { operatingMode } = useCan();
  const [parameters, setParameters] = useState<LiveParameter[]>([]);
  const [reqVsAct, setReqVsAct] = useState<RequestedVsActualPair[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<string>('All');
  const [searchTerm, setSearchTerm] = useState<string>('');

  const fetchLiveParameters = async () => {
    try {
      const cat = selectedCategory === 'All' ? undefined : selectedCategory;
      const params = await api.getLiveParameters(cat);
      setParameters(params);

      const pairs = await api.getRequestedVsActual();
      setReqVsAct(pairs);
    } catch {
      // Ignore
    }
  };

  useEffect(() => {
    fetchLiveParameters();
    const interval = setInterval(fetchLiveParameters, 500);
    return () => clearInterval(interval);
  }, [selectedCategory]);

  const handleTogglePin = async (param: LiveParameter) => {
    await api.setLiveParameterPin(param.id, !param.isPinned);
    fetchLiveParameters();
  };

  const handleClear = async () => {
    await api.clearLiveParameters();
    fetchLiveParameters();
  };

  const filteredParams = parameters.filter(p => {
    if (!searchTerm) return true;
    const term = searchTerm.toLowerCase();
    return p.name.toLowerCase().includes(term) || p.messageName.toLowerCase().includes(term) || p.unit.toLowerCase().includes(term);
  });

  const getProvenanceBadge = (prov: any) => {
    const pStr = typeof prov === 'number' ? ['DBC', 'OBSERVED CAN', 'DIAGNOSTIC', 'SIMULATION', 'UNKNOWN'][prov] || 'UNKNOWN' : String(prov).toUpperCase();
    if (pStr.includes('DBC')) return 'bg-sky-500/20 text-sky-400 border-sky-500/30';
    if (pStr.includes('OBSERVED')) return 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30';
    if (pStr.includes('DIAGNOSTIC')) return 'bg-purple-500/20 text-purple-400 border-purple-500/30';
    if (pStr.includes('SIMULATION')) return 'bg-amber-500/20 text-amber-400 border-amber-500/30';
    return 'bg-slate-500/20 text-slate-400 border-slate-500/30';
  };

  const getCategoryLabel = (cat: any) => {
    if (typeof cat === 'number') {
      const names = ['Engine', 'Air/Turbo', 'Fuel', 'EGR', 'DPF', 'SCR/AdBlue', 'NOx', 'Thermal', 'Vehicle', 'Transmission', 'Diagnostics', 'Other'];
      return names[cat] || 'Other';
    }
    return cat === 'AirTurbo' ? 'Air / Turbo' : cat === 'ScrAdBlue' ? 'SCR / AdBlue' : cat;
  };

  const exportCsv = () => {
    if (parameters.length === 0) return;
    const headers = 'ID,Name,Category,Value,Unit,Provenance,CAN_ID,Message,UpdateRateHz,LastSeenUtc\n';
    const rows = parameters.map(p => 
      `"${p.id}","${p.name}","${getCategoryLabel(p.category)}",${p.value},"${p.unit}","${p.provenance}","0x${p.canId.toString(16).toUpperCase()}","${p.messageName}",${p.updateRateHz},"${p.lastSeenUtc}"`
    ).join('\n');

    const blob = new Blob([headers + rows], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `live_ecu_parameters_${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '_')}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="flex flex-col h-full font-mono select-none overflow-hidden p-4 space-y-4">
      {/* Top Header Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-3 flex items-center justify-between shadow-md">
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2 text-white font-bold text-sm font-sans">
            <Activity className="w-5 h-5 text-sky-400" />
            <span>LIVE ECU DATA & RUNTIME TELEMETRY</span>
          </div>

          <div className="h-4 w-px bg-can-border mx-1" />

          {/* Mode Badge */}
          <span className={`px-2.5 py-0.5 rounded text-[11px] font-sans font-bold flex items-center space-x-1 ${
            operatingMode === 'Live Hardware'
              ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'
              : 'bg-sky-500/20 text-sky-300 border border-sky-500/30'
          }`}>
            <Radio className="w-3 h-3" />
            <span>{operatingMode.toUpperCase()}</span>
          </span>

          <span className="text-xs text-slate-400 font-sans">
            Active Parameters: <span className="text-white font-bold">{parameters.length}</span>
          </span>
        </div>

        <div className="flex items-center space-x-3">
          <button
            onClick={exportCsv}
            className="px-3 py-1.5 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border text-xs flex items-center space-x-1.5"
          >
            <Download className="w-3.5 h-3.5" />
            <span>Export CSV</span>
          </button>

          <button
            onClick={handleClear}
            className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded border border-can-border text-xs flex items-center space-x-1.5"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Clear</span>
          </button>
        </div>
      </div>

      {/* Requested vs Actual Comparison Cards */}
      {reqVsAct.length > 0 && (
        <div className="bg-can-panel border border-can-border rounded-lg p-3 shadow space-y-2">
          <div className="flex items-center space-x-2 text-xs font-sans font-bold text-slate-300">
            <ArrowRightLeft className="w-4 h-4 text-purple-400" />
            <span>REQUESTED VS ACTUAL RUNTIME COMPARISON</span>
          </div>

          <div className="grid grid-cols-3 gap-3">
            {reqVsAct.map(pair => (
              <div key={pair.id} className="bg-[#0A0E17] border border-can-border rounded p-3 flex justify-between items-center text-xs">
                <div>
                  <span className="font-sans font-bold text-white block">{pair.parameterName}</span>
                  <div className="flex space-x-3 text-[11px] text-slate-400 mt-1">
                    <span>Target: <span className="text-purple-300 font-bold">{pair.targetValue.toFixed(1)} {pair.unit}</span></span>
                    <span>Actual: <span className="text-emerald-300 font-bold">{pair.actualValue.toFixed(1)} {pair.unit}</span></span>
                  </div>
                </div>

                <div className="text-right">
                  <span className={`font-bold block ${Math.abs(pair.errorDelta) > 5 ? 'text-amber-400' : 'text-emerald-400'}`}>
                    {pair.errorDelta > 0 ? '+' : ''}{pair.errorDelta.toFixed(1)} {pair.unit}
                  </span>
                  <span className="text-[10px] text-slate-500">
                    Δ {pair.errorPercent.toFixed(1)}%
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Category Pills & Search Bar */}
      <div className="flex items-center justify-between gap-4">
        {/* Category Pills */}
        <div className="flex items-center space-x-1 overflow-x-auto pb-1 text-xs font-sans">
          {CATEGORIES.map(cat => (
            <button
              key={cat}
              onClick={() => setSelectedCategory(cat)}
              className={`px-3 py-1 rounded-full whitespace-nowrap transition-all ${
                selectedCategory === cat
                  ? 'bg-sky-600 text-white font-bold shadow'
                  : 'bg-can-panel text-slate-400 hover:text-white border border-can-border'
              }`}
            >
              {cat === 'AirTurbo' ? 'Air / Turbo' : cat === 'ScrAdBlue' ? 'SCR / AdBlue' : cat}
            </button>
          ))}
        </div>

        {/* Search Input */}
        <div className="relative w-64">
          <Search className="w-3.5 h-3.5 absolute left-3 top-2.5 text-slate-500" />
          <input
            type="text"
            placeholder="Search parameter, CAN ID..."
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            className="w-full bg-can-panel border border-can-border rounded-lg pl-8 pr-3 py-1 text-xs text-white focus:outline-none focus:border-sky-500 font-sans"
          />
        </div>
      </div>

      {/* Main Parameters Table */}
      <div className="flex-1 bg-can-panel border border-can-border rounded-lg overflow-hidden flex flex-col shadow-xl">
        <div className="flex-1 overflow-y-auto">
          <table className="w-full text-left text-xs font-mono">
            <thead className="bg-[#0B0F17] text-slate-400 border-b border-can-border sticky top-0 uppercase text-[10px]">
              <tr>
                <th className="p-2.5 w-10 text-center">Pin</th>
                <th className="p-2.5">Parameter Name</th>
                <th className="p-2.5">Category</th>
                <th className="p-2.5 text-right">Value</th>
                <th className="p-2.5">Unit</th>
                <th className="p-2.5">CAN ID / Msg</th>
                <th className="p-2.5">Provenance</th>
                <th className="p-2.5 text-right">Rate</th>
                <th className="p-2.5 text-right">Min / Max</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-can-border/60">
              {filteredParams.length === 0 ? (
                <tr>
                  <td colSpan={9} className="text-center py-12 text-slate-500 font-sans">
                    No matching live ECU parameters found for this filter.
                  </td>
                </tr>
              ) : (
                filteredParams.map(param => (
                  <tr key={param.id} className={`hover:bg-can-card/60 transition-colors ${param.isPinned ? 'bg-sky-950/20' : ''}`}>
                    <td className="p-2.5 text-center">
                      <button
                        onClick={() => handleTogglePin(param)}
                        className={`p-1 rounded ${param.isPinned ? 'text-amber-400' : 'text-slate-600 hover:text-slate-300'}`}
                      >
                        <Pin className="w-3.5 h-3.5" />
                      </button>
                    </td>
                    <td className="p-2.5 font-bold text-white font-sans">{param.name}</td>
                    <td className="p-2.5 text-slate-400 font-sans">{getCategoryLabel(param.category)}</td>
                    <td className="p-2.5 text-right font-bold text-sky-300 text-sm">
                      {param.value.toFixed(2)}
                    </td>
                    <td className="p-2.5 text-slate-400">{param.unit}</td>
                    <td className="p-2.5 text-slate-400">
                      <span className="text-slate-200 font-bold">0x{param.canId.toString(16).toUpperCase()}</span> ({param.messageName})
                    </td>
                    <td className="p-2.5">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-sans font-bold border ${getProvenanceBadge(param.provenance)}`}>
                        {typeof param.provenance === 'number' ? ['DBC', 'OBSERVED CAN', 'DIAGNOSTIC', 'SIMULATION', 'UNKNOWN'][param.provenance] : param.provenance}
                      </span>
                    </td>
                    <td className="p-2.5 text-right text-slate-300">{param.updateRateHz} Hz</td>
                    <td className="p-2.5 text-right text-slate-400 text-[11px]">
                      {param.minObserved.toFixed(1)} / {param.maxObserved.toFixed(1)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
