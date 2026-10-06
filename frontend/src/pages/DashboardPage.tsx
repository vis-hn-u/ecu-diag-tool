import React, { useState, useEffect } from 'react';
import { useCan } from '../context/CanContext';
import { 
  Activity, Binary, Gauge, Network, PlaySquare, Disc, FileCode, CheckCircle2, 
  AlertTriangle, ShieldCheck, Sliders, Plus, RotateCcw, Trash2, Edit2, Layout, Sparkles 
} from 'lucide-react';
import { api } from '../services/api';
import { TabType } from '../components/Sidebar';
import { DashboardLayout, DashboardWidget } from '../types/diagnosticsExtended';
import { RadialGaugeWidget } from '../components/dashboard/RadialGaugeWidget';
import { DigitalValueWidget } from '../components/dashboard/DigitalValueWidget';
import { MiniGraphWidget } from '../components/dashboard/MiniGraphWidget';
import { LevelBarWidget } from '../components/dashboard/LevelBarWidget';
import { StatusAnnunciatorWidget } from '../components/dashboard/StatusAnnunciatorWidget';
import { WidgetEditorModal } from '../components/dashboard/WidgetEditorModal';

interface DashboardProps {
  onNavigate: (tab: TabType) => void;
}

export const DashboardPage: React.FC<DashboardProps> = ({ onNavigate }) => {
  const { fps, totalFrames, activeDbc, topology, gatewayStatus, signals, refreshDbc } = useCan();
  const [viewMode, setViewMode] = useState<'designer' | 'overview'>('designer');

  // Dashboard layout state
  const [activeLayout, setActiveLayout] = useState<DashboardLayout | null>(null);
  const [isCustomizeMode, setIsCustomizeMode] = useState<boolean>(false);
  const [editingWidget, setEditingWidget] = useState<DashboardWidget | null>(null);
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);

  // Peak tracking map
  const [peaks, setPeaks] = useState<Record<string, number>>({});

  const loadPresetLayout = async (presetKey: string) => {
    try {
      const layout = await api.getDashboardPreset(presetKey);
      if (layout) setActiveLayout(layout);
    } catch {
      // Fallback local preset
    }
  };

  useEffect(() => {
    loadPresetLayout('track_cockpit');
  }, []);

  const handleSaveWidget = (widget: DashboardWidget) => {
    if (!activeLayout) return;

    setActiveLayout(prev => {
      if (!prev) return prev;
      const exists = prev.widgets.some(w => w.id === widget.id);
      const updatedWidgets = exists
        ? prev.widgets.map(w => (w.id === widget.id ? widget : w))
        : [...prev.widgets, widget];
      const nextLayout = { ...prev, widgets: updatedWidgets };
      api.saveDashboardLayout(nextLayout);
      return nextLayout;
    });
  };

  const handleDeleteWidget = (id: string) => {
    if (!activeLayout) return;
    setActiveLayout(prev => {
      if (!prev) return prev;
      const nextLayout = { ...prev, widgets: prev.widgets.filter(w => w.id !== id) };
      api.saveDashboardLayout(nextLayout);
      return nextLayout;
    });
  };

  // Helper to extract live value for any widget source
  const getWidgetLiveValue = (widget: DashboardWidget): number => {
    // Check DBC decoded signals first
    const signalMatch = Object.entries(signals).find(([name]) => 
      name.toLowerCase() === widget.sourceIdentifier.toLowerCase() ||
      name.toLowerCase().includes(widget.sourceIdentifier.toLowerCase())
    );
    if (signalMatch && typeof signalMatch[1].engineeringValue === 'number') {
      const val = signalMatch[1].engineeringValue;
      if (peaks[widget.id] === undefined || val > peaks[widget.id]) {
        setPeaks(p => ({ ...p, [widget.id]: val }));
      }
      return val;
    }

    // Realistic fallback/simulated readings for standalone testing
    const now = Date.now() / 1000.0;
    if (widget.title.includes('ENGINE SPEED') || widget.unit === 'RPM') {
      return 2200 + Math.sin(now * 1.5) * 1800 + 1000;
    }
    if (widget.title.includes('BOOST') || widget.unit === 'bar') {
      return Math.max(0, 0.4 + Math.sin(now * 1.5) * 0.9);
    }
    if (widget.title.includes('SPEED') || widget.unit === 'km/h') {
      return Math.max(0, 60 + Math.sin(now * 0.8) * 45);
    }
    if (widget.title.includes('LAMBDA') || widget.unit === 'λ') {
      return 0.88 + Math.sin(now * 2.0) * 0.08;
    }
    if (widget.title.includes('COOLANT') || widget.unit === '°C') {
      return 89.5 + Math.sin(now * 0.1) * 2.0;
    }
    if (widget.title.includes('FUEL') && widget.unit === 'bar') {
      return 145.0 + Math.sin(now * 1.5) * 25.0;
    }
    if (widget.type === 'StatusAnnunciator') {
      return 0; // Nominal off
    }
    return (widget.minValue + widget.maxValue) / 2.0;
  };

  const handleLoadSampleDbc = async () => {
    await api.loadSampleDbc();
    await refreshDbc();
  };

  const handleStartSim = async () => {
    await api.generateSampleReplay();
    await api.playReplay();
    onNavigate('topology');
  };

  return (
    <div className="p-6 space-y-6 overflow-y-auto h-full font-mono">
      {/* Top Header & Mode Toggle Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow flex flex-wrap items-center justify-between gap-3 font-sans">
        <div className="flex items-center space-x-3">
          <div className="p-2 bg-sky-500/10 border border-sky-500/30 rounded-lg text-sky-400">
            <Layout className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-white tracking-wide">Vehicle Instrumentation & Dashboard</h1>
            <p className="text-xs text-slate-400">
              Customizable real-time gauge cluster, digital telemetry cards, level bars, and status warning annunciators
            </p>
          </div>
        </div>

        <div className="flex items-center space-x-2">
          {/* Mode Switcher */}
          <div className="bg-can-bg border border-can-border rounded p-0.5 flex text-xs">
            <button
              onClick={() => setViewMode('designer')}
              className={`px-3 py-1.5 rounded font-bold transition-all flex items-center space-x-1.5 ${
                viewMode === 'designer' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
              }`}
            >
              <Gauge className="w-3.5 h-3.5" />
              <span>Cockpit Designer</span>
            </button>
            <button
              onClick={() => setViewMode('overview')}
              className={`px-3 py-1.5 rounded font-bold transition-all flex items-center space-x-1.5 ${
                viewMode === 'overview' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
              }`}
            >
              <Activity className="w-3.5 h-3.5" />
              <span>Platform Overview</span>
            </button>
          </div>

          {viewMode === 'designer' && (
            <>
              {/* Preset Selector */}
              <select
                value={activeLayout?.presetKey || ''}
                onChange={e => loadPresetLayout(e.target.value)}
                className="bg-can-bg border border-can-border text-white text-xs rounded px-2.5 py-1.5 font-bold"
              >
                <option value="track_cockpit">Track Cockpit Cluster</option>
                <option value="boost_fuel">Boost & Fuel Diagnostics</option>
              </select>

              {/* Customize Mode Toggle */}
              <button
                onClick={() => setIsCustomizeMode(!isCustomizeMode)}
                className={`px-3 py-1.5 rounded text-xs font-bold border transition-all flex items-center space-x-1.5 ${
                  isCustomizeMode
                    ? 'bg-amber-600/30 border-amber-500 text-amber-300 shadow'
                    : 'bg-can-bg border-can-border text-slate-300 hover:text-white'
                }`}
              >
                <Sliders className="w-3.5 h-3.5" />
                <span>{isCustomizeMode ? 'Done Customizing' : 'Customize Layout'}</span>
              </button>

              {/* Add Widget Button */}
              {isCustomizeMode && (
                <button
                  onClick={() => {
                    setEditingWidget(null);
                    setIsModalOpen(true);
                  }}
                  className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-bold flex items-center space-x-1.5 shadow"
                >
                  <Plus className="w-3.5 h-3.5" />
                  <span>Add Widget</span>
                </button>
              )}
            </>
          )}
        </div>
      </div>

      {/* DESIGNER COCKPIT MODE */}
      {viewMode === 'designer' && (
        <div className="space-y-4">
          {/* Active Grid of Customizable Widgets */}
          <div className="grid grid-cols-12 gap-4">
            {activeLayout?.widgets.map(w => {
              const liveVal = getWidgetLiveValue(w);
              const colSpanClass = 
                w.position.w === 2 ? 'col-span-12 md:col-span-2' :
                w.position.w === 3 ? 'col-span-12 md:col-span-3' :
                w.position.w === 4 ? 'col-span-12 md:col-span-4' :
                w.position.w === 6 ? 'col-span-12 md:col-span-6' :
                'col-span-12';

              return (
                <div
                  key={w.id}
                  className={`bg-can-panel border rounded-xl shadow-md relative overflow-hidden transition-all group ${
                    isCustomizeMode ? 'border-amber-500/60 ring-1 ring-amber-500/30' : 'border-can-border hover:border-sky-500/40'
                  } ${colSpanClass}`}
                  style={{ minHeight: '170px' }}
                >
                  {/* Customize Controls Overlay */}
                  {isCustomizeMode && (
                    <div className="absolute top-2 right-2 z-20 flex items-center space-x-1 bg-slate-900/90 border border-slate-700 rounded p-1 shadow">
                      <button
                        onClick={() => {
                          setEditingWidget(w);
                          setIsModalOpen(true);
                        }}
                        className="p-1 hover:bg-sky-500/20 text-sky-400 rounded"
                        title="Edit Widget"
                      >
                        <Edit2 className="w-3 h-3" />
                      </button>
                      <button
                        onClick={() => handleDeleteWidget(w.id)}
                        className="p-1 hover:bg-rose-500/20 text-rose-400 rounded"
                        title="Delete Widget"
                      >
                        <Trash2 className="w-3 h-3" />
                      </button>
                    </div>
                  )}

                  {/* Render Specific Widget */}
                  {w.type === 'RadialGauge' && (
                    <RadialGaugeWidget widget={w} currentValue={liveVal} />
                  )}
                  {w.type === 'DigitalValue' && (
                    <DigitalValueWidget widget={w} currentValue={liveVal} peakValue={peaks[w.id]} />
                  )}
                  {w.type === 'MiniGraph' && (
                    <MiniGraphWidget widget={w} currentValue={liveVal} />
                  )}
                  {w.type === 'LevelBar' && (
                    <LevelBarWidget widget={w} currentValue={liveVal} />
                  )}
                  {w.type === 'StatusAnnunciator' && (
                    <StatusAnnunciatorWidget widget={w} currentValue={liveVal} />
                  )}
                </div>
              );
            })}
          </div>

          {(!activeLayout || activeLayout.widgets.length === 0) && (
            <div className="p-12 text-center border-2 border-dashed border-can-border rounded-xl">
              <Gauge className="w-12 h-12 text-slate-600 mx-auto mb-3" />
              <div className="text-sm font-bold text-slate-300">No Widgets in Active Layout</div>
              <p className="text-xs text-slate-500 mt-1">Load a preset above or click 'Add Widget' to design your dashboard.</p>
            </div>
          )}
        </div>
      )}

      {/* PLATFORM OVERVIEW MODE */}
      {viewMode === 'overview' && (
        <div className="space-y-6">
          {/* Welcome Banner */}
          <div className="bg-gradient-to-r from-sky-900/40 via-can-card to-can-panel border border-sky-500/30 rounded-lg p-5 flex items-center justify-between shadow-lg font-sans">
            <div>
              <div className="flex items-center space-x-2 text-sky-400 text-xs uppercase font-bold tracking-wider mb-1">
                <ShieldCheck className="w-4 h-4" />
                <span>Phase 2 CAN Engineering Platform Active</span>
              </div>
              <h2 className="text-xl font-bold text-white">High-Speed Automotive Bus Diagnostics & Telemetry</h2>
              <p className="text-xs text-slate-400 mt-1 max-w-2xl">
                Authoritative backend pipeline connected to ESP8266 + MCP2515 Gateway or Waveshare USB-CAN-FD-B with real-time DBC decoding and interactive topology.
              </p>
            </div>

            <div className="flex items-center space-x-3">
              {!activeDbc?.isLoaded && (
                <button
                  onClick={handleLoadSampleDbc}
                  className="px-3 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-semibold flex items-center space-x-2 shadow-md transition-all font-sans"
                >
                  <FileCode className="w-4 h-4" />
                  <span>Load Sample DBC</span>
                </button>
              )}
              <button
                onClick={handleStartSim}
                className="px-3 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-semibold flex items-center space-x-2 shadow-md transition-all font-sans"
              >
                <PlaySquare className="w-4 h-4" />
                <span>Start Driving Simulation</span>
              </button>
            </div>
          </div>

          {/* Metrics Cards */}
          <div className="grid grid-cols-4 gap-4">
            <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-md">
              <div className="flex justify-between items-center text-slate-400 text-xs mb-2 font-sans">
                <span>CAN BUS THROUGHPUT</span>
                <Activity className="w-4 h-4 text-sky-400" />
              </div>
              <div className="text-2xl font-bold text-white">{fps} <span className="text-xs text-slate-400 font-normal">FPS</span></div>
              <div className="text-[11px] text-slate-500 mt-1">Total: {totalFrames.toLocaleString()} frames</div>
            </div>

            <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-md">
              <div className="flex justify-between items-center text-slate-400 text-xs mb-2 font-sans">
                <span>ACTIVE ECU NODES</span>
                <Network className="w-4 h-4 text-emerald-400" />
              </div>
              <div className="text-2xl font-bold text-emerald-400">
                {topology?.activeNodeCount ?? 0} <span className="text-xs text-slate-400 font-normal">/ {topology?.nodes?.length ?? 0}</span>
              </div>
              <div className="text-[11px] text-slate-500 mt-1">Network Topology online</div>
            </div>

            <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-md">
              <div className="flex justify-between items-center text-slate-400 text-xs mb-2 font-sans">
                <span>DECODED SIGNALS</span>
                <Gauge className="w-4 h-4 text-amber-400" />
              </div>
              <div className="text-2xl font-bold text-amber-400">{Object.keys(signals).length}</div>
              <div className="text-[11px] text-slate-500 mt-1">
                DBC: {activeDbc?.isLoaded ? `${activeDbc.messageCount} msgs` : 'None loaded'}
              </div>
            </div>

            <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-md">
              <div className="flex justify-between items-center text-slate-400 text-xs mb-2 font-sans">
                <span>ESP8266 GATEWAY</span>
                <CheckCircle2 className="w-4 h-4 text-sky-400" />
              </div>
              <div className={`text-lg font-bold ${gatewayStatus?.isConnected ? 'text-sky-400' : 'text-slate-400'}`}>
                {gatewayStatus?.isConnected ? 'CONNECTED' : 'OFFLINE'}
              </div>
              <div className="text-[11px] text-slate-500 mt-1 truncate">
                {gatewayStatus?.gatewayAddress || '192.168.4.1:8088'}
              </div>
            </div>
          </div>

          {/* Quick Navigation Cards */}
          <div className="grid grid-cols-3 gap-4 font-sans">
            <div
              onClick={() => onNavigate('analyzer')}
              className="bg-can-panel hover:bg-can-card border border-can-border hover:border-sky-500/40 rounded-lg p-5 cursor-pointer transition-all shadow-md group"
            >
              <div className="flex items-center space-x-3 mb-2">
                <div className="p-2 bg-sky-500/10 rounded border border-sky-500/20 text-sky-400 group-hover:scale-105 transition-transform">
                  <Binary className="w-5 h-5" />
                </div>
                <h3 className="text-sm font-bold text-white">CAN Analyzer</h3>
              </div>
              <p className="text-xs text-slate-400">
                Inspect raw CAN & CAN FD frame streams, filter by ID/flags/DLC, search payloads, and examine bitfield matrix.
              </p>
            </div>

            <div
              onClick={() => onNavigate('livedata')}
              className="bg-can-panel hover:bg-can-card border border-can-border hover:border-emerald-500/40 rounded-lg p-5 cursor-pointer transition-all shadow-md group"
            >
              <div className="flex items-center space-x-3 mb-2">
                <div className="p-2 bg-emerald-500/10 rounded border border-emerald-500/20 text-emerald-400 group-hover:scale-105 transition-transform">
                  <Gauge className="w-5 h-5" />
                </div>
                <h3 className="text-sm font-bold text-white">DBC Signals</h3>
              </div>
              <p className="text-xs text-slate-400">
                Real-time physical engineering values (RPM, Speed, Temperature, Voltage) decoded from active DBC definitions.
              </p>
            </div>

            <div
              onClick={() => onNavigate('diagnostics')}
              className="bg-can-panel hover:bg-can-card border border-can-border hover:border-purple-500/40 rounded-lg p-5 cursor-pointer transition-all shadow-md group"
            >
              <div className="flex items-center space-x-3 mb-2">
                <div className="p-2 bg-purple-500/10 rounded border border-purple-500/20 text-purple-400 group-hover:scale-105 transition-transform">
                  <Network className="w-5 h-5" />
                </div>
                <h3 className="text-sm font-bold text-white">Diagnostics & UDS</h3>
              </div>
              <p className="text-xs text-slate-400">
                UDS services, VAG Measuring Blocks, freeze-frame snapshots, and contextual DTC signal correlation.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Widget Editor Modal */}
      <WidgetEditorModal
        widget={editingWidget}
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSaveWidget}
      />
    </div>
  );
};
