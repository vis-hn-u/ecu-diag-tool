import React, { useState } from 'react';
import { useCan } from '../context/CanContext';
import { SignalChart } from '../components/SignalChart';
import { LineChart, CheckSquare, Square, Search, Layers } from 'lucide-react';

interface GraphsPageProps {
  initialSignal?: string;
}

export const GraphsPage: React.FC<GraphsPageProps> = ({ initialSignal }) => {
  const { signals, activeDbc } = useCan();
  const [selectedSignals, setSelectedSignals] = useState<string[]>(
    initialSignal ? [initialSignal] : ['EngineSpeed', 'VehicleSpeed', 'ThrottlePosition']
  );
  const [searchQuery, setSearchQuery] = useState('');

  const allSignalNames = Object.keys(signals);

  const filteredNames = allSignalNames.filter(name =>
    name.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const toggleSignal = (name: string) => {
    setSelectedSignals(prev =>
      prev.includes(name) ? prev.filter(s => s !== name) : [...prev, name]
    );
  };

  return (
    <div className="flex h-full gap-4 p-4 font-mono select-none overflow-hidden">
      {/* Chart Canvas Column */}
      <div className="flex-1 flex flex-col space-y-4 overflow-y-auto">
        <SignalChart
          selectedSignals={selectedSignals}
          onToggleSignal={toggleSignal}
          height={420}
        />

        {/* Selected Signals Live Gauges */}
        <div className="grid grid-cols-3 gap-3">
          {selectedSignals.map(sigName => {
            const sig = signals[sigName];
            if (!sig) return null;

            return (
              <div key={sigName} className="bg-can-panel border border-can-border rounded-lg p-3.5 shadow space-y-1">
                <span className="text-xs text-slate-400 font-sans font-medium block truncate">{sig.signalName}</span>
                <div className="text-xl font-bold text-sky-400">
                  {sig.engineeringValue.toFixed(2)} <span className="text-xs text-slate-400 font-normal">{sig.unit}</span>
                </div>
                <div className="text-[10px] text-slate-500 font-sans flex justify-between">
                  <span>ECU: {sig.transmitterNode}</span>
                  <span>CAN ID: 0x{sig.canId.toString(16).toUpperCase()}</span>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Signal Selection Sidebar */}
      <div className="w-72 bg-can-panel border border-can-border rounded-lg p-4 flex flex-col shadow-xl space-y-3">
        <div className="flex items-center space-x-2 border-b border-can-border pb-2.5">
          <LineChart className="w-4 h-4 text-sky-400" />
          <span className="text-xs font-bold text-white font-sans uppercase tracking-wider">AVAILABLE SIGNALS</span>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="w-3.5 h-3.5 absolute left-2.5 top-2 text-slate-500" />
          <input
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder="Search signals..."
            className="w-full bg-can-bg border border-can-border rounded pl-8 pr-3 py-1 text-xs text-white focus:outline-none focus:border-sky-500"
          />
        </div>

        {/* Signal Checkbox List */}
        <div className="flex-1 overflow-y-auto space-y-1">
          {filteredNames.length > 0 ? (
            filteredNames.map(sigName => {
              const isSelected = selectedSignals.includes(sigName);
              const sig = signals[sigName];

              return (
                <div
                  key={sigName}
                  onClick={() => toggleSignal(sigName)}
                  className={`p-2 rounded cursor-pointer flex items-center justify-between text-xs border transition-all ${
                    isSelected
                      ? 'bg-sky-600/20 text-white border-sky-500/40 font-semibold'
                      : 'hover:bg-can-card text-slate-400 border-transparent'
                  }`}
                >
                  <div className="flex items-center space-x-2 truncate">
                    {isSelected ? <CheckSquare className="w-3.5 h-3.5 text-sky-400" /> : <Square className="w-3.5 h-3.5 text-slate-600" />}
                    <span className="truncate">{sigName}</span>
                  </div>
                  {sig && (
                    <span className="text-[10px] text-emerald-400 font-bold ml-1 shrink-0">
                      {sig.engineeringValue.toFixed(1)}
                    </span>
                  )}
                </div>
              );
            })
          ) : (
            <div className="text-center py-8 text-slate-500 text-xs">
              <Layers className="w-6 h-6 mx-auto mb-2 text-slate-600" />
              <p>No active decoded signals. Load a DBC and start traffic stream.</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
