import React, { useState, useEffect } from 'react';
import { useCan } from '../context/CanContext';
import { 
  Activity, Binary, Gauge, Network, PlaySquare, Disc, FileCode, CheckCircle2, 
  AlertTriangle, ShieldCheck, Sliders, Plus, RotateCcw, Trash2, Edit2, Layout, Sparkles,
  Search, Radio, Zap, Pin, Flame, ChevronRight
} from 'lucide-react';
import { api } from '../services/api';
import { TabType } from '../components/Sidebar';
import { DecodedSignal } from '../types';
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
  const { fps, totalFrames, activeDbc, topology, gatewayStatus, hardwareStatus, operatingMode, replayStatus, signals, refreshDbc } = useCan();
  const [viewMode, setViewMode] = useState<'designer' | 'overview'>('designer');

  // Dashboard layout state
  const [activeLayout, setActiveLayout] = useState<DashboardLayout | null>(null);
  const [isCustomizeMode, setIsCustomizeMode] = useState<boolean>(false);
  const [editingWidget, setEditingWidget] = useState<DashboardWidget | null>(null);
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);

  // Peak tracking map
  const [peaks, setPeaks] = useState<Record<string, number>>({});
  const [signalSearchFilter, setSignalSearchFilter] = useState<string>('');
  const [loadingDbcKey, setLoadingDbcKey] = useState<string | null>(null);

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

  const handlePinSignalAsWidget = (sig: DecodedSignal) => {
    if (!activeLayout) return;
    const nameLower = sig.signalName.toLowerCase();
    const unitLower = (sig.unit || '').toLowerCase();
    const isRpm = nameLower.includes('rpm') || unitLower === 'rpm' || nameLower.includes('drehzahl');
    const isTemp = unitLower === '°c' || nameLower.includes('temp') || nameLower.includes('kuehl');
    const isPressure = unitLower === 'bar' || nameLower.includes('press') || nameLower.includes('rail');
    const isSpeed = unitLower === 'km/h' || unitLower === 'mph' || nameLower.includes('speed') || nameLower.includes('geschw');

    const newWidget: DashboardWidget = {
      id: `widget_${Date.now()}_${Math.random().toString(36).substring(2, 6)}`,
      title: sig.signalName.toUpperCase(),
      subtitle: sig.messageName || 'Live CAN',
      sourceType: 'CanSignal',
      sourceIdentifier: sig.signalName,
      type: (isRpm || isSpeed) ? 'RadialGauge' : (isPressure || isTemp) ? 'LevelBar' : 'DigitalValue',
      unit: sig.unit || '',
      minValue: isRpm ? 0 : isSpeed ? 0 : isTemp ? 0 : isPressure ? 0 : 0,
      maxValue: isRpm ? 7000 : isSpeed ? 260 : isTemp ? 130 : isPressure ? 200 : 100,
      decimalPlaces: (isRpm || isSpeed) ? 0 : 1,
      warnThreshold: isRpm ? 5000 : isTemp ? 105 : undefined,
      colorScheme: isRpm ? 'amber' : isTemp ? 'rose' : isPressure ? 'sky' : 'emerald',
      position: { x: 0, y: 0, w: (isRpm || isSpeed) ? 4 : 3, h: 2 }
    };

    handleSaveWidget(newWidget);
  };

  const handleQuickLoadDbc = async (key: string) => {
    setLoadingDbcKey(key);
    try {
      await api.loadDbcFromLibrary(key);
      await refreshDbc();
    } catch {
      // Ignore
    } finally {
      setLoadingDbcKey(null);
    }
  };

  // Helper to extract live value for any widget source with fuzzy matching
  const findMatchingSignal = (widget: DashboardWidget): DecodedSignal | null => {
    const signalEntries = Object.entries(signals);
    if (signalEntries.length === 0) return null;

    // 1. Direct or substring match on sourceIdentifier
    if (widget.sourceIdentifier) {
      const idLower = widget.sourceIdentifier.toLowerCase();
      const direct = signalEntries.find(([name]) => 
        name.toLowerCase() === idLower || name.toLowerCase().includes(idLower)
      );
      if (direct) return direct[1];
    }

    const titleLower = widget.title.toLowerCase();
    const unitLower = (widget.unit || '').toLowerCase();

    const matchesAny = (candidates: string[]): DecodedSignal | null => {
      for (const [name, sig] of signalEntries) {
        const n = name.toLowerCase();
        for (const c of candidates) {
          if (n.includes(c)) return sig;
        }
      }
      return null;
    };

    // 2. Engine RPM / Speed
    if (unitLower === 'rpm' || titleLower.includes('rpm') || titleLower.includes('engine speed') || titleLower.includes('drehzahl') || titleLower.includes('engspd')) {
      const m = matchesAny(['rpm', 'engspd', 'eng_spd', 'drehzahl', 'enginespeed', 'engine_speed', 'motor_rpm', 'motordrehzahl', 'cr_ems_engspd', 'mo_drehzahl']);
      if (m) return m;
    }

    // 3. Vehicle Speed
    if (unitLower === 'km/h' || unitLower === 'mph' || titleLower.includes('speed') || titleLower.includes('velocity') || titleLower.includes('geschwindigkeit')) {
      const m = matchesAny(['vehspd', 'veh_spd', 'vehiclespeed', 'vehicle_speed', 'geschw', 'v_fzg', 'speed_vehicle', 'speed']);
      if (m) return m;
    }

    // 4. Coolant / Engine Temp
    if (unitLower === '°c' || unitLower === 'degc' || titleLower.includes('coolant') || titleLower.includes('water') || titleLower.includes('kuehl')) {
      const m = matchesAny(['coolant', 'cool_temp', 'engcool', 'water_temp', 'motortemp', 't_kuehl', 'temp_engine', 'ect']);
      if (m) return m;
    }

    // 5. Fuel Rail Pressure
    if ((unitLower === 'bar' || unitLower === 'mpa' || unitLower === 'psi') && (titleLower.includes('rail') || titleLower.includes('fuel'))) {
      const m = matchesAny(['rail', 'railpress', 'p_rail', 'fuelpress', 'fuel_press', 'common_rail']);
      if (m) return m;
    }

    // 6. Boost Pressure / MAP
    if ((unitLower === 'bar' || unitLower === 'psi' || unitLower === 'kpa') && (titleLower.includes('boost') || titleLower.includes('turbo') || titleLower.includes('manifold') || titleLower.includes('map'))) {
      const m = matchesAny(['boost', 'ladedruck', 'p_lade', 'manifold', 'map', 'turbo_pressure', 'air_press']);
      if (m) return m;
    }

    // 7. Throttle / Accelerator Pedal Position
    if (unitLower === '%' && (titleLower.includes('throttle') || titleLower.includes('pedal') || titleLower.includes('gas') || titleLower.includes('accel'))) {
      const m = matchesAny(['throttle', 'pedal', 'accel', 'gaspedal', 'app', 'throttlepos', 'gas_pedal', 'pedal_pos']);
      if (m) return m;
    }

    // 8. Battery Voltage
    if (unitLower === 'v' || titleLower.includes('volt') || titleLower.includes('batt') || titleLower.includes('spannung')) {
      const m = matchesAny(['batt', 'battery', 'voltage', 'ubatt', 'kl30', 'v_batt']);
      if (m) return m;
    }

    // 9. Engine Torque
    if (unitLower === 'nm' || titleLower.includes('torque') || titleLower.includes('drehmoment')) {
      const m = matchesAny(['torque', 'drehmoment', 'engtrq', 'tq', 'moment']);
      if (m) return m;
    }

    // 10. Intake Air Temperature
    if (titleLower.includes('intake') || titleLower.includes('iat') || titleLower.includes('ansaugluft')) {
      const m = matchesAny(['iat', 'intake', 'ansaug', 't_ansaug']);
      if (m) return m;
    }

    return null;
  };

  interface WidgetLiveState {
    value: number | null;
    quality: 'VALID' | 'STALE' | 'INVALID' | 'UNAVAILABLE';
    source: 'ECU' | 'OBD' | 'UDS' | 'DBC' | 'J1939' | 'SIMULATION' | 'REPLAY' | 'DERIVED' | 'NONE';
    ageMs: number;
  }

  const getWidgetLiveState = (widget: DashboardWidget): WidgetLiveState => {
    // 1. Priority: DBC Decoded Live CAN Bus Signals
    const matchedSignal = findMatchingSignal(widget);
    if (matchedSignal && typeof matchedSignal.engineeringValue === 'number') {
      const val = matchedSignal.engineeringValue;
      const sigTime = matchedSignal.timestampUtc ? new Date(matchedSignal.timestampUtc).getTime() : Date.now();
      const ageMs = Math.max(0, Date.now() - sigTime);
      const isStale = ageMs > 3500;
      const quality = isStale ? 'STALE' : 'VALID';

      const source = (operatingMode === 'Simulation' || hardwareStatus?.type === 'Simulation' || replayStatus?.isPlaying)
        ? 'SIMULATION'
        : 'DBC';

      if (quality === 'VALID' && (peaks[widget.id] === undefined || val > peaks[widget.id])) {
        setPeaks(p => ({ ...p, [widget.id]: val }));
      }

      return {
        value: val,
        quality,
        source,
        ageMs
      };
    }

    // 2. No matching signal available from bus - NEVER synthesize or fabricate fake sine waves
    return {
      value: null,
      quality: 'UNAVAILABLE',
      source: 'NONE',
      ageMs: 0
    };
  };

  const handleAutoMapFromDiscoveredSignals = () => {
    if (!activeLayout) return;
    const signalEntries = Object.entries(signals);
    if (signalEntries.length === 0) return;

    const canonicalMap: {
      key: string;
      matcher: (name: string, unit: string) => boolean;
      widget: (sig: DecodedSignal) => DashboardWidget;
    }[] = [
      {
        key: 'rpm',
        matcher: (n, u) => n.includes('rpm') || u === 'rpm' || n.includes('drehzahl') || n.includes('engspd'),
        widget: (sig) => ({
          id: `auto_rpm_${Date.now()}`,
          title: 'ENGINE SPEED',
          subtitle: sig.messageName || 'Engine Controller',
          sourceType: 'CanSignal',
          sourceIdentifier: sig.signalName,
          type: 'RadialGauge',
          unit: 'RPM',
          minValue: 0,
          maxValue: 7000,
          decimalPlaces: 0,
          warnThreshold: 5500,
          colorScheme: 'amber',
          position: { x: 0, y: 0, w: 4, h: 2 }
        })
      },
      {
        key: 'speed',
        matcher: (n, u) => n.includes('speed') || u === 'km/h' || n.includes('geschw') || n.includes('vehspd'),
        widget: (sig) => ({
          id: `auto_speed_${Date.now()}`,
          title: 'VEHICLE SPEED',
          subtitle: sig.messageName || 'Instrument Cluster / ABS',
          sourceType: 'CanSignal',
          sourceIdentifier: sig.signalName,
          type: 'RadialGauge',
          unit: 'km/h',
          minValue: 0,
          maxValue: 260,
          decimalPlaces: 0,
          colorScheme: 'sky',
          position: { x: 4, y: 0, w: 4, h: 2 }
        })
      },
      {
        key: 'coolant',
        matcher: (n, u) => (n.includes('coolant') || n.includes('kuehl') || n.includes('ect')) && (u === '°c' || u === 'c'),
        widget: (sig) => ({
          id: `auto_ect_${Date.now()}`,
          title: 'COOLANT TEMP',
          subtitle: sig.messageName || 'Engine Telemetry',
          sourceType: 'CanSignal',
          sourceIdentifier: sig.signalName,
          type: 'LevelBar',
          unit: '°C',
          minValue: 0,
          maxValue: 130,
          decimalPlaces: 1,
          warnThreshold: 105,
          colorScheme: 'rose',
          position: { x: 8, y: 0, w: 4, h: 2 }
        })
      },
      {
        key: 'fuel_rail',
        matcher: (n, u) => (n.includes('rail') || n.includes('cr_') || n.includes('fuelpress')) && (u === 'bar' || u === 'mpa'),
        widget: (sig) => ({
          id: `auto_rail_${Date.now()}`,
          title: 'FUEL RAIL PRESSURE',
          subtitle: sig.messageName || 'Common Rail System',
          sourceType: 'CanSignal',
          sourceIdentifier: sig.signalName,
          type: 'LevelBar',
          unit: sig.unit || 'bar',
          minValue: 0,
          maxValue: 2000,
          decimalPlaces: 1,
          warnThreshold: 1800,
          colorScheme: 'amber',
          position: { x: 0, y: 2, w: 4, h: 2 }
        })
      },
      {
        key: 'throttle',
        matcher: (n, u) => n.includes('throttle') || n.includes('pedal') || n.includes('accel') || n.includes('gaspedal'),
        widget: (sig) => ({
          id: `auto_app_${Date.now()}`,
          title: 'THROTTLE POSITION',
          subtitle: sig.messageName || 'Drive-by-Wire Pedal',
          sourceType: 'CanSignal',
          sourceIdentifier: sig.signalName,
          type: 'LevelBar',
          unit: '%',
          minValue: 0,
          maxValue: 100,
          decimalPlaces: 1,
          colorScheme: 'emerald',
          position: { x: 4, y: 2, w: 4, h: 2 }
        })
      },
      {
        key: 'battery',
        matcher: (n, u) => (n.includes('batt') || n.includes('volt') || n.includes('spannung')) && (u === 'v' || u === 'volt'),
        widget: (sig) => ({
          id: `auto_volt_${Date.now()}`,
          title: 'BATTERY VOLTAGE',
          subtitle: sig.messageName || 'Power Management',
          sourceType: 'CanSignal',
          sourceIdentifier: sig.signalName,
          type: 'DigitalValue',
          unit: 'V',
          minValue: 8,
          maxValue: 16,
          decimalPlaces: 2,
          warnThreshold: 14.8,
          colorScheme: 'sky',
          position: { x: 8, y: 2, w: 4, h: 2 }
        })
      },
      {
        key: 'boost',
        matcher: (n, u) => (n.includes('boost') || n.includes('ladedruck') || n.includes('map_')) && (u === 'bar' || u === 'kpa' || u === 'hpa'),
        widget: (sig) => ({
          id: `auto_boost_${Date.now()}`,
          title: 'BOOST PRESSURE',
          subtitle: sig.messageName || 'Turbo Management',
          sourceType: 'CanSignal',
          sourceIdentifier: sig.signalName,
          type: 'LevelBar',
          unit: sig.unit || 'bar',
          minValue: 0,
          maxValue: 3.5,
          decimalPlaces: 2,
          warnThreshold: 2.2,
          colorScheme: 'purple',
          position: { x: 0, y: 4, w: 4, h: 2 }
        })
      },
      {
        key: 'iat',
        matcher: (n, u) => (n.includes('iat') || n.includes('intake') || n.includes('ansaug')) && (u === '°c' || u === 'c'),
        widget: (sig) => ({
          id: `auto_iat_${Date.now()}`,
          title: 'INTAKE AIR TEMP',
          subtitle: sig.messageName || 'Manifold Sensor',
          sourceType: 'CanSignal',
          sourceIdentifier: sig.signalName,
          type: 'DigitalValue',
          unit: '°C',
          minValue: -20,
          maxValue: 80,
          decimalPlaces: 1,
          colorScheme: 'emerald',
          position: { x: 4, y: 4, w: 4, h: 2 }
        })
      }
    ];

    const newWidgets: DashboardWidget[] = [];
    for (const cano of canonicalMap) {
      const found = signalEntries.find(([name, sig]) => cano.matcher(name.toLowerCase(), (sig.unit || '').toLowerCase()));
      if (found) {
        newWidgets.push(cano.widget(found[1]));
      }
    }

    if (newWidgets.length > 0) {
      const updatedLayout = { ...activeLayout, widgets: newWidgets };
      setActiveLayout(updatedLayout);
      api.saveDashboardLayout(updatedLayout);
    }
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

      {/* OPERATING MODE & HARDWARE VERIFICATION STATUS BAR */}
      <div className={`p-3 rounded-lg border flex flex-wrap items-center justify-between gap-3 text-xs font-sans ${
        (operatingMode === 'Simulation' || replayStatus?.isPlaying)
          ? 'bg-purple-950/30 border-purple-800 text-purple-200'
          : hardwareStatus?.isConnected
            ? 'bg-emerald-950/30 border-emerald-800 text-emerald-200'
            : 'bg-amber-950/20 border-amber-800/60 text-amber-200'
      }`}>
        <div className="flex items-center space-x-2.5">
          {(operatingMode === 'Simulation' || replayStatus?.isPlaying) ? (
            <>
              <span className="px-2 py-0.5 bg-purple-600/30 border border-purple-500 rounded font-bold font-mono text-[11px] text-purple-300">
                [SIMULATION / REPLAY MODE]
              </span>
              <span className="text-slate-300">
                Virtual CAN bus active. Decoded signals are generated by deterministic models or recorded log sessions.
              </span>
            </>
          ) : hardwareStatus?.isConnected ? (
            <>
              <span className="px-2 py-0.5 bg-emerald-600/30 border border-emerald-500 rounded font-bold font-mono text-[11px] text-emerald-300">
                [PHYSICAL HARDWARE ONLINE]
              </span>
              <span className="text-slate-300">
                Connected to <strong className="text-white">{hardwareStatus.name}</strong> • Bus traffic: <span className="font-mono text-emerald-400 font-bold">{fps} fps ({totalFrames.toLocaleString()} frames)</span>
              </span>
            </>
          ) : (
            <>
              <span className="px-2 py-0.5 bg-amber-600/30 border border-amber-500 rounded font-bold font-mono text-[11px] text-amber-300">
                [NO HARDWARE CONNECTED]
              </span>
              <span className="text-slate-300">
                Offline state. No physical CAN gateway or vehicle connected. All gauges show <strong className="text-white">NO DATA</strong>.
              </span>
            </>
          )}
        </div>

        <div className="flex items-center space-x-2">
          {!hardwareStatus?.isConnected && operatingMode !== 'Simulation' && (
            <button
              onClick={handleStartSim}
              className="px-2.5 py-1 bg-purple-600 hover:bg-purple-500 text-white rounded text-xs font-bold flex items-center space-x-1 shadow transition-all"
            >
              <Sparkles className="w-3.5 h-3.5" />
              <span>Launch Virtual Simulation</span>
            </button>
          )}
          {Object.keys(signals).length > 0 && (
            <button
              onClick={handleAutoMapFromDiscoveredSignals}
              className="px-2.5 py-1 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold flex items-center space-x-1 shadow transition-all"
              title="Automatically calibrate and layout cockpit gauges from incoming bus signals"
            >
              <Zap className="w-3.5 h-3.5" />
              <span>Auto-Map Cockpit From Active Bus</span>
            </button>
          )}
        </div>
      </div>

      {/* DESIGNER COCKPIT MODE */}
      {viewMode === 'designer' && (
        <div className="space-y-4">
          {/* Active Grid of Customizable Widgets */}
          <div className="grid grid-cols-12 gap-4">
            {activeLayout?.widgets.map(w => {
              const liveState = getWidgetLiveState(w);
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
                    <RadialGaugeWidget 
                      widget={w} 
                      currentValue={liveState.value} 
                      quality={liveState.quality}
                      source={liveState.source}
                      ageMs={liveState.ageMs}
                    />
                  )}
                  {w.type === 'DigitalValue' && (
                    <DigitalValueWidget 
                      widget={w} 
                      currentValue={liveState.value} 
                      peakValue={peaks[w.id]} 
                      quality={liveState.quality}
                      source={liveState.source}
                      ageMs={liveState.ageMs}
                    />
                  )}
                  {w.type === 'MiniGraph' && (
                    <MiniGraphWidget 
                      widget={w} 
                      currentValue={liveState.value} 
                      quality={liveState.quality}
                      source={liveState.source}
                      ageMs={liveState.ageMs}
                    />
                  )}
                  {w.type === 'LevelBar' && (
                    <LevelBarWidget 
                      widget={w} 
                      currentValue={liveState.value} 
                      quality={liveState.quality}
                      source={liveState.source}
                      ageMs={liveState.ageMs}
                    />
                  )}
                  {w.type === 'StatusAnnunciator' && (
                    <StatusAnnunciatorWidget 
                      widget={w} 
                      currentValue={liveState.value} 
                      quality={liveState.quality}
                      source={liveState.source}
                      ageMs={liveState.ageMs}
                    />
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

          {/* LIVE DECODED VEHICLE TELEMETRY STREAM */}
          <div className="bg-can-panel border border-can-border rounded-xl p-5 shadow-lg space-y-4 font-sans">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-can-border pb-3">
              <div className="flex items-center space-x-3">
                <div className="p-2 bg-emerald-500/10 border border-emerald-500/30 rounded-lg text-emerald-400">
                  <Radio className="w-5 h-5 animate-pulse" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-white flex items-center space-x-2">
                    <span>LIVE DECODED VEHICLE TELEMETRY STREAM</span>
                    <span className="px-2 py-0.5 bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 text-[10px] rounded-full font-mono">
                      {Object.keys(signals).length} Active Signals
                    </span>
                  </h3>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Real-time physical engineering parameters automatically decoded from active bus frames. Click "+ Pin as Meter" to instantly add any signal to your cockpit gauges.
                  </p>
                </div>
              </div>

              {/* Search & Active DBC Indicator */}
              <div className="flex items-center space-x-3 text-xs">
                <div className="relative">
                  <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2" />
                  <input
                    type="text"
                    value={signalSearchFilter}
                    onChange={e => setSignalSearchFilter(e.target.value)}
                    placeholder="Search signals (RPM, Rail, Temp...)"
                    className="bg-can-bg border border-can-border rounded pl-8 pr-3 py-1 text-xs text-white placeholder-slate-500 w-56 focus:outline-none focus:border-sky-500"
                  />
                </div>

                <div className="flex items-center space-x-1.5 px-2.5 py-1 bg-can-bg border border-can-border rounded text-[11px] font-mono">
                  <span className="text-slate-400">DBC:</span>
                  <span className="text-sky-400 font-bold truncate max-w-[150px]">
                    {activeDbc?.activeKey || (activeDbc?.isLoaded ? 'Active DBC' : 'None Active')}
                  </span>
                </div>
              </div>
            </div>

            {/* Quick Automotive DBC Switcher Chips */}
            <div className="flex flex-wrap items-center gap-2 text-xs">
              <span className="text-[11px] text-slate-400 font-semibold">1-Click Vehicle DBC:</span>
              {[
                { key: 'magneti_marelli_mjd_ddis', label: 'Magneti Marelli MJD (Maruti/Fiat)' },
                { key: 'bosch_edc17_mhawk_revotorq', label: 'Bosch EDC17 (Mahindra/Tata)' },
                { key: 'bosch_edc16_crde', label: 'Bosch EDC16 (CRDe)' },
                { key: 'universal_obd2_live_data', label: 'Universal OBD-II Live' }
              ].map(dbc => (
                <button
                  key={dbc.key}
                  onClick={() => handleQuickLoadDbc(dbc.key)}
                  disabled={loadingDbcKey === dbc.key}
                  className={`px-2.5 py-1 rounded text-[11px] font-semibold border transition-all flex items-center space-x-1 ${
                    activeDbc?.activeKey?.toLowerCase().includes(dbc.key.toLowerCase())
                      ? 'bg-sky-600/30 border-sky-500 text-sky-300 shadow-sm'
                      : 'bg-can-bg border-can-border text-slate-300 hover:text-white hover:border-slate-500'
                  }`}
                >
                  <FileCode className="w-3 h-3 text-sky-400" />
                  <span>{loadingDbcKey === dbc.key ? 'Loading...' : dbc.label}</span>
                </button>
              ))}
            </div>

            {/* Decoded Signals Live Cards */}
            {Object.keys(signals).length > 0 ? (
              <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
                {Object.values(signals)
                  .filter(sig => 
                    !signalSearchFilter || 
                    sig.signalName.toLowerCase().includes(signalSearchFilter.toLowerCase()) ||
                    sig.messageName?.toLowerCase().includes(signalSearchFilter.toLowerCase())
                  )
                  .map(sig => {
                    const isPinned = activeLayout?.widgets.some(w => w.sourceIdentifier.toLowerCase() === sig.signalName.toLowerCase());
                    return (
                      <div
                        key={`${sig.canId}-${sig.signalName}`}
                        className="bg-can-bg border border-can-border hover:border-sky-500/40 rounded-lg p-3.5 shadow flex flex-col justify-between transition-all group"
                      >
                        <div className="flex items-start justify-between">
                          <div>
                            <span className="font-bold text-white text-xs block group-hover:text-sky-300 transition-colors truncate">
                              {sig.signalName}
                            </span>
                            <span className="text-[10px] text-slate-500 font-mono">
                              0x{sig.canId.toString(16).toUpperCase()} • {sig.messageName || 'MSG'}
                            </span>
                          </div>
                          <span className="text-[10px] px-1.5 py-0.5 bg-slate-800 text-slate-400 rounded font-mono">
                            {sig.transmitterNode || 'ECU'}
                          </span>
                        </div>

                        <div className="my-2.5">
                          <div className="text-xl font-bold text-emerald-400 font-mono">
                            {typeof sig.engineeringValue === 'number' ? sig.engineeringValue.toFixed(2) : sig.formattedValue}
                            <span className="text-xs text-slate-400 font-sans ml-1.5 font-normal">
                              {sig.unit}
                            </span>
                          </div>
                        </div>

                        <div className="pt-2 border-t border-can-border/60 flex items-center justify-between text-[11px]">
                          <span className="text-[10px] text-slate-500">
                            Raw: 0x{sig.rawValue.toString(16).toUpperCase()}
                          </span>
                          <button
                            onClick={() => handlePinSignalAsWidget(sig)}
                            disabled={isPinned}
                            className={`px-2 py-0.5 rounded text-[10px] font-bold flex items-center space-x-1 transition-all ${
                              isPinned
                                ? 'bg-slate-800 text-slate-500 cursor-default'
                                : 'bg-sky-600/20 text-sky-300 hover:bg-sky-600/40 border border-sky-500/30'
                            }`}
                          >
                            <Pin className="w-2.5 h-2.5" />
                            <span>{isPinned ? 'In Cockpit' : '+ Pin as Meter'}</span>
                          </button>
                        </div>
                      </div>
                    );
                  })}
              </div>
            ) : (
              <div className="p-8 text-center border border-dashed border-can-border rounded-lg bg-can-bg/50">
                <Radio className="w-8 h-8 text-slate-600 mx-auto mb-2 animate-pulse" />
                <div className="text-xs font-bold text-slate-300 uppercase tracking-wider">Awaiting Live Bus Signals</div>
                <p className="text-[11px] text-slate-500 mt-1 max-w-md mx-auto">
                  When CAN messages arrive on COM8 (Arduino Nano) or COM14 (ESP8266), decoded physical values will populate here automatically. Ensure a matching DBC database is active.
                </p>
              </div>
            )}
          </div>
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
