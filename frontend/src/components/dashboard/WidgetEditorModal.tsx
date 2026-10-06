import React, { useState } from 'react';
import { DashboardWidget, DashboardWidgetType, WidgetSignalSource } from '../../types/diagnosticsExtended';
import { X, Check, Sliders, Palette, Activity } from 'lucide-react';

interface WidgetEditorModalProps {
  widget: DashboardWidget | null;
  isOpen: boolean;
  onClose: () => void;
  onSave: (widget: DashboardWidget) => void;
}

export const WidgetEditorModal: React.FC<WidgetEditorModalProps> = ({ widget, isOpen, onClose, onSave }) => {
  if (!isOpen) return null;

  const [form, setForm] = useState<DashboardWidget>(() => {
    if (widget) return { ...widget };
    return {
      id: Math.random().toString(36).substring(2, 9),
      type: 'RadialGauge',
      title: 'New Sensor',
      subtitle: 'Telemetry Parameter',
      sourceType: 'CanSignal',
      sourceIdentifier: 'EngineSpeed',
      minValue: 0,
      maxValue: 8000,
      warnThreshold: 6500,
      dangerThreshold: 7200,
      unit: 'RPM',
      colorScheme: 'sky',
      decimalPlaces: 0,
      position: { x: 0, y: 0, w: 4, h: 3 }
    };
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(form);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-xs flex items-center justify-center p-4">
      <div className="bg-can-panel border border-can-border rounded-xl shadow-2xl max-w-lg w-full overflow-hidden text-xs font-sans">
        <div className="flex items-center justify-between px-5 py-3.5 border-b border-can-border bg-slate-900/60">
          <div className="flex items-center space-x-2 text-sky-400 font-bold">
            <Sliders className="w-4 h-4" />
            <span className="text-sm">{widget ? 'Configure Widget' : 'Add Dashboard Widget'}</span>
          </div>
          <button onClick={onClose} className="text-slate-400 hover:text-white p-1">
            <X className="w-4 h-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-5 space-y-4 max-h-[80vh] overflow-y-auto">
          {/* Widget Type */}
          <div>
            <label className="block text-[11px] font-bold text-slate-400 mb-1">WIDGET DISPLAY TYPE</label>
            <div className="grid grid-cols-3 gap-2">
              {(['RadialGauge', 'DigitalValue', 'MiniGraph', 'LevelBar', 'StatusAnnunciator'] as DashboardWidgetType[]).map(t => (
                <button
                  type="button"
                  key={t}
                  onClick={() => setForm(f => ({ ...f, type: t }))}
                  className={`px-2.5 py-2 rounded text-center font-bold border transition-all ${
                    form.type === t
                      ? 'bg-sky-600/30 border-sky-500 text-white'
                      : 'bg-can-bg border-can-border text-slate-400 hover:text-white'
                  }`}
                >
                  {t === 'RadialGauge' && 'Radial Gauge'}
                  {t === 'DigitalValue' && 'Digital Card'}
                  {t === 'MiniGraph' && 'Mini Sparkline'}
                  {t === 'LevelBar' && 'Linear Bar'}
                  {t === 'StatusAnnunciator' && 'Status Light'}
                </button>
              ))}
            </div>
          </div>

          {/* Title & Subtitle */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[11px] font-bold text-slate-400 mb-1">TITLE</label>
              <input
                type="text"
                value={form.title}
                onChange={e => setForm(f => ({ ...f, title: e.target.value }))}
                className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-white"
                required
              />
            </div>
            <div>
              <label className="block text-[11px] font-bold text-slate-400 mb-1">SUBTITLE</label>
              <input
                type="text"
                value={form.subtitle}
                onChange={e => setForm(f => ({ ...f, subtitle: e.target.value }))}
                className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-white"
              />
            </div>
          </div>

          {/* Data Source */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[11px] font-bold text-slate-400 mb-1">SOURCE TYPE</label>
              <select
                value={form.sourceType}
                onChange={e => setForm(f => ({ ...f, sourceType: e.target.value as WidgetSignalSource }))}
                className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-white"
              >
                <option value="CanSignal">DBC CAN Signal</option>
                <option value="ObdPid">OBD-II PID</option>
                <option value="UdsDid">UDS Data Identifier</option>
                <option value="VagMeasuringBlock">VAG Measuring Block</option>
                <option value="ManufacturerLiveData">Manufacturer Live Data</option>
                <option value="CalculatedSignal">Calculated Signal</option>
              </select>
            </div>
            <div>
              <label className="block text-[11px] font-bold text-slate-400 mb-1">IDENTIFIER / NAME</label>
              <input
                type="text"
                value={form.sourceIdentifier}
                onChange={e => setForm(f => ({ ...f, sourceIdentifier: e.target.value }))}
                className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-white font-mono"
                placeholder="e.g. EngineSpeed, PID_0C, 0xF40C"
                required
              />
            </div>
          </div>

          {/* Range & Limits */}
          <div className="grid grid-cols-4 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-slate-400 mb-1">MIN</label>
              <input
                type="number"
                step="any"
                value={form.minValue}
                onChange={e => setForm(f => ({ ...f, minValue: parseFloat(e.target.value) || 0 }))}
                className="w-full bg-can-bg border border-can-border rounded px-2 py-1 text-white font-mono text-center"
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-slate-400 mb-1">MAX</label>
              <input
                type="number"
                step="any"
                value={form.maxValue}
                onChange={e => setForm(f => ({ ...f, maxValue: parseFloat(e.target.value) || 100 }))}
                className="w-full bg-can-bg border border-can-border rounded px-2 py-1 text-white font-mono text-center"
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-slate-400 mb-1">WARN</label>
              <input
                type="number"
                step="any"
                value={form.warnThreshold ?? ''}
                onChange={e => setForm(f => ({ ...f, warnThreshold: e.target.value ? parseFloat(e.target.value) : undefined }))}
                className="w-full bg-can-bg border border-can-border rounded px-2 py-1 text-white font-mono text-center"
                placeholder="None"
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-slate-400 mb-1">DANGER</label>
              <input
                type="number"
                step="any"
                value={form.dangerThreshold ?? ''}
                onChange={e => setForm(f => ({ ...f, dangerThreshold: e.target.value ? parseFloat(e.target.value) : undefined }))}
                className="w-full bg-can-bg border border-can-border rounded px-2 py-1 text-white font-mono text-center"
                placeholder="None"
              />
            </div>
          </div>

          {/* Units & Decimals & Color */}
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="block text-[11px] font-bold text-slate-400 mb-1">UNITS</label>
              <input
                type="text"
                value={form.unit}
                onChange={e => setForm(f => ({ ...f, unit: e.target.value }))}
                className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-white text-center"
                placeholder="RPM, bar, °C, %"
              />
            </div>
            <div>
              <label className="block text-[11px] font-bold text-slate-400 mb-1">DECIMALS</label>
              <input
                type="number"
                min="0"
                max="4"
                value={form.decimalPlaces}
                onChange={e => setForm(f => ({ ...f, decimalPlaces: parseInt(e.target.value) || 0 }))}
                className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-white text-center"
              />
            </div>
            <div>
              <label className="block text-[11px] font-bold text-slate-400 mb-1">COLOR</label>
              <select
                value={form.colorScheme}
                onChange={e => setForm(f => ({ ...f, colorScheme: e.target.value as any }))}
                className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-white"
              >
                <option value="sky">Neon Sky</option>
                <option value="emerald">Emerald</option>
                <option value="amber">Amber</option>
                <option value="rose">Racing Red</option>
                <option value="purple">Purple Glow</option>
              </select>
            </div>
          </div>

          {/* Grid Width */}
          <div>
            <label className="block text-[11px] font-bold text-slate-400 mb-1">GRID COLUMN SPAN (OUT OF 12)</label>
            <div className="flex space-x-2">
              {[2, 3, 4, 6, 12].map(w => (
                <button
                  type="button"
                  key={w}
                  onClick={() => setForm(f => ({ ...f, position: { ...f.position, w } }))}
                  className={`flex-1 py-1.5 rounded font-mono font-bold border text-center ${
                    form.position.w === w
                      ? 'bg-sky-600 border-sky-400 text-white'
                      : 'bg-can-bg border-can-border text-slate-400'
                  }`}
                >
                  {w} cols
                </button>
              ))}
            </div>
          </div>

          {/* Buttons */}
          <div className="flex justify-end space-x-3 pt-3 border-t border-can-border">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded font-semibold"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded font-bold flex items-center space-x-1.5 shadow"
            >
              <Check className="w-4 h-4" />
              <span>Save Widget</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
