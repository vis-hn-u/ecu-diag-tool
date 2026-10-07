import React from 'react';
import { DashboardWidget } from '../../types/diagnosticsExtended';
import { AlertTriangle, CheckCircle2, ShieldAlert, Zap, Flame, Thermometer, BatteryCharging, Power } from 'lucide-react';

interface StatusAnnunciatorWidgetProps {
  widget: DashboardWidget;
  currentValue: number | null;
  quality?: 'VALID' | 'STALE' | 'INVALID' | 'UNAVAILABLE';
  source?: string;
  ageMs?: number;
}

export const StatusAnnunciatorWidget: React.FC<StatusAnnunciatorWidgetProps> = ({ 
  widget, 
  currentValue,
  quality = 'VALID',
  source,
  ageMs = 0
}) => {
  const isAvailable = currentValue !== null && !isNaN(currentValue) && quality !== 'UNAVAILABLE';
  const isStale = quality === 'STALE';
  const isActive = isAvailable && currentValue > 0;

  // Icon mapping
  const getIcon = () => {
    const title = widget.title.toLowerCase();
    if (title.includes('mil') || title.includes('check') || title.includes('engine')) return AlertTriangle;
    if (title.includes('battery') || title.includes('voltage') || title.includes('charge')) return BatteryCharging;
    if (title.includes('temp') || title.includes('heat')) return Thermometer;
    if (title.includes('boost') || title.includes('knock')) return Flame;
    if (title.includes('power') || title.includes('relay')) return Zap;
    return ShieldAlert;
  };

  const IconComponent = getIcon();

  let activeBg = 'bg-amber-500/20 border-amber-500 text-amber-400 shadow-[0_0_15px_rgba(245,158,11,0.35)]';
  if (widget.colorScheme === 'rose') {
    activeBg = 'bg-rose-500/20 border-rose-500 text-rose-400 shadow-[0_0_15px_rgba(244,63,94,0.4)] animate-pulse';
  } else if (widget.colorScheme === 'emerald') {
    activeBg = 'bg-emerald-500/20 border-emerald-500 text-emerald-400 shadow-[0_0_15px_rgba(52,211,153,0.35)]';
  }

  const inactiveBg = !isAvailable 
    ? 'bg-slate-950/80 border-slate-800/60 text-slate-700' 
    : isStale 
      ? 'bg-amber-950/20 border-amber-800/40 text-amber-500/60'
      : 'bg-slate-900/60 border-slate-800 text-slate-600';

  const statusLabel = !isAvailable
    ? 'NO DATA / OFFLINE'
    : isStale
      ? `STALE (${(ageMs / 1000).toFixed(1)}s)`
      : isActive
        ? 'ACTIVE / FAULT'
        : 'NORMAL / OFF';

  return (
    <div className={`flex flex-col items-center justify-center h-full w-full p-3 rounded-lg border transition-all duration-200 select-none ${isActive ? activeBg : inactiveBg}`}>
      <div className="p-2 rounded-full mb-1">
        <IconComponent className={`w-8 h-8 ${isActive ? 'scale-110' : isAvailable ? 'opacity-40' : 'opacity-20'} transition-transform`} />
      </div>
      <div className="text-xs font-bold text-center uppercase tracking-wider line-clamp-1">{widget.title}</div>
      <div className="text-[10px] font-mono mt-1 px-2 py-0.5 rounded font-bold uppercase">
        {statusLabel}
      </div>
    </div>
  );
};
