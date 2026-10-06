import React, { useState, useEffect } from 'react';
import { api, downloadBlobAsFile } from '../services/api';
import { VehicleProfile } from '../types';
import { 
  FileText, 
  Download, 
  Eye, 
  ExternalLink, 
  CheckCircle2, 
  AlertTriangle, 
  Activity, 
  ShieldCheck, 
  Cpu, 
  Layers, 
  Gauge, 
  RefreshCw,
  Printer,
  Sparkles,
  Search,
  HardDrive
} from 'lucide-react';

export const ReportsPage: React.FC = () => {
  const [reportTypes, setReportTypes] = useState<any[]>([]);
  const [selectedType, setSelectedType] = useState<string>('diagnostic');
  const [vehicleProfiles, setVehicleProfiles] = useState<VehicleProfile[]>([]);
  const [selectedVehicleId, setSelectedVehicleId] = useState<string>('');
  const [technicianNotes, setTechnicianNotes] = useState<string>('Routine diagnostic inspection. Verified electrical systems and CAN communication. Zero write/flash activity recorded.');
  const [isGenerating, setIsGenerating] = useState<boolean>(false);
  const [previewMode, setPreviewMode] = useState<'pdf' | 'structure'>('pdf');
  const [pdfBlobUrl, setPdfBlobUrl] = useState<string | null>(null);
  const [structurePreview, setStructurePreview] = useState<any | null>(null);
  const [statusMessage, setStatusMessage] = useState<{ type: 'success' | 'error' | 'info'; text: string } | null>(null);

  useEffect(() => {
    // Load report types and vehicle profiles
    api.getReportTypes().then(types => {
      setReportTypes(types);
    }).catch(err => {
      console.error('Failed to load report types:', err);
    });

    api.getVehicleProfiles().then(profiles => {
      setVehicleProfiles(profiles);
      if (profiles.length > 0) {
        setSelectedVehicleId(profiles[0].id);
      }
    }).catch(err => {
      console.error('Failed to load vehicle profiles:', err);
    });
  }, []);

  // Whenever selected report type changes, refresh preview
  useEffect(() => {
    handleLoadPreview(selectedType, selectedVehicleId);
  }, [selectedType, selectedVehicleId]);

  const handleLoadPreview = async (type: string, vehicleId?: string) => {
    setIsGenerating(true);
    setStatusMessage(null);
    try {
      // 1. Fetch live PDF blob for embedded viewing
      let blob: Blob;
      switch (type) {
        case 'dyno':
          blob = await api.downloadDynoReportPdf(undefined, undefined, vehicleId || undefined);
          break;
        case 'ecu-flash':
          blob = await api.downloadEcuFlashReportPdf(undefined, undefined, vehicleId || undefined);
          break;
        case 'session-log':
          blob = await api.downloadSessionLogReportPdf(undefined, vehicleId || undefined);
          break;
        case 'vehicle-health':
          blob = await api.downloadVehicleHealthReportPdf(vehicleId || undefined, technicianNotes);
          break;
        case 'diagnostic':
        default:
          blob = await api.downloadDiagnosticReportPdf(vehicleId || undefined, technicianNotes);
          break;
      }

      if (pdfBlobUrl) {
        window.URL.revokeObjectURL(pdfBlobUrl);
      }
      const newUrl = window.URL.createObjectURL(blob);
      setPdfBlobUrl(newUrl);

      // 2. Fetch structural preview
      const previewJson = await api.getReportPreview(type, vehicleId || undefined);
      setStructurePreview(previewJson);

      setStatusMessage({ type: 'success', text: `Report ready: ${previewJson?.metadata?.title || 'Report generated'}` });
    } catch (err: any) {
      console.error('Failed to generate report preview:', err);
      setStatusMessage({ type: 'error', text: `Failed to load report: ${err.message || 'Unknown error'}` });
    } finally {
      setIsGenerating(false);
    }
  };

  const handleDownloadPdf = async () => {
    setIsGenerating(true);
    setStatusMessage({ type: 'info', text: 'Generating vector PDF document...' });
    try {
      let blob: Blob;
      const dateStr = new Date().toISOString().slice(0, 10);
      let filename = `Report_${dateStr}.pdf`;

      switch (selectedType) {
        case 'dyno':
          blob = await api.downloadDynoReportPdf(undefined, undefined, selectedVehicleId || undefined);
          filename = `Dyno_Performance_Report_${dateStr}.pdf`;
          break;
        case 'ecu-flash':
          blob = await api.downloadEcuFlashReportPdf(undefined, undefined, selectedVehicleId || undefined);
          filename = `ECU_Calibration_Report_${dateStr}.pdf`;
          break;
        case 'session-log':
          blob = await api.downloadSessionLogReportPdf(undefined, selectedVehicleId || undefined);
          filename = `Session_Telemetry_Report_${dateStr}.pdf`;
          break;
        case 'vehicle-health':
          blob = await api.downloadVehicleHealthReportPdf(selectedVehicleId || undefined, technicianNotes);
          filename = `Vehicle_Health_Report_${dateStr}.pdf`;
          break;
        case 'diagnostic':
        default:
          blob = await api.downloadDiagnosticReportPdf(selectedVehicleId || undefined, technicianNotes);
          filename = `Diagnostic_Report_${dateStr}.pdf`;
          break;
      }

      downloadBlobAsFile(blob, filename);
      setStatusMessage({ type: 'success', text: `Downloaded ${filename} successfully!` });
    } catch (err: any) {
      console.error('Failed to download PDF:', err);
      setStatusMessage({ type: 'error', text: `Export failed: ${err.message}` });
    } finally {
      setIsGenerating(false);
    }
  };

  const handleOpenInNewTab = () => {
    if (pdfBlobUrl) {
      window.open(pdfBlobUrl, '_blank');
    }
  };

  const reportIcons: Record<string, any> = {
    'diagnostic': Activity,
    'dyno': Gauge,
    'ecu-flash': Cpu,
    'session-log': HardDrive,
    'vehicle-health': ShieldCheck
  };

  return (
    <div className="flex flex-col h-full overflow-hidden bg-[#0A0E17] font-mono select-none">
      {/* Top Header */}
      <div className="p-4 border-b border-can-border bg-can-panel flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center space-x-3">
          <div className="p-2.5 bg-sky-600/20 text-sky-400 border border-sky-500/30 rounded-lg">
            <FileText className="w-6 h-6" />
          </div>
          <div>
            <h1 className="text-base font-bold text-white font-sans flex items-center space-x-2">
              <span>CENTRALIZED AUTOMOTIVE REPORT & PDF CENTER</span>
              <span className="text-[10px] px-2 py-0.5 bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 rounded font-mono">
                STRICT READ-ONLY
              </span>
            </h1>
            <p className="text-xs text-slate-400 font-sans mt-0.5">
              Vector PDF generation engine for diagnostics, chassis dyno pulls, ECU firmware calibrations, telemetry sessions, and vehicle health audits.
            </p>
          </div>
        </div>

        {/* Global Actions */}
        <div className="flex items-center space-x-2.5">
          <button
            onClick={() => handleLoadPreview(selectedType, selectedVehicleId)}
            disabled={isGenerating}
            className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border rounded text-xs font-sans font-semibold flex items-center space-x-1.5 transition-all shadow-sm"
            title="Refresh Report Data"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isGenerating ? 'animate-spin text-sky-400' : 'text-slate-400'}`} />
            <span>Refresh</span>
          </button>

          <button
            onClick={handleOpenInNewTab}
            disabled={!pdfBlobUrl}
            className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border rounded text-xs font-sans font-semibold flex items-center space-x-1.5 transition-all shadow-sm"
            title="Open in Native Browser Tab"
          >
            <ExternalLink className="w-3.5 h-3.5 text-sky-400" />
            <span>Open in Tab</span>
          </button>

          <button
            onClick={handleDownloadPdf}
            disabled={isGenerating}
            className="px-4 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-sans font-bold flex items-center space-x-1.5 transition-all shadow-md"
          >
            <Download className="w-3.5 h-3.5" />
            <span>{isGenerating ? 'Generating...' : 'Download PDF'}</span>
          </button>
        </div>
      </div>

      {/* Status Alert Banner */}
      {statusMessage && (
        <div className={`px-4 py-2 text-xs font-sans flex items-center justify-between border-b ${
          statusMessage.type === 'error'
            ? 'bg-rose-950/60 border-rose-800 text-rose-300'
            : statusMessage.type === 'success'
            ? 'bg-emerald-950/60 border-emerald-800 text-emerald-300'
            : 'bg-sky-950/60 border-sky-800 text-sky-300'
        }`}>
          <div className="flex items-center space-x-2">
            {statusMessage.type === 'error' && <AlertTriangle className="w-4 h-4 text-rose-400 flex-shrink-0" />}
            {statusMessage.type === 'success' && <CheckCircle2 className="w-4 h-4 text-emerald-400 flex-shrink-0" />}
            {statusMessage.type === 'info' && <Sparkles className="w-4 h-4 text-sky-400 flex-shrink-0" />}
            <span>{statusMessage.text}</span>
          </div>
          <button onClick={() => setStatusMessage(null)} className="text-slate-400 hover:text-white">✕</button>
        </div>
      )}

      {/* Main Workspace Layout: Configuration on Left, Preview on Right */}
      <div className="flex-1 grid grid-cols-12 gap-4 p-4 overflow-hidden">
        {/* LEFT COLUMN: Report Catalog & Options */}
        <div className="col-span-12 lg:col-span-4 flex flex-col space-y-4 overflow-y-auto pr-1">
          {/* 1. Report Type Selector */}
          <div className="bg-can-panel border border-can-border rounded-lg p-3.5 shadow-md space-y-2 font-sans">
            <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">
              SELECT REPORT TYPE (5 AVAILABLE)
            </div>
            <div className="space-y-1.5">
              {[
                { id: 'diagnostic', label: 'Diagnostic Audit Report', desc: 'Active DTCs, freeze frames, ECU ID & anomaly correlation' },
                { id: 'dyno', label: 'Dyno Performance Report', desc: 'Power/torque curves, splits, ambient correction & baseline' },
                { id: 'ecu-flash', label: 'ECU Firmware & Calibration', desc: 'Binary SHA-256, memory maps & 2D/3D calibration preview' },
                { id: 'session-log', label: 'Telemetry & Session Report', desc: 'Bus statistics, channel summaries & time-series trends' },
                { id: 'vehicle-health', label: 'Vehicle Health Audit', desc: 'Multi-module inventory, emissions readiness & live sensors' }
              ].map(item => {
                const isSelected = selectedType === item.id;
                const Icon = reportIcons[item.id] || FileText;
                return (
                  <button
                    key={item.id}
                    onClick={() => setSelectedType(item.id)}
                    className={`w-full text-left p-2.5 rounded-lg border transition-all flex items-start space-x-3 ${
                      isSelected
                        ? 'bg-sky-600/20 border-sky-500/50 text-white shadow-sm'
                        : 'bg-can-bg border-can-border/70 text-slate-400 hover:text-slate-200 hover:border-slate-600'
                    }`}
                  >
                    <div className={`p-2 rounded-md mt-0.5 ${isSelected ? 'bg-sky-600 text-white' : 'bg-slate-800 text-slate-400'}`}>
                      <Icon className="w-4 h-4" />
                    </div>
                    <div className="flex-1">
                      <div className="font-bold text-xs flex items-center justify-between">
                        <span>{item.label}</span>
                        {isSelected && <span className="w-2 h-2 rounded-full bg-sky-400" />}
                      </div>
                      <p className="text-[11px] text-slate-400 mt-0.5 leading-snug">
                        {item.desc}
                      </p>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* 2. Vehicle & Metadata Options */}
          <div className="bg-can-panel border border-can-border rounded-lg p-3.5 shadow-md space-y-3 font-sans text-xs">
            <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider border-b border-can-border pb-1.5">
              REPORT METADATA & VEHICLE CONTEXT
            </div>

            <div>
              <label className="block text-slate-400 font-bold mb-1 text-[11px]">VEHICLE PROFILE</label>
              <select
                value={selectedVehicleId}
                onChange={e => setSelectedVehicleId(e.target.value)}
                className="w-full bg-can-bg border border-can-border rounded px-3 py-1.5 text-white font-mono text-xs focus:outline-none focus:border-sky-500"
              >
                {vehicleProfiles.map(v => (
                  <option key={v.id} value={v.id}>
                    {v.name} (VIN: {v.vin})
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-slate-400 font-bold mb-1 text-[11px]">TECHNICIAN OBSERVATIONS & NOTES</label>
              <textarea
                value={technicianNotes}
                onChange={e => setTechnicianNotes(e.target.value)}
                rows={4}
                placeholder="Enter technician observations or notes for the report..."
                className="w-full bg-can-bg border border-can-border rounded p-2.5 text-white font-sans text-xs focus:outline-none focus:border-sky-500 resize-none"
              />
            </div>

            <div className="pt-2 border-t border-can-border">
              <button
                onClick={handleDownloadPdf}
                disabled={isGenerating}
                className="w-full py-2 bg-sky-600 hover:bg-sky-500 text-white rounded font-bold text-xs flex items-center justify-center space-x-2 transition-all shadow"
              >
                <Download className="w-4 h-4" />
                <span>Download Vector PDF ({selectedType})</span>
              </button>
            </div>
          </div>
        </div>

        {/* RIGHT COLUMN: Interactive Live PDF & Structural Preview */}
        <div className="col-span-12 lg:col-span-8 flex flex-col bg-can-panel border border-can-border rounded-lg overflow-hidden shadow-xl">
          {/* Preview Navigation Tabs */}
          <div className="p-3 border-b border-can-border bg-slate-900/60 flex items-center justify-between text-xs font-sans">
            <div className="flex items-center space-x-2">
              <span className="font-bold text-white uppercase tracking-wider flex items-center space-x-1.5">
                <Eye className="w-4 h-4 text-sky-400" />
                <span>Live Document Preview</span>
              </span>
              {structurePreview?.metadata?.softwareVersion && (
                <span className="text-[10px] text-slate-400 bg-slate-800 px-2 py-0.5 rounded font-mono">
                  {structurePreview.metadata.softwareVersion}
                </span>
              )}
            </div>

            <div className="flex items-center space-x-2">
              <div className="bg-can-bg border border-can-border rounded p-0.5 flex">
                <button
                  onClick={() => setPreviewMode('pdf')}
                  className={`px-3 py-1 rounded font-semibold text-xs transition-all flex items-center space-x-1 ${
                    previewMode === 'pdf' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
                  }`}
                >
                  <FileText className="w-3.5 h-3.5" />
                  <span>Interactive PDF</span>
                </button>
                <button
                  onClick={() => setPreviewMode('structure')}
                  className={`px-3 py-1 rounded font-semibold text-xs transition-all flex items-center space-x-1 ${
                    previewMode === 'structure' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
                  }`}
                >
                  <Layers className="w-3.5 h-3.5" />
                  <span>Sections & KPI</span>
                </button>
              </div>
            </div>
          </div>

          {/* Preview Content Area */}
          <div className="flex-1 overflow-hidden relative bg-slate-950 flex flex-col">
            {isGenerating && (
              <div className="absolute inset-0 bg-slate-950/70 backdrop-blur-sm z-20 flex flex-col items-center justify-center space-y-3 font-sans">
                <RefreshCw className="w-8 h-8 text-sky-400 animate-spin" />
                <span className="text-sm font-bold text-white">Rendering Vector Automotive PDF...</span>
              </div>
            )}

            {previewMode === 'pdf' ? (
              pdfBlobUrl ? (
                <iframe
                  src={pdfBlobUrl}
                  title="PDF Preview"
                  className="w-full h-full border-0 bg-slate-900"
                />
              ) : (
                <div className="flex-1 flex flex-col items-center justify-center p-8 text-center text-slate-500 font-sans space-y-2">
                  <FileText className="w-12 h-12 text-slate-600" />
                  <p className="text-sm font-semibold">Select a report type to generate a live PDF preview.</p>
                </div>
              )
            ) : (
              /* Structural Section Viewer */
              <div className="flex-1 overflow-y-auto p-5 space-y-5 font-sans">
                {structurePreview ? (
                  <>
                    {/* Header Card */}
                    <div className="bg-slate-900 border border-can-border rounded-lg p-4 space-y-2">
                      <div className="flex justify-between items-start">
                        <div>
                          <h2 className="text-sm font-bold text-sky-400 uppercase tracking-wide">
                            {structurePreview.metadata?.title}
                          </h2>
                          <p className="text-xs text-slate-400 mt-0.5">
                            {structurePreview.metadata?.subtitle}
                          </p>
                        </div>
                        <span className="px-2 py-0.5 bg-sky-950 border border-sky-800 text-sky-300 rounded font-mono text-[10px]">
                          VIN: {structurePreview.metadata?.vehicleVin}
                        </span>
                      </div>
                      <div className="grid grid-cols-3 gap-2 text-[11px] pt-2 border-t border-can-border/60 text-slate-400">
                        <div><strong className="text-slate-300">Generated:</strong> {new Date(structurePreview.metadata?.generatedAtUtc).toLocaleString()}</div>
                        <div><strong className="text-slate-300">Operator:</strong> {structurePreview.metadata?.technicianName}</div>
                        <div><strong className="text-slate-300">Vehicle:</strong> {structurePreview.metadata?.vehicleDescription}</div>
                      </div>
                    </div>

                    {/* Report Sections */}
                    {structurePreview.sections?.map((sec: any, idx: number) => (
                      <div key={idx} className="bg-slate-900 border border-can-border rounded-lg p-4 space-y-3">
                        <div className="flex items-center justify-between border-b border-can-border pb-2">
                          <h3 className="text-xs font-bold text-white uppercase tracking-wider flex items-center space-x-2">
                            <span className="w-1.5 h-3.5 bg-sky-500 rounded-sm" />
                            <span>{sec.title}</span>
                          </h3>
                          <span className="text-[10px] text-slate-500 font-mono">
                            Type: {sec.type}
                          </span>
                        </div>

                        {/* KPI Metrics */}
                        {sec.metrics && sec.metrics.length > 0 && (
                          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                            {sec.metrics.map((m: any, mi: number) => (
                              <div key={mi} className="bg-can-bg border border-can-border rounded p-2.5 space-y-1">
                                <span className="text-[10px] text-slate-400 uppercase font-bold block">{m.label}</span>
                                <span className="text-sm font-bold font-mono text-white">
                                  {m.value} {m.unit && <span className="text-xs text-slate-400 font-normal">{m.unit}</span>}
                                </span>
                              </div>
                            ))}
                          </div>
                        )}

                        {/* Key-Value Pairs */}
                        {sec.keyValues && (
                          <div className="grid grid-cols-2 gap-2 text-xs font-mono">
                            {Object.entries(sec.keyValues).map(([k, v]: [string, any], ki: number) => (
                              <div key={ki} className="flex justify-between p-1.5 bg-can-bg rounded border border-can-border/50">
                                <span className="text-slate-400">{k}:</span>
                                <span className="text-white font-bold">{String(v)}</span>
                              </div>
                            ))}
                          </div>
                        )}

                        {/* Table */}
                        {sec.table && (
                          <div className="overflow-x-auto">
                            <table className="w-full text-left text-xs border-collapse">
                              <thead>
                                <tr className="text-slate-400 text-[10px] uppercase border-b border-can-border bg-slate-950/60">
                                  {sec.table.headers?.map((h: string, hi: number) => (
                                    <th key={hi} className="p-2 font-bold">{h}</th>
                                  ))}
                                </tr>
                              </thead>
                              <tbody className="divide-y divide-can-border/60 font-mono text-[11px]">
                                {sec.table.rows?.map((row: string[], ri: number) => (
                                  <tr key={ri} className="hover:bg-slate-800/50">
                                    {row.map((cell: string, ci: number) => (
                                      <td key={ci} className="p-2 text-slate-300">{cell}</td>
                                    ))}
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        )}

                        {/* Alert / Notice */}
                        {sec.alertMessage && (
                          <div className="p-3 bg-emerald-950/40 border border-emerald-800 text-emerald-300 rounded text-xs leading-relaxed">
                            {sec.alertMessage}
                          </div>
                        )}

                        {/* Notes */}
                        {sec.notesContent && (
                          <div className="p-3 bg-can-bg border border-can-border text-slate-300 rounded text-xs italic">
                            {sec.notesContent}
                          </div>
                        )}
                      </div>
                    ))}
                  </>
                ) : (
                  <div className="p-8 text-center text-slate-500 text-xs">
                    No structural preview available. Click Refresh to regenerate.
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
