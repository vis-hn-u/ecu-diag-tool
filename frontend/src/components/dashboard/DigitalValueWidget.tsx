import React from 'react';
import { DashboardWidget } from '../../types/diagnosticsExtended';

interface DigitalValueWidgetProps {
  widget: DashboardWidget;
  currentValue: number | null;
  peakValue?: number;
  quality?: 'VALID' | 'STALE' | 'INVALID' | 'UNAVAILABLE';
  source?: string;
  ageMs?: number;
}

export const DigitalValueWidget: React.FC<DigitalValueWidgetProps> = ({ 
  widget, 
  currentValue, 
  peakValue,
  quality = 'VALID',
  source,
  ageMs = 0
}) => {
  const isAvailable = currentValue !== null && !isNaN(currentValue) && quality !== 'UNAVAILABLE';
  const isStale = quality === 'STALE';
  const isWarn = isAvailable && widget.warnThreshold !== undefined && currentValue >= widget.warnThreshold;
  const isDanger = isAvailable && widget.dangerThreshold !== undefined && currentValue >= widget.dangerThreshold;

  let badgeColor = 'text-sky-400 bg-sky-950/60 border-sky-800/60';
  let valColor = 'text-white';
  if (widget.colorScheme === 'emerald') badgeColor = 'text-emerald-400 bg-emerald-950/60 border-emerald-800/60';
  if (widget.colorScheme === 'amber') badgeColor = 'text-amber-400 bg-amber-950/60 border-amber-800/60';
  if (widget.colorScheme === 'rose') badgeColor = 'text-rose-400 bg-rose-950/60 border-rose-800/60';
  if (widget.colorScheme === 'purple') badgeColor = 'text-purple-400 bg-purple-950/60 border-purple-800/60';

  if (!isAvailable) {
    badgeColor = 'text-slate-400 bg-slate-900 border-slate-700';
    valColor = 'text-slate-500';
  } else if (isStale) {
    badgeColor = 'text-amber-300 bg-amber-950/80 border-amber-600';
    valColor = 'text-amber-200/80';
  } else if (isDanger) {
    valColor = 'text-rose-400';
    badgeColor = 'text-rose-400 bg-rose-950/80 border-rose-600 animate-pulse';
  } else if (isWarn) {
    valColor = 'text-amber-300';
    badgeColor = 'text-amber-300 bg-amber-950/80 border-amber-600';
  }

  const statusText = !isAvailable 
    ? 'NO DATA' 
    : isStale 
      ? `STALE (${(ageMs / 1000).toFixed(1)}s)` 
      : isDanger 
        ? 'ALERT' 
        : isWarn 
          ? 'WARN' 
          : 'NOMINAL';

  return (
    <div className="flex flex-col justify-between h-full w-full p-3.5 select-none font-sans">
      <div className="flex justify-between items-start">
        <div>
          <div className="text-xs font-bold text-slate-300 tracking-wide flex items-center space-x-1.5">
            <span>{widget.title}</span>
            {source && source !== 'NONE' && (
              <span className="text-[9px] px-1 py-0.2 bg-slate-800 text-slate-400 rounded border border-slate-700 font-mono">
                {source}
              </span>
            )}
          </div>
          {widget.subtitle && <div className="text-[10px] text-slate-500 mt-0.5">{widget.subtitle}</div>}
        </div>
        <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded border uppercase tracking-wider ${badgeColor}`}>
          {statusText}
        </span>
      </div>

      <div className="my-auto py-1 flex items-baseline justify-center space-x-1.5 font-mono">
        <span className={`text-4xl font-black tracking-tight ${valColor}`}>
          {isAvailable ? currentValue.toFixed(widget.decimalPlaces) : '--'}
        </span>
        <span className="text-xs font-bold text-slate-400 font-sans">{widget.unit}</span>
      </div>

      <div className="flex justify-between items-center text-[10px] font-mono text-slate-400 pt-1 border-t border-slate-800/80">
        <span>MIN: {widget.minValue}</span>
        {peakValue !== undefined && isAvailable && (
          <span>PEAK: <strong className="text-sky-300">{peakValue.toFixed(widget.decimalPlaces)}</strong></span>
        )}
        <span>MAX: {widget.maxValue}</span>
      </div>
    </div>
  );
};
