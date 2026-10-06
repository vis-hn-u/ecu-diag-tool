import React from 'react';
import { DashboardWidget } from '../../types/diagnosticsExtended';

interface RadialGaugeWidgetProps {
  widget: DashboardWidget;
  currentValue: number;
}

export const RadialGaugeWidget: React.FC<RadialGaugeWidgetProps> = ({ widget, currentValue }) => {
  const min = widget.minValue;
  const max = widget.maxValue;
  const clampedVal = Math.min(max, Math.max(min, currentValue));
  const range = max - min || 1;
  const fraction = (clampedVal - min) / range;

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

  const isWarn = widget.warnThreshold && currentValue >= widget.warnThreshold;
  const isDanger = widget.dangerThreshold && currentValue >= widget.dangerThreshold;
  if (isDanger) strokeColor = '#ef4444';
  else if (isWarn) strokeColor = '#f59e0b';

  return (
    <div className="flex flex-col items-center justify-between h-full w-full p-2 select-none">
      <div className="text-center">
        <div className="text-[11px] font-bold tracking-wider text-slate-300 truncate max-w-[170px]">{widget.title}</div>
        {widget.subtitle && <div className="text-[9px] text-slate-500 truncate">{widget.subtitle}</div>}
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
          {fraction > 0.005 && (
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
            <line x1="90" y1="90" x2="90" y2="28" stroke="#ffffff" strokeWidth="2.5" strokeLinecap="round" />
            <circle cx="90" cy="28" r="3" fill={strokeColor} />
          </g>
          <circle cx="90" cy="90" r="7" fill="#0f172a" stroke="#64748b" strokeWidth="2" />
        </svg>

        {/* Digital Readout In Center */}
        <div className="absolute bottom-1 text-center">
          <div className="text-xl font-bold font-mono text-white tracking-tight leading-none">
            {currentValue.toFixed(widget.decimalPlaces)}
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
