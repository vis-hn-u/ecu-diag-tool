import React, { useState, useEffect, useMemo, useRef } from 'react';
import { api, downloadBlobAsFile } from '../services/api';
import { EcuDefinitionPack, ReadonlyEcuMap, OperatingPointTracer } from '../types/diagnosticsExtended';
import { useCan } from '../context/CanContext';
import { 
  Sliders, Grid, ShieldAlert, Cpu, Layers, Eye, Activity, Rotate3d, 
  Sparkles, Compass, CheckCircle2, ChevronRight, Hash, FileText,
  Radio, Play, Square, Gauge, Flame, Zap, PlusCircle, Save, Table,
  RefreshCw, Globe
} from 'lucide-react';

export const MappackViewerPage: React.FC = () => {
  const { signals, totalFrames, fps, hardwareStatus, activeTransportId } = useCan();

  // Top level workspace view: Standard Catalog vs Dynamic Live 3D Builder
  const [workspaceMode, setWorkspaceMode] = useState<'CATALOG' | 'DYNAMIC_BUILDER'>('CATALOG');

  // Definition Packs State
  const [packs, setPacks] = useState<EcuDefinitionPack[]>([]);
  const [selectedPackId, setSelectedPackId] = useState<string>('universal_obd2_powertrain');
  const [selectedMapId, setSelectedMapId] = useState<string>('OBD2_SparkAdvance_Load');
  const [activeCategory, setActiveCategory] = useState<string>('All');
  const [viewMode, setViewMode] = useState<'2D' | '3D'>('2D');
  const [isExportingPdf, setIsExportingPdf] = useState(false);

  // Tracer acquisition mode: Live CAN Bus, Manual Bench Slider, or Demo Simulation
  type TracerMode = 'LIVE_CAN' | 'MANUAL_BENCH' | 'SIMULATION';
  const [tracerMode, setTracerMode] = useState<TracerMode>(() => (totalFrames > 0 ? 'LIVE_CAN' : 'LIVE_CAN'));

  // Manual bench scrubbers (for testing on desk without engine running)
  const [manualRpm, setManualRpm] = useState<number>(2000);
  const [manualLoad, setManualLoad] = useState<number>(45.0);

  // Simulation drift values (used only in SIMULATION mode)
  const [simRpm, setSimRpm] = useState<number>(2400);
  const [simLoad, setSimLoad] = useState<number>(50.0);

  // Dynamic Live 3D Table Builder State (Universal for ANY ECU & signals)
  const availableSignalNames = useMemo(() => {
    const keys = Object.keys(signals);
    if (keys.length === 0) {
      return ['EngineSpeed', 'VehicleSpeed', 'ThrottlePosition', 'ManifoldPressure', 'FuelRailPressure', 'EngineTorque', 'CoolantTemp', 'MassAirFlow'];
    }
    return keys;
  }, [signals]);

  const [dynXSignal, setDynXSignal] = useState<string>('EngineSpeed');
  const [dynYSignal, setDynYSignal] = useState<string>('ThrottlePosition');
  const [dynZSignal, setDynZSignal] = useState<string>('ManifoldPressure');
  const [dynTableName, setDynTableName] = useState<string>('Live Dynamic Powertrain Surface');
  const [dynCategory, setDynCategory] = useState<string>('Boost');
  const [dynCellAccumulator, setDynCellAccumulator] = useState<number[][]>(() => {
    // 8 rows x 8 cols grid initialized with sensible physical defaults
    const g: number[][] = [];
    for (let r = 0; r < 8; r++) {
      const row: number[] = [];
      for (let c = 0; c < 8; c++) {
        row.push(Math.round(1000 + (r * 150) + (c * 30)));
      }
      g.push(row);
    }
    return g;
  });
  const [isSavingCustomPack, setIsSavingCustomPack] = useState(false);
  const [saveSuccessMsg, setSaveSuccessMsg] = useState<string | null>(null);

  // Helper to extract live numerical values matching candidate keywords from CAN bus
  const getLiveVal = (candidates: string[]): number | null => {
    for (const [key, sig] of Object.entries(signals)) {
      const k = key.toLowerCase();
      for (const c of candidates) {
        if (k.includes(c) && typeof sig.engineeringValue === 'number') {
          return sig.engineeringValue;
        }
      }
    }
    return null;
  };

  const liveCanRpm = getLiveVal(['rpm', 'engspd', 'eng_spd', 'drehzahl', 'cr_ems_engspd', 'mo_drehzahl', 'enginespeed']);
  const liveCanThrottle = getLiveVal(['throttle', 'pedal', 'accel', 'gaspedal', 'app', 'throttlepos', 'gas_pedal']);
  const liveCanRailPressure = getLiveVal(['rail', 'railpressure', 'cr_p_act', 'fuelpressure', 'p_rail']);
  const liveCanBoostPressure = getLiveVal(['boost', 'boostpressure', 'map', 'manifold', 'p_man', 'airpressure']);
  const liveCanAirMass = getLiveVal(['airmass', 'maf', 'massair', 'luftmasse', 'air_mass']);
  const liveCanTorque = getLiveVal(['torque', 'enginetorque', 'mo_drehmoment', 'actualenginetorque']);
  const liveCanCoolantTemp = getLiveVal(['coolant', 'ect', 'temp_engine', 'motortemp', 'coolanttemperature']);
  const liveCanInjectedQty = getLiveVal(['injected', 'injection', 'iq', 'fuelqty', 'kraftstoff', 'fuelrate']);

  // Fetch packs on mount
  const refreshPacks = () => {
    api.getMappacks().then(pList => {
      setPacks(pList);
      if (pList.length > 0 && !pList.some(p => p.id === selectedPackId)) {
        setSelectedPackId(pList[0].id);
        if (pList[0].maps.length > 0) {
          setSelectedMapId(pList[0].maps[0].mapId);
        }
      }
    }).catch(() => {});
  };

  useEffect(() => {
    refreshPacks();
  }, []);

  const activePack = packs.find(p => p.id === selectedPackId) || packs[0];
  const activeMap = activePack?.maps.find(m => m.mapId === selectedMapId) || activePack?.maps[0];

  // Simulation drift loop (only active in SIMULATION mode)
  useEffect(() => {
    if (tracerMode !== 'SIMULATION') return;
    const interval = setInterval(() => {
      const t = Date.now() / 1000;
      setSimRpm(Math.round(1800 + Math.sin(t * 0.9) * 1600 + 1000));
      setSimLoad(Math.round(45 + Math.sin(t * 1.3) * 35));
    }, 100);
    return () => clearInterval(interval);
  }, [tracerMode]);

  // Determine current effective RPM based on active mode
  const currentRpm = useMemo(() => {
    if (tracerMode === 'LIVE_CAN') {
      return liveCanRpm !== null ? Math.round(liveCanRpm) : 850;
    }
    if (tracerMode === 'MANUAL_BENCH') {
      return manualRpm;
    }
    return simRpm;
  }, [tracerMode, liveCanRpm, manualRpm, simRpm]);

  // Determine current effective Y-Axis value smartly matched to the map's unit and dimension
  const currentYData = useMemo(() => {
    if (!activeMap || !activeMap.yAxis) {
      return { value: 0, label: 'Row', unit: '' };
    }

    const yName = (activeMap.yAxis.name + ' ' + activeMap.yAxis.unit).toLowerCase();

    // In MANUAL_BENCH mode: scale from manualLoad %
    if (tracerMode === 'MANUAL_BENCH') {
      const pct = manualLoad / 100.0;
      const minBp = activeMap.yAxis.breakpoints[0] || 0;
      const maxBp = activeMap.yAxis.breakpoints[activeMap.yAxis.breakpoints.length - 1] || 100;
      const scaled = minBp + pct * (maxBp - minBp);
      return {
        value: Math.round(scaled * 10) / 10,
        label: activeMap.yAxis.name,
        unit: activeMap.yAxis.unit
      };
    }

    // In SIMULATION mode:
    if (tracerMode === 'SIMULATION') {
      const pct = simLoad / 100.0;
      const minBp = activeMap.yAxis.breakpoints[0] || 0;
      const maxBp = activeMap.yAxis.breakpoints[activeMap.yAxis.breakpoints.length - 1] || 100;
      const scaled = minBp + pct * (maxBp - minBp);
      return {
        value: Math.round(scaled * 10) / 10,
        label: activeMap.yAxis.name,
        unit: activeMap.yAxis.unit
      };
    }

    // LIVE_CAN mode: Map accurately to physical CAN signals
    if (yName.includes('pedal') || (yName.includes('%') && (yName.includes('position') || yName.includes('throttle')))) {
      const val = liveCanThrottle ?? 0.0;
      return { value: val, label: 'Pedal / Throttle (CAN)', unit: '%' };
    }

    if (yName.includes('rail') || yName.includes('bar')) {
      const val = liveCanRailPressure ?? 320.0;
      return { value: val, label: 'Rail Pressure (CAN)', unit: 'bar' };
    }

    if (yName.includes('boost') || yName.includes('mbar') || yName.includes('map') || yName.includes('charge')) {
      const val = liveCanBoostPressure ?? 1013.0;
      return { value: val, label: 'Boost / MAP (CAN)', unit: 'mbar' };
    }

    if (yName.includes('air mass') || yName.includes('mg/hub') || yName.includes('maf') || yName.includes('g/s')) {
      const val = liveCanAirMass ?? 480.0;
      return { value: val, label: 'Air Mass (CAN)', unit: activeMap.yAxis.unit || 'mg/hub' };
    }

    if (yName.includes('mm³') || yName.includes('iq') || yName.includes('injected')) {
      const val = liveCanInjectedQty ?? ((liveCanThrottle ?? 15) / 100.0 * 58.0);
      return { value: Math.round(val * 10) / 10, label: 'Injected Qty (CAN)', unit: 'mm³/stk' };
    }

    if (yName.includes('nm') || yName.includes('torque')) {
      const val = liveCanTorque ?? 240.0;
      return { value: val, label: 'Engine Torque (CAN)', unit: 'Nm' };
    }

    if (yName.includes('temp') || yName.includes('°c')) {
      const val = liveCanCoolantTemp ?? 85.0;
      return { value: val, label: 'Coolant Temp (CAN)', unit: '°C' };
    }

    if (yName.includes('hpa') || yName.includes('atmosphere')) {
      return { value: 1000.0, label: 'Atmosphere', unit: 'hPa' };
    }

    // Default fallback to throttle or 50%
    return { value: liveCanThrottle ?? 50.0, label: activeMap.yAxis.name || 'Load', unit: activeMap.yAxis.unit || '%' };
  }, [
    tracerMode, 
    activeMap, 
    manualLoad, 
    simLoad, 
    liveCanThrottle, 
    liveCanRailPressure, 
    liveCanBoostPressure, 
    liveCanAirMass, 
    liveCanInjectedQty, 
    liveCanTorque, 
    liveCanCoolantTemp
  ]);

  // Compute local real-time bilinear operating point tracer with 0ms latency
  const tracer = useMemo<OperatingPointTracer | null>(() => {
    if (!activeMap || activeMap.xAxis.breakpoints.length === 0) return null;

    const xBp = activeMap.xAxis.breakpoints;
    const yBp = activeMap.yAxis ? activeMap.yAxis.breakpoints : [0];
    const rpm = currentRpm;
    const load = currentYData.value;

    // Find nearest X
    let bestX = 0;
    let minXDiff = Infinity;
    for (let i = 0; i < xBp.length; i++) {
      const diff = Math.abs(xBp[i] - rpm);
      if (diff < minXDiff) {
        minXDiff = diff;
        bestX = i;
      }
    }

    // Find nearest Y
    let bestY = 0;
    let minYDiff = Infinity;
    for (let j = 0; j < yBp.length; j++) {
      const diff = Math.abs(yBp[j] - load);
      if (diff < minYDiff) {
        minYDiff = diff;
        bestY = j;
      }
    }

    // Bilinear interpolation
    let interpZ = activeMap.zData[bestY]?.[bestX] ?? 0;
    if (xBp.length > 1 && yBp.length > 1 && activeMap.zData.length > bestY) {
      // Find bounding X
      let x0 = 0;
      for (let i = 0; i < xBp.length - 1; i++) {
        if (rpm >= xBp[i] && rpm <= xBp[i + 1]) {
          x0 = i;
          break;
        }
        if (rpm > xBp[i + 1]) x0 = i + 1;
      }
      let x1 = Math.min(x0 + 1, xBp.length - 1);
      if (x0 === x1 && x0 > 0) x0--;

      // Find bounding Y
      let y0 = 0;
      for (let j = 0; j < yBp.length - 1; j++) {
        if (load >= yBp[j] && load <= yBp[j + 1]) {
          y0 = j;
          break;
        }
        if (load > yBp[j + 1]) y0 = j + 1;
      }
      let y1 = Math.min(y0 + 1, yBp.length - 1);
      if (y0 === y1 && y0 > 0) y0--;

      const xSpan = xBp[x1] - xBp[x0];
      const ySpan = yBp[y1] - yBp[y0];

      const tx = xSpan > 0.001 ? Math.max(0, Math.min(1, (rpm - xBp[x0]) / xSpan)) : 0;
      const ty = ySpan > 0.001 ? Math.max(0, Math.min(1, (load - yBp[y0]) / ySpan)) : 0;

      if (activeMap.zData[y0] && activeMap.zData[y1]) {
        const z00 = activeMap.zData[y0][x0] ?? 0;
        const z01 = activeMap.zData[y0][x1] ?? 0;
        const z10 = activeMap.zData[y1][x0] ?? 0;
        const z11 = activeMap.zData[y1][x1] ?? 0;

        interpZ = (1 - tx) * (1 - ty) * z00 +
                  tx * (1 - ty) * z01 +
                  (1 - tx) * ty * z10 +
                  tx * ty * z11;
      }
    }

    return {
      activeMapId: activeMap.mapId,
      currentRpm: rpm,
      currentLoad: load,
      nearestXIndex: bestX,
      nearestYIndex: bestY,
      interpolatedZValue: Math.round(interpZ * Math.pow(10, activeMap.decimalPrecision)) / Math.pow(10, activeMap.decimalPrecision)
    };
  }, [activeMap, currentRpm, currentYData]);

  // Dynamic Live Table Breakpoints (8 x 8)
  const dynXBreakpoints = useMemo(() => {
    const sig = signals[dynXSignal] as any;
    const minVal = sig?.minimum !== undefined && sig?.maximum !== undefined && sig.minimum < sig.maximum ? sig.minimum : 800;
    const maxVal = sig?.maximum !== undefined && sig.maximum > minVal ? sig.maximum : 6000;
    const arr: number[] = [];
    for (let i = 0; i < 8; i++) {
      arr.push(Math.round(minVal + (i / 7) * (maxVal - minVal)));
    }
    return arr;
  }, [signals, dynXSignal]);

  const dynYBreakpoints = useMemo(() => {
    const sig = signals[dynYSignal] as any;
    const minVal = sig?.minimum !== undefined && sig?.maximum !== undefined && sig.minimum < sig.maximum ? sig.minimum : 0;
    const maxVal = sig?.maximum !== undefined && sig.maximum > minVal ? sig.maximum : 100;
    const arr: number[] = [];
    for (let i = 0; i < 8; i++) {
      arr.push(Math.round(minVal + (i / 7) * (maxVal - minVal)));
    }
    return arr;
  }, [signals, dynYSignal]);

  // Live Accumulator into dynamic cell grid as CAN frames arrive
  useEffect(() => {
    if (workspaceMode !== 'DYNAMIC_BUILDER') return;
    const xVal = signals[dynXSignal]?.engineeringValue ?? liveCanRpm ?? currentRpm;
    const yVal = signals[dynYSignal]?.engineeringValue ?? liveCanThrottle ?? currentYData.value;
    const zVal = signals[dynZSignal]?.engineeringValue;

    if (typeof zVal !== 'number') return;

    // Find nearest cell in dynamic grid
    let bx = 0;
    let bxd = Infinity;
    for (let i = 0; i < dynXBreakpoints.length; i++) {
      const d = Math.abs(dynXBreakpoints[i] - xVal);
      if (d < bxd) { bxd = d; bx = i; }
    }

    let by = 0;
    let byd = Infinity;
    for (let j = 0; j < dynYBreakpoints.length; j++) {
      const d = Math.abs(dynYBreakpoints[j] - yVal);
      if (d < byd) { byd = d; by = j; }
    }

    setDynCellAccumulator(prev => {
      const next = prev.map(row => [...row]);
      if (next[by] && next[by][bx] !== undefined) {
        // Smooth exponential moving average update into active cell
        next[by][bx] = Math.round(((next[by][bx] * 0.7) + (zVal * 0.3)) * 10) / 10;
      }
      return next;
    });
  }, [signals, workspaceMode, dynXSignal, dynYSignal, dynZSignal, dynXBreakpoints, dynYBreakpoints, currentRpm, currentYData.value]);

  // Save Dynamic Table to Database
  const handleSaveDynamicTable = async () => {
    setIsSavingCustomPack(true);
    setSaveSuccessMsg(null);
    try {
      const customPack: EcuDefinitionPack = {
        id: `custom_${Date.now()}`,
        name: `Custom Live: ${dynTableName}`,
        targetPlatform: 'Universal CAN Bus Telemetry (Any Connected ECU)',
        hardwarePartNumber: 'CUSTOM-CAN-GENERIC',
        softwareVersion: 'LIVE-DYN-V1',
        calibrationId: 'CUSTOM_LIVE_CORRELATION',
        author: 'User Dynamic Table Generator',
        version: '1.0',
        supportedProtocols: ['CAN', 'OBD-II', 'UDS'],
        createdAtUtc: new Date().toISOString(),
        isReadOnly: true,
        maps: [
          {
            mapId: `MAP_${Date.now()}`,
            name: dynTableName,
            description: `Universal Live Correlation Map (${dynXSignal} x ${dynYSignal} -> ${dynZSignal})`,
            category: dynCategory as any,
            dimension: 'TwoDimensional',
            memoryAddressHex: '0xLIVE0001',
            xAxis: {
              name: dynXSignal,
              unit: signals[dynXSignal]?.unit || 'Units',
              breakpoints: dynXBreakpoints
            },
            yAxis: {
              name: dynYSignal,
              unit: signals[dynYSignal]?.unit || 'Units',
              breakpoints: dynYBreakpoints
            },
            zData: dynCellAccumulator,
            zUnit: signals[dynZSignal]?.unit || 'Units',
            minValue: Math.min(...dynCellAccumulator.flat()),
            maxValue: Math.max(...dynCellAccumulator.flat()),
            decimalPrecision: 1,
            heatmapPalette: 'turbo',
            isReadOnly: true
          }
        ]
      };

      await api.addCustomMappack(customPack);
      setSaveSuccessMsg(`Table "${dynTableName}" saved to Mappack Catalog!`);
      refreshPacks();
      setTimeout(() => setSaveSuccessMsg(null), 4000);
    } catch (err: any) {
      alert(`Failed to save table: ${err.message}`);
    } finally {
      setIsSavingCustomPack(false);
    }
  };

  const handleExportCalibrationPdf = async () => {
    setIsExportingPdf(true);
    try {
      const blob = await api.downloadEcuFlashReportPdf(undefined, selectedPackId);
      downloadBlobAsFile(blob, `ECU_Calibration_Analysis_${activePack?.calibrationId || 'Report'}.pdf`);
    } catch (e) {
      console.error('Failed to export calibration PDF:', e);
    } finally {
      setIsExportingPdf(false);
    }
  };

  // Heatmap color generator
  const getCellHeatmapBg = (val: number, min: number, max: number, palette: string = 'viridis') => {
    const range = max - min || 1;
    const fraction = Math.min(1, Math.max(0, (val - min) / range));

    if (palette === 'turbo') {
      if (fraction < 0.25) return 'bg-sky-950/80 text-sky-200';
      if (fraction < 0.5) return 'bg-emerald-950/80 text-emerald-200';
      if (fraction < 0.75) return 'bg-amber-950/80 text-amber-200 font-bold';
      return 'bg-rose-950/90 text-rose-200 font-bold';
    }

    if (palette === 'magma') {
      if (fraction < 0.25) return 'bg-slate-950 text-slate-400';
      if (fraction < 0.5) return 'bg-purple-950/80 text-purple-200';
      if (fraction < 0.75) return 'bg-rose-950/90 text-rose-200 font-bold';
      return 'bg-amber-500/80 text-black font-black';
    }

    if (palette === 'coolwarm') {
      if (fraction < 0.35) return 'bg-sky-950/80 text-sky-200';
      if (fraction < 0.65) return 'bg-slate-900 text-slate-200';
      return 'bg-rose-950/90 text-rose-200 font-bold';
    }

    if (fraction < 0.25) return 'bg-slate-900 text-slate-300';
    if (fraction < 0.5) return 'bg-sky-900/60 text-sky-100';
    if (fraction < 0.75) return 'bg-amber-900/60 text-amber-100 font-bold';
    return 'bg-rose-900/80 text-rose-100 font-bold';
  };

  const filteredMaps = activePack?.maps.filter(m => 
    activeCategory === 'All' || m.category === activeCategory
  ) || [];

  return (
    <div className="p-5 space-y-5 overflow-y-auto h-full font-mono select-none">
      {/* Strict Read-Only Safety Banner */}
      <div className="bg-amber-950/30 border border-amber-500/40 rounded-lg p-3 flex flex-wrap items-center justify-between shadow font-sans gap-2">
        <div className="flex items-center space-x-3 text-amber-300 text-xs">
          <ShieldAlert className="w-5 h-5 text-amber-400 shrink-0" />
          <div>
            <span className="font-bold uppercase tracking-wider">Universal ECU Calibration & Live Mapping Platform:</span>
            <span className="text-slate-300 ml-1.5">
              ECU-agnostic logic compatible with all OEM protocols (OBD-II, UDS, KWP2000, J1939) and any physical ECU on CANH/CANL.
            </span>
          </div>
        </div>
        <div className="flex items-center space-x-2">
          <span className="px-2.5 py-1 bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-[11px] font-bold rounded font-mono flex items-center space-x-1">
            <Globe className="w-3.5 h-3.5 text-emerald-400" />
            <span>ALL ECUS SUPPORTED</span>
          </span>
          <div className="px-2.5 py-1 bg-amber-500/20 border border-amber-500/40 text-amber-300 text-[11px] font-bold rounded font-mono">
            READ-ONLY
          </div>
        </div>
      </div>

      {/* Primary Workspace Navigation Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-3 shadow-md flex flex-wrap items-center justify-between gap-3 font-sans">
        <div className="flex items-center space-x-2">
          <button
            onClick={() => setWorkspaceMode('CATALOG')}
            className={`px-3.5 py-1.5 rounded font-bold text-xs flex items-center space-x-2 transition-all ${
              workspaceMode === 'CATALOG'
                ? 'bg-sky-600 text-white shadow'
                : 'bg-can-bg border border-can-border text-slate-400 hover:text-white'
            }`}
          >
            <Table className="w-4 h-4" />
            <span>Standard & OEM Mappacks</span>
          </button>
          <button
            onClick={() => setWorkspaceMode('DYNAMIC_BUILDER')}
            className={`px-3.5 py-1.5 rounded font-bold text-xs flex items-center space-x-2 transition-all ${
              workspaceMode === 'DYNAMIC_BUILDER'
                ? 'bg-gradient-to-r from-emerald-600 to-teal-600 text-white shadow'
                : 'bg-can-bg border border-can-border text-slate-400 hover:text-white'
            }`}
          >
            <Sparkles className="w-4 h-4 text-emerald-300" />
            <span>⚡ Universal Live 3D Table Builder (Works on ANY CAN Bus)</span>
          </button>
        </div>

        <div className="flex items-center space-x-3 text-xs">
          <span className="text-slate-400">CAN Bus Frames: <strong className="text-emerald-400 font-mono">{totalFrames.toLocaleString()}</strong></span>
          <span className="text-slate-400">Rate: <strong className="text-sky-400 font-mono">{fps} FPS</strong></span>
          <span className="px-2 py-0.5 bg-slate-800 border border-slate-700 text-slate-300 rounded font-mono text-[11px]">
            {activeTransportId || 'None'}
          </span>
        </div>
      </div>

      {/* =========================================================================
          VIEW 1: STANDARD CATALOG & FACTORY MAPPACKS
          ========================================================================= */}
      {workspaceMode === 'CATALOG' && (
        <div className="space-y-5">
          {/* Header & Pack Selector Bar */}
          <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow flex flex-wrap items-center justify-between gap-4 font-sans">
            <div className="flex items-center space-x-3">
              <div className="p-2 bg-purple-500/10 border border-purple-500/30 rounded-lg text-purple-400">
                <Cpu className="w-5 h-5" />
              </div>
              <div>
                <h1 className="text-lg font-bold text-white tracking-wide">ECU Definition Packs & 2D/3D Map Viewer</h1>
                <p className="text-xs text-slate-400">
                  Universal OBD-II, Common Rail Diesel, Turbo Petrol, and OEM-specific calibration definitions with real-time CAN tracing
                </p>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-3 text-xs">
              {/* Pack Selector */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">DEFINITION MAPPACK</label>
                <select
                  value={selectedPackId}
                  onChange={e => {
                    setSelectedPackId(e.target.value);
                    const p = packs.find(x => x.id === e.target.value);
                    if (p && p.maps.length > 0) setSelectedMapId(p.maps[0].mapId);
                  }}
                  className="bg-can-bg border border-can-border rounded px-3 py-1.5 text-white font-bold max-w-[320px]"
                >
                  {packs.map(p => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>

              {/* Tracer Acquisition Source Selector */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">TRACER DATA SOURCE</label>
                <div className="bg-can-bg border border-can-border rounded p-0.5 flex">
                  <button
                    onClick={() => setTracerMode('LIVE_CAN')}
                    className={`px-2.5 py-1 rounded font-bold transition-all flex items-center space-x-1 ${
                      tracerMode === 'LIVE_CAN' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-white'
                    }`}
                    title="Trace live engine point from real physical CANH / CANL bus traffic"
                  >
                    <Radio className="w-3.5 h-3.5" />
                    <span>Live CAN ({totalFrames > 0 ? `${fps} fps` : 'Waiting'})</span>
                  </button>
                  <button
                    onClick={() => setTracerMode('MANUAL_BENCH')}
                    className={`px-2.5 py-1 rounded font-bold transition-all flex items-center space-x-1 ${
                      tracerMode === 'MANUAL_BENCH' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
                    }`}
                    title="Manual slider controls for testing map cells on a bench ECU"
                  >
                    <Sliders className="w-3.5 h-3.5" />
                    <span>Bench Sliders</span>
                  </button>
                  <button
                    onClick={() => setTracerMode('SIMULATION')}
                    className={`px-2.5 py-1 rounded font-bold transition-all flex items-center space-x-1 ${
                      tracerMode === 'SIMULATION' ? 'bg-purple-600 text-white shadow' : 'text-slate-400 hover:text-white'
                    }`}
                    title="Simulated engine drift for visual demonstration"
                  >
                    <Play className="w-3.5 h-3.5" />
                    <span>Demo</span>
                  </button>
                </div>
              </div>

              {/* 2D / 3D Mode Toggle */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">VISUALIZATION MODE</label>
                <div className="bg-can-bg border border-can-border rounded p-0.5 flex">
                  <button
                    onClick={() => setViewMode('2D')}
                    className={`px-3 py-1 rounded font-bold transition-all flex items-center space-x-1 ${
                      viewMode === '2D' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
                    }`}
                  >
                    <Grid className="w-3.5 h-3.5" />
                    <span>2D Matrix</span>
                  </button>
                  <button
                    onClick={() => setViewMode('3D')}
                    className={`px-3 py-1 rounded font-bold transition-all flex items-center space-x-1 ${
                      viewMode === '3D' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
                    }`}
                  >
                    <Rotate3d className="w-3.5 h-3.5" />
                    <span>3D Surface</span>
                  </button>
                </div>
              </div>

              {/* PDF Report Export */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">AUDIT REPORT</label>
                <button
                  onClick={handleExportCalibrationPdf}
                  disabled={isExportingPdf}
                  className="px-3 py-1.5 bg-sky-600/20 hover:bg-sky-600/30 text-sky-300 border border-sky-500/40 rounded font-bold transition-all flex items-center space-x-1.5 shadow text-xs font-sans"
                  title="Download ECU Firmware & Calibration Analysis PDF Report"
                >
                  <FileText className="w-3.5 h-3.5 text-sky-400" />
                  <span>{isExportingPdf ? 'Generating...' : 'Export Calibration PDF'}</span>
                </button>
              </div>
            </div>
          </div>

          {/* Manual Bench Slider Scrubbers (Visible when in MANUAL_BENCH mode) */}
          {tracerMode === 'MANUAL_BENCH' && (
            <div className="bg-sky-950/30 border border-sky-500/40 rounded-lg p-3.5 shadow flex flex-wrap items-center justify-between gap-4 font-sans text-xs">
              <div className="flex items-center space-x-2 text-sky-300 font-bold">
                <Sliders className="w-4 h-4 text-sky-400" />
                <span>BENCH MAP CALIBRATION SCRUBBERS:</span>
                <span className="text-slate-400 font-normal">Manually sweep operating points across the table matrix</span>
              </div>
              <div className="flex flex-wrap items-center gap-6 font-mono">
                <div className="flex items-center space-x-2">
                  <span className="text-[11px] text-slate-400">ENGINE RPM:</span>
                  <input
                    type="range"
                    min="600"
                    max="5000"
                    step="50"
                    value={manualRpm}
                    onChange={e => setManualRpm(Number(e.target.value))}
                    className="w-36 accent-sky-400 cursor-pointer"
                  />
                  <span className="font-bold text-white w-16 text-right">{manualRpm} RPM</span>
                </div>
                <div className="flex items-center space-x-2">
                  <span className="text-[11px] text-slate-400">LOAD / PEDAL:</span>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    step="1"
                    value={manualLoad}
                    onChange={e => setManualLoad(Number(e.target.value))}
                    className="w-36 accent-amber-400 cursor-pointer"
                  />
                  <span className="font-bold text-amber-300 w-14 text-right">{manualLoad}%</span>
                </div>
              </div>
            </div>
          )}

          {/* Main Container: Map List on Left, Map Visualization on Right */}
          <div className="grid grid-cols-12 gap-5 items-start">
            {/* LEFT: Map Catalog & Hardware Metadata */}
            <div className="col-span-12 lg:col-span-4 space-y-4 font-sans">
              {/* Pack Metadata Card */}
              {activePack && (
                <div className="bg-can-panel border border-can-border rounded-lg p-3.5 shadow-md space-y-2 text-xs">
                  <div className="font-bold text-white uppercase tracking-wider flex items-center justify-between border-b border-can-border pb-2">
                    <span>ECU IDENTIFICATION</span>
                    <span className="text-[10px] px-1.5 py-0.5 bg-sky-950 border border-sky-800 text-sky-400 rounded font-mono">
                      v{activePack.version}
                    </span>
                  </div>
                  <div className="grid grid-cols-2 gap-2 text-[11px] font-mono">
                    <div>
                      <span className="text-slate-500 block text-[9px] font-sans font-bold">HW PART NO</span>
                      <span className="text-sky-300 font-bold">{activePack.hardwarePartNumber}</span>
                    </div>
                    <div>
                      <span className="text-slate-500 block text-[9px] font-sans font-bold">SW VERSION</span>
                      <span className="text-white">{activePack.softwareVersion}</span>
                    </div>
                    <div className="col-span-2">
                      <span className="text-slate-500 block text-[9px] font-sans font-bold">CALIBRATION ID</span>
                      <span className="text-emerald-400 font-bold">{activePack.calibrationId}</span>
                    </div>
                    <div className="col-span-2">
                      <span className="text-slate-500 block text-[9px] font-sans font-bold">PLATFORM TARGET</span>
                      <span className="text-slate-300 text-[10px]">{activePack.targetPlatform}</span>
                    </div>
                  </div>
                </div>
              )}

              {/* Map Category Filters */}
              <div className="bg-can-panel border border-can-border rounded-lg p-3 shadow-md space-y-2">
                <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">MAP CATEGORIES</div>
                <div className="flex flex-wrap gap-1 text-xs">
                  {['All', 'Fuel', 'Boost', 'Torque', 'Ignition', 'Airflow'].map(cat => (
                    <button
                      key={cat}
                      onClick={() => setActiveCategory(cat)}
                      className={`px-2 py-1 rounded font-semibold transition-all ${
                        activeCategory === cat
                          ? 'bg-sky-600 text-white shadow'
                          : 'bg-can-bg border border-can-border text-slate-400 hover:text-white'
                      }`}
                    >
                      {cat}
                    </button>
                  ))}
                </div>

                {/* List of Maps */}
                <div className="space-y-1 pt-2 max-h-72 overflow-y-auto">
                  {filteredMaps.map(m => {
                    const isSelected = activeMap?.mapId === m.mapId;
                    return (
                      <button
                        key={m.mapId}
                        onClick={() => setSelectedMapId(m.mapId)}
                        className={`w-full text-left px-3 py-2 rounded flex items-center justify-between border transition-all ${
                          isSelected
                            ? 'bg-sky-600/20 border-sky-500 text-white font-bold'
                            : 'bg-can-bg border-can-border/60 text-slate-300 hover:bg-slate-800'
                        }`}
                      >
                        <div>
                          <div className="text-xs truncate">{m.name}</div>
                          <div className="text-[10px] text-slate-500 font-mono mt-0.5">
                            {m.xAxis.breakpoints.length}×{m.yAxis ? m.yAxis.breakpoints.length : 1} • {m.zUnit}
                          </div>
                        </div>
                        <ChevronRight className={`w-3.5 h-3.5 ${isSelected ? 'text-sky-400' : 'text-slate-600'}`} />
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Live Operating Point Monitor */}
              <div className="bg-can-panel border border-can-border rounded-lg p-3.5 shadow-md space-y-2">
                <div className="flex items-center justify-between text-xs font-bold text-slate-300 border-b border-can-border pb-1.5">
                  <div className="flex items-center space-x-1.5 text-sky-400">
                    <Activity className="w-4 h-4 animate-pulse" />
                    <span>LIVE OPERATING POINT TRACER</span>
                  </div>
                  <span className={`text-[10px] font-mono px-1.5 py-0.5 rounded border ${
                    tracerMode === 'LIVE_CAN' 
                      ? 'bg-emerald-950 text-emerald-400 border-emerald-800' 
                      : tracerMode === 'MANUAL_BENCH' 
                      ? 'bg-sky-950 text-sky-400 border-sky-800' 
                      : 'bg-purple-950 text-purple-400 border-purple-800'
                  }`}>
                    {tracerMode === 'LIVE_CAN' ? 'CAN BUS' : tracerMode === 'MANUAL_BENCH' ? 'BENCH SLIDER' : 'DEMO'}
                  </span>
                </div>
                <div className="grid grid-cols-3 gap-2 text-center text-xs font-mono pt-1">
                  <div className="bg-can-bg border border-can-border p-2 rounded">
                    <div className="text-[9px] text-slate-500 font-sans">CURRENT RPM</div>
                    <div className="text-white font-bold text-sm mt-0.5">{currentRpm}</div>
                  </div>
                  <div className="bg-can-bg border border-can-border p-2 rounded">
                    <div className="text-[9px] text-slate-500 font-sans truncate">{currentYData.label.toUpperCase()}</div>
                    <div className="text-amber-400 font-bold text-sm mt-0.5">
                      {currentYData.value} <span className="text-[10px] text-slate-400">{currentYData.unit}</span>
                    </div>
                  </div>
                  <div className="bg-can-bg border border-can-border p-2 rounded">
                    <div className="text-[9px] text-slate-500 font-sans">OUTPUT VALUE</div>
                    <div className="text-sky-300 font-bold text-sm mt-0.5">
                      {tracer ? tracer.interpolatedZValue.toFixed(activeMap?.decimalPrecision || 1) : '-'} <span className="text-[10px] text-slate-400">{activeMap?.zUnit}</span>
                    </div>
                  </div>
                </div>
                {tracerMode === 'LIVE_CAN' && (
                  <div className="text-[10px] text-slate-400 font-sans pt-1 flex justify-between items-center border-t border-can-border/60">
                    <span>CAN frames: <strong className="text-emerald-400 font-mono">{totalFrames}</strong></span>
                    <span>Bus rate: <strong className="text-sky-400 font-mono">{fps} FPS</strong></span>
                  </div>
                )}
              </div>
            </div>

            {/* RIGHT: 2D Matrix or 3D Surface Visualization */}
            <div className="col-span-12 lg:col-span-8 bg-can-panel border border-can-border rounded-lg p-5 shadow-xl space-y-4">
              {activeMap ? (
                <>
                  {/* Map Title Bar */}
                  <div className="flex flex-wrap items-center justify-between border-b border-can-border pb-3 gap-2">
                    <div>
                      <div className="flex items-center space-x-2">
                        <h2 className="text-base font-bold text-white font-sans">{activeMap.name}</h2>
                        <span className="text-[10px] font-mono px-2 py-0.5 bg-slate-800 text-slate-400 rounded">
                          ADDR: {activeMap.memoryAddressHex}
                        </span>
                      </div>
                      <p className="text-xs text-slate-400 font-sans mt-0.5">{activeMap.description}</p>
                    </div>

                    <div className="flex items-center space-x-3 text-xs font-mono">
                      <span className="text-slate-400">
                        Range: <strong className="text-white">{activeMap.minValue}</strong> to <strong className="text-white">{activeMap.maxValue} {activeMap.zUnit}</strong>
                      </span>
                    </div>
                  </div>

                  {/* 2D MATRIX VIEW */}
                  {viewMode === '2D' && (
                    <div className="overflow-x-auto pb-2">
                      <table className="w-full text-[11px] font-mono border-collapse">
                        <thead>
                          <tr>
                            <th className="p-2 text-left bg-slate-900 border border-slate-800 text-slate-500 text-[10px]">
                              {activeMap.yAxis ? `${activeMap.yAxis.name} (${activeMap.yAxis.unit})` : 'Row'} \ {activeMap.xAxis.name} ({activeMap.xAxis.unit})
                            </th>
                            {activeMap.xAxis.breakpoints.map((bp, xIdx) => {
                              const isHighlightedX = tracer?.nearestXIndex === xIdx;
                              return (
                                <th
                                  key={xIdx}
                                  className={`p-2 border text-center transition-colors ${
                                    isHighlightedX ? 'bg-sky-600 text-white font-bold border-sky-400' : 'bg-slate-900 border-slate-800 text-sky-400'
                                  }`}
                                >
                                  {bp}
                                </th>
                              );
                            })}
                          </tr>
                        </thead>
                        <tbody>
                          {activeMap.zData.map((row, yIdx) => {
                            const isHighlightedY = tracer?.nearestYIndex === yIdx;
                            const rowLabel = activeMap.yAxis && activeMap.yAxis.breakpoints[yIdx] !== undefined 
                              ? activeMap.yAxis.breakpoints[yIdx] 
                              : yIdx;

                            return (
                              <tr key={yIdx}>
                                <td
                                  className={`p-2 border font-bold text-left whitespace-nowrap transition-colors ${
                                    isHighlightedY ? 'bg-amber-600 text-white border-amber-400' : 'bg-slate-900/80 border-slate-800 text-amber-300'
                                  }`}
                                >
                                  {rowLabel}
                                </td>

                                {row.map((cellVal, xIdx) => {
                                  const isActiveCell = tracer?.nearestXIndex === xIdx && tracer?.nearestYIndex === yIdx;
                                  const cellBg = getCellHeatmapBg(cellVal, activeMap.minValue, activeMap.maxValue, activeMap.heatmapPalette);

                                  return (
                                    <td
                                      key={xIdx}
                                      className={`p-2 border text-center transition-all ${cellBg} ${
                                        isActiveCell
                                          ? 'ring-2 ring-emerald-400 border-emerald-400 scale-105 z-10 shadow-lg font-black text-white'
                                          : 'border-slate-800/80'
                                      }`}
                                      title={`RPM: ${activeMap.xAxis.breakpoints[xIdx]}, ${currentYData.label}: ${rowLabel}, Value: ${cellVal} ${activeMap.zUnit}`}
                                    >
                                      {cellVal.toFixed(activeMap.decimalPrecision)}
                                    </td>
                                  );
                                })}
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}

                  {/* 3D SURFACE / WIREFRAME PERSPECTIVE VIEW */}
                  {viewMode === '3D' && (
                    <div className="relative w-full h-[420px] bg-slate-950 border border-slate-800 rounded-lg overflow-hidden flex items-center justify-center p-4">
                      {/* Isometric SVG 3D Mesh Renderer */}
                      <svg viewBox="0 0 650 380" className="w-full h-full">
                        <defs>
                          <linearGradient id="meshGrad" x1="0" y1="1" x2="0" y2="0">
                            <stop offset="0%" stopColor="#0369a1" stopOpacity="0.4" />
                            <stop offset="50%" stopColor="#10b981" stopOpacity="0.5" />
                            <stop offset="100%" stopColor="#f43f5e" stopOpacity="0.7" />
                          </linearGradient>
                        </defs>

                        {/* Render isometric mesh rows */}
                        {activeMap.zData.map((row, yIdx) => {
                          const originX = 320;
                          const originY = 120;
                          const colSpacing = 28;
                          const rowSpacing = 16;
                          const elevationScale = 140;

                          const pts = row.map((val, xIdx) => {
                            const normalizedZ = (val - activeMap.minValue) / (activeMap.maxValue - activeMap.minValue || 1);
                            const sx = originX + (xIdx - yIdx) * colSpacing;
                            const sy = originY + (xIdx + yIdx) * rowSpacing - (normalizedZ * elevationScale);
                            return `${sx},${sy}`;
                          });

                          const polylinePoints = pts.join(' ');

                          return (
                            <g key={yIdx}>
                              <polyline
                                points={polylinePoints}
                                fill="none"
                                stroke="#38bdf8"
                                strokeWidth="1.5"
                                strokeOpacity={0.85}
                              />

                              {/* Highlight Active Tracer Node on 3D Surface */}
                              {tracer?.nearestYIndex === yIdx && (
                                <g>
                                  {(() => {
                                    const xIdx = tracer.nearestXIndex;
                                    const val = row[xIdx] || 0;
                                    const normZ = (val - activeMap.minValue) / (activeMap.maxValue - activeMap.minValue || 1);
                                    const sx = originX + (xIdx - yIdx) * colSpacing;
                                    const sy = originY + (xIdx + yIdx) * rowSpacing - (normZ * elevationScale);
                                    return (
                                      <g>
                                        <circle cx={sx} cy={sy} r="6" fill="#10b981" stroke="#ffffff" strokeWidth="2" className="animate-ping" />
                                        <circle cx={sx} cy={sy} r="4" fill="#34d399" />
                                        <text x={sx + 10} y={sy - 10} fill="#34d399" fontSize="11" fontWeight="bold" fontFamily="monospace">
                                          {tracer.interpolatedZValue.toFixed(activeMap.decimalPrecision)} {activeMap.zUnit}
                                        </text>
                                      </g>
                                    );
                                  })()}
                                </g>
                              )}
                            </g>
                          );
                        })}

                        {/* Column connection wireframes */}
                        {activeMap.xAxis.breakpoints.map((_, xIdx) => {
                          const originX = 320;
                          const originY = 120;
                          const colSpacing = 28;
                          const rowSpacing = 16;
                          const elevationScale = 140;

                          const colPts = activeMap.zData.map((row, yIdx) => {
                            const val = row[xIdx] || 0;
                            const normZ = (val - activeMap.minValue) / (activeMap.maxValue - activeMap.minValue || 1);
                            const sx = originX + (xIdx - yIdx) * colSpacing;
                            const sy = originY + (xIdx + yIdx) * rowSpacing - (normZ * elevationScale);
                            return `${sx},${sy}`;
                          }).join(' ');

                          return (
                            <polyline
                              key={`col_${xIdx}`}
                              points={colPts}
                              fill="none"
                              stroke="#64748b"
                              strokeWidth="1"
                              strokeOpacity={0.4}
                              strokeDasharray="2,2"
                            />
                          );
                        })}
                      </svg>

                      {/* 3D Legend & Navigation Hint */}
                      <div className="absolute bottom-3 left-3 bg-slate-900/80 border border-slate-800 rounded p-2 text-[10px] text-slate-400 font-sans space-y-1">
                        <div className="font-bold text-white flex items-center space-x-1">
                          <Compass className="w-3 h-3 text-sky-400" />
                          <span>ISOMETRIC SURFACE PROJECTION</span>
                        </div>
                        <div>X-Axis: {activeMap.xAxis.name} ({activeMap.xAxis.unit}) • Y-Axis: {activeMap.yAxis?.name || 'Load'} ({activeMap.yAxis?.unit || '%'})</div>
                        <div className="text-emerald-400 font-bold">Green Beacon: Active Operating Point ({tracerMode === 'LIVE_CAN' ? 'Live CANH/CANL' : tracerMode === 'MANUAL_BENCH' ? 'Bench Scrub' : 'Demo'})</div>
                      </div>
                    </div>
                  )}
                </>
              ) : (
                <div className="p-12 text-center text-slate-500">Select a map from the catalog to view</div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* =========================================================================
          VIEW 2: DYNAMIC LIVE 3D TABLE BUILDER (WORKS ON ANY ECU & ANY SIGNALS)
          ========================================================================= */}
      {workspaceMode === 'DYNAMIC_BUILDER' && (
        <div className="space-y-5">
          {/* Builder Configuration Bar */}
          <div className="bg-can-panel border border-emerald-500/30 rounded-lg p-4 shadow-lg space-y-3 font-sans">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-can-border pb-3">
              <div className="flex items-center space-x-3">
                <div className="p-2 bg-emerald-500/20 border border-emerald-500/40 rounded-lg text-emerald-400">
                  <Sparkles className="w-5 h-5" />
                </div>
                <div>
                  <h2 className="text-base font-bold text-white">Universal Live 3D Surface Generator</h2>
                  <p className="text-xs text-slate-400">
                    Select any 3 signals from your connected CAN bus to generate a real-time correlated 2D matrix and 3D surface mesh
                  </p>
                </div>
              </div>

              <div className="flex items-center space-x-3">
                <button
                  onClick={handleSaveDynamicTable}
                  disabled={isSavingCustomPack}
                  className="px-4 py-1.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded font-bold text-xs flex items-center space-x-1.5 shadow transition-all"
                  title="Persist this live generated table into the permanent Mappack catalog"
                >
                  <Save className="w-4 h-4" />
                  <span>{isSavingCustomPack ? 'Saving...' : 'Save to Mappack Catalog'}</span>
                </button>
              </div>
            </div>

            {saveSuccessMsg && (
              <div className="p-2 bg-emerald-950/60 border border-emerald-500 rounded text-emerald-300 text-xs font-bold flex items-center space-x-2">
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                <span>{saveSuccessMsg}</span>
              </div>
            )}

            {/* Signal Mapping Controls */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4 text-xs">
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-1">X-AXIS SIGNAL (HORIZONTAL)</label>
                <select
                  value={dynXSignal}
                  onChange={e => setDynXSignal(e.target.value)}
                  className="w-full bg-can-bg border border-can-border rounded px-2.5 py-1.5 text-white font-mono font-bold"
                >
                  {availableSignalNames.map(sig => (
                    <option key={sig} value={sig}>{sig} ({signals[sig]?.unit || ''})</option>
                  ))}
                </select>
                <span className="text-[10px] text-sky-400 mt-1 block font-mono">
                  Live: {signals[dynXSignal]?.engineeringValue !== undefined ? `${signals[dynXSignal]?.engineeringValue} ${signals[dynXSignal]?.unit}` : 'Waiting'}
                </span>
              </div>

              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-1">Y-AXIS SIGNAL (VERTICAL)</label>
                <select
                  value={dynYSignal}
                  onChange={e => setDynYSignal(e.target.value)}
                  className="w-full bg-can-bg border border-can-border rounded px-2.5 py-1.5 text-white font-mono font-bold"
                >
                  {availableSignalNames.map(sig => (
                    <option key={sig} value={sig}>{sig} ({signals[sig]?.unit || ''})</option>
                  ))}
                </select>
                <span className="text-[10px] text-amber-400 mt-1 block font-mono">
                  Live: {signals[dynYSignal]?.engineeringValue !== undefined ? `${signals[dynYSignal]?.engineeringValue} ${signals[dynYSignal]?.unit}` : 'Waiting'}
                </span>
              </div>

              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-1">Z-OUTPUT VALUE (CELL METRIC)</label>
                <select
                  value={dynZSignal}
                  onChange={e => setDynZSignal(e.target.value)}
                  className="w-full bg-can-bg border border-can-border rounded px-2.5 py-1.5 text-white font-mono font-bold"
                >
                  {availableSignalNames.map(sig => (
                    <option key={sig} value={sig}>{sig} ({signals[sig]?.unit || ''})</option>
                  ))}
                </select>
                <span className="text-[10px] text-emerald-400 mt-1 block font-mono">
                  Live: {signals[dynZSignal]?.engineeringValue !== undefined ? `${signals[dynZSignal]?.engineeringValue} ${signals[dynZSignal]?.unit}` : 'Waiting'}
                </span>
              </div>

              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-1">CUSTOM TABLE LABEL</label>
                <input
                  type="text"
                  value={dynTableName}
                  onChange={e => setDynTableName(e.target.value)}
                  placeholder="e.g. Live Boost Target"
                  className="w-full bg-can-bg border border-can-border rounded px-2.5 py-1.5 text-white text-xs"
                />
                <span className="text-[10px] text-slate-500 mt-1 block">
                  Grid: 8×8 Matrix • Auto-Binned Breakpoints
                </span>
              </div>
            </div>
          </div>

          {/* Dynamic Generated Table Grid and 3D Visualizer */}
          <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow-xl space-y-4">
            <div className="flex flex-wrap items-center justify-between border-b border-can-border pb-3">
              <div>
                <h3 className="text-base font-bold text-white font-sans">{dynTableName}</h3>
                <p className="text-xs text-slate-400 font-sans mt-0.5">
                  X: <strong className="text-sky-300 font-mono">{dynXSignal}</strong> • Y: <strong className="text-amber-300 font-mono">{dynYSignal}</strong> • Z (Cells): <strong className="text-emerald-300 font-mono">{dynZSignal}</strong>
                </p>
              </div>

              <div className="flex items-center space-x-2">
                <button
                  onClick={() => setViewMode(viewMode === '2D' ? '3D' : '2D')}
                  className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded text-xs font-bold font-sans flex items-center space-x-1.5 transition-all"
                >
                  {viewMode === '2D' ? <Rotate3d className="w-3.5 h-3.5" /> : <Grid className="w-3.5 h-3.5" />}
                  <span>Switch to {viewMode === '2D' ? '3D Surface' : '2D Matrix'}</span>
                </button>
              </div>
            </div>

            {/* 2D Dynamic Table Grid */}
            {viewMode === '2D' && (
              <div className="overflow-x-auto pb-2 font-mono">
                <table className="w-full text-[11px] border-collapse">
                  <thead>
                    <tr>
                      <th className="p-2 text-left bg-slate-900 border border-slate-800 text-slate-500 text-[10px]">
                        {dynYSignal} \ {dynXSignal}
                      </th>
                      {dynXBreakpoints.map((bp, xIdx) => (
                        <th key={xIdx} className="p-2 border text-center bg-slate-900 border-slate-800 text-sky-400 font-bold">
                          {bp}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {dynCellAccumulator.map((row, yIdx) => {
                      const rowLabel = dynYBreakpoints[yIdx];
                      const minZ = Math.min(...dynCellAccumulator.flat());
                      const maxZ = Math.max(...dynCellAccumulator.flat());

                      return (
                        <tr key={yIdx}>
                          <td className="p-2 border font-bold text-left bg-slate-900/80 border-slate-800 text-amber-300 whitespace-nowrap">
                            {rowLabel}
                          </td>
                          {row.map((cellVal, xIdx) => {
                            const bg = getCellHeatmapBg(cellVal, minZ, maxZ, 'turbo');
                            return (
                              <td
                                key={xIdx}
                                className={`p-2 border text-center transition-all ${bg} border-slate-800/80`}
                              >
                                {cellVal.toFixed(1)}
                              </td>
                            );
                          })}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}

            {/* 3D Isometric Mesh View for Dynamic Builder */}
            {viewMode === '3D' && (
              <div className="relative w-full h-[400px] bg-slate-950 border border-slate-800 rounded-lg overflow-hidden flex items-center justify-center p-4">
                <svg viewBox="0 0 650 380" className="w-full h-full">
                  {dynCellAccumulator.map((row, yIdx) => {
                    const originX = 320;
                    const originY = 120;
                    const colSpacing = 28;
                    const rowSpacing = 16;
                    const elevationScale = 120;
                    const minZ = Math.min(...dynCellAccumulator.flat());
                    const maxZ = Math.max(...dynCellAccumulator.flat()) || 1;

                    const pts = row.map((val, xIdx) => {
                      const normalizedZ = (val - minZ) / (maxZ - minZ || 1);
                      const sx = originX + (xIdx - yIdx) * colSpacing;
                      const sy = originY + (xIdx + yIdx) * rowSpacing - (normalizedZ * elevationScale);
                      return `${sx},${sy}`;
                    });

                    return (
                      <polyline
                        key={yIdx}
                        points={pts.join(' ')}
                        fill="none"
                        stroke="#10b981"
                        strokeWidth="1.5"
                        strokeOpacity={0.85}
                      />
                    );
                  })}
                </svg>

                <div className="absolute bottom-3 left-3 bg-slate-900/80 border border-slate-800 rounded p-2 text-[10px] text-slate-400 font-sans">
                  <div className="font-bold text-white">UNIVERSAL SIGNAL SURFACE PROJECTION</div>
                  <div>Live Real-Time CAN Bivariate Mesh Generator</div>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
