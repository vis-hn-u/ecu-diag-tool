import React from 'react';
import { DashboardWidget } from '../../types/diagnosticsExtended';

interface RadialGaugeWidgetProps {
  widget: DashboardWidget;
  currentValue: number | null;
  quality?: 'VALID' | 'STALE' | 'INVALID' | 'UNAVAILABLE';
  source?: string;
  ageMs?: number;
}

export const RadialGaugeWidget: React.FC<RadialGaugeWidgetProps> = ({ 
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
  const clampedVal = Math.min(max, Math.max(min, safeVal));
  const range = max - min || 1;
  const fraction = isAvailable ? (clampedVal - min) / range : 0;

  // 240 degree sweep: -120 deg (min) to +120 deg (max)
  const startAngle = -120;
  const endAngle = 120;
  const currentAngle = startAngle + fraction * (endAngle - startAngle);

  // SVG arc calculation (radius = 70, center = 90,90)
  const polarToCartesian = (cx: number, cy: number, r: number, angleInDegrees: number) => {
    const angleInRadians = ((angleInDegrees - 90) * Math.PI) / 180.0;
    return {
      x: cx + r * Math.cos(angleInRadians),
      y: cy + r * Math.sin(angleInRadians)
    };
  };

  const describeArc = (x: number, y: number, radius: number, startA: number, endA: number) => {
    const start = polarToCartesian(x, y, radius, endA);
    const end = polarToCartesian(x, y, radius, startA);
    const largeArcFlag = endA - startA <= 180 ? '0' : '1';
    return ['M', start.x, start.y, 'A', radius, radius, 0, largeArcFlag, 0, end.x, end.y].join(' ');
  };

  const bgArc = describeArc(90, 90, 68, startAngle, endAngle);
  const valueArc = describeArc(90, 90, 68, startAngle, currentAngle);

  // Status color
  let strokeColor = '#38bdf8'; // sky
  if (widget.colorScheme === 'emerald') strokeColor = '#34d399';
  if (widget.colorScheme === 'amber') strokeColor = '#fbbf24';
  if (widget.colorScheme === 'rose') strokeColor = '#f43f5e';
  if (widget.colorScheme === 'purple') strokeColor = '#c084fc';

  if (!isAvailable) {
    strokeColor = '#475569';
  } else if (isStale) {
    strokeColor = '#d97706';
  } else {
    const isWarn = widget.warnThreshold && currentValue >= widget.warnThreshold;
    const isDanger = widget.dangerThreshold && currentValue >= widget.dangerThreshold;
    if (isDanger) strokeColor = '#ef4444';
    else if (isWarn) strokeColor = '#f59e0b';
  }

  return (
    <div className="flex flex-col items-center justify-between h-full w-full p-2 select-none">
      <div className="text-center w-full">
        <div className="flex items-center justify-center space-x-1">
          <span className="text-[11px] font-bold tracking-wider text-slate-300 truncate max-w-[140px]">{widget.title}</span>
          {source && source !== 'NONE' && (
            <span className="text-[8px] px-1 py-0.2 bg-slate-800 text-slate-400 rounded border border-slate-700 font-mono">
              {source}
            </span>
          )}
        </div>
        <div className="flex items-center justify-center space-x-1.5 mt-0.5">
          {widget.subtitle && <span className="text-[9px] text-slate-500 truncate">{widget.subtitle}</span>}
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

      <div className="relative w-44 h-36 flex items-center justify-center">
        <svg viewBox="0 0 180 150" className="w-full h-full overflow-visible">
          {/* Background Track Arc */}
          <path
            d={bgArc}
            fill="none"
            stroke="#1e293b"
            strokeWidth="10"
            strokeLinecap="round"
          />

          {/* Active Value Arc */}
          {fraction > 0.005 && isAvailable && (
            <path
              d={valueArc}
              fill="none"
              stroke={strokeColor}
              strokeWidth="10"
              strokeLinecap="round"
              className="transition-all duration-150 ease-out"
              style={{ filter: `drop-shadow(0 0 6px ${strokeColor}66)` }}
            />
          )}

          {/* Needle Pin */}
          <g transform={`rotate(${currentAngle}, 90, 90)`} className="transition-transform duration-150 ease-out">
            <line x1="90" y1="90" x2="90" y2="28" stroke={isAvailable ? "#ffffff" : "#64748b"} strokeWidth="2.5" strokeLinecap="round" />
            <circle cx="90" cy="28" r="3" fill={strokeColor} />
          </g>
          <circle cx="90" cy="90" r="7" fill="#0f172a" stroke="#64748b" strokeWidth="2" />
        </svg>

        {/* Digital Readout In Center */}
        <div className="absolute bottom-1 text-center">
          <div className={`text-xl font-bold font-mono tracking-tight leading-none ${isAvailable ? (isStale ? 'text-amber-200/80' : 'text-white') : 'text-slate-500'}`}>
            {isAvailable ? currentValue.toFixed(widget.decimalPlaces) : '--'}
          </div>
          <div className="text-[10px] font-bold text-slate-400 mt-0.5">{widget.unit}</div>
        </div>
      </div>

      <div className="w-full flex justify-between text-[9px] text-slate-500 font-mono px-2">
        <span>{min}</span>
        <span>{max}</span>
      </div>
    </div>
  );
};
