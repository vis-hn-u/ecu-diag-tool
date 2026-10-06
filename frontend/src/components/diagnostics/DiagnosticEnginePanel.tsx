import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import {
  DiagnosticSessionStatusDto,
  DiagnosticSessionTypeValue,
  DtcRecordDto,
  DtcClearResultDto,
  LiveDataSampleDto,
  RoutineDescriptorDto,
  RoutineExecutionResultDto,
  EcuResetResultDto,
  DiagnosticTraceEntryDto
} from '../../types';
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock,
  Cpu,
  Flame,
  HelpCircle,
  Play,
  Radio,
  RefreshCw,
  RotateCcw,
  Search,
  ShieldAlert,
  ShieldCheck,
  Square,
  Terminal,
  Trash2,
  Zap
} from 'lucide-react';

interface DiagnosticEnginePanelProps {
  requestCanId: number;
  responseCanId: number;
  ecuName: string;
}

export const DiagnosticEnginePanel: React.FC<DiagnosticEnginePanelProps> = ({
  requestCanId,
  responseCanId,
  ecuName
}) => {
  const [activeSubView, setActiveSubView] = useState<'sessions' | 'dtcs' | 'livedata' | 'routines' | 'reset' | 'trace'>('sessions');
  const [statusMessage, setStatusMessage] = useState<{ text: string; isError?: boolean } | null>(null);

  // 1. Session State
  const [sessionStatus, setSessionStatus] = useState<DiagnosticSessionStatusDto | null>(null);
  const [loadingSession, setLoadingSession] = useState(false);
  const [testerPresentInterval, setTesterPresentInterval] = useState(2000);
  const [suppressPosResponse, setSuppressPosResponse] = useState(false);

  // 2. DTC State
  const [dtcs, setDtcs] = useState<DtcRecordDto[]>([]);
  const [loadingDtcs, setLoadingDtcs] = useState(false);
  const [showClearModal, setShowClearModal] = useState(false);
  const [clearConfirmed, setClearConfirmed] = useState(false);
  const [clearReason, setClearReason] = useState('Service verification and physical repair completion');
  const [clearResult, setClearResult] = useState<DtcClearResultDto | null>(null);
  const [clearHistory, setClearHistory] = useState<DtcClearResultDto[]>([]);

  // 3. Live Data State
  const [liveSamples, setLiveSamples] = useState<LiveDataSampleDto[]>([]);
  const [isPolling, setIsPolling] = useState(false);
  const [pollIntervalMs, setPollIntervalMs] = useState(250);

  // 4. Routine State
  const [routines, setRoutines] = useState<RoutineDescriptorDto[]>([]);
  const [selectedRoutine, setSelectedRoutine] = useState<RoutineDescriptorDto | null>(null);
  const [showRoutineModal, setShowRoutineModal] = useState(false);
  const [routineConfirmed, setRoutineConfirmed] = useState(false);
  const [executingRoutine, setExecutingRoutine] = useState(false);
  const [routineHistory, setRoutineHistory] = useState<RoutineExecutionResultDto[]>([]);

  // 5. Reset State
  const [resetType, setResetType] = useState<number>(3); // 3: Soft Reset
  const [showResetModal, setShowResetModal] = useState(false);
  const [resetConfirmed, setResetConfirmed] = useState(false);
  const [resettingEcu, setResettingEcu] = useState(false);
  const [resetHistory, setResetHistory] = useState<EcuResetResultDto[]>([]);

  // 6. Trace State
  const [traces, setTraces] = useState<DiagnosticTraceEntryDto[]>([]);
  const [traceFilterResult, setTraceFilterResult] = useState<string>('all');

  // Load initial data
  useEffect(() => {
    refreshSessionStatus();
    loadDtcs();
    loadRoutines();
    loadTraces();
  }, [requestCanId, ecuName]);

  // Periodic polling for live data & session status
  useEffect(() => {
    if (!isPolling) return;
    const timer = setInterval(async () => {
      try {
        const samples = await api.getLiveDataSamples();
        setLiveSamples(samples);
      } catch {}
    }, pollIntervalMs);
    return () => clearInterval(timer);
  }, [isPolling, pollIntervalMs]);

  const refreshSessionStatus = async () => {
    try {
      const st = await api.getDiagnosticSessionStatus();
      setSessionStatus(st);
    } catch {}
  };

  const handleSwitchSession = async (sessionType: DiagnosticSessionTypeValue) => {
    setLoadingSession(true);
    setStatusMessage(null);
    try {
      await api.switchDiagnosticSession(requestCanId, sessionType, ecuName);
      setStatusMessage({ text: `Diagnostic session successfully switched to ${sessionType}.` });
      await refreshSessionStatus();
    } catch (err: any) {
      setStatusMessage({ text: `Session switch failed: ${err.message}`, isError: true });
    } finally {
      setLoadingSession(false);
    }
  };

  const handleToggleTesterPresent = async (enable: boolean) => {
    try {
      await api.setTesterPresent(requestCanId, ecuName, testerPresentInterval, suppressPosResponse, enable);
      await refreshSessionStatus();
      setStatusMessage({ text: enable ? 'TesterPresent (0x3E) watchdog activated.' : 'TesterPresent stopped.' });
    } catch (err: any) {
      setStatusMessage({ text: `TesterPresent error: ${err.message}`, isError: true });
    }
  };

  const loadDtcs = async () => {
    setLoadingDtcs(true);
    try {
      const data = await api.readDiagnosticDtcs(requestCanId, 0xFF, ecuName);
      setDtcs(data);
      const hist = await api.getDtcClearHistory(ecuName);
      setClearHistory(hist);
    } catch (err: any) {
      setStatusMessage({ text: `Failed to read DTCs: ${err.message}`, isError: true });
    } finally {
      setLoadingDtcs(false);
    }
  };

  const handleExecuteSafeClear = async () => {
    setShowClearModal(false);
    setStatusMessage(null);
    try {
      const res = await api.clearDiagnosticDtcsSafe({
        ecuName,
        requestCanId,
        groupOfDtc: 0xFFFFFF,
        requireUserConfirmation: true,
        userConfirmed: clearConfirmed,
        confirmationReason: clearReason
      });
      setClearResult(res);
      await loadDtcs();
      setStatusMessage({ text: `DTC Clear executed: ${res.clearedCodes.length} cleared, ${res.returningCodes.length} returning.` });
    } catch (err: any) {
      setStatusMessage({ text: `Safe DTC clear rejected: ${err.message}`, isError: true });
    }
  };

  const loadRoutines = async () => {
    try {
      const list = await api.getDiagnosticRoutines();
      setRoutines(list);
      const hist = await api.getRoutineExecutionHistory();
      setRoutineHistory(hist);
    } catch {}
  };

  const handleExecuteRoutine = async () => {
    if (!selectedRoutine) return;
    setShowRoutineModal(false);
    setExecutingRoutine(true);
    setStatusMessage(null);
    try {
      const res = await api.executeDiagnosticRoutine({
        ecuName,
        requestCanId,
        routineId: selectedRoutine.routineId,
        subFunction: 0x01,
        requireUserConfirmation: true,
        userConfirmed: routineConfirmed,
        confirmationReason: `Operator bench test for ${selectedRoutine.name}`
      });
      const hist = await api.getRoutineExecutionHistory();
      setRoutineHistory(hist);
      setStatusMessage({ text: `Routine 0x${selectedRoutine.routineId.toString(16).toUpperCase()} execution completed (${res.executionLatencyMs.toFixed(1)}ms).` });
    } catch (err: any) {
      setStatusMessage({ text: `Routine execution failed: ${err.message}`, isError: true });
    } finally {
      setExecutingRoutine(false);
    }
  };

  const handleExecuteEcuReset = async () => {
    setShowResetModal(false);
    setResettingEcu(true);
    setStatusMessage(null);
    try {
      const res = await api.executeEcuReset({
        ecuName,
        requestCanId,
        resetType,
        requireUserConfirmation: true,
        userConfirmed: resetConfirmed,
        confirmationReason: 'Operator requested diagnostic module reset'
      });
      const hist = await api.getEcuResetHistory();
      setResetHistory(hist);
      await refreshSessionStatus();
      setStatusMessage({ text: `ECU Reset completed. Communication restored: ${res.communicationRestored ? 'YES' : 'NO'}` });
    } catch (err: any) {
      setStatusMessage({ text: `ECU Reset rejected: ${err.message}`, isError: true });
    } finally {
      setResettingEcu(false);
    }
  };

  const loadTraces = async () => {
    try {
      const t = await api.getDiagnosticTraces(200);
      setTraces(t);
    } catch {}
  };

  return (
    <div className="space-y-4">
      {/* Top Bar Navigation */}
      <div className="flex flex-wrap items-center justify-between gap-3 bg-can-card p-3 rounded-lg border border-can-border">
        <div className="flex items-center space-x-2">
          <button
            onClick={() => setActiveSubView('sessions')}
            className={`px-3 py-1.5 rounded text-xs font-medium transition-colors ${
              activeSubView === 'sessions' ? 'bg-sky-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
            }`}
          >
            Sessions & TesterPresent
          </button>
          <button
            onClick={() => setActiveSubView('dtcs')}
            className={`px-3 py-1.5 rounded text-xs font-medium transition-colors ${
              activeSubView === 'dtcs' ? 'bg-sky-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
            }`}
          >
            DTCs & Clear Safety ({dtcs.length})
          </button>
          <button
            onClick={() => setActiveSubView('livedata')}
            className={`px-3 py-1.5 rounded text-xs font-medium transition-colors ${
              activeSubView === 'livedata' ? 'bg-sky-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
            }`}
          >
            Live Data & Gauges
          </button>
          <button
            onClick={() => setActiveSubView('routines')}
            className={`px-3 py-1.5 rounded text-xs font-medium transition-colors ${
              activeSubView === 'routines' ? 'bg-sky-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
            }`}
          >
            Routine Controls (0x31)
          </button>
          <button
            onClick={() => setActiveSubView('reset')}
            className={`px-3 py-1.5 rounded text-xs font-medium transition-colors ${
              activeSubView === 'reset' ? 'bg-sky-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
            }`}
          >
            ECU Reset (0x11)
          </button>
          <button
            onClick={() => setActiveSubView('trace')}
            className={`px-3 py-1.5 rounded text-xs font-medium transition-colors ${
              activeSubView === 'trace' ? 'bg-sky-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
            }`}
          >
            Diagnostic Traces ({traces.length})
          </button>
        </div>

        <div className="flex items-center space-x-2 text-xs font-mono">
          <span className="text-slate-400">STATE:</span>
          <span className="px-2 py-0.5 rounded font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
            {sessionStatus?.currentState || 'Disconnected'}
          </span>
        </div>
      </div>

      {statusMessage && (
        <div className={`p-3 rounded text-xs flex justify-between items-center ${
          statusMessage.isError ? 'bg-rose-500/10 text-rose-300 border border-rose-500/30' : 'bg-emerald-500/10 text-emerald-300 border border-emerald-500/30'
        }`}>
          <span>{statusMessage.text}</span>
          <button onClick={() => setStatusMessage(null)}>✕</button>
        </div>
      )}

      {/* 1. SESSIONS & TESTER PRESENT VIEW */}
      {activeSubView === 'sessions' && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="bg-can-card p-4 rounded-lg border border-can-border space-y-4">
            <h3 className="text-sm font-semibold text-white flex items-center space-x-2">
              <Terminal className="w-4 h-4 text-sky-400" />
              <span>ISO 14229-1 Diagnostic Session Control (0x10)</span>
            </h3>

            <div className="space-y-3">
              <div className="p-3 bg-slate-900/60 rounded border border-slate-800 space-y-2">
                <div className="flex justify-between text-xs font-mono">
                  <span className="text-slate-400">Current Session State:</span>
                  <span className="text-sky-300 font-semibold">{sessionStatus?.currentState}</span>
                </div>
                <div className="flex justify-between text-xs font-mono">
                  <span className="text-slate-400">Active UDS Type:</span>
                  <span className="text-emerald-300">{sessionStatus?.activeUdsSession || 'DefaultSession (0x01)'}</span>
                </div>
                <div className="flex justify-between text-xs font-mono">
                  <span className="text-slate-400">P2 Server Timing:</span>
                  <span className="text-slate-200">{sessionStatus?.p2ServerMs || 50} ms</span>
                </div>
                <div className="flex justify-between text-xs font-mono">
                  <span className="text-slate-400">P2* Extended Timing:</span>
                  <span className="text-slate-200">{sessionStatus?.p2StarServerMs || 5000} ms</span>
                </div>
              </div>

              <div className="grid grid-cols-3 gap-2">
                <button
                  onClick={() => handleSwitchSession('DefaultSession')}
                  disabled={loadingSession}
                  className="px-3 py-2 rounded text-xs font-medium bg-slate-800 hover:bg-slate-700 text-white border border-slate-700 transition-colors"
                >
                  Default (0x01)
                </button>
                <button
                  onClick={() => handleSwitchSession('ExtendedDiagnosticSession')}
                  disabled={loadingSession}
                  className="px-3 py-2 rounded text-xs font-medium bg-sky-600 hover:bg-sky-500 text-white transition-colors"
                >
                  Extended (0x03)
                </button>
                <button
                  onClick={() => handleSwitchSession('ProgrammingSession')}
                  disabled={loadingSession}
                  className="px-3 py-2 rounded text-xs font-medium bg-amber-600/80 hover:bg-amber-600 text-white transition-colors"
                >
                  Programming (0x02)
                </button>
              </div>
            </div>
          </div>

          <div className="bg-can-card p-4 rounded-lg border border-can-border space-y-4">
            <h3 className="text-sm font-semibold text-white flex items-center space-x-2">
              <Clock className="w-4 h-4 text-emerald-400" />
              <span>TesterPresent Watchdog (0x3E)</span>
            </h3>

            <div className="space-y-3">
              <div className="p-3 bg-slate-900/60 rounded border border-slate-800 space-y-2 text-xs">
                <div className="flex justify-between">
                  <span className="text-slate-400">Watchdog Active:</span>
                  <span className={sessionStatus?.isTesterPresentActive ? 'text-emerald-400 font-semibold' : 'text-slate-500'}>
                    {sessionStatus?.isTesterPresentActive ? 'YES (RUNNING)' : 'NO (STOPPED)'}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Keep-Alive Interval:</span>
                  <span className="font-mono text-slate-200">{testerPresentInterval} ms</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Suppress Positive Response (0x80):</span>
                  <span className="font-mono text-slate-200">{suppressPosResponse ? 'Enabled' : 'Disabled'}</span>
                </div>
              </div>

              <div className="flex items-center space-x-2">
                {sessionStatus?.isTesterPresentActive ? (
                  <button
                    onClick={() => handleToggleTesterPresent(false)}
                    className="flex-1 px-3 py-2 rounded text-xs font-medium bg-rose-600 hover:bg-rose-500 text-white flex items-center justify-center space-x-1"
                  >
                    <Square className="w-3.5 h-3.5" />
                    <span>Stop TesterPresent</span>
                  </button>
                ) : (
                  <button
                    onClick={() => handleToggleTesterPresent(true)}
                    className="flex-1 px-3 py-2 rounded text-xs font-medium bg-emerald-600 hover:bg-emerald-500 text-white flex items-center justify-center space-x-1"
                  >
                    <Play className="w-3.5 h-3.5" />
                    <span>Start TesterPresent</span>
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* 2. DTCS & SAFE CLEAR VIEW */}
      {activeSubView === 'dtcs' && (
        <div className="space-y-4">
          <div className="flex justify-between items-center bg-can-card p-3 rounded-lg border border-can-border">
            <div className="flex items-center space-x-2">
              <button
                onClick={loadDtcs}
                disabled={loadingDtcs}
                className="flex items-center space-x-1.5 px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-medium transition-colors"
              >
                <RefreshCw className={`w-3.5 h-3.5 ${loadingDtcs ? 'animate-spin' : ''}`} />
                <span>Read DTCs (0x19 0x02)</span>
              </button>
              <button
                onClick={() => {
                  setClearConfirmed(false);
                  setShowClearModal(true);
                }}
                disabled={dtcs.length === 0}
                className="flex items-center space-x-1.5 px-3 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-medium transition-colors disabled:opacity-50"
              >
                <Trash2 className="w-3.5 h-3.5" />
                <span>Safe Clear DTCs (0x14)</span>
              </button>
            </div>
            <div className="text-xs text-slate-400 font-mono">
              Total Faults: <span className="text-white font-bold">{dtcs.length}</span>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {dtcs.map(dtc => (
              <div key={dtc.code} className="bg-can-card p-3 rounded-lg border border-can-border space-y-2">
                <div className="flex justify-between items-start">
                  <div>
                    <span className="text-sm font-mono font-bold text-amber-400">{dtc.code}</span>
                    <h4 className="text-xs font-medium text-white">{dtc.description}</h4>
                  </div>
                  <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-400">
                    Status: 0x{dtc.statusByte.toString(16).toUpperCase().padStart(2, '0')}
                  </span>
                </div>
                <div className="flex flex-wrap gap-1">
                  {dtc.formattedFlags.map(f => (
                    <span key={f} className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-900 text-sky-300 border border-slate-800">
                      {f}
                    </span>
                  ))}
                </div>
              </div>
            ))}
          </div>

          {clearResult && (
            <div className="bg-can-card p-3 rounded-lg border border-emerald-500/30 bg-emerald-500/5 space-y-2">
              <h4 className="text-xs font-semibold text-emerald-400">Post-Clear Verification Diff</h4>
              <div className="grid grid-cols-2 gap-2 text-xs">
                <div>
                  <span className="text-slate-400">Cleared DTCs:</span>
                  <div className="text-emerald-300 font-mono">{clearResult.clearedCodes.join(', ') || 'None'}</div>
                </div>
                <div>
                  <span className="text-slate-400">Returning / Persistent DTCs:</span>
                  <div className="text-rose-400 font-mono">{clearResult.returningCodes.join(', ') || 'None'}</div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* 3. LIVE DATA & GAUGES */}
      {activeSubView === 'livedata' && (
        <div className="space-y-4">
          <div className="flex justify-between items-center bg-can-card p-3 rounded-lg border border-can-border">
            <div className="flex items-center space-x-2">
              {isPolling ? (
                <button
                  onClick={() => setIsPolling(false)}
                  className="flex items-center space-x-1.5 px-3 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-medium"
                >
                  <Square className="w-3.5 h-3.5" />
                  <span>Stop Polling</span>
                </button>
              ) : (
                <button
                  onClick={() => setIsPolling(true)}
                  className="flex items-center space-x-1.5 px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-medium"
                >
                  <Play className="w-3.5 h-3.5" />
                  <span>Start Live Polling (UDS 0x22)</span>
                </button>
              )}
            </div>
            <div className="text-xs text-slate-400">
              Update Rate: <span className="text-white font-mono">{pollIntervalMs} ms</span>
            </div>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
            {liveSamples.map(sample => (
              <div key={sample.parameterId} className="bg-can-card p-3 rounded-lg border border-can-border space-y-1">
                <div className="flex justify-between items-start">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">{sample.category}</span>
                  <span className={`text-[10px] px-1.5 py-0.5 rounded font-mono font-medium ${
                    sample.quality === 'Valid' ? 'bg-emerald-500/10 text-emerald-400' : 'bg-rose-500/10 text-rose-400'
                  }`}>
                    {sample.quality}
                  </span>
                </div>
                <div className="text-xs font-medium text-slate-300">{sample.name}</div>
                <div className="text-lg font-bold font-mono text-white">
                  {sample.scaledValue.toFixed(1)} <span className="text-xs text-sky-400">{sample.unit}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* 4. ROUTINE CONTROLS */}
      {activeSubView === 'routines' && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            {routines.map(routine => (
              <div key={routine.routineId} className="bg-can-card p-4 rounded-lg border border-can-border space-y-3">
                <div className="flex justify-between items-start">
                  <span className="text-xs font-mono font-bold text-sky-400">0x{routine.routineId.toString(16).toUpperCase()}</span>
                  <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-400">
                    {routine.expectedDurationSeconds}s Test
                  </span>
                </div>
                <div>
                  <h4 className="text-sm font-semibold text-white">{routine.name}</h4>
                  <p className="text-xs text-slate-400 mt-1">{routine.description}</p>
                </div>
                <button
                  onClick={() => {
                    setSelectedRoutine(routine);
                    setRoutineConfirmed(false);
                    setShowRoutineModal(true);
                  }}
                  className="w-full px-3 py-1.5 rounded text-xs font-medium bg-sky-600 hover:bg-sky-500 text-white transition-colors"
                >
                  Actuate Routine
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* 5. ECU RESET */}
      {activeSubView === 'reset' && (
        <div className="bg-can-card p-4 rounded-lg border border-can-border max-w-xl mx-auto space-y-4">
          <h3 className="text-sm font-semibold text-white flex items-center space-x-2">
            <RotateCcw className="w-4 h-4 text-amber-400" />
            <span>ISO 14229-1 ECU Reset (Service 0x11)</span>
          </h3>

          <div className="p-3 bg-amber-500/10 border border-amber-500/30 rounded text-xs text-amber-300 flex items-start space-x-2">
            <ShieldAlert className="w-4 h-4 mt-0.5 shrink-0" />
            <span>
              CRITICAL: Resetting the ECU will reboot the microcontroller. Ongoing memory reads or flash operations will be aborted.
            </span>
          </div>

          <div className="space-y-2">
            <label className="text-xs text-slate-400">Reset Type:</label>
            <select
              value={resetType}
              onChange={e => setResetType(parseInt(e.target.value))}
              className="w-full bg-slate-900 border border-slate-700 rounded px-3 py-2 text-xs text-white"
            >
              <option value={3}>0x03: Soft Reset (Processor Software Restart)</option>
              <option value={1}>0x01: Hard Reset (Power-On Reset Simulation)</option>
              <option value={2}>0x02: KeyOffOnReset (Simulate Ignition Cycle)</option>
            </select>
          </div>

          <button
            onClick={() => {
              setResetConfirmed(false);
              setShowResetModal(true);
            }}
            disabled={resettingEcu}
            className="w-full py-2 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-medium transition-colors"
          >
            Command ECU Reset
          </button>
        </div>
      )}

      {/* 6. DIAGNOSTIC TRACE */}
      {activeSubView === 'trace' && (
        <div className="bg-can-card p-4 rounded-lg border border-can-border space-y-3">
          <div className="flex justify-between items-center">
            <h3 className="text-xs font-semibold text-white">Live Diagnostic Trace Buffer</h3>
            <button onClick={() => api.clearDiagnosticTraces().then(loadTraces)} className="text-xs text-rose-400 hover:text-rose-300">
              Clear Traces
            </button>
          </div>

          <div className="overflow-x-auto max-h-96">
            <table className="w-full text-left text-[11px] font-mono">
              <thead className="bg-slate-900/80 text-slate-400 border-b border-slate-800">
                <tr>
                  <th className="p-2">Time</th>
                  <th className="p-2">Dir</th>
                  <th className="p-2">CAN ID</th>
                  <th className="p-2">SID</th>
                  <th className="p-2">Result</th>
                  <th className="p-2">Payload</th>
                  <th className="p-2">Latency</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800">
                {traces.map(t => (
                  <tr key={t.id} className="hover:bg-slate-800/40">
                    <td className="p-2 text-slate-500">{new Date(t.timestampUtc).toLocaleTimeString()}</td>
                    <td className="p-2">
                      <span className={`px-1 rounded ${t.direction === 'Tx' ? 'bg-sky-500/20 text-sky-400' : 'bg-emerald-500/20 text-emerald-400'}`}>
                        {t.direction}
                      </span>
                    </td>
                    <td className="p-2 text-slate-300">0x{t.direction === 'Tx' ? t.requestCanId.toString(16) : t.responseCanId.toString(16)}</td>
                    <td className="p-2 text-slate-300">0x{t.serviceId.toString(16).toUpperCase()}</td>
                    <td className="p-2">
                      <span className={t.result === 'PositiveResponse' ? 'text-emerald-400' : 'text-rose-400'}>
                        {t.result}
                      </span>
                    </td>
                    <td className="p-2 text-slate-400">{t.formattedHex}</td>
                    <td className="p-2 text-slate-400">{t.latencyMs.toFixed(1)} ms</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* MODAL: Clear DTCs Confirmation */}
      {showClearModal && (
        <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-can-border p-5 rounded-lg max-w-md w-full space-y-4">
            <h3 className="text-sm font-semibold text-white flex items-center space-x-2">
              <ShieldAlert className="w-5 h-5 text-rose-500" />
              <span>Confirm Clear Diagnostic Trouble Codes</span>
            </h3>
            <p className="text-xs text-slate-300">
              Clearing DTCs is a state-changing diagnostic operation. The platform will snapshot pre-clear faults, execute UDS 0x14, re-read DTCs, and preserve the original evidence.
            </p>
            <div className="p-3 bg-slate-800/60 rounded text-xs space-y-1 font-mono">
              <div>ECU: <span className="text-white">{ecuName}</span></div>
              <div>DTC Count: <span className="text-amber-400">{dtcs.length}</span></div>
            </div>
            <label className="flex items-center space-x-2 text-xs text-slate-300 cursor-pointer">
              <input
                type="checkbox"
                checked={clearConfirmed}
                onChange={e => setClearConfirmed(e.target.checked)}
                className="rounded border-slate-700 text-sky-600 focus:ring-0"
              />
              <span>I confirm that I authorize clearing DTCs on this controller.</span>
            </label>
            <div className="flex justify-end space-x-2 pt-2">
              <button onClick={() => setShowClearModal(false)} className="px-3 py-1.5 text-xs text-slate-400 hover:text-white">
                Cancel
              </button>
              <button
                onClick={handleExecuteSafeClear}
                disabled={!clearConfirmed}
                className="px-4 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-medium disabled:opacity-50"
              >
                Execute Clear
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL: Routine Execution Confirmation */}
      {showRoutineModal && selectedRoutine && (
        <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-can-border p-5 rounded-lg max-w-md w-full space-y-4">
            <h3 className="text-sm font-semibold text-white">Actuate Diagnostic Routine</h3>
            <p className="text-xs text-slate-300">{selectedRoutine.description}</p>
            <div className="p-3 bg-slate-800/60 rounded text-xs space-y-1">
              <div className="font-semibold text-slate-300">Preconditions:</div>
              {selectedRoutine.safetyPreconditions.map(p => (
                <div key={p} className="text-slate-400 font-mono">• {p}</div>
              ))}
            </div>
            <label className="flex items-center space-x-2 text-xs text-slate-300 cursor-pointer">
              <input
                type="checkbox"
                checked={routineConfirmed}
                onChange={e => setRoutineConfirmed(e.target.checked)}
                className="rounded border-slate-700 text-sky-600"
              />
              <span>I confirm the vehicle is in a safe condition for actuation.</span>
            </label>
            <div className="flex justify-end space-x-2 pt-2">
              <button onClick={() => setShowRoutineModal(false)} className="px-3 py-1.5 text-xs text-slate-400 hover:text-white">
                Cancel
              </button>
              <button
                onClick={handleExecuteRoutine}
                disabled={!routineConfirmed || executingRoutine}
                className="px-4 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-medium disabled:opacity-50"
              >
                {executingRoutine ? 'Executing...' : 'Start Routine'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL: ECU Reset Confirmation */}
      {showResetModal && (
        <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-can-border p-5 rounded-lg max-w-md w-full space-y-4">
            <h3 className="text-sm font-semibold text-white flex items-center space-x-2">
              <ShieldAlert className="w-5 h-5 text-rose-500" />
              <span>Confirm ECU Reset</span>
            </h3>
            <p className="text-xs text-slate-300">
              Are you sure you want to command an ECU Reset? The module will temporarily drop from the CAN bus while executing reboot sequence.
            </p>
            <label className="flex items-center space-x-2 text-xs text-slate-300 cursor-pointer">
              <input
                type="checkbox"
                checked={resetConfirmed}
                onChange={e => setResetConfirmed(e.target.checked)}
                className="rounded border-slate-700 text-rose-600"
              />
              <span>I confirm no memory writing or flashing is in progress.</span>
            </label>
            <div className="flex justify-end space-x-2 pt-2">
              <button onClick={() => setShowResetModal(false)} className="px-3 py-1.5 text-xs text-slate-400 hover:text-white">
                Cancel
              </button>
              <button
                onClick={handleExecuteEcuReset}
                disabled={!resetConfirmed || resettingEcu}
                className="px-4 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-medium disabled:opacity-50"
              >
                {resettingEcu ? 'Resetting...' : 'Confirm Reset'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
