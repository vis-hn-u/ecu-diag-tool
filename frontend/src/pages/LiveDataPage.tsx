import React, { useState, useMemo } from 'react';
import { useCan } from '../context/CanContext';
import { Gauge, Search, Pin, PinOff, LineChart, FileCode, Layers } from 'lucide-react';
import { TabType } from '../components/Sidebar';

interface LiveDataProps {
  onNavigateToGraphs?: (sigName: string) => void;
}

export const LiveDataPage: React.FC<LiveDataProps> = ({ onNavigateToGraphs }) => {
  const { signals, pinnedSignals, togglePinSignal, activeDbc } = useCan();
  const [searchQuery, setSearchQuery] = useState('');
  const [groupBy, setGroupBy] = useState<'none' | 'ecu' | 'message'>('ecu');

  const signalList = useMemo(() => Object.values(signals), [signals]);

  const filteredSignals = useMemo(() => {
    if (!searchQuery.trim()) return signalList;
    const q = searchQuery.toLowerCase();
    return signalList.filter(s =>
      s.signalName.toLowerCase().includes(q) ||
      s.messageName.toLowerCase().includes(q) ||
      s.transmitterNode.toLowerCase().includes(q)
    );
  }, [signalList, searchQuery]);

  // Grouped signals
  const grouped = useMemo(() => {
    if (groupBy === 'none') {
      return { 'All Signals': filteredSignals };
    }

    const groups: Record<string, typeof filteredSignals> = {};
    filteredSignals.forEach(s => {
      const key = groupBy === 'ecu' ? (s.transmitterNode || 'Unknown ECU') : (s.messageName || 'Unknown Message');
      if (!groups[key]) groups[key] = [];
      groups[key].push(s);
    });
    return groups;
  }, [filteredSignals, groupBy]);

  return (
    <div className="p-6 space-y-6 overflow-y-auto h-full font-mono select-none">
      {/* Header & Controls */}
      <div className="flex flex-wrap items-center justify-between gap-4 border-b border-can-border pb-4">
        <div>
          <div className="flex items-center space-x-2 text-white font-bold text-base font-sans">
            <Gauge className="w-5 h-5 text-emerald-400" />
            <span>LIVE DATA DASHBOARD</span>
          </div>
          <p className="text-xs text-slate-400 font-sans mt-0.5">
            Real-time physical engineering values decoded from authoritative DBC message definitions.
          </p>
        </div>

        <div className="flex items-center space-x-3 text-xs">
          {/* Search */}
          <div className="relative">
            <Search className="w-3.5 h-3.5 absolute left-2.5 top-2.5 text-slate-500" />
            <input
              type="text"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              placeholder="Search signal / ECU..."
              className="bg-can-panel border border-can-border rounded pl-8 pr-3 py-1.5 text-xs text-white focus:outline-none focus:border-emerald-500 w-48"
            />
          </div>

          {/* Grouping Selector */}
          <select
            value={groupBy}
            onChange={e => setGroupBy(e.target.value as any)}
            className="bg-can-panel border border-can-border rounded px-3 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-emerald-500"
          >
            <option value="ecu">Group by: ECU Node</option>
            <option value="message">Group by: CAN Message</option>
            <option value="none">Flat List</option>
          </select>
        </div>
      </div>

      {/* No DBC Warning */}
      {!activeDbc?.isLoaded && signalList.length === 0 && (
        <div className="bg-amber-500/10 border border-amber-500/30 rounded-lg p-5 text-center text-amber-300 text-xs">
          <FileCode className="w-8 h-8 mx-auto mb-2 text-amber-400" />
          <p className="font-bold text-sm">No DBC Database Loaded</p>
          <p className="text-slate-400 mt-1 max-w-md mx-auto">
            Please load a DBC file in the DBC Manager or click "Load Sample DBC" on the Dashboard to decode physical engineering signals.
          </p>
        </div>
      )}

      {/* Pinned Signals Banner */}
      {pinnedSignals.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center space-x-1.5 text-xs font-semibold text-slate-400 uppercase tracking-wider">
            <Pin className="w-3.5 h-3.5 text-sky-400" />
            <span>PINNED SIGNALS</span>
          </div>
          <div className="grid grid-cols-4 gap-3">
            {pinnedSignals.map(sigName => {
              const sig = signals[sigName];
              if (!sig) return null;

              return (
                <div
                  key={sigName}
                  className="bg-can-panel border border-sky-500/40 rounded-lg p-3.5 shadow-md space-y-1 relative"
                >
                  <div className="flex justify-between items-start">
                    <span className="text-slate-400 text-xs truncate max-w-[140px] font-sans font-medium" title={sig.signalName}>
                      {sig.signalName}
                    </span>
                    <button onClick={() => togglePinSignal(sigName)} className="text-sky-400 hover:text-white" title="Unpin">
                      <PinOff className="w-3.5 h-3.5" />
                    </button>
                  </div>
                  <div className="text-xl font-bold text-white tracking-wide">
                    {sig.engineeringValue.toFixed(2)}{' '}
                    <span className="text-xs text-sky-400 font-normal">{sig.unit}</span>
                  </div>
                  <div className="flex justify-between items-center text-[10px] text-slate-500 pt-1">
                    <span>{sig.transmitterNode} ({sig.messageName})</span>
                    {onNavigateToGraphs && (
                      <button
                        onClick={() => onNavigateToGraphs(sig.signalName)}
                        className="text-slate-400 hover:text-white flex items-center space-x-1"
                        title="Graph Signal"
                      >
                        <LineChart className="w-3 h-3" />
                      </button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Grouped Signal Cards */}
      <div className="space-y-6">
        {Object.entries(grouped).map(([groupName, sigs]) => (
          <div key={groupName} className="space-y-2.5">
            <div className="flex items-center space-x-2 text-xs font-bold text-slate-300 uppercase tracking-wider border-b border-can-border pb-1">
              <Layers className="w-3.5 h-3.5 text-emerald-400" />
              <span>{groupName}</span>
              <span className="text-slate-500 font-normal">({sigs.length} signals)</span>
            </div>

            <div className="grid grid-cols-3 gap-3">
              {sigs.map(sig => {
                const isPinned = pinnedSignals.includes(sig.signalName);

                return (
                  <div
                    key={sig.signalName}
                    className="bg-can-panel border border-can-border hover:border-slate-600 rounded-lg p-3.5 shadow-md space-y-1.5 transition-all"
                  >
                    <div className="flex justify-between items-start">
                      <div>
                        <span className="text-white text-xs font-sans font-bold block" title={sig.signalName}>
                          {sig.signalName}
                        </span>
                        <span className="text-[10px] text-slate-500 font-sans">
                          {sig.messageName} (0x{sig.canId.toString(16).toUpperCase()})
                        </span>
                      </div>
                      <div className="flex items-center space-x-1">
                        <button
                          onClick={() => togglePinSignal(sig.signalName)}
                          className={`p-1 rounded ${isPinned ? 'text-sky-400' : 'text-slate-600 hover:text-slate-300'}`}
                          title={isPinned ? 'Unpin' : 'Pin to Top'}
                        >
                          <Pin className="w-3.5 h-3.5" />
                        </button>
                        {onNavigateToGraphs && (
                          <button
                            onClick={() => onNavigateToGraphs(sig.signalName)}
                            className="p-1 rounded text-slate-600 hover:text-white"
                            title="Graph Signal"
                          >
                            <LineChart className="w-3.5 h-3.5" />
                          </button>
                        )}
                      </div>
                    </div>

                    {/* Value */}
                    <div className="text-lg font-bold text-emerald-400">
                      {sig.engineeringValue.toFixed(2)}{' '}
                      <span className="text-xs text-slate-400 font-normal">{sig.unit}</span>
                      {sig.valueDescription && (
                        <span className="ml-2 text-[10px] text-amber-300 font-sans font-medium px-1.5 py-0.5 bg-amber-500/10 rounded">
                          {sig.valueDescription}
                        </span>
                      )}
                    </div>

                    {/* Footer */}
                    <div className="flex justify-between items-center text-[10px] text-slate-500 pt-1 border-t border-can-border/60">
                      <span>Source: {sig.transmitterNode}</span>
                      <span>Raw: {sig.rawValue}</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
