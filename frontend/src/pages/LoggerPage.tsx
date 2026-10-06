import React, { useState, useEffect, useRef } from 'react';
import { api, downloadBlobAsFile } from '../services/api';
import {
  LogSession,
  CanonicalLogDataSet,
  LogValidationReport,
  RunComparisonResult,
  ComparisonAlignmentMode
} from '../types';
import { SignalChart } from '../components/SignalChart';
import {
  Disc,
  Play,
  Pause,
  Square,
  Download,
  PlaySquare,
  HardDrive,
  Upload,
  CheckCircle2,
  AlertTriangle,
  AlertCircle,
  GitCompare,
  FileText,
  Sliders,
  MapPin,
  BarChart3,
  Activity,
  Layers,
  Sparkles
} from 'lucide-react';

interface LoggerProps {
  onNavigateToReplay?: () => void;
}

export const LoggerPage: React.FC<LoggerProps> = ({ onNavigateToReplay }) => {
  // Navigation / Tabs within Logger
  const [activeTab, setActiveTab] = useState<'record' | 'import' | 'compare'>('record');

  // Live Recording State
  const [sessions, setSessions] = useState<LogSession[]>([]);
  const [currentSession, setCurrentSession] = useState<any>(null);
  const [sessionName, setSessionName] = useState('');
  const [isRecording, setIsRecording] = useState(false);
  const [isPaused, setIsPaused] = useState(false);

  // Import & Analysis State
  const [importedDataSets, setImportedDataSets] = useState<CanonicalLogDataSet[]>([]);
  const [activeDataSet, setActiveDataSet] = useState<CanonicalLogDataSet | null>(null);
  const [validationReport, setValidationReport] = useState<LogValidationReport | null>(null);
  const [selectedChartSignals, setSelectedChartSignals] = useState<string[]>([]);
  const [isImporting, setIsImporting] = useState(false);
  const [importStatusMessage, setImportStatusMessage] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  // Run Comparison State
  const [comparisonRunAId, setComparisonRunAId] = useState<string>('');
  const [comparisonRunBId, setComparisonRunBId] = useState<string>('');
  const [comparisonMode, setComparisonMode] = useState<ComparisonAlignmentMode>('TimeTriggered');
  const [triggerChannel, setTriggerChannel] = useState<string>('ThrottlePosition');
  const [triggerThreshold, setTriggerThreshold] = useState<number>(85.0);
  const [comparisonResult, setComparisonResult] = useState<RunComparisonResult | null>(null);
  const [selectedComparisonSignal, setSelectedComparisonSignal] = useState<string>('');

  // Fetch recorded sessions
  const fetchSessions = async () => {
    try {
      const list = await api.getLogSessions();
      setSessions(list);
      const curr = await api.getCurrentLogSession();
      setCurrentSession(curr);
      setIsRecording(curr?.isActive || false);
      setIsPaused(curr?.isPaused || false);
    } catch {
      // Ignore
    }
  };

  useEffect(() => {
    fetchSessions();
    const interval = setInterval(fetchSessions, 1500);
    return () => clearInterval(interval);
  }, []);

  // Handle Recording actions
  const handleStart = async () => {
    await api.startLogging(sessionName);
    setSessionName('');
    await fetchSessions();
  };

  const handlePause = async () => {
    await api.pauseLogging();
    await fetchSessions();
  };

  const handleResume = async () => {
    await api.resumeLogging();
    await fetchSessions();
  };

  const handleStop = async () => {
    await api.stopLogging();
    await fetchSessions();
  };

  const handleLoadToReplay = async (sessionId: string) => {
    await api.loadSessionForReplay(sessionId);
    if (onNavigateToReplay) {
      onNavigateToReplay();
    }
  };

  // Convert a live recorded session to canonical dataset for analysis
  const handleAnalyzeRecordedSession = async (sessionId: string) => {
    try {
      const ds = await api.convertSessionToCanonical(sessionId);
      if (ds) {
        setImportedDataSets(prev => [ds, ...prev.filter(d => d.datasetId !== ds.datasetId)]);
        setActiveDataSet(ds);
        setSelectedChartSignals(Object.keys(ds.channels).slice(0, 3));
        setActiveTab('import');

        // Run validation
        const report = await api.validateDataSet(ds);
        setValidationReport(report);
      }
    } catch (err: any) {
      alert(`Error analyzing session: ${err.message}`);
    }
  };

  // File Upload Handler
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsImporting(true);
    setImportStatusMessage(`Importing ${file.name}...`);

    try {
      const res = await api.importLogFile(file);
      if (res.success && res.dataSet) {
        setImportedDataSets(prev => [res.dataSet!, ...prev]);
        setActiveDataSet(res.dataSet);
        const channelKeys = Object.keys(res.dataSet.channels);
        setSelectedChartSignals(channelKeys.slice(0, Math.min(4, channelKeys.length)));
        setImportStatusMessage(`Imported ${res.dataSet.name} (${res.detectedFormat}) with ${channelKeys.length} channels.`);

        // Auto-validate
        const report = await api.validateDataSet(res.dataSet);
        setValidationReport(report);
      } else {
        setImportStatusMessage(`Import failed: ${res.message}`);
      }
    } catch (err: any) {
      setImportStatusMessage(`Upload error: ${err.message}`);
    } finally {
      setIsImporting(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  // Run Comparison Handler
  const handleExecuteComparison = async () => {
    const runA = importedDataSets.find(d => d.datasetId === comparisonRunAId);
    const runB = importedDataSets.find(d => d.datasetId === comparisonRunBId);

    if (!runA || !runB) {
      alert('Please select both Run A and Run B datasets to compare.');
      return;
    }

    try {
      const result = await api.compareRuns(runA, runB, comparisonMode, triggerChannel, triggerThreshold);
      setComparisonResult(result);
      const chKeys = Object.keys(result.channels);
      if (chKeys.length > 0) {
        setSelectedComparisonSignal(chKeys[0]);
      }
    } catch (err: any) {
      alert(`Comparison failed: ${err.message}`);
    }
  };

  const toggleChartSignal = (sigName: string) => {
    setSelectedChartSignals(prev =>
      prev.includes(sigName) ? prev.filter(s => s !== sigName) : [...prev, sigName]
    );
  };

  const handleExportSessionPdf = async (sessionId?: string, sessionName?: string) => {
    try {
      const blob = await api.downloadSessionLogReportPdf(sessionId);
      downloadBlobAsFile(blob, `${sessionName || 'Session'}_Telemetry_Report.pdf`);
    } catch (e) {
      console.error('Failed to export session PDF:', e);
    }
  };

  return (
    <div className="p-6 space-y-6 overflow-y-auto h-full font-mono select-none">
      {/* Header with Navigation Tabs */}
      <div className="border-b border-can-border pb-4 flex flex-wrap items-center justify-between gap-4">
        <div>
          <div className="flex items-center space-x-2 text-white font-bold text-base font-sans">
            <Disc className="w-5 h-5 text-rose-400" />
            <span>CAN LOGGING & TIME-SERIES ANALYSIS SUITE</span>
          </div>
          <p className="text-xs text-slate-400 font-sans mt-0.5">
            Authoritative multi-format log import, automated data quality validation, synchronized multi-signal charting, and dyno/track run comparison.
          </p>
        </div>

        {/* Tab Switcher */}
        <div className="flex items-center bg-slate-900 border border-can-border rounded-lg p-1 text-xs">
          <button
            onClick={() => setActiveTab('record')}
            className={`px-3 py-1.5 rounded flex items-center space-x-1.5 transition-colors font-semibold ${
              activeTab === 'record'
                ? 'bg-rose-600 text-white shadow'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Disc className="w-3.5 h-3.5" />
            <span>Live Recording</span>
          </button>

          <button
            onClick={() => setActiveTab('import')}
            className={`px-3 py-1.5 rounded flex items-center space-x-1.5 transition-colors font-semibold ${
              activeTab === 'import'
                ? 'bg-sky-600 text-white shadow'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Upload className="w-3.5 h-3.5" />
            <span>Import & Analysis</span>
          </button>

          <button
            onClick={() => setActiveTab('compare')}
            className={`px-3 py-1.5 rounded flex items-center space-x-1.5 transition-colors font-semibold ${
              activeTab === 'compare'
                ? 'bg-purple-600 text-white shadow'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <GitCompare className="w-3.5 h-3.5" />
            <span>Run Comparison</span>
          </button>
        </div>

        {/* Global PDF Report Export */}
        <button
          onClick={() => handleExportSessionPdf(currentSession?.id || (sessions.length > 0 ? sessions[0].id : undefined))}
          className="px-3.5 py-1.5 bg-sky-600/20 hover:bg-sky-600/30 text-sky-300 border border-sky-500/40 rounded flex items-center space-x-1.5 font-semibold text-xs transition-all shadow-sm font-sans"
          title="Download Telemetry & Session Report PDF"
        >
          <FileText className="w-3.5 h-3.5 text-sky-400" />
          <span>Export Session PDF</span>
        </button>
      </div>

      {/* TAB 1: LIVE RECORDING & EXPORT */}
      {activeTab === 'record' && (
        <div className="space-y-6">
          {/* Recording Controls Card */}
          <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow-md space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-3">
                <div className={`p-3 rounded-lg border ${isRecording ? 'bg-rose-500/10 border-rose-500/30 text-rose-400 animate-pulse' : 'bg-slate-800 text-slate-500 border-can-border'}`}>
                  <Disc className="w-6 h-6" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-white font-sans">
                    {isRecording ? (isPaused ? 'Recording Paused' : 'Recording in Progress...') : 'Ready to Record Live Session'}
                  </h3>
                  <p className="text-xs text-slate-400 font-sans mt-0.5">
                    {isRecording
                      ? `Active Session: ${currentSession?.name} | Frames: ${currentSession?.frameCount?.toLocaleString()}`
                      : 'Specify an optional session name or leave blank for automatic timestamped naming.'}
                  </p>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="flex items-center space-x-2 text-xs">
                {!isRecording ? (
                  <div className="flex items-center space-x-2">
                    <input
                      type="text"
                      value={sessionName}
                      onChange={e => setSessionName(e.target.value)}
                      placeholder="Session Name (optional)"
                      className="bg-can-bg border border-can-border rounded px-3 py-1.5 text-xs text-white focus:outline-none focus:border-rose-500 w-52"
                    />
                    <button
                      onClick={handleStart}
                      className="px-4 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded font-bold font-sans flex items-center space-x-1.5 shadow"
                    >
                      <Disc className="w-3.5 h-3.5" />
                      <span>Start Recording</span>
                    </button>
                  </div>
                ) : (
                  <div className="flex items-center space-x-2">
                    {isPaused ? (
                      <button
                        onClick={handleResume}
                        className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-semibold font-sans flex items-center space-x-1.5"
                      >
                        <Play className="w-3.5 h-3.5" />
                        <span>Resume</span>
                      </button>
                    ) : (
                      <button
                        onClick={handlePause}
                        className="px-3 py-1.5 bg-amber-600 hover:bg-amber-500 text-white rounded font-semibold font-sans flex items-center space-x-1.5"
                      >
                        <Pause className="w-3.5 h-3.5" />
                        <span>Pause</span>
                      </button>
                    )}
                    <button
                      onClick={handleStop}
                      className="px-4 py-1.5 bg-slate-800 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 rounded font-bold font-sans flex items-center space-x-1.5"
                    >
                      <Square className="w-3.5 h-3.5" />
                      <span>Stop & Save</span>
                    </button>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Recorded Sessions History Table */}
          <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow-md space-y-3">
            <div className="flex items-center justify-between text-xs border-b border-can-border pb-2">
              <span className="font-bold text-white uppercase tracking-wider font-sans">
                Recorded Sessions History ({sessions.length})
              </span>
            </div>

            {sessions.length > 0 ? (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="text-slate-400 text-[10px] uppercase border-b border-can-border">
                      <th className="p-2">Session Name</th>
                      <th className="p-2">Start Time (UTC)</th>
                      <th className="p-2">Frame Count</th>
                      <th className="p-2">Status</th>
                      <th className="p-2 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-can-border/60">
                    {sessions.map(s => (
                      <tr key={s.id} className="hover:bg-can-card text-slate-300">
                        <td className="p-2 font-bold text-white">{s.name}</td>
                        <td className="p-2 text-slate-400">{new Date(s.startTimeUtc).toLocaleString()}</td>
                        <td className="p-2 font-mono text-sky-400 font-bold">{s.frameCount.toLocaleString()}</td>
                        <td className="p-2">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${s.isActive ? 'bg-rose-500/20 text-rose-300' : 'bg-slate-800 text-slate-400'}`}>
                            {s.isActive ? 'RECORDING' : 'SAVED'}
                          </span>
                        </td>
                        <td className="p-2 text-right space-x-2">
                          <button
                            onClick={() => handleAnalyzeRecordedSession(s.id)}
                            className="inline-flex items-center space-x-1 px-2.5 py-1 bg-purple-600/20 hover:bg-purple-600/30 text-purple-300 rounded text-xs border border-purple-500/30 font-sans"
                            title="Open in Time-Series Analysis & Validation Suite"
                          >
                            <BarChart3 className="w-3.5 h-3.5" />
                            <span>Analyze</span>
                          </button>
                          <a
                            href={api.getExportUrl(s.id)}
                            download={`${s.name}.csv`}
                            className="inline-flex items-center space-x-1 px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs border border-can-border font-sans"
                          >
                            <Download className="w-3.5 h-3.5 text-emerald-400" />
                            <span>Export CSV</span>
                          </a>
                          <button
                            onClick={() => handleExportSessionPdf(s.id, s.name)}
                            className="inline-flex items-center space-x-1 px-2.5 py-1 bg-sky-600/20 hover:bg-sky-600/30 text-sky-300 rounded text-xs border border-sky-500/30 font-sans"
                            title="Download PDF Telemetry & Session Report"
                          >
                            <FileText className="w-3.5 h-3.5 text-sky-400" />
                            <span>Export PDF</span>
                          </button>
                          <button
                            onClick={() => handleLoadToReplay(s.id)}
                            className="inline-flex items-center space-x-1 px-2.5 py-1 bg-sky-600/20 hover:bg-sky-600/30 text-sky-300 rounded text-xs border border-sky-500/30 font-sans"
                          >
                            <PlaySquare className="w-3.5 h-3.5" />
                            <span>Replay</span>
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="text-center py-10 text-slate-500 text-xs">
                <HardDrive className="w-10 h-10 mx-auto mb-2 text-slate-600" />
                <p>No recorded sessions yet. Click "Start Recording" above to capture CAN traffic.</p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 2: IMPORT & TIME-SERIES ANALYSIS */}
      {activeTab === 'import' && (
        <div className="space-y-6">
          {/* Universal Log File Dropzone Card */}
          <div className="bg-can-panel border border-dashed border-can-border hover:border-sky-500/50 rounded-lg p-6 text-center transition-colors shadow">
            <input
              type="file"
              ref={fileInputRef}
              onChange={handleFileUpload}
              className="hidden"
              accept=".csv,.json,.log,.asc,.txt"
            />
            <Upload className="w-8 h-8 text-sky-400 mx-auto mb-2" />
            <h4 className="text-sm font-bold text-white font-sans">
              Universal Automotive Log Importer
            </h4>
            <p className="text-xs text-slate-400 max-w-lg mx-auto mt-1 mb-4 font-sans">
              Supports CSV, JSON, Torque Pro CSV, VCDS-style CSV, Generic CAN CSV, Linux candump text logs, and Vector ASC logs.
            </p>
            <div className="flex justify-center items-center space-x-3">
              <button
                onClick={() => fileInputRef.current?.click()}
                disabled={isImporting}
                className="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded font-bold font-sans text-xs flex items-center space-x-2 shadow"
              >
                <FileText className="w-4 h-4" />
                <span>{isImporting ? 'Processing Log...' : 'Select File to Import'}</span>
              </button>
            </div>
            {importStatusMessage && (
              <p className="text-xs text-sky-300 mt-3 font-sans">{importStatusMessage}</p>
            )}
          </div>

          {/* Active Dataset Inspection & Validation Summary */}
          {activeDataSet && (
            <div className="space-y-4">
              {/* Dataset Info Bar */}
              <div className="bg-can-panel border border-can-border rounded-lg p-4 flex flex-wrap items-center justify-between gap-3 shadow">
                <div className="flex items-center space-x-3">
                  <div className="p-2 rounded bg-sky-500/20 text-sky-400 border border-sky-500/30">
                    <BarChart3 className="w-5 h-5" />
                  </div>
                  <div>
                    <h3 className="text-sm font-bold text-white font-sans">{activeDataSet.name}</h3>
                    <div className="flex items-center space-x-3 text-xs text-slate-400 mt-0.5">
                      <span>Format: <strong className="text-sky-300">{activeDataSet.sourceFormat}</strong></span>
                      <span>Channels: <strong className="text-white">{Object.keys(activeDataSet.channels).length}</strong></span>
                      <span>Duration: <strong className="text-white">{activeDataSet.durationSeconds.toFixed(1)}s</strong></span>
                      {activeDataSet.gpsTrack.length > 0 && (
                        <span className="flex items-center text-emerald-400">
                          <MapPin className="w-3 h-3 mr-1" />
                          GPS ({activeDataSet.gpsTrack.length} pts)
                        </span>
                      )}
                    </div>
                  </div>
                </div>

                {/* Validation Status Badge */}
                {validationReport && (
                  <div className="flex items-center space-x-3">
                    <div className={`px-3 py-1.5 rounded-lg border flex items-center space-x-2 ${
                      validationReport.isValid
                        ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-400'
                        : 'bg-amber-500/10 border-amber-500/30 text-amber-400'
                    }`}>
                      {validationReport.isValid ? (
                        <CheckCircle2 className="w-4 h-4" />
                      ) : (
                        <AlertTriangle className="w-4 h-4" />
                      )}
                      <span className="font-bold text-xs font-sans">
                        {validationReport.isValid ? 'LOG HEALTHY (0 CRITICAL ERRORS)' : `${validationReport.issues.length} ISSUES IDENTIFIED`}
                      </span>
                    </div>
                  </div>
                )}
              </div>

              {/* Validation Detailed Cards */}
              {validationReport && (
                <div className="grid grid-cols-2 md:grid-cols-5 gap-3 text-xs font-sans">
                  <div className="bg-can-panel border border-can-border rounded-lg p-3">
                    <span className="text-slate-400 text-[10px] block">MISSING SAMPLES</span>
                    <span className="text-lg font-bold text-white font-mono">{validationReport.missingGapsCount}</span>
                  </div>
                  <div className="bg-can-panel border border-can-border rounded-lg p-3">
                    <span className="text-slate-400 text-[10px] block">TIMESTAMP JUMPS</span>
                    <span className="text-lg font-bold text-white font-mono">{validationReport.timestampJumpsCount}</span>
                  </div>
                  <div className="bg-can-panel border border-can-border rounded-lg p-3">
                    <span className="text-slate-400 text-[10px] block">DUPLICATES</span>
                    <span className="text-lg font-bold text-white font-mono">{validationReport.duplicatesCount}</span>
                  </div>
                  <div className="bg-can-panel border border-can-border rounded-lg p-3">
                    <span className="text-slate-400 text-[10px] block">SPIKES & OUTLIERS</span>
                    <span className="text-lg font-bold text-white font-mono">{validationReport.spikesCount}</span>
                  </div>
                  <div className="bg-can-panel border border-can-border rounded-lg p-3">
                    <span className="text-slate-400 text-[10px] block">IMPOSSIBLE VALUES</span>
                    <span className="text-lg font-bold text-white font-mono">{validationReport.impossibleValuesCount}</span>
                  </div>
                </div>
              )}

              {/* Channel Selector Chips */}
              <div className="bg-can-panel border border-can-border rounded-lg p-3.5 space-y-2">
                <span className="text-xs font-bold text-white font-sans flex items-center space-x-1.5">
                  <Layers className="w-3.5 h-3.5 text-sky-400" />
                  <span>SELECT CHANNELS TO GRAPH & OVERLAY</span>
                </span>
                <div className="flex flex-wrap gap-1.5">
                  {Object.keys(activeDataSet.channels).map(chName => {
                    const isSelected = selectedChartSignals.includes(chName);
                    return (
                      <button
                        key={chName}
                        onClick={() => toggleChartSignal(chName)}
                        className={`px-2.5 py-1 rounded text-xs transition-colors border ${
                          isSelected
                            ? 'bg-sky-500/20 text-sky-300 border-sky-500/50 font-bold'
                            : 'bg-slate-800 text-slate-400 border-can-border hover:bg-slate-700'
                        }`}
                      >
                        {chName}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Synchronized Multi-Signal Chart */}
              <SignalChart
                selectedSignals={selectedChartSignals}
                onToggleSignal={toggleChartSignal}
                dataSet={activeDataSet}
                height={380}
              />

              {/* Validation Issues Log Table if any issues exist */}
              {validationReport && validationReport.issues.length > 0 && (
                <div className="bg-can-panel border border-can-border rounded-lg p-4 space-y-2">
                  <span className="text-xs font-bold text-white font-sans flex items-center space-x-1.5 text-amber-400">
                    <AlertCircle className="w-3.5 h-3.5" />
                    <span>AUTOMATED AUDIT LOG ({validationReport.issues.length} Items)</span>
                  </span>
                  <div className="max-h-52 overflow-y-auto">
                    <table className="w-full text-left text-xs border-collapse font-sans">
                      <thead>
                        <tr className="text-slate-400 text-[10px] border-b border-can-border">
                          <th className="p-1.5">Severity</th>
                          <th className="p-1.5">Issue Type</th>
                          <th className="p-1.5">Channel</th>
                          <th className="p-1.5">Timestamp (ms)</th>
                          <th className="p-1.5">Observed Value</th>
                          <th className="p-1.5">Description</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-can-border/60">
                        {validationReport.issues.slice(0, 15).map((iss, i) => (
                          <tr key={i} className="hover:bg-can-card text-slate-300">
                            <td className="p-1.5">
                              <span className={`px-1.5 py-0.5 rounded text-[9px] font-bold ${
                                iss.severity === 'Error' ? 'bg-rose-500/20 text-rose-300' : 'bg-amber-500/20 text-amber-300'
                              }`}>
                                {iss.severity}
                              </span>
                            </td>
                            <td className="p-1.5 text-white font-medium">{iss.type}</td>
                            <td className="p-1.5 text-sky-400 font-mono">{iss.channelName || 'N/A'}</td>
                            <td className="p-1.5 font-mono">{iss.timestampMs.toFixed(1)}</td>
                            <td className="p-1.5 font-mono text-amber-300">{iss.observedValue !== null ? iss.observedValue?.toFixed(2) : '-'}</td>
                            <td className="p-1.5 text-slate-400">{iss.description}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* TAB 3: RUN & SESSION COMPARISON */}
      {activeTab === 'compare' && (
        <div className="space-y-6">
          {/* Comparison Setup Card */}
          <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow space-y-4">
            <div className="flex items-center space-x-2 text-white font-bold text-sm font-sans border-b border-can-border pb-2.5">
              <GitCompare className="w-4 h-4 text-purple-400" />
              <span>CONFIGURE RUN COMPARISON (TIME OR RPM DOMAIN)</span>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-4 text-xs font-sans">
              {/* Run A Selection */}
              <div>
                <label className="block text-slate-400 text-[10px] mb-1 font-bold">RUN A (BASELINE / STOCK)</label>
                <select
                  value={comparisonRunAId}
                  onChange={e => setComparisonRunAId(e.target.value)}
                  className="w-full bg-can-bg border border-can-border rounded px-2.5 py-2 text-white text-xs"
                >
                  <option value="">Select Run A...</option>
                  {importedDataSets.map(d => (
                    <option key={d.datasetId} value={d.datasetId}>
                      {d.name} ({Object.keys(d.channels).length} ch)
                    </option>
                  ))}
                </select>
              </div>

              {/* Run B Selection */}
              <div>
                <label className="block text-slate-400 text-[10px] mb-1 font-bold">RUN B (TEST / TUNED)</label>
                <select
                  value={comparisonRunBId}
                  onChange={e => setComparisonRunBId(e.target.value)}
                  className="w-full bg-can-bg border border-can-border rounded px-2.5 py-2 text-white text-xs"
                >
                  <option value="">Select Run B...</option>
                  {importedDataSets.map(d => (
                    <option key={d.datasetId} value={d.datasetId}>
                      {d.name} ({Object.keys(d.channels).length} ch)
                    </option>
                  ))}
                </select>
              </div>

              {/* Alignment Mode */}
              <div>
                <label className="block text-slate-400 text-[10px] mb-1 font-bold">ALIGNMENT DOMAIN</label>
                <select
                  value={comparisonMode}
                  onChange={e => setComparisonMode(e.target.value as any)}
                  className="w-full bg-can-bg border border-can-border rounded px-2.5 py-2 text-white text-xs"
                >
                  <option value="TimeTriggered">Time-Triggered (Trigger-Aligned Relative Time)</option>
                  <option value="RpmDomain">RPM-Domain (Uniform RPM Grid 2000-7000)</option>
                  <option value="SpeedDomain">Speed-Domain (Uniform Speed Grid km/h)</option>
                </select>
              </div>

              {/* Compare Action Button */}
              <div className="flex items-end">
                <button
                  onClick={handleExecuteComparison}
                  className="w-full py-2 bg-purple-600 hover:bg-purple-500 text-white rounded font-bold font-sans flex items-center justify-center space-x-2 shadow"
                >
                  <Sparkles className="w-4 h-4" />
                  <span>Execute Comparison</span>
                </button>
              </div>
            </div>
          </div>

          {/* Comparison Results Card */}
          {comparisonResult && (
            <div className="space-y-4">
              {/* Summary Banner */}
              <div className="bg-can-panel border border-can-border rounded-lg p-4 flex flex-wrap items-center justify-between gap-3 shadow">
                <div>
                  <h3 className="text-sm font-bold text-white font-sans">
                    Comparison: <span className="text-sky-400">{comparisonResult.runAName}</span> vs <span className="text-purple-400">{comparisonResult.runBName}</span>
                  </h3>
                  <p className="text-xs text-slate-400 font-sans mt-0.5">
                    Mode: <strong>{comparisonResult.alignment}</strong> | Channels compared: <strong>{Object.keys(comparisonResult.channels).length}</strong>
                  </p>
                </div>

                {/* Channel Selector for Delta View */}
                <div className="flex items-center space-x-2 text-xs font-sans">
                  <span className="text-slate-400">Inspecting Channel:</span>
                  <select
                    value={selectedComparisonSignal}
                    onChange={e => setSelectedComparisonSignal(e.target.value)}
                    className="bg-can-bg border border-can-border rounded px-2.5 py-1 text-white"
                  >
                    {Object.keys(comparisonResult.channels).map(chName => (
                      <option key={chName} value={chName}>{chName}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Peak & Delta Metrics Table */}
              <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow space-y-2">
                <span className="text-xs font-bold text-white font-sans uppercase">
                  Comparative Channel Metrics & Peak Delta
                </span>
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs border-collapse font-sans">
                    <thead>
                      <tr className="text-slate-400 text-[10px] uppercase border-b border-can-border">
                        <th className="p-2">Signal</th>
                        <th className="p-2">Units</th>
                        <th className="p-2">Peak Run A</th>
                        <th className="p-2">Peak Run B</th>
                        <th className="p-2">Peak Delta</th>
                        <th className="p-2">Mean Delta</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-can-border/60">
                      {Object.values(comparisonResult.channels).map(ch => (
                        <tr key={ch.channelName} className="hover:bg-can-card text-slate-300">
                          <td className="p-2 font-bold text-white">{ch.channelName}</td>
                          <td className="p-2 text-slate-400">{ch.units || '-'}</td>
                          <td className="p-2 font-mono text-sky-400">{ch.peakRunA.toFixed(2)}</td>
                          <td className="p-2 font-mono text-purple-400">{ch.peakRunB.toFixed(2)}</td>
                          <td className="p-2 font-mono font-bold">
                            <span className={ch.peakDelta >= 0 ? 'text-emerald-400' : 'text-rose-400'}>
                              {ch.peakDelta >= 0 ? '+' : ''}{ch.peakDelta.toFixed(2)} ({ch.peakDeltaPercent.toFixed(1)}%)
                            </span>
                          </td>
                          <td className="p-2 font-mono text-slate-300">
                            {ch.meanDelta >= 0 ? '+' : ''}{ch.meanDelta.toFixed(2)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Comparison Visualizer */}
              {selectedComparisonSignal && comparisonResult.channels[selectedComparisonSignal] && (
                <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow space-y-3 font-sans">
                  <div className="flex items-center justify-between text-xs">
                    <span className="font-bold text-white">
                      OVERLAID CURVES & DELTA: {selectedComparisonSignal}
                    </span>
                    <div className="flex items-center space-x-3 text-xs">
                      <span className="flex items-center space-x-1">
                        <span className="w-2.5 h-2.5 rounded-full bg-sky-400" />
                        <span className="text-slate-300">Run A (Baseline)</span>
                      </span>
                      <span className="flex items-center space-x-1">
                        <span className="w-2.5 h-2.5 rounded-full bg-purple-400" />
                        <span className="text-slate-300">Run B (Tuned)</span>
                      </span>
                      <span className="flex items-center space-x-1">
                        <span className="w-2.5 h-2.5 rounded-full bg-emerald-400" />
                        <span className="text-slate-300">Delta (B - A)</span>
                      </span>
                    </div>
                  </div>

                  {/* Render Custom Comparison Canvas */}
                  <div className="h-64 w-full bg-can-bg rounded border border-can-border p-2">
                    <ComparisonChartViewer
                      comparisonData={comparisonResult.channels[selectedComparisonSignal]}
                      alignmentMode={comparisonResult.alignment}
                    />
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
};

// Internal mini-component for rendering the comparison curves and delta
const ComparisonChartViewer: React.FC<{
  comparisonData: any;
  alignmentMode: ComparisonAlignmentMode;
}> = ({ comparisonData, alignmentMode }) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
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
    const padding = { top: 15, right: 25, bottom: 25, left: 45 };
    const plotW = w - padding.left - padding.right;
    const plotH = h - padding.top - padding.bottom;

    ctx.clearRect(0, 0, w, h);

    const axis = comparisonData.alignedAxisValues || [];
    const runA = comparisonData.runAValues || [];
    const runB = comparisonData.runBValues || [];
    const delta = comparisonData.deltaValues || [];

    if (axis.length < 2) return;

    // Y limits
    const allVals = [...runA, ...runB];
    let minVal = Math.min(...allVals);
    let maxVal = Math.max(...allVals);
    if (minVal === maxVal) {
      minVal -= 1;
      maxVal += 1;
    }

    // Grid lines
    ctx.strokeStyle = '#1E293B';
    ctx.lineWidth = 1;
    for (let i = 0; i <= 4; i++) {
      const y = padding.top + (plotH / 4) * i;
      ctx.beginPath();
      ctx.moveTo(padding.left, y);
      ctx.lineTo(w - padding.right, y);
      ctx.stroke();
    }

    // Draw Run A line (Sky blue)
    ctx.strokeStyle = '#38BDF8';
    ctx.lineWidth = 2;
    ctx.beginPath();
    for (let i = 0; i < axis.length; i++) {
      const x = padding.left + (i / (axis.length - 1)) * plotW;
      const normY = (runA[i] - minVal) / (maxVal - minVal);
      const y = padding.top + plotH - normY * plotH;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.stroke();

    // Draw Run B line (Purple)
    ctx.strokeStyle = '#C084FC';
    ctx.lineWidth = 2;
    ctx.beginPath();
    for (let i = 0; i < axis.length; i++) {
      const x = padding.left + (i / (axis.length - 1)) * plotW;
      const normY = (runB[i] - minVal) / (maxVal - minVal);
      const y = padding.top + plotH - normY * plotH;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.stroke();

    // Draw Delta line (Emerald dashed)
    let deltaMin = Math.min(...delta);
    let deltaMax = Math.max(...delta);
    if (deltaMin === deltaMax) {
      deltaMin -= 1;
      deltaMax += 1;
    }

    ctx.strokeStyle = '#34D399';
    ctx.lineWidth = 1.5;
    ctx.setLineDash([3, 3]);
    ctx.beginPath();
    for (let i = 0; i < axis.length; i++) {
      const x = padding.left + (i / (axis.length - 1)) * plotW;
      const normY = (delta[i] - deltaMin) / (deltaMax - deltaMin);
      const y = padding.top + plotH - normY * (plotH * 0.4); // compressed height
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.stroke();
    ctx.setLineDash([]);
  }, [comparisonData, alignmentMode]);

  return <canvas ref={canvasRef} className="w-full h-full block" />;
};
