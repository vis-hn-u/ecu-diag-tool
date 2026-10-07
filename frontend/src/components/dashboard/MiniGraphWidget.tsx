import React, { useState, useEffect } from 'react';
import { DashboardWidget } from '../../types/diagnosticsExtended';

interface MiniGraphWidgetProps {
  widget: DashboardWidget;
  currentValue: number | null;
  quality?: 'VALID' | 'STALE' | 'INVALID' | 'UNAVAILABLE';
  source?: string;
  ageMs?: number;
}

export const MiniGraphWidget: React.FC<MiniGraphWidgetProps> = ({ 
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

  const [history, setHistory] = useState<number[]>(() => Array(25).fill(safeVal));

  useEffect(() => {
    if (isAvailable) {
      setHistory(prev => {
        const next = [...prev.slice(1), currentValue!];
        return next;
      });
    }
  }, [currentValue, isAvailable]);

  const range = max - min || 1;

  // SVG coordinates: width 180, height 70
  const width = 180;
  const height = 70;
  const step = width / (history.length - 1);

  const points = history.map((val, i) => {
    const clamped = Math.min(max, Math.max(min, val));
    const normalizedY = 1 - (clamped - min) / range;
    const y = 5 + normalizedY * (height - 10);
    return `${i * step},${y}`;
  }).join(' ');

  const areaPoints = `0,${height} ${points} ${width},${height}`;

  let strokeColor = '#38bdf8'; // sky
  if (widget.colorScheme === 'emerald') strokeColor = '#34d399';
  if (widget.colorScheme === 'amber') strokeColor = '#fbbf24';
  if (widget.colorScheme === 'rose') strokeColor = '#f43f5e';
  if (widget.colorScheme === 'purple') strokeColor = '#c084fc';

  if (!isAvailable) {
    strokeColor = '#64748b';
  } else if (isStale) {
    strokeColor = '#d97706';
  }

  return (
    <div className="flex flex-col justify-between h-full w-full p-2.5 select-none font-sans">
      <div className="flex justify-between items-baseline">
        <div className="flex items-center space-x-1.5 truncate max-w-[140px]">
          <span className="text-xs font-bold text-slate-300 truncate">{widget.title}</span>
          {source && source !== 'NONE' && (
            <span className="text-[8px] px-1 py-0.2 bg-slate-800 text-slate-400 rounded border border-slate-700 font-mono">
              {source}
            </span>
          )}
        </div>
        <div className="text-xs font-mono font-bold">
          <span className={isAvailable ? (isStale ? 'text-amber-200/80' : 'text-white') : 'text-slate-500'}>
            {isAvailable ? currentValue.toFixed(widget.decimalPlaces) : '--'}
          </span>
          <span className="text-[10px] text-slate-400 font-sans ml-1">{widget.unit}</span>
        </div>
      </div>

      <div className="w-full h-18 my-1 relative">
        <svg viewBox={`0 0 ${width} ${height}`} className="w-full h-full overflow-hidden">
          <defs>
            <linearGradient id={`grad_${widget.id}`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={strokeColor} stopOpacity="0.35" />
              <stop offset="100%" stopColor={strokeColor} stopOpacity="0.0" />
            </linearGradient>
          </defs>
          <polygon points={areaPoints} fill={`url(#grad_${widget.id})`} />
          <polyline
            points={points}
            fill="none"
            stroke={strokeColor}
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </div>

      <div className="flex justify-between text-[9px] font-mono text-slate-500">
        <span>MIN {min}</span>
        <span className="text-slate-400">
          {!isAvailable ? 'NO DATA' : isStale ? `STALE (${(ageMs / 1000).toFixed(1)}s)` : 'TREND (25s)'}
        </span>
        <span>MAX {max}</span>
      </div>
    </div>
  );
};
