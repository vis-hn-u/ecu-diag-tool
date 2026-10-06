import React from 'react';
import { useCan } from '../context/CanContext';
import { TopologyGraph } from '../components/TopologyGraph';
import { Network, Play, Pause, Radio, RotateCcw } from 'lucide-react';
import { api } from '../services/api';
import { TopologyMode } from '../types';

export const TopologyPage: React.FC = () => {
  const { topology } = useCan();

  const currentMode = topology?.mode || 'Live';

  const handleSetMode = async (mode: TopologyMode) => {
    await api.setTopologyMode(mode);
  };

  const handleReset = async () => {
    await api.resetTopology();
  };

  return (
    <div className="flex flex-col h-full p-4 font-mono select-none space-y-3">
      {/* Top Topology Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-3 flex items-center justify-between shadow-md">
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2 text-white font-bold text-sm font-sans">
            <Network className="w-5 h-5 text-sky-400" />
            <span>REAL-TIME CAN NETWORK TOPOLOGY</span>
          </div>

          <div className="h-4 w-px bg-can-border mx-1" />

          {/* Mode Switcher */}
          <div className="flex items-center bg-can-bg rounded border border-can-border p-0.5 text-xs">
            <button
              onClick={() => handleSetMode('Live')}
              className={`px-3 py-1 rounded font-semibold flex items-center space-x-1.5 transition-all ${
                currentMode === 'Live'
                  ? 'bg-emerald-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <Radio className="w-3 h-3" />
              <span>LIVE</span>
            </button>
            <button
              onClick={() => handleSetMode('Paused')}
              className={`px-3 py-1 rounded font-semibold flex items-center space-x-1.5 transition-all ${
                currentMode === 'Paused'
                  ? 'bg-amber-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <Pause className="w-3 h-3" />
              <span>PAUSED</span>
            </button>
            <button
              onClick={() => handleSetMode('Simulation')}
              className={`px-3 py-1 rounded font-semibold flex items-center space-x-1.5 transition-all ${
                currentMode === 'Simulation'
                  ? 'bg-sky-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <Play className="w-3 h-3" />
              <span>SIMULATION</span>
            </button>
          </div>
        </div>

        <div className="flex items-center space-x-4 text-xs">
          <div className="text-slate-400">
            Total Frames Ingested: <span className="text-white font-bold">{topology?.totalFramesObserved.toLocaleString() ?? 0}</span>
          </div>
          <button
            onClick={handleReset}
            className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded border border-can-border flex items-center space-x-1.5 text-xs"
            title="Reset Topology Stats"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Reset Stats</span>
          </button>
        </div>
      </div>

      {/* Main Graph View */}
      <div className="flex-1 overflow-hidden">
        <TopologyGraph />
      </div>
    </div>
  );
};
