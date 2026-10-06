import React, { useState, useMemo } from 'react';
import { useCan } from '../context/CanContext';
import { NetworkNode, TopologyEdge, CanMessageInfo } from '../types';
import { Network, ZoomIn, ZoomOut, RotateCcw, Activity, ArrowRight, Info, Search, GitFork, Radio } from 'lucide-react';

export const TopologyGraph: React.FC = () => {
  const { topology, signals, activeDbc } = useCan();
  const [selectedNode, setSelectedNode] = useState<NetworkNode | null>(null);
  const [selectedSignalName, setSelectedSignalName] = useState<string | null>(null);
  const [zoom, setZoom] = useState<number>(1.0);
  const [filterText, setFilterText] = useState<string>('');
  const [viewType, setViewType] = useState<'observed' | 'reference'>('observed');

  const nodes = topology?.nodes || [];
  const edges = topology?.edges || [];
  const messages = topology?.messages || [];

  // Filtered nodes
  const filteredNodes = useMemo(() => {
    let list = nodes;
    if (viewType === 'reference') {
      // Show only nodes defined in DBC
      list = nodes.filter(n => n.source === 'Dbc');
    }
    if (!filterText.trim()) return list;
    const q = filterText.toLowerCase();
    return list.filter(n => n.name.toLowerCase().includes(q) || n.type.toLowerCase().includes(q));
  }, [nodes, filterText, viewType]);

  // Layout positions for nodes around a central CAN Bus spine
  const nodePositions = useMemo(() => {
    const pos: Record<string, { x: number; y: number }> = {};
    const total = filteredNodes.length;
    if (total === 0) return pos;

    const centerX = 450;
    const centerY = 260;
    const radiusX = 320;
    const radiusY = 180;

    filteredNodes.forEach((node, idx) => {
      const angle = (idx / total) * Math.PI * 2 - Math.PI / 2;
      pos[node.name] = {
        x: centerX + Math.cos(angle) * radiusX,
        y: centerY + Math.sin(angle) * radiusY
      };
    });

    return pos;
  }, [filteredNodes]);

  // Traced edges if a signal or node is selected
  const activeTracedEdges = useMemo(() => {
    if (selectedSignalName) {
      return edges.filter(e => e.signalName === selectedSignalName);
    }
    if (selectedNode) {
      return edges.filter(e => e.source === selectedNode.name || e.target === selectedNode.name);
    }
    return edges;
  }, [edges, selectedSignalName, selectedNode]);

  return (
    <div className="flex h-full gap-4 font-mono select-none">
      {/* Topology Canvas Area */}
      <div className="flex-1 bg-can-panel border border-can-border rounded-lg flex flex-col relative overflow-hidden shadow-xl">
        {/* Graph Controls Toolbar */}
        <div className="p-3 border-b border-can-border flex items-center justify-between z-10 bg-can-panel/90 backdrop-blur-sm">
          <div className="flex items-center space-x-3">
            {/* View Mode Toggle: Observed vs Reference */}
            <div className="flex items-center bg-can-bg rounded border border-can-border p-0.5 text-xs">
              <button
                onClick={() => setViewType('observed')}
                className={`px-2.5 py-1 rounded font-semibold flex items-center space-x-1.5 transition-all ${
                  viewType === 'observed'
                    ? 'bg-sky-600 text-white shadow'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                <Radio className="w-3 h-3" />
                <span>LIVE OBSERVED</span>
              </button>
              <button
                onClick={() => setViewType('reference')}
                className={`px-2.5 py-1 rounded font-semibold flex items-center space-x-1.5 transition-all ${
                  viewType === 'reference'
                    ? 'bg-purple-600 text-white shadow'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                <GitFork className="w-3 h-3" />
                <span>DBC REFERENCE</span>
              </button>
            </div>

            <span className="text-[11px] text-slate-500">
              ({topology?.activeNodeCount ?? 0} Active ECUs, {messages.length} Messages, {edges.length} Signal Links)
            </span>
          </div>

          <div className="flex items-center space-x-2">
            <div className="relative">
              <Search className="w-3.5 h-3.5 absolute left-2.5 top-2 text-slate-500" />
              <input
                type="text"
                value={filterText}
                onChange={e => setFilterText(e.target.value)}
                placeholder="Filter nodes..."
                className="bg-can-bg border border-can-border rounded pl-8 pr-3 py-1 text-xs text-white focus:outline-none focus:border-sky-500 w-36"
              />
            </div>

            <button
              onClick={() => setZoom(z => Math.min(z + 0.15, 2.0))}
              className="p-1.5 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border"
              title="Zoom In"
            >
              <ZoomIn className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={() => setZoom(z => Math.max(z - 0.15, 0.5))}
              className="p-1.5 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border"
              title="Zoom Out"
            >
              <ZoomOut className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={() => { setZoom(1.0); setSelectedNode(null); setSelectedSignalName(null); }}
              className="p-1.5 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border"
              title="Reset View"
            >
              <RotateCcw className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>

        {/* SVG Graph View */}
        <div className="flex-1 relative overflow-hidden bg-[#0A0E17]">
          <svg
            className="w-full h-full cursor-grab active:cursor-grabbing"
            viewBox="0 0 900 520"
            style={{ transform: `scale(${zoom})`, transformOrigin: 'center center', transition: 'transform 0.15s ease-out' }}
          >
            <defs>
              <linearGradient id="busGrad" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stopColor="#0284C7" stopOpacity="0.2" />
                <stop offset="50%" stopColor="#38BDF8" stopOpacity="0.8" />
                <stop offset="100%" stopColor="#0284C7" stopOpacity="0.2" />
              </linearGradient>

              <marker id="arrow" viewBox="0 0 10 10" refX="22" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
                <path d="M 0 1 L 10 5 L 0 9 z" fill="#38BDF8" />
              </marker>
              <marker id="arrow-active" viewBox="0 0 10 10" refX="22" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
                <path d="M 0 1 L 10 5 L 0 9 z" fill="#10B981" />
              </marker>
            </defs>

            {/* Central CAN Bus Spine Line */}
            <g>
              <line x1="120" y1="260" x2="780" y2="260" stroke="url(#busGrad)" strokeWidth="6" strokeLinecap="round" />
              <line x1="120" y1="260" x2="780" y2="260" stroke="#0284C7" strokeWidth="2" strokeDasharray="6,6" opacity="0.6" />
              <text x="450" y="252" fill="#38BDF8" fontSize="11" textAnchor="middle" fontWeight="bold" letterSpacing="2">
                HIGH-SPEED CAN BUS (500 kbit/s)
              </text>
            </g>

            {/* Edges / Signal Paths */}
            {activeTracedEdges.map((edge, idx) => {
              const srcPos = nodePositions[edge.source];
              const tgtPos = nodePositions[edge.target];
              if (!srcPos || !tgtPos) return null;

              const isHighlighted = selectedSignalName === edge.signalName;
              const isActive = edge.state === 'Active';

              return (
                <g key={idx}>
                  <line
                    x1={srcPos.x}
                    y1={srcPos.y}
                    x2={tgtPos.x}
                    y2={tgtPos.y}
                    stroke={isHighlighted ? '#10B981' : isActive ? '#38BDF8' : '#334155'}
                    strokeWidth={isHighlighted ? 2.5 : isActive ? 1.5 : 1}
                    strokeDasharray={isActive ? '4,4' : undefined}
                    markerEnd={isActive ? 'url(#arrow-active)' : 'url(#arrow)'}
                    opacity={isHighlighted ? 1 : 0.7}
                  />
                </g>
              );
            })}

            {/* Bus Drop Lines connecting each Node to Central Spine */}
            {filteredNodes.map(node => {
              const pos = nodePositions[node.name];
              if (!pos) return null;

              return (
                <line
                  key={`drop-${node.name}`}
                  x1={pos.x}
                  y1={pos.y}
                  x2={pos.x}
                  y2="260"
                  stroke="#1E293B"
                  strokeWidth="1.5"
                  strokeDasharray="2,2"
                />
              );
            })}

            {/* Nodes */}
            {filteredNodes.map(node => {
              const pos = nodePositions[node.name];
              if (!pos) return null;

              const isSelected = selectedNode?.name === node.name;
              const isActive = node.status === 'Active';
              const isUnknown = node.name.startsWith('UNKNOWN');

              return (
                <g
                  key={node.name}
                  transform={`translate(${pos.x}, ${pos.y})`}
                  className="cursor-pointer group"
                  onClick={() => setSelectedNode(node)}
                >
                  {/* Active Pulse Glow */}
                  {isActive && (
                    <circle r="32" fill="none" stroke="#38BDF8" strokeWidth="1.5" opacity="0.4" className="animate-ping" />
                  )}

                  {/* Node Outer Circle */}
                  <circle
                    r="26"
                    fill={isUnknown ? '#1F1417' : isSelected ? '#0369A1' : '#182234'}
                    stroke={isSelected ? '#38BDF8' : isActive ? (isUnknown ? '#F43F5E' : '#0EA5E9') : '#334155'}
                    strokeWidth={isSelected ? 3 : 2}
                  />

                  {/* Status Ring */}
                  <circle
                    r="4"
                    cx="18"
                    cy="-18"
                    fill={isActive ? '#10B981' : '#64748B'}
                    stroke="#0B0F17"
                    strokeWidth="1.5"
                  />

                  {/* Node Name */}
                  <text
                    y="4"
                    fill="#FFFFFF"
                    fontSize="11"
                    fontWeight="bold"
                    textAnchor="middle"
                    pointerEvents="none"
                  >
                    {node.name.length > 8 ? node.name.substring(0, 7) + '..' : node.name}
                  </text>

                  {/* Node Type & Rate */}
                  <text
                    y="38"
                    fill={isActive ? '#38BDF8' : '#64748B'}
                    fontSize="9"
                    textAnchor="middle"
                    fontWeight="500"
                  >
                    {isActive ? `${node.txRateHz} Hz` : node.type}
                  </text>
                </g>
              );
            })}
          </svg>
        </div>
      </div>

      {/* Node & Signal Details Inspector Panel */}
      <div className="w-80 bg-can-panel border border-can-border rounded-lg p-4 flex flex-col justify-between overflow-y-auto shadow-xl space-y-4">
        <div>
          <div className="flex items-center space-x-2 border-b border-can-border pb-2.5 mb-3">
            <Info className="w-4 h-4 text-sky-400" />
            <h2 className="text-sm font-semibold text-white font-sans">
              {selectedNode ? `NODE: ${selectedNode.name}` : 'NETWORK TRACE'}
            </h2>
          </div>

          {selectedNode ? (
            <div className="space-y-3 text-xs">
              {/* Node Summary */}
              <div className="bg-can-card p-3 rounded border border-can-border space-y-1.5">
                <div className="flex justify-between">
                  <span className="text-slate-400">Type:</span>
                  <span className="text-white font-semibold">{selectedNode.type}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Status:</span>
                  <span className={`font-semibold ${selectedNode.status === 'Active' ? 'text-emerald-400' : 'text-slate-500'}`}>
                    {selectedNode.status}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">TX Frequency:</span>
                  <span className="text-sky-400 font-bold">{selectedNode.txRateHz} Hz</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Total Messages:</span>
                  <span className="text-slate-200">{selectedNode.messageCount.toLocaleString()}</span>
                </div>
              </div>

              {/* Transmitted Messages */}
              <div>
                <span className="text-[11px] text-slate-400 font-semibold block mb-1.5 uppercase">
                  Transmitted Messages
                </span>
                <div className="space-y-1.5">
                  {messages.filter(m => m.transmitter === selectedNode.name).map(m => (
                    <div key={m.id} className="bg-can-bg p-2 rounded border border-can-border">
                      <div className="flex justify-between items-center mb-1">
                        <span className="font-bold text-sky-400">{m.hexId}</span>
                        <span className="text-[10px] text-slate-400 font-sans">{m.name}</span>
                      </div>
                      <div className="flex flex-wrap gap-1 mt-1">
                        {m.signals.map(sName => (
                          <button
                            key={sName}
                            onClick={() => setSelectedSignalName(sName === selectedSignalName ? null : sName)}
                            className={`px-1.5 py-0.5 rounded text-[10px] border transition-all ${
                              selectedSignalName === sName
                                ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40 font-bold'
                                : 'bg-can-card text-slate-300 border-can-border hover:bg-slate-700'
                            }`}
                          >
                            {sName}
                          </button>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          ) : (
            <div className="text-center py-12 text-slate-500 text-xs">
              <Activity className="w-8 h-8 mx-auto mb-2 text-slate-600 animate-pulse" />
              <p>Click any ECU node in the network to inspect transmitted CAN messages, active signals, and receiver lineage.</p>
            </div>
          )}
        </div>

        {/* Active Signal Lineage Tracer */}
        {selectedSignalName && (
          <div className="bg-can-card p-3 rounded border border-emerald-500/30 text-xs space-y-2">
            <div className="flex items-center space-x-1.5 text-emerald-400 font-bold">
              <ArrowRight className="w-3.5 h-3.5" />
              <span>SIGNAL TRACE: {selectedSignalName}</span>
            </div>
            {signals[selectedSignalName] && (
              <div className="text-[11px] space-y-1 text-slate-300">
                <div>Live Value: <span className="text-white font-bold">{signals[selectedSignalName].formattedValue}</span></div>
                <div>Source: <span className="text-sky-400">{signals[selectedSignalName].transmitterNode}</span></div>
                <div>Receivers: <span className="text-emerald-300">{signals[selectedSignalName].receiverNodes.join(', ') || 'Broadcast'}</span></div>
              </div>
            )}
            <button
              onClick={() => setSelectedSignalName(null)}
              className="w-full mt-2 py-1 text-[11px] bg-slate-800 hover:bg-slate-700 text-slate-300 rounded"
            >
              Clear Signal Highlight
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
