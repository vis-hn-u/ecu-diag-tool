import React, { useState, useEffect } from 'react';
import { api } from '../services/api';
import { 
  CalibrationProject, 
  CalibrationTable, 
  CalibrationDiffCell, 
  CalibrationEditRecord 
} from '../types/engine';
import { 
  Sliders, 
  Grid, 
  Undo2, 
  Redo2, 
  RotateCcw, 
  GitCompare, 
  Save, 
  FileText, 
  Plus, 
  Minus, 
  Percent, 
  HelpCircle,
  Activity,
  Layers,
  Sparkles
} from 'lucide-react';

export const CalibrationPage: React.FC = () => {
  const [project, setProject] = useState<CalibrationProject | null>(null);
  const [selectedTableId, setSelectedTableId] = useState<string>('spark_timing_main');
  const [table, setTable] = useState<CalibrationTable | null>(null);
  const [diffs, setDiffs] = useState<CalibrationDiffCell[]>([]);
  const [history, setHistory] = useState<CalibrationEditRecord[]>([]);
  const [isDiffMode, setIsDiffMode] = useState<boolean>(false);

  // Selected cell coordinates
  const [selectedCell, setSelectedCell] = useState<{ x: number; y: number } | null>(null);
  const [editVal, setEditVal] = useState<string>('');
  const [bulkOffset, setBulkOffset] = useState<string>('1.0');
  const [bulkScale, setBulkScale] = useState<string>('5.0');

  const fetchProjectAndTable = async (tableId?: string) => {
    try {
      const proj = await api.getCalibrationProject();
      setProject(proj);
      const tId = tableId || selectedTableId || (proj.tables.length > 0 ? proj.tables[0].id : '');
      if (tId) {
        const tbl = await api.getCalibrationTable(tId);
        setTable(tbl);
        const d = await api.getCalibrationDiff(tId);
        setDiffs(d);
        const h = await api.getCalibrationHistory(tId);
        setHistory(h);
      }
    } catch {
      // Ignore
    }
  };

  useEffect(() => {
    fetchProjectAndTable();
  }, [selectedTableId]);

  const handleSelectTable = (tId: string) => {
    setSelectedTableId(tId);
    setSelectedCell(null);
    fetchProjectAndTable(tId);
  };

  const handleCellClick = (x: number, y: number) => {
    setSelectedCell({ x, y });
    if (table && table.data[y]) {
      setEditVal(table.data[y][x].toString());
    }
  };

  const handleUpdateCell = async () => {
    if (!selectedCell || !table) return;
    const num = parseFloat(editVal);
    if (isNaN(num)) return;

    await api.updateCalibrationCell(table.id, selectedCell.x, selectedCell.y, num);
    fetchProjectAndTable(table.id);
  };

  const handleBulkOffset = async (val: number) => {
    if (!table) return;
    await api.bulkOffsetCalibration(table.id, val);
    fetchProjectAndTable(table.id);
  };

  const handleBulkScale = async (val: number) => {
    if (!table) return;
    await api.bulkScaleCalibration(table.id, val);
    fetchProjectAndTable(table.id);
  };

  const handleUndo = async () => {
    if (!table) return;
    await api.undoCalibration(table.id);
    fetchProjectAndTable(table.id);
  };

  const handleRedo = async () => {
    if (!table) return;
    await api.redoCalibration(table.id);
    fetchProjectAndTable(table.id);
  };

  const handleResetTable = async () => {
    if (!table) return;
    await api.resetCalibrationTable(table.id);
    fetchProjectAndTable(table.id);
  };

  const getHeatmapColor = (val: number, min: number, max: number) => {
    const fraction = Math.min(1, Math.max(0, (val - min) / (max - min || 1)));
    if (fraction < 0.25) return 'bg-sky-950/80 text-sky-200';
    if (fraction < 0.5) return 'bg-sky-800/80 text-sky-100';
    if (fraction < 0.75) return 'bg-amber-800/80 text-amber-100 font-bold';
    return 'bg-rose-800/80 text-rose-100 font-bold';
  };

  return (
    <div className="flex flex-col h-full font-mono select-none overflow-hidden p-4 space-y-4">
      {/* Calibration Header Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-3 flex items-center justify-between shadow-md">
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2 text-white font-bold text-sm font-sans">
            <Sliders className="w-5 h-5 text-sky-400" />
            <span>ECU CALIBRATION STUDIO</span>
          </div>

          <div className="h-4 w-px bg-can-border mx-1" />

          {/* Project & Vehicle Info */}
          <span className="text-xs text-slate-400 font-sans">
            Target: <span className="text-white font-bold">{project?.vehicleModel || '2.0L Turbo'}</span> ({project?.ecuType || 'Bosch CAN'})
          </span>
        </div>

        {/* Undo / Redo & Diff Toggle */}
        <div className="flex items-center space-x-2">
          <button
            onClick={() => setIsDiffMode(!isDiffMode)}
            className={`px-3 py-1.5 rounded text-xs font-sans font-bold flex items-center space-x-1.5 transition-all ${
              isDiffMode
                ? 'bg-purple-600 text-white shadow'
                : 'bg-can-card text-slate-300 hover:text-white border border-can-border'
            }`}
          >
            <GitCompare className="w-3.5 h-3.5" />
            <span>{isDiffMode ? 'Diff Mode Active' : 'Show Diff vs Stock'}</span>
          </button>

          <button
            onClick={handleUndo}
            className="p-1.5 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border"
            title="Undo"
          >
            <Undo2 className="w-4 h-4" />
          </button>

          <button
            onClick={handleRedo}
            className="p-1.5 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border"
            title="Redo"
          >
            <Redo2 className="w-4 h-4" />
          </button>

          <button
            onClick={handleResetTable}
            className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded text-xs border border-can-border flex items-center space-x-1"
            title="Revert Table to Stock"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Revert Stock</span>
          </button>
        </div>
      </div>

      {/* Main Workspace Grid (Left Maps List, Center Grid Table, Right Inspector) */}
      <div className="flex-1 flex gap-4 overflow-hidden">
        {/* Map Selector Navigation */}
        <div className="w-60 bg-can-panel border border-can-border rounded-lg p-3 flex flex-col space-y-2 overflow-y-auto shadow-xl">
          <span className="text-[10px] text-slate-500 font-bold uppercase tracking-wider px-2">CALIBRATION MAPS</span>
          {project?.tables.map(tbl => (
            <button
              key={tbl.id}
              onClick={() => handleSelectTable(tbl.id)}
              className={`text-left px-3 py-2 rounded text-xs transition-all flex flex-col space-y-0.5 ${
                selectedTableId === tbl.id
                  ? 'bg-sky-600/20 text-sky-400 border border-sky-500/30 font-bold shadow-sm'
                  : 'text-slate-400 hover:text-white hover:bg-can-card border border-transparent'
              }`}
            >
              <span className="font-sans">{tbl.name}</span>
              <span className="text-[10px] font-mono text-slate-500">{tbl.unit}</span>
            </button>
          ))}
        </div>

        {/* Center Calibration 2D/3D Heatmap Matrix Table */}
        <div className="flex-1 bg-can-panel border border-can-border rounded-lg p-4 flex flex-col overflow-hidden shadow-xl">
          <div className="flex justify-between items-center border-b border-can-border pb-2.5 mb-3">
            <div>
              <h2 className="text-sm font-bold text-white font-sans">{table?.name}</h2>
              <p className="text-[11px] text-slate-400 font-sans">{table?.description}</p>
            </div>
            <span className="text-xs text-sky-400 font-mono font-bold">
              Range: {table?.minValue} - {table?.maxValue} {table?.unit}
            </span>
          </div>

          {/* Matrix Grid Canvas */}
          <div className="flex-1 overflow-auto border border-can-border rounded bg-[#0A0E17]">
            <table className="w-full text-center text-xs border-collapse font-mono">
              <thead>
                <tr className="bg-[#0B0F17] text-slate-400 border-b border-can-border">
                  <th className="p-2 border-r border-can-border text-[10px] uppercase">{table?.yAxis?.name || 'Y'} \ {table?.xAxis.name}</th>
                  {table?.xAxis.breakpoints.map((bp, xIdx) => (
                    <th key={xIdx} className="p-2 border-r border-can-border text-[11px] text-sky-300 font-bold">
                      {bp}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {table?.data.map((row, yIdx) => {
                  const yBp = table.yAxis?.breakpoints[yIdx] ?? yIdx;
                  return (
                    <tr key={yIdx} className="border-b border-can-border/60">
                      <td className="p-2 bg-[#0B0F17] text-amber-300 font-bold text-[11px] border-r border-can-border">
                        {yBp}
                      </td>
                      {row.map((val, xIdx) => {
                        const isSelected = selectedCell?.x === xIdx && selectedCell?.y === yIdx;
                        const diff = diffs.find(d => d.xIndex === xIdx && d.yIndex === yIdx);
                        const isModified = diff && diff.state !== 'UNCHANGED';

                        return (
                          <td
                            key={xIdx}
                            onClick={() => handleCellClick(xIdx, yIdx)}
                            className={`p-2 border-r border-can-border/40 cursor-pointer transition-all ${
                              isSelected
                                ? 'ring-2 ring-sky-400 bg-sky-500/30 text-white font-bold'
                                : isDiffMode && isModified
                                ? (diff?.state === 'INCREASED' ? 'bg-emerald-900/60 text-emerald-200 font-bold' : 'bg-rose-900/60 text-rose-200 font-bold')
                                : getHeatmapColor(val, table.minValue, table.maxValue)
                            }`}
                          >
                            {isDiffMode && diff ? `${diff.absoluteDiff > 0 ? '+' : ''}${diff.absoluteDiff}` : val.toFixed(1)}
                          </td>
                        );
                      })}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>

        {/* Right Editor & Inspector Panel */}
        <div className="w-80 bg-can-panel border border-can-border rounded-lg p-4 flex flex-col justify-between overflow-y-auto shadow-xl space-y-4">
          <div>
            <div className="flex items-center space-x-2 border-b border-can-border pb-2.5 mb-3 text-white font-bold text-xs font-sans">
              <Sliders className="w-4 h-4 text-sky-400" />
              <span>CELL INSPECTOR & EDIT TOOLS</span>
            </div>

            {selectedCell && table ? (
              <div className="space-y-4 text-xs">
                <div className="bg-can-bg p-3 rounded border border-can-border space-y-1.5">
                  <div className="flex justify-between">
                    <span className="text-slate-400">X ({table.xAxis.name}):</span>
                    <span className="text-white font-bold">{table.xAxis.breakpoints[selectedCell.x]} {table.xAxis.unit}</span>
                  </div>
                  {table.yAxis && (
                    <div className="flex justify-between">
                      <span className="text-slate-400">Y ({table.yAxis.name}):</span>
                      <span className="text-white font-bold">{table.yAxis.breakpoints[selectedCell.y]} {table.yAxis.unit}</span>
                    </div>
                  )}
                  <div className="flex justify-between">
                    <span className="text-slate-400">Original Stock:</span>
                    <span className="text-slate-300">{table.originalData[selectedCell.y][selectedCell.x]} {table.unit}</span>
                  </div>
                </div>

                {/* Direct Cell Edit */}
                <div className="space-y-2">
                  <span className="text-slate-400 font-sans font-semibold">Direct Cell Value:</span>
                  <div className="flex space-x-2">
                    <input
                      type="number"
                      step="0.1"
                      value={editVal}
                      onChange={e => setEditVal(e.target.value)}
                      className="flex-1 bg-can-bg border border-can-border rounded px-3 py-1.5 text-xs text-white font-mono focus:border-sky-500 focus:outline-none"
                    />
                    <button
                      onClick={handleUpdateCell}
                      className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold font-sans"
                    >
                      Set
                    </button>
                  </div>
                </div>

                {/* Bulk Offset & Scale */}
                <div className="pt-2 border-t border-can-border space-y-3">
                  <span className="text-[11px] text-slate-400 font-bold uppercase block">Bulk Map Operations:</span>
                  
                  {/* Bulk Offset */}
                  <div className="flex items-center space-x-2">
                    <button
                      onClick={() => handleBulkOffset(1.0)}
                      className="flex-1 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded border border-can-border font-bold text-xs"
                    >
                      +1.0
                    </button>
                    <button
                      onClick={() => handleBulkOffset(-1.0)}
                      className="flex-1 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded border border-can-border font-bold text-xs"
                    >
                      -1.0
                    </button>
                  </div>

                  {/* Bulk Scale */}
                  <div className="flex items-center space-x-2">
                    <button
                      onClick={() => handleBulkScale(5.0)}
                      className="flex-1 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded border border-can-border font-bold text-xs"
                    >
                      +5 %
                    </button>
                    <button
                      onClick={() => handleBulkScale(-5.0)}
                      className="flex-1 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded border border-can-border font-bold text-xs"
                    >
                      -5 %
                    </button>
                  </div>
                </div>
              </div>
            ) : (
              <div className="text-center py-12 text-slate-500 text-xs">
                Click any cell in the calibration table to edit values, inspect stock deltas, or execute bulk offset adjustments.
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
