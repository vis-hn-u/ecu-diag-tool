import React, { useState } from 'react';
import { useCan } from '../context/CanContext';
import { api } from '../services/api';
import { FileCode, Upload, Trash2, CheckCircle2, ChevronDown, ChevronRight, Layers, FileText } from 'lucide-react';
import { DbcMessage } from '../types';

export const DbcManagerPage: React.FC = () => {
  const { activeDbc, refreshDbc } = useCan();
  const [uploading, setUploading] = useState(false);
  const [expandedMsgIds, setExpandedMsgIds] = useState<number[]>([]);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  const handleFileUpload = async () => {
    if (!selectedFile) return;
    setUploading(true);
    try {
      await api.uploadDbc(selectedFile);
      await refreshDbc();
      setSelectedFile(null);
    } catch {
      // Ignore
    } finally {
      setUploading(false);
    }
  };

  const handleLoadSample = async () => {
    setUploading(true);
    try {
      await api.loadSampleDbc();
      await refreshDbc();
    } catch {
      // Ignore
    } finally {
      setUploading(false);
    }
  };

  const handleClear = async () => {
    await api.clearActiveDbc();
    await refreshDbc();
  };

  const toggleExpand = (id: number) => {
    setExpandedMsgIds(prev =>
      prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
    );
  };

  return (
    <div className="p-6 space-y-6 overflow-y-auto h-full font-mono select-none">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-can-border pb-4">
        <div>
          <div className="flex items-center space-x-2 text-white font-bold text-base font-sans">
            <FileCode className="w-5 h-5 text-sky-400" />
            <span>DBC DATABASE MANAGER</span>
          </div>
          <p className="text-xs text-slate-400 font-sans mt-0.5">
            Import, inspect, and manage Vector CANdb (.dbc) definitions for signal decoding and network topology.
          </p>
        </div>

        <div className="flex items-center space-x-3 text-xs">
          <button
            onClick={handleLoadSample}
            disabled={uploading}
            className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-semibold font-sans flex items-center space-x-1.5 transition-all shadow"
          >
            <CheckCircle2 className="w-3.5 h-3.5" />
            <span>Load Sample DBC</span>
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

      {/* Upload Box */}
      <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow-md">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="p-3 bg-sky-500/10 rounded-lg border border-sky-500/20 text-sky-400">
              <Upload className="w-6 h-6" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white font-sans">Upload .DBC File</h3>
              <p className="text-xs text-slate-400 font-sans mt-0.5">
                Supports Standard CAN 2.0A (11-bit) and Extended CAN 2.0B (29-bit) message definitions.
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
        <div className="space-y-4">
          <div className="flex items-center justify-between text-xs text-slate-400 border-b border-can-border pb-2">
            <span className="font-bold text-white uppercase tracking-wider">
              ACTIVE DATABASE: {activeDbc.messageCount} Messages, {activeDbc.nodeCount} ECU Nodes
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
        <div className="text-center py-12 text-slate-500 text-xs">
          <FileText className="w-12 h-12 mx-auto mb-2 text-slate-600" />
          <p>No active DBC file loaded. Upload a DBC or click "Load Sample DBC" above.</p>
        </div>
      )}
    </div>
  );
};
