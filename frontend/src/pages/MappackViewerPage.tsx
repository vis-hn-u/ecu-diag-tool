import React, { useState, useEffect } from 'react';
import { api, downloadBlobAsFile } from '../services/api';
import { EcuDefinitionPack, ReadonlyEcuMap, OperatingPointTracer } from '../types/diagnosticsExtended';
import { 
  Sliders, Grid, ShieldAlert, Cpu, Layers, Eye, Activity, Rotate3d, 
  Sparkles, Compass, CheckCircle2, ChevronRight, Hash, FileText 
} from 'lucide-react';

export const MappackViewerPage: React.FC = () => {
  const [packs, setPacks] = useState<EcuDefinitionPack[]>([]);
  const [selectedPackId, setSelectedPackId] = useState<string>('med17_ea888_gen2');
  const [selectedMapId, setSelectedMapId] = useState<string>('KFZW');
  const [activeCategory, setActiveCategory] = useState<string>('All');
  const [viewMode, setViewMode] = useState<'2D' | '3D'>('2D');
  const [isExportingPdf, setIsExportingPdf] = useState(false);

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

  // Simulated live engine operating point
  const [liveRpm, setLiveRpm] = useState<number>(3200);
  const [liveLoad, setLiveLoad] = useState<number>(115.0);
  const [tracer, setTracer] = useState<OperatingPointTracer | null>(null);

  useEffect(() => {
    api.getMappacks().then(pList => {
      setPacks(pList);
      if (pList.length > 0 && !pList.some(p => p.id === selectedPackId)) {
        setSelectedPackId(pList[0].id);
      }
    }).catch(() => {});
  }, []);

  const activePack = packs.find(p => p.id === selectedPackId) || packs[0];
  const activeMap = activePack?.maps.find(m => m.mapId === selectedMapId) || activePack?.maps[0];

  // Fetch or calculate real-time operating point
  useEffect(() => {
    if (activePack && activeMap) {
      api.getOperatingPointTracer(activePack.id, activeMap.mapId, liveRpm, liveLoad)
        .then(setTracer)
        .catch(() => {});
    }
  }, [activePack, activeMap, liveRpm, liveLoad]);

  // Periodic simulated live engine drift
  useEffect(() => {
    const interval = setInterval(() => {
      const t = Date.now() / 1000;
      setLiveRpm(Math.round(2200 + Math.sin(t * 0.8) * 1800 + 800));
      setLiveLoad(Math.round(60 + Math.sin(t * 1.2) * 50));
    }, 1200);
    return () => clearInterval(interval);
  }, []);

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
      <div className="bg-amber-950/30 border border-amber-500/40 rounded-lg p-3 flex items-center justify-between shadow font-sans">
        <div className="flex items-center space-x-3 text-amber-300 text-xs">
          <ShieldAlert className="w-5 h-5 text-amber-400 shrink-0" />
          <div>
            <span className="font-bold uppercase tracking-wider">Strictly Read-Only ECU Calibration Reference:</span>
            <span className="text-slate-300 ml-1.5">
              ECU definition mappacks display verified factory & reverse-engineered table metadata. Absolutely no ECU flash, write, or security bypass routines exist.
            </span>
          </div>
        </div>
        <div className="px-2.5 py-1 bg-amber-500/20 border border-amber-500/40 text-amber-300 text-[11px] font-bold rounded font-mono">
          READ-ONLY
        </div>
      </div>

      {/* Header & Pack Selector Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow flex flex-wrap items-center justify-between gap-4 font-sans">
        <div className="flex items-center space-x-3">
          <div className="p-2 bg-purple-500/10 border border-purple-500/30 rounded-lg text-purple-400">
            <Cpu className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-white tracking-wide">ECU Definition Packs & 2D/3D Map Viewer</h1>
            <p className="text-xs text-slate-400">
              Hardware/Software matched calibration mappacks, 2D matrix heatmaps, and 3D surface mesh visualization
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
              className="bg-can-bg border border-can-border rounded px-3 py-1.5 text-white font-bold"
            >
              {packs.map(p => (
                <option key={p.id} value={p.id}>
                  {p.name} ({p.hardwarePartNumber})
                </option>
              ))}
            </select>
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

      {/* Main Container: Map List on Left, Map Visualization on Right */}
      <div className="grid grid-cols-12 gap-5 items-start">
        {/* LEFT: Map Catalog & Hardware Metadata */}
        <div className="col-span-12 lg:col-span-4 space-y-4 font-sans">
          {/* Pack Metadata Card */}
          {activePack && (
            <div className="bg-can-panel border border-can-border rounded-lg p-3.5 shadow-md space-y-2 text-xs">
              <div className="font-bold text-white uppercase tracking-wider flex items-center justify-between border-b border-can-border pb-2">
                <span>ECU IDENTIFICATION</span>
                <span className="text-[10px] px-1.5 py-0.5 bg-sky-950 border border-sky-800 text-sky-400 rounded">
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
                <div>
                  <span className="text-slate-500 block text-[9px] font-sans font-bold">CALIBRATION ID</span>
                  <span className="text-emerald-400 font-bold">{activePack.calibrationId}</span>
                </div>
                <div>
                  <span className="text-slate-500 block text-[9px] font-sans font-bold">PROTOCOLS</span>
                  <span className="text-slate-300">{activePack.supportedProtocols.join(', ')}</span>
                </div>
              </div>
            </div>
          )}

          {/* Map Category Filters */}
          <div className="bg-can-panel border border-can-border rounded-lg p-3 shadow-md space-y-2">
            <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">MAP CATEGORIES</div>
            <div className="flex flex-wrap gap-1 text-xs">
              {['All', 'Ignition', 'Boost', 'Fuel', 'Torque', 'Airflow'].map(cat => (
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
              <span className="text-[10px] text-emerald-400 font-mono">TRACKING</span>
            </div>
            <div className="grid grid-cols-3 gap-2 text-center text-xs font-mono pt-1">
              <div className="bg-can-bg border border-can-border p-2 rounded">
                <div className="text-[9px] text-slate-500 font-sans">CURRENT RPM</div>
                <div className="text-white font-bold text-sm mt-0.5">{liveRpm}</div>
              </div>
              <div className="bg-can-bg border border-can-border p-2 rounded">
                <div className="text-[9px] text-slate-500 font-sans">CURRENT LOAD</div>
                <div className="text-amber-400 font-bold text-sm mt-0.5">{liveLoad}%</div>
              </div>
              <div className="bg-can-bg border border-can-border p-2 rounded">
                <div className="text-[9px] text-slate-500 font-sans">TABLE VALUE</div>
                <div className="text-sky-300 font-bold text-sm mt-0.5">
                  {tracer ? tracer.interpolatedZValue.toFixed(activeMap?.decimalPrecision || 1) : '-'} {activeMap?.zUnit}
                </div>
              </div>
            </div>
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
                                  title={`RPM: ${activeMap.xAxis.breakpoints[xIdx]}, Load: ${rowLabel}, Value: ${cellVal} ${activeMap.zUnit}`}
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
                      const numRows = activeMap.zData.length;
                      const numCols = row.length;

                      // Isometric projection formulas:
                      // screenX = originX + (col - row) * colSpacing
                      // screenY = originY + (col + row) * rowSpacing - (zValue / maxZ) * elevation
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
                                      {val.toFixed(activeMap.decimalPrecision)} {activeMap.zUnit}
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
                    <div>X-Axis: Engine Speed (RPM) • Y-Axis: Engine Load (%)</div>
                    <div className="text-emerald-400 font-bold">Green Dot: Active Live Operating Point</div>
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
  );
};
