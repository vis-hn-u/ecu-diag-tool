import React, { useState, useEffect, useMemo } from 'react';
import { useCan } from '../context/CanContext';
import { api } from '../services/api';
import { 
  FileCode, Upload, Trash2, CheckCircle2, ChevronDown, ChevronRight, 
  FileText, Search, Cpu, Car, Gauge, Eye, RefreshCw, 
  Database, Check, X, Copy, Zap
} from 'lucide-react';
import { DbcMessage, DbcLibraryItem } from '../types';

export const DbcManagerPage: React.FC = () => {
  const { activeDbc, refreshDbc } = useCan();
  const [library, setLibrary] = useState<DbcLibraryItem[]>([]);
  const [loadingLibrary, setLoadingLibrary] = useState(false);
  const [activatingKey, setActivatingKey] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('All');
  const [expandedMsgIds, setExpandedMsgIds] = useState<number[]>([]);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  
  // Modal for viewing raw DBC file content
  const [viewingDbc, setViewingDbc] = useState<{ title: string; content: string } | null>(null);
  const [loadingContent, setLoadingContent] = useState(false);
  const [copied, setCopied] = useState(false);

  // Load DBC library on mount
  const loadLibrary = async () => {
    setLoadingLibrary(true);
    try {
      const items = await api.getDbcLibrary();
      setLibrary(items);
    } catch {
      // Ignore
    } finally {
      setLoadingLibrary(false);
    }
  };

  useEffect(() => {
    loadLibrary();
  }, []);

  // Update active state in library when activeDbc changes
  useEffect(() => {
    if (activeDbc) {
      loadLibrary();
    }
  }, [activeDbc?.messageCount, activeDbc?.nodeCount]);

  const handleActivateFromLibrary = async (item: DbcLibraryItem) => {
    setActivatingKey(item.key);
    try {
      await api.loadDbcFromLibrary(item.key);
      await refreshDbc();
      await loadLibrary();
    } catch {
      // Ignore
    } finally {
      setActivatingKey(null);
    }
  };

  const handleViewContent = async (item: DbcLibraryItem) => {
    setLoadingContent(true);
    try {
      const content = await api.getDbcContent(item.key);
      setViewingDbc({ title: item.title, content });
      setCopied(false);
    } catch {
      // Ignore
    } finally {
      setLoadingContent(false);
    }
  };

  const handleCopyContent = () => {
    if (viewingDbc) {
      navigator.clipboard.writeText(viewingDbc.content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleFileUpload = async () => {
    if (!selectedFile) return;
    setUploading(true);
    try {
      await api.uploadDbc(selectedFile);
      await refreshDbc();
      await loadLibrary();
      setSelectedFile(null);
    } catch {
      // Ignore
    } finally {
      setUploading(false);
    }
  };

  const handleClear = async () => {
    await api.clearActiveDbc();
    await refreshDbc();
    await loadLibrary();
  };

  const toggleExpand = (id: number) => {
    setExpandedMsgIds(prev =>
      prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
    );
  };

  // Categories list
  const categories = useMemo(() => {
    const set = new Set<string>();
    library.forEach(i => set.add(i.category));
    return ['All', ...Array.from(set)];
  }, [library]);

  // Filtered library items
  const filteredLibrary = useMemo(() => {
    return library.filter(item => {
      const matchesCategory = selectedCategory === 'All' || item.category === selectedCategory;
      const q = searchQuery.toLowerCase().trim();
      if (!q) return matchesCategory;

      const matchesSearch = 
        item.title.toLowerCase().includes(q) ||
        item.ecuFamily.toLowerCase().includes(q) ||
        item.manufacturer.toLowerCase().includes(q) ||
        item.description.toLowerCase().includes(q) ||
        item.keySignals.some(s => s.toLowerCase().includes(q)) ||
        item.fileName.toLowerCase().includes(q);

      return matchesCategory && matchesSearch;
    });
  }, [library, selectedCategory, searchQuery]);

  return (
    <div className="p-6 space-y-6 overflow-y-auto h-full font-mono select-none">
      {/* Top Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between border-b border-can-border pb-4 gap-4">
        <div>
          <div className="flex items-center space-x-2 text-white font-bold text-base font-sans">
            <Database className="w-5 h-5 text-sky-400" />
            <span>AUTOMOTIVE ECU & VEHICLE DBC CONTROL CENTER</span>
          </div>
          <p className="text-xs text-slate-400 font-sans mt-0.5">
            Real-time CANdb (.dbc) definitions for Indian & global automotive ECUs (Bosch EDC16/17/MD1, Denso, Marelli, Continental, Tata, Mahindra, J1939).
          </p>
        </div>

        <div className="flex items-center space-x-3 text-xs">
          <button
            onClick={loadLibrary}
            disabled={loadingLibrary}
            className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 border border-can-border rounded font-semibold font-sans flex items-center space-x-1.5 transition-all"
            title="Refresh library catalog"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${loadingLibrary ? 'animate-spin text-sky-400' : ''}`} />
            <span>Refresh</span>
          </button>

          {activeDbc?.isLoaded && (
            <button
              onClick={handleClear}
              className="px-3 py-1.5 bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 rounded font-semibold font-sans flex items-center space-x-1.5 transition-all"
            >
              <Trash2 className="w-3.5 h-3.5" />
              <span>Clear Active DBC</span>
            </button>
          )}
        </div>
      </div>

      {/* Active Database Summary Card */}
      <div className="bg-gradient-to-r from-slate-900 via-can-panel to-slate-900 border border-can-border rounded-lg p-4 shadow-md">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="flex items-center space-x-3">
            <div className={`p-2.5 rounded-lg border ${activeDbc?.isLoaded ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-400' : 'bg-slate-800 border-slate-700 text-slate-500'}`}>
              <Zap className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider font-sans">Active CAN Decoder:</span>
                {activeDbc?.isLoaded ? (
                  <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                    LIVE & DECODING
                  </span>
                ) : (
                  <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                    NO DBC LOADED
                  </span>
                )}
              </div>
              <h2 className="text-sm font-bold text-white font-sans mt-0.5">
                {activeDbc?.isLoaded 
                  ? (library.find(l => l.isActive)?.title || 'Custom Loaded DBC Database')
                  : 'Select an ECU from the library below or upload your custom .dbc file'}
              </h2>
            </div>
          </div>

          {activeDbc?.isLoaded && (
            <div className="flex items-center space-x-4 text-xs font-mono bg-black/40 px-3 py-2 rounded border border-can-border">
              <div>
                <span className="text-slate-500 text-[10px] block">MESSAGES</span>
                <span className="text-sky-400 font-bold">{activeDbc.messageCount}</span>
              </div>
              <div className="border-l border-can-border pl-4">
                <span className="text-slate-500 text-[10px] block">ECU NODES</span>
                <span className="text-emerald-400 font-bold">{activeDbc.nodeCount}</span>
              </div>
              <div className="border-l border-can-border pl-4">
                <span className="text-slate-500 text-[10px] block">TOTAL SIGNALS</span>
                <span className="text-amber-400 font-bold">
                  {activeDbc.messages?.reduce((acc, m) => acc + (m.signals?.length || 0), 0) || 0}
                </span>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Library Section */}
      <div className="space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-can-border pb-3">
          <div className="flex items-center space-x-2">
            <Cpu className="w-4 h-4 text-sky-400" />
            <h3 className="text-sm font-bold text-white font-sans uppercase tracking-wide">
              Automotive ECU Library Catalog ({library.length} Databases)
            </h3>
          </div>

          {/* Search Input */}
          <div className="relative w-full sm:w-72">
            <Search className="w-3.5 h-3.5 absolute left-3 top-2.5 text-slate-500" />
            <input
              type="text"
              placeholder="Search ECU, car (e.g. Thar, Swift, EDC17)..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              className="w-full pl-8 pr-3 py-1.5 bg-slate-900 border border-can-border rounded text-xs text-white placeholder-slate-500 focus:outline-none focus:border-sky-500 font-sans"
            />
            {searchQuery && (
              <button 
                onClick={() => setSearchQuery('')}
                className="absolute right-2.5 top-2 text-slate-400 hover:text-white"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        </div>

        {/* Category Filter Chips */}
        <div className="flex items-center space-x-2 overflow-x-auto pb-2 scrollbar-thin">
          {categories.map(cat => (
            <button
              key={cat}
              onClick={() => setSelectedCategory(cat)}
              className={`px-3 py-1 rounded-full text-xs font-sans whitespace-nowrap transition-all ${
                selectedCategory === cat
                  ? 'bg-sky-600 text-white font-semibold shadow'
                  : 'bg-slate-800 text-slate-400 hover:bg-slate-700 hover:text-white border border-can-border'
              }`}
            >
              {cat}
            </button>
          ))}
        </div>

        {/* DBC Cards Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filteredLibrary.map(item => {
            const isCurrentlyActive = item.isActive;
            const isActivating = activatingKey === item.key;

            return (
              <div
                key={item.key}
                className={`flex flex-col justify-between bg-can-panel border rounded-lg p-4 transition-all shadow-sm ${
                  isCurrentlyActive
                    ? 'border-emerald-500/60 ring-1 ring-emerald-500/40 bg-emerald-950/10'
                    : 'border-can-border hover:border-slate-600 hover:bg-slate-800/40'
                }`}
              >
                <div className="space-y-3">
                  {/* Card Header */}
                  <div className="flex items-start justify-between gap-2">
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-sky-500/10 text-sky-400 border border-sky-500/20 uppercase tracking-wider font-sans">
                      {item.ecuFamily}
                    </span>
                    {isCurrentlyActive ? (
                      <span className="flex items-center space-x-1 px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                        <Check className="w-3 h-3" />
                        <span>ACTIVE</span>
                      </span>
                    ) : (
                      <span className="text-[10px] text-slate-500 font-mono">
                        {item.protocol}
                      </span>
                    )}
                  </div>

                  {/* Title & Description */}
                  <div>
                    <h4 className="text-sm font-bold text-white font-sans leading-snug">
                      {item.title}
                    </h4>
                    <p className="text-[11px] text-slate-400 font-sans mt-1 leading-relaxed line-clamp-3">
                      {item.description}
                    </p>
                  </div>

                  {/* Key Signals Preview */}
                  {item.keySignals && item.keySignals.length > 0 && (
                    <div className="space-y-1">
                      <span className="text-[10px] text-slate-500 uppercase tracking-wider font-sans">Live Signals:</span>
                      <div className="flex flex-wrap gap-1">
                        {item.keySignals.map(sig => (
                          <span
                            key={sig}
                            className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-800/80 text-slate-300 border border-can-border/60"
                          >
                            {sig}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Metrics Row */}
                  <div className="flex items-center space-x-3 text-[11px] text-slate-400 pt-2 border-t border-can-border/60">
                    <div>
                      <span className="text-white font-bold">{item.messageCount}</span> Messages
                    </div>
                    <div>•</div>
                    <div>
                      <span className="text-white font-bold">{item.signalCount}</span> Signals
                    </div>
                    <div>•</div>
                    <div>
                      <span className="text-white font-bold">{item.nodes.length}</span> Nodes
                    </div>
                  </div>
                </div>

                {/* Card Actions */}
                <div className="flex items-center space-x-2 pt-4 mt-2">
                  <button
                    onClick={() => handleActivateFromLibrary(item)}
                    disabled={isActivating || isCurrentlyActive}
                    className={`flex-1 py-1.5 px-3 rounded text-xs font-semibold font-sans flex items-center justify-center space-x-1.5 transition-all ${
                      isCurrentlyActive
                        ? 'bg-emerald-600/20 text-emerald-300 border border-emerald-500/30 cursor-default'
                        : 'bg-sky-600 hover:bg-sky-500 text-white shadow'
                    }`}
                  >
                    {isActivating ? (
                      <>
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                        <span>Activating...</span>
                      </>
                    ) : isCurrentlyActive ? (
                      <>
                        <CheckCircle2 className="w-3.5 h-3.5" />
                        <span>Loaded & Active</span>
                      </>
                    ) : (
                      <>
                        <Zap className="w-3.5 h-3.5" />
                        <span>Load & Activate</span>
                      </>
                    )}
                  </button>

                  <button
                    onClick={() => handleViewContent(item)}
                    disabled={loadingContent}
                    className="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 border border-can-border rounded transition-all"
                    title="View Vector DBC Syntax"
                  >
                    <Eye className="w-4 h-4" />
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        {filteredLibrary.length === 0 && (
          <div className="text-center py-12 text-slate-500 text-xs bg-can-panel border border-can-border rounded-lg">
            <Search className="w-8 h-8 mx-auto mb-2 text-slate-600" />
            <p>No DBC databases found matching "{searchQuery}". Try a different keyword.</p>
          </div>
        )}
      </div>

      {/* Upload Custom File Box */}
      <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow-md">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-center space-x-3">
            <div className="p-3 bg-sky-500/10 rounded-lg border border-sky-500/20 text-sky-400">
              <Upload className="w-6 h-6" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white font-sans">Upload Custom .DBC File</h3>
              <p className="text-xs text-slate-400 font-sans mt-0.5">
                Bring your own Vector CANdb (.dbc) file for proprietary ECUs or custom test bench setups.
              </p>
            </div>
          </div>

          <div className="flex items-center space-x-3">
            <input
              type="file"
              accept=".dbc"
              id="dbc-file-input"
              className="hidden"
              onChange={e => setSelectedFile(e.target.files?.[0] || null)}
            />
            <label
              htmlFor="dbc-file-input"
              className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border rounded text-xs cursor-pointer font-sans"
            >
              {selectedFile ? selectedFile.name : 'Select .DBC File'}
            </label>
            {selectedFile && (
              <button
                onClick={handleFileUpload}
                disabled={uploading}
                className="px-4 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-semibold font-sans"
              >
                {uploading ? 'Parsing...' : 'Upload & Activate'}
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Active DBC Inspection Browser */}
      {activeDbc?.isLoaded ? (
        <div className="space-y-4 pt-2">
          <div className="flex items-center justify-between text-xs text-slate-400 border-b border-can-border pb-2">
            <span className="font-bold text-white uppercase tracking-wider font-sans">
              ACTIVE DBC SIGNAL DICTIONARY: {activeDbc.messageCount} Messages, {activeDbc.nodeCount} ECU Nodes
            </span>
            <span>Version: {activeDbc.version || '1.0'}</span>
          </div>

          {/* Messages & Signals Accordion */}
          <div className="space-y-2">
            {activeDbc.messages?.map((msg: DbcMessage) => {
              const isExpanded = expandedMsgIds.includes(msg.id);
              const hexId = `0x${(msg.cleanId ?? msg.id).toString(16).toUpperCase().padStart(3, '0')}`;

              return (
                <div key={msg.id} className="bg-can-panel border border-can-border rounded-lg overflow-hidden shadow">
                  <div
                    onClick={() => toggleExpand(msg.id)}
                    className="p-3 bg-can-card hover:bg-slate-800 cursor-pointer flex items-center justify-between text-xs transition-colors"
                  >
                    <div className="flex items-center space-x-3">
                      {isExpanded ? <ChevronDown className="w-4 h-4 text-sky-400" /> : <ChevronRight className="w-4 h-4 text-slate-500" />}
                      <span className="font-bold text-sky-400">{hexId}</span>
                      <span className="text-white font-bold font-sans">{msg.name}</span>
                      <span className="text-[10px] text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
                        TX: {msg.transmitter}
                      </span>
                      <span className="text-slate-400 text-[10px]">DLC: {msg.dlc} Bytes</span>
                    </div>

                    <div className="text-slate-400 text-xs font-sans">
                      {msg.signals.length} Signals
                    </div>
                  </div>

                  {isExpanded && (
                    <div className="p-3 border-t border-can-border bg-can-bg overflow-x-auto">
                      <table className="w-full text-left text-xs border-collapse">
                        <thead>
                          <tr className="text-slate-500 text-[10px] uppercase border-b border-can-border">
                            <th className="p-1.5">Signal Name</th>
                            <th className="p-1.5">Bit Range</th>
                            <th className="p-1.5">Order</th>
                            <th className="p-1.5">Type</th>
                            <th className="p-1.5">Factor</th>
                            <th className="p-1.5">Offset</th>
                            <th className="p-1.5">Min / Max</th>
                            <th className="p-1.5">Unit</th>
                            <th className="p-1.5">Receivers</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-can-border/60">
                          {msg.signals.map(sig => (
                            <tr key={sig.name} className="hover:bg-can-panel text-slate-300">
                              <td className="p-1.5 font-bold text-white">{sig.name}</td>
                              <td className="p-1.5 font-mono text-sky-400">
                                {sig.startBit} | {sig.length}b
                              </td>
                              <td className="p-1.5 text-slate-400">{sig.isLittleEndian ? 'Intel (LE)' : 'Motorola (BE)'}</td>
                              <td className="p-1.5 text-slate-400">{sig.isSigned ? 'Signed' : 'Unsigned'}</td>
                              <td className="p-1.5 font-mono">{sig.factor}</td>
                              <td className="p-1.5 font-mono">{sig.offset}</td>
                              <td className="p-1.5 font-mono text-slate-400">[{sig.minimum} | {sig.maximum}]</td>
                              <td className="p-1.5 text-emerald-400 font-bold">{sig.unit || '—'}</td>
                              <td className="p-1.5 text-slate-400 text-[11px]">{sig.receivers.join(', ') || 'Broadcast'}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      ) : (
        <div className="text-center py-8 text-slate-500 text-xs">
          <FileText className="w-8 h-8 mx-auto mb-2 text-slate-600" />
          <p>No active DBC file loaded. Select an ECU from the library above to begin signal decoding.</p>
        </div>
      )}

      {/* Raw DBC Code Modal */}
      {viewingDbc && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-can-panel border border-can-border rounded-xl max-w-4xl w-full max-h-[85vh] flex flex-col shadow-2xl overflow-hidden">
            <div className="flex items-center justify-between p-4 border-b border-can-border bg-slate-900">
              <div className="flex items-center space-x-2">
                <FileCode className="w-5 h-5 text-sky-400" />
                <h3 className="text-sm font-bold text-white font-sans">{viewingDbc.title}</h3>
              </div>
              <div className="flex items-center space-x-2">
                <button
                  onClick={handleCopyContent}
                  className="px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border rounded text-xs flex items-center space-x-1.5 transition-all font-sans"
                >
                  {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                  <span>{copied ? 'Copied!' : 'Copy Code'}</span>
                </button>
                <button
                  onClick={() => setViewingDbc(null)}
                  className="p-1 text-slate-400 hover:text-white rounded"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>
            </div>

            <div className="p-4 overflow-y-auto flex-1 font-mono text-xs bg-black/60 text-emerald-300 whitespace-pre">
              {viewingDbc.content}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
