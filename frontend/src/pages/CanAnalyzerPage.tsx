import React, { useState, useMemo, useRef, useEffect } from 'react';
import { useCan } from '../context/CanContext';
import { CanFrame } from '../types';
import { FrameInspector } from '../components/FrameInspector';
import { Binary, Play, Pause, Trash2, Search, Filter, ArrowDown } from 'lucide-react';

export const CanAnalyzerPage: React.FC = () => {
  const { frames, isStreaming, setIsStreaming, clearFrames, selectedFrame, setSelectedFrame, fps } = useCan();

  const [searchText, setSearchText] = useState('');
  const [typeFilter, setTypeFilter] = useState<'all' | 'standard' | 'extended'>('all');
  const [protocolFilter, setProtocolFilter] = useState<'all' | 'classic' | 'canfd'>('all');
  const [channelFilter, setChannelFilter] = useState<string>('all');
  const [rtrFilter, setRtrFilter] = useState<'all' | 'data' | 'rtr'>('all');
  const [dlcFilter, setDlcFilter] = useState<string>('all');
  const [autoScroll, setAutoScroll] = useState(true);

  const tableBottomRef = useRef<HTMLDivElement | null>(null);

  // Filter frames
  const filteredFrames = useMemo(() => {
    const q = searchText.trim().toLowerCase();
    return frames.filter(f => {
      // Type filter
      if (typeFilter === 'standard' && f.isExtended) return false;
      if (typeFilter === 'extended' && !f.isExtended) return false;

      // Protocol filter (Classic CAN vs CAN FD)
      if (protocolFilter === 'classic' && f.isCanFd) return false;
      if (protocolFilter === 'canfd' && !f.isCanFd) return false;

      // Channel filter
      if (channelFilter !== 'all' && (f.channel ?? 0) !== parseInt(channelFilter, 10)) return false;

      // RTR filter
      if (rtrFilter === 'data' && f.isRtr) return false;
      if (rtrFilter === 'rtr' && !f.isRtr) return false;

      // DLC filter
      if (dlcFilter !== 'all' && f.dlc !== parseInt(dlcFilter, 10)) return false;

      // Search filter
      if (q) {
        const idHex = f.id.toString(16).toLowerCase();
        const idDec = f.id.toString();
        const len = f.actualLength ?? (f.isCanFd ? Math.min(64, f.data?.length || 0) : Math.min(8, f.dlc));
        const dataHex = f.data ? f.data.slice(0, len).map(b => b.toString(16).padStart(2, '0')).join(' ').toLowerCase() : '';

        if (!idHex.includes(q) && !idDec.includes(q) && !dataHex.includes(q)) {
          return false;
        }
      }

      return true;
    });
  }, [frames, searchText, typeFilter, protocolFilter, channelFilter, rtrFilter, dlcFilter]);

  // Auto-scroll to bottom on new frames
  useEffect(() => {
    if (autoScroll && isStreaming && tableBottomRef.current) {
      tableBottomRef.current.scrollIntoView({ behavior: 'auto' });
    }
  }, [filteredFrames.length, autoScroll, isStreaming]);

  return (
    <div className="flex h-full gap-4 p-4 font-mono select-none overflow-hidden">
      {/* Main Table Column */}
      <div className="flex-1 bg-can-panel border border-can-border rounded-lg flex flex-col shadow-xl overflow-hidden">
        {/* Toolbar */}
        <div className="p-3 border-b border-can-border bg-can-panel flex flex-wrap items-center justify-between gap-3 text-xs">
          <div className="flex items-center space-x-2">
            <Binary className="w-4 h-4 text-sky-400" />
            <span className="font-bold text-white font-sans">CAN ANALYZER</span>
            <span className="text-[11px] text-slate-500">
              ({filteredFrames.length.toLocaleString()} frames, {fps} FPS)
            </span>
          </div>

          {/* Filters & Controls */}
          <div className="flex items-center space-x-2">
            {/* Search */}
            <div className="relative">
              <Search className="w-3.5 h-3.5 absolute left-2.5 top-2 text-slate-500" />
              <input
                type="text"
                value={searchText}
                onChange={e => setSearchText(e.target.value)}
                placeholder="Filter ID / Data..."
                className="bg-can-bg border border-can-border rounded pl-8 pr-3 py-1 text-xs text-white focus:outline-none focus:border-sky-500 w-32"
              />
            </div>

            {/* Channel Filter */}
            <select
              value={channelFilter}
              onChange={e => setChannelFilter(e.target.value)}
              className="bg-can-bg border border-can-border rounded px-2 py-1 text-xs text-slate-300 focus:outline-none focus:border-sky-500"
            >
              <option value="all">CH: All</option>
              <option value="0">CAN1</option>
              <option value="1">CAN2</option>
            </select>

            {/* Protocol Filter */}
            <select
              value={protocolFilter}
              onChange={e => setProtocolFilter(e.target.value as any)}
              className="bg-can-bg border border-can-border rounded px-2 py-1 text-xs text-slate-300 focus:outline-none focus:border-sky-500"
            >
              <option value="all">Protocol: All</option>
              <option value="classic">Classic CAN</option>
              <option value="canfd">CAN FD</option>
            </select>

            {/* Type Filter */}
            <select
              value={typeFilter}
              onChange={e => setTypeFilter(e.target.value as any)}
              className="bg-can-bg border border-can-border rounded px-2 py-1 text-xs text-slate-300 focus:outline-none focus:border-sky-500"
            >
              <option value="all">Type: All</option>
              <option value="standard">STD (11-bit)</option>
              <option value="extended">EXT (29-bit)</option>
            </select>

            {/* RTR Filter */}
            <select
              value={rtrFilter}
              onChange={e => setRtrFilter(e.target.value as any)}
              className="bg-can-bg border border-can-border rounded px-2 py-1 text-xs text-slate-300 focus:outline-none focus:border-sky-500"
            >
              <option value="all">RTR: All</option>
              <option value="data">Data Only</option>
              <option value="rtr">RTR Only</option>
            </select>

            {/* DLC Filter */}
            <select
              value={dlcFilter}
              onChange={e => setDlcFilter(e.target.value)}
              className="bg-can-bg border border-can-border rounded px-2 py-1 text-xs text-slate-300 focus:outline-none focus:border-sky-500"
            >
              <option value="all">DLC: All</option>
              {[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15].map(d => (
                <option key={d} value={d}>{d}</option>
              ))}
            </select>

            {/* Auto Scroll Toggle */}
            <button
              onClick={() => setAutoScroll(!autoScroll)}
              className={`p-1.5 rounded border transition-colors ${
                autoScroll
                  ? 'bg-sky-500/20 text-sky-400 border-sky-500/40'
                  : 'bg-can-bg text-slate-500 border-can-border hover:text-slate-300'
              }`}
              title="Auto-scroll"
            >
              <ArrowDown className="w-3.5 h-3.5" />
            </button>

            {/* Pause / Resume */}
            <button
              onClick={() => setIsStreaming(!isStreaming)}
              className={`px-2.5 py-1 rounded font-semibold flex items-center space-x-1.5 border transition-colors ${
                isStreaming
                  ? 'bg-slate-800 text-slate-300 border-can-border hover:bg-slate-700'
                  : 'bg-amber-500/20 text-amber-300 border-amber-500/40'
              }`}
            >
              {isStreaming ? <Pause className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5" />}
              <span>{isStreaming ? 'PAUSE' : 'RESUME'}</span>
            </button>

            {/* Clear */}
            <button
              onClick={clearFrames}
              className="p-1.5 bg-can-bg hover:bg-rose-500/20 text-slate-400 hover:text-rose-300 rounded border border-can-border transition-colors"
              title="Clear Table"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>

        {/* Frame Table */}
        <div className="flex-1 overflow-y-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead className="sticky top-0 bg-[#0B0F17] text-slate-400 text-[10px] uppercase font-semibold border-b border-can-border z-10 whitespace-nowrap">
              <tr>
                <th className="p-2 w-12 text-center">#</th>
                <th className="p-2 w-28">Timestamp (µs)</th>
                <th className="p-2 w-16 text-center">CH</th>
                <th className="p-2 w-24">CAN ID</th>
                <th className="p-2 w-20">Type</th>
                <th className="p-2 w-20">Protocol</th>
                <th className="p-2 w-12 text-center">DLC</th>
                <th className="p-2 w-12 text-center">Len</th>
                <th className="p-2 w-12 text-center">BRS</th>
                <th className="p-2 w-12 text-center">ESI</th>
                <th className="p-2 w-12 text-center">RTR</th>
                <th className="p-2 min-w-48">Data Bytes (Hex)</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-can-border">
              {filteredFrames.slice(-1000).map((frame, idx) => {
                const idHex = frame.isExtended
                  ? `0x${frame.id.toString(16).toUpperCase().padStart(8, '0')}`
                  : `0x${frame.id.toString(16).toUpperCase().padStart(3, '0')}`;

                const isSelected = selectedFrame?.timestampUs === frame.timestampUs && selectedFrame?.id === frame.id;
                const payloadLen = frame.actualLength ?? (frame.isCanFd ? Math.min(64, frame.data?.length || 0) : Math.min(8, frame.dlc));
                const chName = `CAN${(frame.channel ?? 0) + 1}`;

                return (
                  <tr
                    key={idx}
                    onClick={() => setSelectedFrame(frame)}
                    className={`cursor-pointer transition-colors whitespace-nowrap ${
                      isSelected
                        ? 'bg-sky-600/20 text-white'
                        : 'hover:bg-can-card text-slate-300'
                    }`}
                  >
                    <td className="p-2 text-slate-500 text-[11px] text-center">{idx + 1}</td>
                    <td className="p-2 font-mono text-slate-400">{frame.timestampUs.toLocaleString()}</td>
                    <td className="p-2 text-center">
                      <span className={`px-1.5 py-0.5 rounded text-[10px] font-bold ${
                        (frame.channel ?? 0) === 1
                          ? 'bg-purple-500/20 text-purple-300 border border-purple-500/30'
                          : 'bg-sky-500/20 text-sky-300 border border-sky-500/30'
                      }`}>
                        {chName}
                      </span>
                    </td>
                    <td className="p-2 font-bold text-sky-400">{idHex}</td>
                    <td className="p-2 text-slate-400">{frame.isExtended ? 'EXT (29b)' : 'STD (11b)'}</td>
                    <td className="p-2">
                      <span className={`px-1.5 py-0.5 rounded text-[10px] font-bold ${
                        frame.isCanFd
                          ? 'bg-purple-500/20 text-purple-300 border border-purple-500/40'
                          : 'bg-slate-800 text-slate-400 border border-can-border'
                      }`}>
                        {frame.isCanFd ? 'CAN FD' : 'Classic'}
                      </span>
                    </td>
                    <td className="p-2 font-bold text-emerald-400 text-center">{frame.dlc}</td>
                    <td className="p-2 text-slate-300 text-center font-bold">{payloadLen}B</td>
                    <td className="p-2 text-center">
                      <span className={`px-1 py-0.5 rounded text-[9px] font-bold ${frame.bitRateSwitch ? 'bg-amber-500/20 text-amber-300 border border-amber-500/40' : 'text-slate-600'}`}>
                        {frame.bitRateSwitch ? 'BRS' : '—'}
                      </span>
                    </td>
                    <td className="p-2 text-center">
                      <span className={`px-1 py-0.5 rounded text-[9px] font-bold ${frame.errorStateIndicator ? 'bg-rose-500/20 text-rose-300 border border-rose-500/40' : 'text-slate-600'}`}>
                        {frame.errorStateIndicator ? 'ESI' : '—'}
                      </span>
                    </td>
                    <td className="p-2 text-center">
                      <span className={`px-1.5 py-0.5 rounded text-[10px] font-bold ${frame.isRtr ? 'bg-amber-500/20 text-amber-300' : 'text-slate-600'}`}>
                        {frame.isRtr ? 'RTR' : '—'}
                      </span>
                    </td>
                    <td className="p-2 font-mono text-slate-200 tracking-wider">
                      {frame.data ? frame.data.slice(0, payloadLen).map(b => b.toString(16).toUpperCase().padStart(2, '0')).join(' ') : ''}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <div ref={tableBottomRef} />
        </div>
      </div>

      {/* Selected Frame Inspector Side Panel */}
      {selectedFrame && (
        <div className="w-96">
          <FrameInspector frame={selectedFrame} onClose={() => setSelectedFrame(null)} />
        </div>
      )}
    </div>
  );
};
