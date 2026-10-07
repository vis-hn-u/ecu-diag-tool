import React from 'react';
import { DashboardWidget } from '../../types/diagnosticsExtended';

interface LevelBarWidgetProps {
  widget: DashboardWidget;
  currentValue: number | null;
  quality?: 'VALID' | 'STALE' | 'INVALID' | 'UNAVAILABLE';
  source?: string;
  ageMs?: number;
}

export const LevelBarWidget: React.FC<LevelBarWidgetProps> = ({ 
  widget, 
  currentValue,
  quality = 'VALID',
  source,
  ageMs = 0
}) => {
  const isAvailable = currentValue !== null && !isNaN(currentValue) && quality !== 'UNAVAILABLE';
  const isStale = quality === 'STALE';
  const min = widget.minValue;
  const max = widget.maxValue;
  const safeVal = isAvailable ? currentValue : min;
  const clamped = Math.min(max, Math.max(min, safeVal));
  const range = max - min || 1;
  const pct = isAvailable ? Math.max(0, Math.min(100, ((clamped - min) / range) * 100)) : 0;

  const isWarn = isAvailable && widget.warnThreshold !== undefined && currentValue >= widget.warnThreshold;
  const isDanger = isAvailable && widget.dangerThreshold !== undefined && currentValue >= widget.dangerThreshold;

  let barBg = 'bg-sky-500';
  if (widget.colorScheme === 'emerald') barBg = 'bg-emerald-500';
  if (widget.colorScheme === 'amber') barBg = 'bg-amber-500';
  if (widget.colorScheme === 'rose') barBg = 'bg-rose-500';
  if (widget.colorScheme === 'purple') barBg = 'bg-purple-500';

  if (!isAvailable) {
    barBg = 'bg-slate-700';
  } else if (isStale) {
    barBg = 'bg-amber-500/70';
  } else if (isDanger) {
    barBg = 'bg-rose-600 animate-pulse';
  } else if (isWarn) {
    barBg = 'bg-amber-500';
  }

  return (
    <div className="flex flex-col justify-between h-full w-full p-3 select-none font-sans">
      <div className="flex justify-between items-baseline">
        <div>
          <div className="text-xs font-bold text-slate-300 truncate max-w-[150px] flex items-center space-x-1.5">
            <span>{widget.title}</span>
            {source && source !== 'NONE' && (
              <span className="text-[8px] px-1 py-0.2 bg-slate-800 text-slate-400 rounded border border-slate-700 font-mono">
                {source}
              </span>
            )}
          </div>
          <div className="flex items-center space-x-1 mt-0.5">
            {widget.subtitle && <span className="text-[10px] text-slate-500">{widget.subtitle}</span>}
            {isStale && (
              <span className="text-[8px] font-bold px-1 py-0.2 bg-amber-950/80 border border-amber-600 text-amber-300 rounded uppercase">
                STALE ({(ageMs / 1000).toFixed(1)}s)
              </span>
            )}
            {!isAvailable && (
              <span className="text-[8px] font-bold px-1 py-0.2 bg-slate-900 border border-slate-700 text-slate-400 rounded uppercase">
                NO DATA
              </span>
            )}
          </div>
        </div>
        <div className="text-right">
          <span className={`text-lg font-bold font-mono ${isAvailable ? (isStale ? 'text-amber-200/80' : 'text-white') : 'text-slate-500'}`}>
            {isAvailable ? currentValue.toFixed(widget.decimalPlaces) : '--'}
          </span>
          <span className="text-[10px] text-slate-400 ml-1">{widget.unit}</span>
        </div>
      </div>

      {/* Main Bar Track */}
      <div className="my-2">
        <div className="w-full h-5 bg-slate-900 border border-slate-700/80 rounded-sm overflow-hidden p-0.5 relative">
          {/* Threshold marker lines if configured */}
          {widget.warnThreshold && (
            <div
              className="absolute top-0 bottom-0 w-0.5 bg-amber-400 z-10 opacity-75"
              style={{ left: `${Math.min(100, Math.max(0, ((widget.warnThreshold - min) / range) * 100))}%` }}
              title={`Warning: ${widget.warnThreshold}`}
            />
          )}
          {widget.dangerThreshold && (
            <div
              className="absolute top-0 bottom-0 w-0.5 bg-rose-500 z-10 opacity-90"
              style={{ left: `${Math.min(100, Math.max(0, ((widget.dangerThreshold - min) / range) * 100))}%` }}
              title={`Danger: ${widget.dangerThreshold}`}
            />
          )}

          {/* Filled Bar */}
          <div
            className={`h-full rounded-xs transition-all duration-150 ease-out shadow-sm ${barBg}`}
            style={{ width: `${pct}%` }}
          />
        </div>
      </div>

      {/* Scale indicators */}
      <div className="flex justify-between text-[9px] font-mono text-slate-500">
        <span>{min} {widget.unit}</span>
        <span className="text-slate-400">{isAvailable ? `${Math.round(pct)}%` : '--'}</span>
        <span>{max} {widget.unit}</span>
      </div>
    </div>
  );
};
