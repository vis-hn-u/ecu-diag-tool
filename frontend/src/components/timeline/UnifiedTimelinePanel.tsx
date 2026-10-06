import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { TimelineEntry, TimelineCategory, TimelineSeverity } from '../../types';
import { 
  Clock, 
  Search, 
  Trash2, 
  Filter, 
  Activity, 
  AlertTriangle, 
  CheckCircle2, 
  ShieldAlert, 
  Binary, 
  Zap, 
  Cpu 
} from 'lucide-react';

export const UnifiedTimelinePanel: React.FC = () => {
  const [entries, setEntries] = useState<TimelineEntry[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('');
  const [selectedSeverity, setSelectedSeverity] = useState<string>('');
  const [selectedEntry, setSelectedEntry] = useState<TimelineEntry | null>(null);

  useEffect(() => {
    loadTimeline();
    const interval = setInterval(loadTimeline, 2000);
    return () => clearInterval(interval);
  }, [selectedCategory, selectedSeverity]);

  const loadTimeline = async () => {
    try {
      if (searchQuery.trim().length > 1) {
        const results = await api.searchTimeline(searchQuery);
        setEntries(results);
      } else {
        const res = await api.getTimelineEntries(
          200, 
          selectedCategory || undefined, 
          selectedSeverity || undefined
        );
        setEntries(res);
      }
    } catch { }
  };

  const handleClear = async () => {
    await api.clearTimeline();
    setEntries([]);
    setSelectedEntry(null);
  };

  const getSeverityBadge = (sev: TimelineSeverity) => {
    switch (sev) {
      case 'Critical':
      case 'Error':
        return 'bg-rose-950 text-rose-300 border-rose-800';
      case 'Warning':
        return 'bg-amber-950 text-amber-300 border-amber-800';
      case 'Notice':
        return 'bg-indigo-950 text-indigo-300 border-indigo-800';
      default:
        return 'bg-slate-800 text-slate-300 border-slate-700';
    }
  };

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-200 font-mono text-xs overflow-hidden">
      {/* Top Bar */}
      <div className="bg-[#101726] border-b border-slate-800 p-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center space-x-2">
          <div className="p-1.5 bg-sky-500/10 border border-sky-500/30 rounded text-sky-400">
            <Clock className="w-4 h-4" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-white tracking-wide">UNIFIED FORENSIC EVIDENCE TIMELINE</h2>
            <p className="text-[11px] text-slate-400">
              Synchronized chronological sequence across CAN, CAN FD, UDS, DTC, Signals, Flash, and User actions
            </p>
          </div>
        </div>

        {/* Filters & Actions */}
        <div className="flex items-center space-x-2">
          <select
            value={selectedCategory}
            onChange={e => setSelectedCategory(e.target.value)}
            className="bg-slate-900 border border-slate-800 rounded px-2.5 py-1 text-xs text-slate-300 font-mono"
          >
            <option value="">All Categories</option>
            <option value="CanFrame">CAN Frames</option>
            <option value="DtcEvent">DTC Events</option>
            <option value="UdsExchange">UDS Diagnostic Exchanges</option>
            <option value="FlashOperation">Flash Operations</option>
            <option value="UserAction">User Actions</option>
            <option value="HardwareEvent">Hardware Events</option>
          </select>

          <select
            value={selectedSeverity}
            onChange={e => setSelectedSeverity(e.target.value)}
            className="bg-slate-900 border border-slate-800 rounded px-2.5 py-1 text-xs text-slate-300 font-mono"
          >
            <option value="">All Severities</option>
            <option value="Info">Info & Above</option>
            <option value="Warning">Warning & Above</option>
            <option value="Error">Errors Only</option>
          </select>

          <button
            onClick={handleClear}
            className="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-rose-400 rounded transition-colors"
            title="Clear Timeline"
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Main Stream */}
      <div className="flex-1 flex overflow-hidden">
        {/* Timeline Entries Table */}
        <div className="w-2/3 border-r border-slate-800 overflow-y-auto p-3">
          <div className="space-y-1.5">
            {entries.map(entry => {
              const isSelected = selectedEntry?.sequenceId === entry.sequenceId;
              const dateStr = new Date(entry.timestampUtc).toLocaleTimeString();

              return (
                <div
                  key={entry.sequenceId}
                  onClick={() => setSelectedEntry(entry)}
                  className={`p-2 rounded border cursor-pointer transition-all ${
                    isSelected
                      ? 'bg-sky-950/40 border-sky-500/80 text-white'
                      : 'bg-slate-900/40 border-slate-800 hover:border-slate-700 text-slate-300'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-2">
                      <span className="text-[10px] text-slate-500 font-mono">#{entry.sequenceId}</span>
                      <span className="text-[10px] text-slate-400">{dateStr}</span>
                      <span className={`px-1.5 py-0.5 rounded text-[9px] font-bold border ${getSeverityBadge(entry.severity)}`}>
                        {entry.severity}
                      </span>
                      <span className="font-bold text-xs text-white">{entry.title}</span>
                    </div>
                    <span className="text-[10px] text-indigo-400 font-mono">{entry.category}</span>
                  </div>

                  <div className="text-[11px] text-slate-400 mt-1 truncate">{entry.description}</div>

                  {entry.rawHex && (
                    <div className="text-[10px] text-emerald-400 font-mono mt-1 bg-slate-950 px-2 py-0.5 rounded truncate">
                      RAW: {entry.rawHex}
                    </div>
                  )}
                </div>
              );
            })}

            {entries.length === 0 && (
              <div className="p-8 text-center text-slate-500">No timeline entries recorded yet.</div>
            )}
          </div>
        </div>

        {/* Selected Entry Inspector */}
        <div className="w-1/3 p-4 bg-[#0C101A] overflow-y-auto">
          {selectedEntry ? (
            <div className="space-y-3">
              <div className="border-b border-slate-800 pb-2">
                <span className="text-[10px] text-slate-500 font-mono">ENTRY #{selectedEntry.sequenceId}</span>
                <h3 className="text-sm font-bold text-white mt-0.5">{selectedEntry.title}</h3>
                <div className="text-[10px] text-slate-400 mt-1">{new Date(selectedEntry.timestampUtc).toISOString()}</div>
              </div>

              <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg space-y-2">
                <div>
                  <div className="text-[10px] text-slate-500">CATEGORY & SOURCE</div>
                  <div className="text-white font-semibold">{selectedEntry.category} • {selectedEntry.sourceId}</div>
                </div>
                <div>
                  <div className="text-[10px] text-slate-500">DESCRIPTION</div>
                  <div className="text-slate-300 whitespace-pre-line">{selectedEntry.description}</div>
                </div>
                {selectedEntry.rawHex && (
                  <div>
                    <div className="text-[10px] text-slate-500">RAW PAYLOAD (HEX)</div>
                    <div className="text-emerald-400 font-mono bg-slate-950 p-2 rounded break-all">{selectedEntry.rawHex}</div>
                  </div>
                )}
              </div>
            </div>
          ) : (
            <div className="p-8 text-center text-slate-500">Select a timeline entry to inspect full metadata.</div>
          )}
        </div>
      </div>
    </div>
  );
};
