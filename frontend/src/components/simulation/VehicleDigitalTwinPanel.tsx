import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { DigitalTwinNode } from '../../types';
import { 
  Network, 
  Play, 
  Square, 
  AlertTriangle, 
  ShieldAlert, 
  CheckCircle2, 
  Cpu, 
  BatteryCharging, 
  Sliders, 
  RefreshCw,
  Zap,
  Activity
} from 'lucide-react';

export const VehicleDigitalTwinPanel: React.FC = () => {
  const [nodes, setNodes] = useState<DigitalTwinNode[]>([]);
  const [isRunning, setIsRunning] = useState(false);
  const [activeCount, setActiveCount] = useState(0);
  const [selectedNodeId, setSelectedNodeId] = useState<string>('ECM');
  const [faultDtc, setFaultDtc] = useState('P0171');
  const [voltageOverride, setVoltageOverride] = useState<number>(11.2);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadStatus();
    const interval = setInterval(loadStatus, 2000);
    return () => clearInterval(interval);
  }, []);

  const loadStatus = async () => {
    try {
      const [nList, status] = await Promise.all([
        api.getDigitalTwinNodes(),
        api.getDigitalTwinStatus()
      ]);
      setNodes(nList);
      setIsRunning(status.isRunning);
      setActiveCount(status.activeNodeCount);
    } catch { }
  };

  const handleToggleSimulation = async () => {
    setLoading(true);
    try {
      if (isRunning) {
        await api.stopDigitalTwinSimulation();
      } else {
        await api.startDigitalTwinSimulation();
      }
      await loadStatus();
    } catch { }
    finally { setLoading(false); }
  };

  const handleInjectDtc = async () => {
    if (!faultDtc) return;
    await api.setDigitalTwinFault({
      nodeId: selectedNodeId,
      dtcCode: faultDtc,
      setDtcActive: true
    });
    await loadStatus();
  };

  const handleInjectVoltage = async () => {
    await api.setDigitalTwinFault({
      nodeId: selectedNodeId,
      voltageOverride: voltageOverride
    });
    await loadStatus();
  };

  const handleToggleBusDrop = async (currentDrop: boolean) => {
    await api.setDigitalTwinFault({
      nodeId: selectedNodeId,
      busDrop: !currentDrop
    });
    await loadStatus();
  };

  const handleClearFaults = async () => {
    await api.clearDigitalTwinFaults(selectedNodeId);
    await loadStatus();
  };

  const selectedNode = nodes.find(n => n.nodeId === selectedNodeId);

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-200 font-mono text-xs overflow-hidden">
      {/* Top Bar */}
      <div className="bg-[#101726] border-b border-slate-800 p-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center space-x-2">
          <div className="p-1.5 bg-indigo-500/10 border border-indigo-500/30 rounded text-indigo-400">
            <Network className="w-4 h-4" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-white tracking-wide">VEHICLE DIGITAL TWIN & MULTI-ECU SIMULATOR</h2>
            <p className="text-[11px] text-slate-400">
              Coordinated simulation of ECM, TCM, ABS, BCM, EPS, Gateway, ADAS & BMS network traffic
            </p>
          </div>
        </div>

        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2 bg-slate-900 border border-slate-800 px-3 py-1.5 rounded-lg">
            <span className={`w-2 h-2 rounded-full ${isRunning ? 'bg-emerald-400 animate-pulse' : 'bg-slate-600'}`} />
            <span className="text-[11px] font-semibold text-slate-300">
              {isRunning ? `SIMULATING (${activeCount}/${nodes.length} Nodes Online)` : 'STOPPED'}
            </span>
          </div>

          <button
            onClick={handleToggleSimulation}
            disabled={loading}
            className={`flex items-center space-x-1.5 px-3.5 py-1.5 rounded-lg text-xs font-semibold shadow-md transition-all ${
              isRunning
                ? 'bg-rose-600 hover:bg-rose-500 text-white'
                : 'bg-emerald-600 hover:bg-emerald-500 text-white'
            }`}
          >
            {isRunning ? (
              <>
                <Square className="w-3.5 h-3.5 fill-current" />
                <span>Stop Network Simulation</span>
              </>
            ) : (
              <>
                <Play className="w-3.5 h-3.5 fill-current" />
                <span>Start Simulation</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Main Grid */}
      <div className="flex-1 flex overflow-hidden">
        {/* Node Topology Grid */}
        <div className="w-2/3 p-4 overflow-y-auto border-r border-slate-800">
          <div className="grid grid-cols-3 gap-3">
            {nodes.map(node => {
              const isSelected = node.nodeId === selectedNodeId;
              const hasFault = node.activeDtcs.length > 0 || node.injectedBusDrop || node.voltage < 12.0;

              return (
                <div
                  key={node.nodeId}
                  onClick={() => setSelectedNodeId(node.nodeId)}
                  className={`p-3 rounded-lg border cursor-pointer transition-all ${
                    isSelected
                      ? 'bg-sky-950/40 border-sky-500/80 shadow-md ring-1 ring-sky-500/30'
                      : 'bg-slate-900/60 border-slate-800 hover:border-slate-700'
                  }`}
                >
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-bold text-white text-sm">{node.nodeId}</span>
                    <span className={`px-1.5 py-0.5 rounded text-[10px] font-semibold ${
                      node.injectedBusDrop
                        ? 'bg-rose-950 text-rose-300 border border-rose-800'
                        : hasFault
                        ? 'bg-amber-950 text-amber-300 border border-amber-800'
                        : 'bg-emerald-950 text-emerald-300 border border-emerald-800'
                    }`}>
                      {node.injectedBusDrop ? 'BUS DROP' : hasFault ? 'FAULT' : 'ONLINE'}
                    </span>
                  </div>

                  <div className="text-[11px] text-slate-400 mb-2 truncate">{node.name}</div>

                  <div className="text-[10px] text-slate-500 space-y-0.5 font-mono border-t border-slate-800/60 pt-1.5">
                    <div>REQ: 0x{node.requestCanId.toString(16).toUpperCase()} • RESP: 0x{node.responseCanId.toString(16).toUpperCase()}</div>
                    <div>Voltage: <span className={node.voltage < 12.0 ? 'text-amber-400 font-bold' : 'text-slate-300'}>{node.voltage.toFixed(1)}V</span></div>
                    {node.activeDtcs.length > 0 && (
                      <div className="text-amber-400 font-semibold truncate">DTCs: {node.activeDtcs.join(', ')}</div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Fault Injection Panel */}
        <div className="w-1/3 p-4 bg-[#0C101A] overflow-y-auto">
          {selectedNode ? (
            <div className="space-y-4">
              <div className="border-b border-slate-800 pb-2 flex items-center justify-between">
                <div>
                  <h3 className="text-sm font-bold text-white">{selectedNode.nodeId} Fault Injection</h3>
                  <p className="text-[11px] text-slate-400">{selectedNode.name}</p>
                </div>
                <button
                  onClick={handleClearFaults}
                  className="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 rounded text-[11px]"
                >
                  Clear Node Faults
                </button>
              </div>

              {/* Inject DTC */}
              <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg space-y-2">
                <span className="text-xs font-semibold text-slate-300 flex items-center space-x-1">
                  <AlertTriangle className="w-3.5 h-3.5 text-amber-400" />
                  <span>Inject Diagnostic Trouble Code</span>
                </span>
                <div className="flex space-x-2">
                  <input
                    type="text"
                    value={faultDtc}
                    onChange={e => setFaultDtc(e.target.value.toUpperCase())}
                    placeholder="e.g. P0300, P0171"
                    className="flex-1 bg-slate-950 border border-slate-800 rounded px-2.5 py-1 text-xs text-white uppercase font-mono"
                  />
                  <button
                    onClick={handleInjectDtc}
                    className="px-3 py-1 bg-amber-600 hover:bg-amber-500 text-white rounded text-xs font-semibold"
                  >
                    Inject
                  </button>
                </div>
                {selectedNode.activeDtcs.length > 0 && (
                  <div className="text-[11px] text-amber-300">
                    Active: {selectedNode.activeDtcs.join(', ')}
                  </div>
                )}
              </div>

              {/* Voltage Override */}
              <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg space-y-2">
                <span className="text-xs font-semibold text-slate-300 flex items-center space-x-1">
                  <Zap className="w-3.5 h-3.5 text-sky-400" />
                  <span>Supply Voltage Override</span>
                </span>
                <div className="flex items-center space-x-2">
                  <input
                    type="range"
                    min="9.0"
                    max="16.0"
                    step="0.1"
                    value={voltageOverride}
                    onChange={e => setVoltageOverride(parseFloat(e.target.value))}
                    className="flex-1 accent-sky-500"
                  />
                  <span className="w-12 text-right font-bold text-white">{voltageOverride.toFixed(1)}V</span>
                  <button
                    onClick={handleInjectVoltage}
                    className="px-2.5 py-1 bg-sky-600 hover:bg-sky-500 text-white rounded text-[11px] font-semibold"
                  >
                    Apply
                  </button>
                </div>
              </div>

              {/* Bus Drop Toggle */}
              <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg flex items-center justify-between">
                <div>
                  <div className="font-semibold text-white">Simulate CAN Bus Sever / Offline</div>
                  <div className="text-[11px] text-slate-400">Node ceases all transmissions</div>
                </div>
                <button
                  onClick={() => handleToggleBusDrop(selectedNode.injectedBusDrop)}
                  className={`px-3 py-1.5 rounded text-xs font-semibold transition-all ${
                    selectedNode.injectedBusDrop
                      ? 'bg-rose-600 text-white'
                      : 'bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700'
                  }`}
                >
                  {selectedNode.injectedBusDrop ? 'Restore Bus' : 'Drop Node'}
                </button>
              </div>

              {/* Node Simulated Signals */}
              <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg space-y-1.5">
                <span className="text-xs font-semibold text-slate-300">Live Simulated Signals</span>
                <div className="space-y-1 text-[11px]">
                  {Object.entries(selectedNode.simulatedSignals).map(([k, v]) => (
                    <div key={k} className="flex justify-between border-b border-slate-800/40 pb-0.5">
                      <span className="text-slate-400">{k}</span>
                      <span className="text-emerald-400 font-bold">{v}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          ) : (
            <div className="p-8 text-center text-slate-500">Select a node to inspect and inject faults.</div>
          )}
        </div>
      </div>
    </div>
  );
};
