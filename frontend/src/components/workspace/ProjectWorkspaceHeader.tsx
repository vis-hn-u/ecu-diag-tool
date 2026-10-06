import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { EngineeringProject, OneClickSessionReport } from '../../types';
import { 
  FolderGit2, 
  Play, 
  CheckCircle2, 
  AlertCircle, 
  Search, 
  Layers, 
  Cpu, 
  Car, 
  Activity, 
  Sparkles,
  ChevronDown
} from 'lucide-react';

interface ProjectWorkspaceHeaderProps {
  onStartOneClick?: (report: OneClickSessionReport) => void;
  onOpenCopilot?: () => void;
}

export const ProjectWorkspaceHeader: React.FC<ProjectWorkspaceHeaderProps> = ({ onStartOneClick, onOpenCopilot }) => {
  const [project, setProject] = useState<EngineeringProject | null>(null);
  const [loading, setLoading] = useState(false);
  const [oneClickRunning, setOneClickRunning] = useState(false);
  const [oneClickResult, setOneClickResult] = useState<OneClickSessionReport | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [isSearching, setIsSearching] = useState(false);

  useEffect(() => {
    loadActiveProject();
  }, []);

  const loadActiveProject = async () => {
    try {
      const p = await api.getActiveProject();
      setProject(p);
    } catch {
      // fallback
    }
  };

  const handleOneClickStart = async () => {
    setOneClickRunning(true);
    try {
      const res = await api.startOneClickSession({
        projectName: project?.name || 'Quick Session',
        ecuName: project?.ecuTarget.ecuName || 'ECM',
        requestCanId: project?.ecuTarget.requestCanId || 0x7E0,
        responseCanId: project?.ecuTarget.responseCanId || 0x7E8,
        vin: project?.vehicle.vin
      });
      setOneClickResult(res);
      if (onStartOneClick) onStartOneClick(res);
    } catch (e) {
      console.error(e);
    } finally {
      setOneClickRunning(false);
    }
  };

  const handleSearch = async (q: string) => {
    setSearchQuery(q);
    if (q.trim().length > 1) {
      setIsSearching(true);
      try {
        const res = await api.searchGlobal(q);
        setSearchResults(res);
      } catch {
        setSearchResults([]);
      } finally {
        setIsSearching(false);
      }
    } else {
      setSearchResults([]);
    }
  };

  return (
    <div className="bg-[#101726] border-b border-slate-800 px-4 py-2.5 flex flex-wrap items-center justify-between gap-4">
      {/* Active Project & Target Vehicle */}
      <div className="flex items-center space-x-3">
        <div className="p-2 bg-sky-500/10 border border-sky-500/30 rounded-lg text-sky-400">
          <FolderGit2 className="w-5 h-5" />
        </div>
        <div>
          <div className="flex items-center space-x-2">
            <span className="text-xs font-bold text-white font-mono tracking-wide">
              {project?.name || 'Active Engineering Project'}
            </span>
            <span className="px-1.5 py-0.5 text-[10px] font-mono rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
              OPEN
            </span>
          </div>
          <div className="flex items-center space-x-3 text-[11px] text-slate-400 font-mono mt-0.5">
            <span className="flex items-center space-x-1">
              <Car className="w-3 h-3 text-slate-500" />
              <span>{project?.vehicle.make} {project?.vehicle.model} ({project?.vehicle.year})</span>
            </span>
            <span>•</span>
            <span className="flex items-center space-x-1">
              <Cpu className="w-3 h-3 text-indigo-400" />
              <span className="text-indigo-300 font-semibold">{project?.ecuTarget.ecuName} ({project?.ecuTarget.family})</span>
            </span>
            <span>•</span>
            <span>0x{project?.ecuTarget.requestCanId.toString(16).toUpperCase()}</span>
          </div>
        </div>
      </div>

      {/* Global Cross-Domain Search */}
      <div className="relative flex-1 max-w-md">
        <div className="relative">
          <Search className="w-3.5 h-3.5 absolute left-3 top-2.5 text-slate-500" />
          <input
            type="text"
            placeholder="Global Search (VIN, CAN ID, DTC, A2L, Firmware, DID)..."
            value={searchQuery}
            onChange={e => handleSearch(e.target.value)}
            className="w-full bg-[#0B0F19] border border-slate-800 rounded-lg pl-9 pr-4 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-sky-500 transition-colors font-mono"
          />
        </div>
        {searchResults.length > 0 && (
          <div className="absolute top-full left-0 right-0 mt-1 bg-[#121927] border border-slate-700 rounded-lg shadow-2xl z-50 max-h-64 overflow-y-auto p-1 font-mono text-xs">
            {searchResults.map((r, i) => (
              <div key={i} className="p-2 hover:bg-slate-800/60 rounded cursor-pointer border-b border-slate-800/40 last:border-0">
                <div className="flex items-center justify-between">
                  <span className="text-sky-300 font-semibold">{r.title}</span>
                  <span className="text-[10px] text-slate-400 bg-slate-800 px-1.5 py-0.5 rounded">{r.category}</span>
                </div>
                <div className="text-[11px] text-slate-400">{r.subtitle}</div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Action Buttons */}
      <div className="flex items-center space-x-2">
        {onOpenCopilot && (
          <button
            onClick={onOpenCopilot}
            className="flex items-center space-x-1.5 px-3 py-1.5 bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 border border-indigo-500/30 rounded-lg text-xs font-mono font-medium transition-all"
          >
            <Sparkles className="w-3.5 h-3.5 text-indigo-400" />
            <span>AI Copilot</span>
          </button>
        )}

        <button
          onClick={handleOneClickStart}
          disabled={oneClickRunning}
          className="flex items-center space-x-2 px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-lg text-xs font-mono font-semibold shadow-lg shadow-emerald-900/20 transition-all"
        >
          {oneClickRunning ? (
            <>
              <Activity className="w-3.5 h-3.5 animate-spin text-white" />
              <span>Initializing Session...</span>
            </>
          ) : (
            <>
              <Play className="w-3.5 h-3.5 fill-current" />
              <span>START ENGINEERING SESSION</span>
            </>
          )}
        </button>
      </div>

      {/* One-Click Result Banner if complete */}
      {oneClickResult && (
        <div className="w-full bg-emerald-950/40 border border-emerald-500/30 rounded-lg p-2.5 flex items-center justify-between text-xs font-mono text-emerald-300 animate-fadeIn">
          <div className="flex items-center space-x-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-400" />
            <span>
              <strong>Automated Engineering Session Online:</strong> Transport: <em>{oneClickResult.activeTransportId}</em> • 
              ECU: <em>{oneClickResult.identifiedEcuProfile}</em> • Protocol: <em>{oneClickResult.detectedProtocol}</em> • 
              DTC Baseline: <em>{oneClickResult.activeDtcCount} active</em>
            </span>
          </div>
          <button
            onClick={() => setOneClickResult(null)}
            className="text-slate-400 hover:text-white text-[11px]"
          >
            Dismiss
          </button>
        </div>
      )}
    </div>
  );
};
