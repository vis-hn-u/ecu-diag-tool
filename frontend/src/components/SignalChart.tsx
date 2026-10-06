import React, { useRef, useEffect, useState, useMemo } from 'react';
import { useCan } from '../context/CanContext';
import { LineChart as ChartIcon, Play, Pause, ZoomIn, ZoomOut, RotateCcw, Filter, Activity } from 'lucide-react';
import { CanonicalLogDataSet, TimeSeriesChannel } from '../types';

interface SignalChartProps {
  selectedSignals: string[];
  onToggleSignal?: (name: string) => void;
  height?: number;
  dataSet?: CanonicalLogDataSet | null;
  channels?: Record<string, TimeSeriesChannel>;
  showProcessingControls?: boolean;
}

const COLORS = [
  '#38BDF8', // sky
  '#34D399', // emerald
  '#FBBF24', // amber
  '#F472B6', // pink
  '#A78BFA', // purple
  '#FB923C', // orange
  '#E879F9', // fuchsia
  '#4ADE80'  // green
];

export const SignalChart: React.FC<SignalChartProps> = ({
  selectedSignals,
  onToggleSignal,
  height = 320,
  dataSet,
  channels,
  showProcessingControls = true
}) => {
  const { signalHistory, signals } = useCan();
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  const [isPaused, setIsPaused] = useState(false);
  const [isFiltered, setIsFiltered] = useState(false);
  const [filterMode, setFilterMode] = useState<'movingAvg' | 'median'>('movingAvg');
  const [zoomLevel, setZoomLevel] = useState(1.0);
  const [panOffset, setPanOffset] = useState(0.0); // 0.0 to 1.0 (start point)
  const [cursorPos, setCursorPos] = useState<{ x: number; y: number } | null>(null);
  const [cursorValues, setCursorValues] = useState<{ timestamp: string; items: { name: string; value: number; unit: string; color: string }[] } | null>(null);

  // Normalize data points into a uniform format { timestamp: number, value: number }[]
  const preparedSeries = useMemo(() => {
    const seriesMap: Record<string, { x: number; y: number }[]> = {};

    selectedSignals.forEach(sigName => {
      let rawPoints: { x: number; y: number }[] = [];

      if (channels && channels[sigName]) {
        const chan = channels[sigName];
        rawPoints = chan.timestampsMs.map((t, i) => ({ x: t, y: chan.values[i] }));
      } else if (dataSet && dataSet.channels[sigName]) {
        const chan = dataSet.channels[sigName];
        rawPoints = chan.timestampsMs.map((t, i) => ({ x: t, y: chan.values[i] }));
      } else if (signalHistory[sigName]) {
        rawPoints = signalHistory[sigName].map(pt => ({ x: pt.timestamp, y: pt.value }));
      }

      if (rawPoints.length === 0) return;

      // Apply client-side filter if toggled
      if (isFiltered && rawPoints.length > 3) {
        const filtered: { x: number; y: number }[] = [];
        const windowSize = 5;
        const half = Math.floor(windowSize / 2);

        for (let i = 0; i < rawPoints.length; i++) {
          const start = Math.max(0, i - half);
          const end = Math.min(rawPoints.length, i + half + 1);
          const slice = rawPoints.slice(start, end).map(p => p.y);

          let smoothedY = rawPoints[i].y;
          if (filterMode === 'median') {
            slice.sort((a, b) => a - b);
            smoothedY = slice[Math.floor(slice.length / 2)];
          } else {
            smoothedY = slice.reduce((a, b) => a + b, 0) / slice.length;
          }

          filtered.push({ x: rawPoints[i].x, y: smoothedY });
        }
        seriesMap[sigName] = filtered;
      } else {
        seriesMap[sigName] = rawPoints;
      }
    });

    return seriesMap;
  }, [selectedSignals, channels, dataSet, signalHistory, isFiltered, filterMode]);

  // Handle Zoom In / Out / Reset
  const handleZoomIn = () => setZoomLevel(prev => Math.min(prev * 1.5, 10.0));
  const handleZoomOut = () => {
    setZoomLevel(prev => {
      const next = Math.max(prev / 1.5, 1.0);
      if (next === 1.0) setPanOffset(0.0);
      return next;
    });
  };
  const handleResetZoom = () => {
    setZoomLevel(1.0);
    setPanOffset(0.0);
  };

  // Canvas drawing effect
  useEffect(() => {
    if (isPaused && !dataSet && !channels) return;

    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    canvas.width = rect.width * dpr;
    canvas.height = rect.height * dpr;
    ctx.scale(dpr, dpr);

    const w = rect.width;
    const h = rect.height;
    const padding = { top: 25, right: 35, bottom: 35, left: 65 };
    const plotWidth = w - padding.left - padding.right;
    const plotHeight = h - padding.top - padding.bottom;

    ctx.clearRect(0, 0, w, h);

    // Draw background grid
    ctx.strokeStyle = '#1E293B';
    ctx.lineWidth = 1;
    const gridRows = 5;
    for (let i = 0; i <= gridRows; i++) {
      const y = padding.top + (plotHeight / gridRows) * i;
      ctx.beginPath();
      ctx.moveTo(padding.left, y);
      ctx.lineTo(w - padding.right, y);
      ctx.stroke();
    }

    if (selectedSignals.length === 0) {
      ctx.fillStyle = '#64748B';
      ctx.font = '12px Inter, sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText('Select one or more signals to graph in real time', w / 2, h / 2);
      return;
    }

    // Determine min and max Y across all selected signals
    let minVal = Number.POSITIVE_INFINITY;
    let maxVal = Number.NEGATIVE_INFINITY;

    selectedSignals.forEach(name => {
      const pts = preparedSeries[name];
      if (pts && pts.length > 0) {
        pts.forEach(p => {
          if (p.y < minVal) minVal = p.y;
          if (p.y > maxVal) maxVal = p.y;
        });
      }
    });

    if (minVal === Number.POSITIVE_INFINITY || maxVal === Number.NEGATIVE_INFINITY || minVal === maxVal) {
      minVal = minVal === maxVal ? minVal - 10 : 0;
      maxVal = minVal + 20;
    }

    // Draw Y-axis labels
    ctx.fillStyle = '#94A3B8';
    ctx.font = '10px JetBrains Mono, monospace';
    ctx.textAlign = 'right';
    for (let i = 0; i <= gridRows; i++) {
      const y = padding.top + (plotHeight / gridRows) * i;
      const val = maxVal - ((maxVal - minVal) / gridRows) * i;
      ctx.fillText(val.toFixed(1), padding.left - 8, y + 3);
    }

    // Draw traces with zoom and pan slicing
    selectedSignals.forEach((sigName, sigIdx) => {
      const allPts = preparedSeries[sigName];
      if (!allPts || allPts.length < 2) return;

      // Apply zoom window
      const totalLen = allPts.length;
      const visibleCount = Math.max(2, Math.floor(totalLen / zoomLevel));
      const maxStartIndex = Math.max(0, totalLen - visibleCount);
      const startIndex = Math.min(maxStartIndex, Math.floor(panOffset * maxStartIndex));
      const visiblePts = allPts.slice(startIndex, startIndex + visibleCount);

      if (visiblePts.length < 2) return;

      const color = COLORS[sigIdx % COLORS.length];

      // Draw faint raw shadow if filtered mode is active
      if (isFiltered) {
        ctx.strokeStyle = `${color}44`;
        ctx.lineWidth = 1;
        ctx.setLineDash([2, 2]);
        ctx.beginPath();
        visiblePts.forEach((pt, i) => {
          const x = padding.left + (i / (visiblePts.length - 1)) * plotWidth;
          const normY = (pt.y - minVal) / (maxVal - minVal);
          const y = padding.top + plotHeight - normY * plotHeight;
          if (i === 0) ctx.moveTo(x, y);
          else ctx.lineTo(x, y);
        });
        ctx.stroke();
        ctx.setLineDash([]);
      }

      // Draw main signal trace
      ctx.strokeStyle = color;
      ctx.lineWidth = 2;
      ctx.beginPath();

      visiblePts.forEach((pt, i) => {
        const x = padding.left + (i / (visiblePts.length - 1)) * plotWidth;
        const normY = (pt.y - minVal) / (maxVal - minVal);
        const y = padding.top + plotHeight - normY * plotHeight;

        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      });

      ctx.stroke();

      // Current/End point indicator
      const lastPt = visiblePts[visiblePts.length - 1];
      const lastX = padding.left + plotWidth;
      const lastNormY = (lastPt.y - minVal) / (maxVal - minVal);
      const lastY = padding.top + plotHeight - lastNormY * plotHeight;

      ctx.fillStyle = color;
      ctx.beginPath();
      ctx.arc(lastX, lastY, 4, 0, Math.PI * 2);
      ctx.fill();
    });

    // Draw shared vertical cursor line if hovered
    if (cursorPos && cursorPos.x >= padding.left && cursorPos.x <= w - padding.right) {
      ctx.strokeStyle = '#E2E8F0';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 3]);
      ctx.beginPath();
      ctx.moveTo(cursorPos.x, padding.top);
      ctx.lineTo(cursorPos.x, padding.top + plotHeight);
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }, [preparedSeries, selectedSignals, isPaused, zoomLevel, panOffset, cursorPos, isFiltered, dataSet, channels]);

  // Handle canvas mouse move for shared cursor & values tooltip
  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    const padding = { top: 25, right: 35, bottom: 35, left: 65 };
    const plotWidth = rect.width - padding.left - padding.right;

    if (x < padding.left || x > rect.width - padding.right) {
      setCursorPos(null);
      setCursorValues(null);
      return;
    }

    setCursorPos({ x, y });

    // Calculate ratio across visible range
    const ratio = (x - padding.left) / plotWidth;
    const items: { name: string; value: number; unit: string; color: string }[] = [];
    let hoveredTs = '';

    selectedSignals.forEach((sigName, sigIdx) => {
      const pts = preparedSeries[sigName];
      if (!pts || pts.length === 0) return;

      const totalLen = pts.length;
      const visibleCount = Math.max(2, Math.floor(totalLen / zoomLevel));
      const maxStartIndex = Math.max(0, totalLen - visibleCount);
      const startIndex = Math.min(maxStartIndex, Math.floor(panOffset * maxStartIndex));
      const targetIndex = Math.min(pts.length - 1, Math.max(0, startIndex + Math.round(ratio * (visibleCount - 1))));

      const pt = pts[targetIndex];
      if (pt) {
        let unit = '';
        if (channels && channels[sigName]) unit = channels[sigName].units;
        else if (signals[sigName]) unit = signals[sigName].unit;

        items.push({
          name: sigName,
          value: pt.y,
          unit,
          color: COLORS[sigIdx % COLORS.length]
        });

        if (!hoveredTs) {
          hoveredTs = `${(pt.x / 1000).toFixed(3)}s`;
        }
      }
    });

    setCursorValues({ timestamp: hoveredTs, items });
  };

  const handleMouseLeave = () => {
    setCursorPos(null);
    setCursorValues(null);
  };

  return (
    <div className="bg-can-panel border border-can-border rounded-lg p-4 font-mono shadow-xl space-y-3 select-none">
      {/* Chart Top Header & Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-can-border pb-2.5">
        <div className="flex items-center space-x-2">
          <ChartIcon className="w-4 h-4 text-sky-400" />
          <span className="text-sm font-semibold text-white font-sans">SYNCHRONIZED MULTI-SIGNAL OSCILLOSCOPE</span>
          {zoomLevel > 1.0 && (
            <span className="text-[10px] px-2 py-0.5 rounded bg-sky-500/20 text-sky-400 border border-sky-500/30">
              {zoomLevel.toFixed(1)}x ZOOM
            </span>
          )}
        </div>

        {/* Toolbar Controls */}
        <div className="flex items-center space-x-2 text-xs">
          {showProcessingControls && (
            <>
              {/* Raw vs Filtered Toggle */}
              <button
                onClick={() => setIsFiltered(!isFiltered)}
                className={`px-2.5 py-1 rounded flex items-center space-x-1.5 border transition-colors ${
                  isFiltered
                    ? 'bg-purple-500/20 text-purple-300 border-purple-500/40 font-bold'
                    : 'bg-slate-800 text-slate-400 border-can-border hover:bg-slate-700'
                }`}
                title="Toggle real-time smoothing filter on noisy channels"
              >
                <Filter className="w-3.5 h-3.5" />
                <span>{isFiltered ? 'FILTERED' : 'RAW'}</span>
              </button>

              {isFiltered && (
                <select
                  value={filterMode}
                  onChange={e => setFilterMode(e.target.value as any)}
                  className="bg-can-bg border border-can-border rounded px-2 py-1 text-[11px] text-slate-200"
                >
                  <option value="movingAvg">Moving Avg</option>
                  <option value="median">Median Spike</option>
                </select>
              )}
            </>
          )}

          {/* Zoom In, Out, Reset */}
          <div className="flex items-center bg-can-card rounded border border-can-border p-0.5">
            <button
              onClick={handleZoomIn}
              className="p-1 hover:bg-slate-700 text-slate-300 rounded"
              title="Zoom In (+)"
            >
              <ZoomIn className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={handleZoomOut}
              className="p-1 hover:bg-slate-700 text-slate-300 rounded"
              title="Zoom Out (-)"
            >
              <ZoomOut className="w-3.5 h-3.5" />
            </button>
            {zoomLevel > 1.0 && (
              <button
                onClick={handleResetZoom}
                className="p-1 hover:bg-slate-700 text-amber-400 rounded"
                title="Reset Zoom"
              >
                <RotateCcw className="w-3.5 h-3.5" />
              </button>
            )}
          </div>

          {/* Live Pause/Resume if live */}
          {!dataSet && !channels && (
            <button
              onClick={() => setIsPaused(!isPaused)}
              className={`px-2.5 py-1 rounded font-semibold flex items-center space-x-1.5 border transition-colors ${
                isPaused
                  ? 'bg-amber-500/10 text-amber-400 border-amber-500/30'
                  : 'bg-slate-800 text-slate-300 border-can-border hover:bg-slate-700'
              }`}
            >
              {isPaused ? <Play className="w-3.5 h-3.5" /> : <Pause className="w-3.5 h-3.5" />}
              <span>{isPaused ? 'RESUME' : 'PAUSE'}</span>
            </button>
          )}
        </div>
      </div>

      {/* Signal Legend / Interactive Badges */}
      <div className="flex flex-wrap gap-2 items-center">
        {selectedSignals.map((sigName, idx) => {
          const color = COLORS[idx % COLORS.length];
          let displayVal = 'N/A';

          if (channels && channels[sigName]) {
            const ch = channels[sigName];
            displayVal = ch.sampleCount > 0 ? `${ch.values[ch.sampleCount - 1].toFixed(2)} ${ch.units}` : '0';
          } else if (dataSet && dataSet.channels[sigName]) {
            const ch = dataSet.channels[sigName];
            displayVal = ch.sampleCount > 0 ? `${ch.values[ch.sampleCount - 1].toFixed(2)} ${ch.units}` : '0';
          } else if (signals[sigName]) {
            displayVal = signals[sigName].formattedValue;
          }

          return (
            <div
              key={sigName}
              className="flex items-center space-x-2 px-2.5 py-1 rounded bg-can-card border border-can-border text-xs"
            >
              <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: color }} />
              <span className="text-white font-medium">{sigName}:</span>
              <span className="text-sky-400 font-bold">{displayVal}</span>
              {onToggleSignal && (
                <button
                  onClick={() => onToggleSignal(sigName)}
                  className="text-slate-500 hover:text-slate-300 ml-1 text-xs"
                >
                  ×
                </button>
              )}
            </div>
          );
        })}
      </div>

      {/* Canvas Area with Shared Crosshair Tooltip */}
      <div className="relative w-full" style={{ height: `${height}px` }}>
        <canvas
          ref={canvasRef}
          onMouseMove={handleMouseMove}
          onMouseLeave={handleMouseLeave}
          className="w-full h-full block cursor-crosshair"
        />

        {/* Floating Crosshair Cursor Tooltip */}
        {cursorPos && cursorValues && (
          <div
            className="absolute pointer-events-none bg-slate-900/95 border border-can-border shadow-2xl rounded p-2.5 text-[11px] backdrop-blur z-20"
            style={{
              left: Math.min(cursorPos.x + 15, (canvasRef.current?.getBoundingClientRect().width || 600) - 180),
              top: Math.max(10, cursorPos.y - 40)
            }}
          >
            <div className="text-[10px] text-slate-400 font-sans border-b border-can-border/60 pb-1 mb-1.5 flex justify-between">
              <span>TIME CURSOR</span>
              <span className="font-mono text-white font-bold">{cursorValues.timestamp}</span>
            </div>
            <div className="space-y-1">
              {cursorValues.items.map(item => (
                <div key={item.name} className="flex items-center justify-between space-x-3">
                  <div className="flex items-center space-x-1.5">
                    <span className="w-2 h-2 rounded-full" style={{ backgroundColor: item.color }} />
                    <span className="text-slate-300 font-medium">{item.name}:</span>
                  </div>
                  <span className="font-bold text-white">
                    {item.value.toFixed(2)} <span className="text-[10px] text-slate-400 font-normal">{item.unit}</span>
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Panning Scrollbar (Visible only when zoomed) */}
      {zoomLevel > 1.0 && (
        <div className="flex items-center space-x-2 pt-1">
          <span className="text-[10px] text-slate-500 font-sans">PAN:</span>
          <input
            type="range"
            min="0"
            max="1"
            step="0.01"
            value={panOffset}
            onChange={e => setPanOffset(parseFloat(e.target.value))}
            className="w-full accent-sky-500 h-1.5 bg-slate-800 rounded appearance-none cursor-pointer"
          />
        </div>
      )}
    </div>
  );
};
