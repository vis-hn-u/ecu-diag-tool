import React, { useState, useCallback } from 'react';
import { api } from '../../services/api';
import {
  EcuHealthReportDto,
  EcuHealthStatusType,
  SensorPlausibilityReportDto,
  SensorPlausibilityCheckResultDto,
  OemNormalizationPlanDto,
  OemTargetParameterDto,
  DiagnosticRootCauseCandidateDto,
  SensorPlausibilityStateType
} from '../../types';
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  HelpCircle,
  Info,
  RefreshCw,
  Shield,
  ShieldAlert,
  ShieldCheck,
  Thermometer,
  TrendingDown,
  TrendingUp,
  Wrench,
  Zap,
  Target,
  BarChart2,
  AlertCircle
} from 'lucide-react';

interface EcuHealthPanelProps {
  ecuName: string;
  requestCanId: number;
  responseCanId: number;
}

// ─── Health status helpers ──────────────────────────────────────────────────

function healthColor(status: EcuHealthStatusType) {
  switch (status) {
    case 'Normal':  return 'text-emerald-400';
    case 'Warning': return 'text-amber-400';
    case 'Fault':   return 'text-rose-400';
    default:        return 'text-slate-400';
  }
}

function healthBg(status: EcuHealthStatusType) {
  switch (status) {
    case 'Normal':  return 'bg-emerald-500/10 border-emerald-500/30';
    case 'Warning': return 'bg-amber-500/10 border-amber-500/30';
    case 'Fault':   return 'bg-rose-500/10 border-rose-500/30';
    default:        return 'bg-slate-500/10 border-slate-500/30';
  }
}

function healthIcon(status: EcuHealthStatusType) {
  switch (status) {
    case 'Normal':  return <ShieldCheck className="w-5 h-5 text-emerald-400" />;
    case 'Warning': return <AlertTriangle className="w-5 h-5 text-amber-400" />;
    case 'Fault':   return <ShieldAlert className="w-5 h-5 text-rose-400" />;
    default:        return <HelpCircle className="w-5 h-5 text-slate-400" />;
  }
}

function plausibilityColor(state: SensorPlausibilityStateType) {
  switch (state) {
    case 'Plausible':          return 'text-emerald-400';
    case 'Questionable':       return 'text-amber-400';
    case 'OutOfRange':         return 'text-rose-400';
    case 'Inconsistent':       return 'text-rose-400';
    case 'InsufficientData':   return 'text-slate-400';
    default:                   return 'text-slate-400';
  }
}

function plausibilityBadgeBg(state: SensorPlausibilityStateType) {
  switch (state) {
    case 'Plausible':          return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30';
    case 'Questionable':       return 'bg-amber-500/10 text-amber-400 border-amber-500/30';
    case 'OutOfRange':         return 'bg-rose-500/10 text-rose-400 border-rose-500/30';
    case 'Inconsistent':       return 'bg-rose-500/10 text-rose-400 border-rose-500/30';
    default:                   return 'bg-slate-500/10 text-slate-400 border-slate-500/30';
  }
}

function confidenceColor(c: string) {
  switch (c) {
    case 'Confirmed': return 'text-rose-400';
    case 'Likely':    return 'text-amber-400';
    case 'Possible':  return 'text-yellow-400';
    default:          return 'text-slate-400';
  }
}

// ─── Score ring ──────────────────────────────────────────────────────────────

const ScoreRing: React.FC<{ score: number; status: EcuHealthStatusType }> = ({ score, status }) => {
  const r = 40;
  const circ = 2 * Math.PI * r;
  const filled = circ * (score / 100);
  const stroke = status === 'Normal' ? '#34d399' : status === 'Warning' ? '#fbbf24' : '#f87171';

  return (
    <svg width={100} height={100} viewBox="0 0 100 100" className="mx-auto">
      <circle cx={50} cy={50} r={r} fill="none" stroke="#1e293b" strokeWidth={10} />
      <circle
        cx={50} cy={50} r={r}
        fill="none"
        stroke={stroke}
        strokeWidth={10}
        strokeDasharray={`${filled} ${circ - filled}`}
        strokeLinecap="round"
        transform="rotate(-90 50 50)"
        style={{ transition: 'stroke-dasharray 0.5s ease' }}
      />
      <text x={50} y={54} textAnchor="middle" fontSize={16} fontWeight="bold" fill={stroke}>{score}%</text>
    </svg>
  );
};

// ─── Root-cause tree row ─────────────────────────────────────────────────────

const RootCauseRow: React.FC<{ candidate: DiagnosticRootCauseCandidateDto; index: number }> = ({ candidate, index }) => {
  const [expanded, setExpanded] = useState(false);
  return (
    <div className="border border-can-border rounded-lg overflow-hidden">
      <button
        onClick={() => setExpanded(e => !e)}
        className="w-full flex items-center justify-between p-3 bg-can-card hover:bg-slate-800/50 transition-colors text-left"
      >
        <div className="flex items-center space-x-3">
          <span className="w-5 h-5 flex items-center justify-center rounded-full bg-slate-700 text-[10px] font-bold text-slate-300">
            {index + 1}
          </span>
          <div>
            <span className="text-xs font-semibold text-white">{candidate.suspectComponent}</span>
            <span className={`ml-2 text-[10px] font-mono uppercase px-1.5 py-0.5 rounded border ${
              candidate.confidence === 'Confirmed' ? 'bg-rose-500/15 text-rose-400 border-rose-500/30' :
              candidate.confidence === 'Likely'    ? 'bg-amber-500/15 text-amber-400 border-amber-500/30' :
              candidate.confidence === 'Possible'  ? 'bg-yellow-500/15 text-yellow-400 border-yellow-500/30' :
              'bg-slate-500/15 text-slate-400 border-slate-500/30'
            }`}>
              {candidate.confidence}
            </span>
          </div>
        </div>
        {expanded ? <ChevronDown className="w-3.5 h-3.5 text-slate-400" /> : <ChevronRight className="w-3.5 h-3.5 text-slate-400" />}
      </button>

      {expanded && (
        <div className="p-3 bg-can-panel/50 border-t border-can-border space-y-3">
          <p className="text-xs text-slate-300">{candidate.description}</p>

          {candidate.supportingEvidence.length > 0 && (
            <div>
              <div className="text-[10px] font-semibold text-sky-400 uppercase mb-1 tracking-wide">Supporting Evidence</div>
              <ul className="space-y-0.5">
                {candidate.supportingEvidence.map((ev, i) => (
                  <li key={i} className="flex items-start space-x-1.5 text-[11px] text-slate-300">
                    <ChevronRight className="w-3 h-3 mt-0.5 text-sky-500 flex-shrink-0" />
                    <span>{ev}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {candidate.recommendedPhysicalChecks.length > 0 && (
            <div>
              <div className="text-[10px] font-semibold text-amber-400 uppercase mb-1 tracking-wide flex items-center space-x-1">
                <Wrench className="w-3 h-3" />
                <span>Recommended Physical Checks</span>
              </div>
              <ul className="space-y-0.5">
                {candidate.recommendedPhysicalChecks.map((check, i) => (
                  <li key={i} className="flex items-start space-x-1.5 text-[11px] text-amber-200/80">
                    <span className="text-amber-500 font-bold flex-shrink-0">{i + 1}.</span>
                    <span>{check}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

// ─── OEM parameter row ───────────────────────────────────────────────────────

const OemParamRow: React.FC<{ param: OemTargetParameterDto }> = ({ param }) => {
  const rangeWidth = param.oemTargetMax - param.oemTargetMin;
  const clampedValue = Math.min(Math.max(param.measuredValue, param.oemTargetMin - rangeWidth * 0.5), param.oemTargetMax + rangeWidth * 0.5);
  const visualMin = param.oemTargetMin - rangeWidth * 0.25;
  const visualMax = param.oemTargetMax + rangeWidth * 0.25;
  const visualRange = visualMax - visualMin;
  const minPct = ((param.oemTargetMin - visualMin) / visualRange) * 100;
  const maxPct = ((param.oemTargetMax - visualMin) / visualRange) * 100;
  const valuePct = ((clampedValue - visualMin) / visualRange) * 100;

  return (
    <tr className="hover:bg-slate-800/30">
      <td className="py-2 px-3 text-slate-200 text-xs font-medium">{param.parameterName}</td>
      <td className="py-2 px-3 text-slate-300 text-xs font-mono">
        {param.oemTargetMin}–{param.oemTargetMax} {param.unit}
      </td>
      <td className="py-2 px-3">
        <div className="flex items-center space-x-2">
          <span className={`text-xs font-mono font-bold ${param.isWithinTarget ? 'text-emerald-400' : 'text-rose-400'}`}>
            {param.measuredValue.toFixed(2)} {param.unit}
          </span>
        </div>
      </td>
      <td className="py-2 px-3 w-40">
        <div className="relative h-2 bg-slate-700 rounded-full">
          {/* OEM target band */}
          <div
            className="absolute inset-y-0 bg-emerald-500/25 rounded-full"
            style={{ left: `${minPct}%`, width: `${maxPct - minPct}%` }}
          />
          {/* Measured value marker */}
          <div
            className={`absolute w-2 h-2 rounded-full -translate-x-1/2 top-0 ${param.isWithinTarget ? 'bg-emerald-400' : 'bg-rose-400'}`}
            style={{ left: `${Math.min(Math.max(valuePct, 0), 100)}%` }}
          />
        </div>
      </td>
      <td className="py-2 px-3">
        {param.isWithinTarget ? (
          <span className="text-[10px] bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 px-1.5 py-0.5 rounded font-mono">OK</span>
        ) : (
          <span className="text-[10px] bg-rose-500/10 text-rose-400 border border-rose-500/30 px-1.5 py-0.5 rounded font-mono">
            {param.deviation > 0 ? '+' : ''}{param.deviation.toFixed(2)} {param.unit}
          </span>
        )}
      </td>
      <td className="py-2 px-3 text-[11px] text-slate-400 max-w-xs">{param.notes}</td>
    </tr>
  );
};

// ─── Main panel ──────────────────────────────────────────────────────────────

export const EcuHealthPanel: React.FC<EcuHealthPanelProps> = ({
  ecuName,
  requestCanId,
  responseCanId
}) => {
  const [activeView, setActiveView] = useState<'health' | 'plausibility' | 'rootcause' | 'normalization'>('health');
  const [statusMessage, setStatusMessage] = useState<{ text: string; isError?: boolean } | null>(null);

  // Health
  const [healthReport, setHealthReport] = useState<EcuHealthReportDto | null>(null);
  const [loadingHealth, setLoadingHealth] = useState(false);

  // Plausibility
  const [plausibilityReport, setPlausibilityReport] = useState<SensorPlausibilityReportDto | null>(null);
  const [loadingPlausibility, setLoadingPlausibility] = useState(false);
  const [expandedCheck, setExpandedCheck] = useState<number | null>(null);

  // OEM Normalization
  const [normalizationPlan, setNormalizationPlan] = useState<OemNormalizationPlanDto | null>(null);
  const [loadingNorm, setLoadingNorm] = useState(false);
  const [normHwNumber, setNormHwNumber] = useState('');
  const [normSwNumber, setNormSwNumber] = useState('');

  const showStatus = (text: string, isError = false) => {
    setStatusMessage({ text, isError });
    setTimeout(() => setStatusMessage(null), 5000);
  };

  // ── Load ECU Health ────────────────────────────────────────────────────────
  const loadHealthReport = useCallback(async () => {
    setLoadingHealth(true);
    try {
      const report = await api.getEcuHealthReport(ecuName);
      setHealthReport(report);
      showStatus(`ECU health evaluated: ${report.overallHealth} (${report.healthScorePercent}%)`);
    } catch (err: any) {
      showStatus(`Health assessment failed: ${err.message}`, true);
    } finally {
      setLoadingHealth(false);
    }
  }, [ecuName]);

  // ── Load Plausibility ──────────────────────────────────────────────────────
  const loadPlausibility = useCallback(async () => {
    setLoadingPlausibility(true);
    try {
      const report = await api.getSensorPlausibilityReport(ecuName);
      setPlausibilityReport(report);
      showStatus(`Plausibility: ${report.plausibleCount}/${report.totalChecksCount} checks OK`);
    } catch (err: any) {
      showStatus(`Plausibility check failed: ${err.message}`, true);
    } finally {
      setLoadingPlausibility(false);
    }
  }, [ecuName]);

  // ── Load OEM Normalization ─────────────────────────────────────────────────
  const loadNormalization = useCallback(async () => {
    setLoadingNorm(true);
    try {
      const plan = await api.getOemNormalizationPlan(ecuName, normHwNumber || undefined, normSwNumber || undefined);
      setNormalizationPlan(plan);
      const discrepancies = plan.discrepancies.length;
      showStatus(discrepancies === 0
        ? 'ECU meets all OEM target parameters'
        : `${discrepancies} parameter(s) outside OEM target range`
      , discrepancies > 0);
    } catch (err: any) {
      showStatus(`Normalization plan failed: ${err.message}`, true);
    } finally {
      setLoadingNorm(false);
    }
  }, [ecuName, normHwNumber, normSwNumber]);

  // ─── Tabs ─────────────────────────────────────────────────────────────────
  const tabs = [
    { id: 'health' as const,         label: 'Health Score',     icon: <Shield className="w-4 h-4" /> },
    { id: 'plausibility' as const,   label: 'Sensor Plausibility', icon: <Activity className="w-4 h-4" /> },
    { id: 'rootcause' as const,      label: 'Root-Cause Analysis', icon: <AlertCircle className="w-4 h-4" /> },
    { id: 'normalization' as const,  label: 'OEM Normalization',   icon: <Target className="w-4 h-4" /> },
  ];

  return (
    <div className="space-y-4">

      {/* Sub-nav */}
      <div className="flex flex-wrap items-center gap-2 border-b border-can-border pb-0">
        {tabs.map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveView(tab.id)}
            className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
              activeView === tab.id
                ? 'border-purple-500 text-purple-400 font-semibold'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
          >
            {tab.icon}
            <span>{tab.label}</span>
          </button>
        ))}
      </div>

      {/* Status banner */}
      {statusMessage && (
        <div className={`p-3 rounded-lg border text-xs flex items-center justify-between ${
          statusMessage.isError
            ? 'bg-rose-500/10 border-rose-500/30 text-rose-300'
            : 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
        }`}>
          <span>{statusMessage.text}</span>
          <button onClick={() => setStatusMessage(null)} className="text-slate-400 hover:text-white ml-4">✕</button>
        </div>
      )}

      {/* ── VIEW: Health Score ────────────────────────────────────────────── */}
      {activeView === 'health' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-white">ECU Health Assessment</h3>
              <p className="text-xs text-slate-400 mt-0.5">Aggregated fault count, communication quality, and voltage analysis.</p>
            </div>
            <button
              onClick={loadHealthReport}
              disabled={loadingHealth}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded text-xs font-medium bg-purple-600 hover:bg-purple-500 text-white shadow transition-colors disabled:opacity-50"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${loadingHealth ? 'animate-spin' : ''}`} />
              <span>{loadingHealth ? 'Evaluating...' : 'Evaluate Health'}</span>
            </button>
          </div>

          {!healthReport ? (
            <div className="p-10 bg-can-panel border border-can-border rounded-lg text-center space-y-3">
              <Shield className="w-12 h-12 text-slate-600 mx-auto" />
              <p className="text-sm text-slate-300 font-semibold">No Health Report Yet</p>
              <p className="text-xs text-slate-500 max-w-sm mx-auto">
                Click <strong>Evaluate Health</strong> to generate an aggregate health score using live DTC data,
                communication latency, and voltage readings.
              </p>
              <button
                onClick={loadHealthReport}
                disabled={loadingHealth}
                className="px-4 py-2 rounded text-xs font-medium bg-purple-600 hover:bg-purple-500 text-white shadow"
              >
                Evaluate Health Now
              </button>
            </div>
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">

              {/* Score card */}
              <div className={`col-span-1 p-4 rounded-lg border space-y-3 text-center ${healthBg(healthReport.overallHealth)}`}>
                {healthIcon(healthReport.overallHealth)}
                <div className={`text-sm font-bold ${healthColor(healthReport.overallHealth)}`}>
                  {healthReport.overallHealth}
                </div>
                <ScoreRing score={healthReport.healthScorePercent} status={healthReport.overallHealth} />
                <div className="text-[10px] text-slate-400 font-mono uppercase tracking-wide">ECU: {healthReport.ecuName}</div>
                <div className="text-[10px] text-slate-500">
                  Evaluated {new Date(healthReport.evaluatedUtc).toLocaleTimeString()}
                </div>
              </div>

              {/* Metric tiles */}
              <div className="col-span-2 grid grid-cols-2 sm:grid-cols-3 gap-3">
                {[
                  { label: 'Active DTCs',       value: healthReport.activeDtcCount,            color: healthReport.activeDtcCount > 0 ? 'text-rose-400' : 'text-emerald-400', icon: <AlertTriangle className="w-4 h-4" /> },
                  { label: 'Confirmed DTCs',     value: healthReport.confirmedDtcCount,         color: healthReport.confirmedDtcCount > 0 ? 'text-rose-400' : 'text-emerald-400', icon: <ShieldAlert className="w-4 h-4" /> },
                  { label: 'Pending DTCs',       value: healthReport.pendingDtcCount,           color: healthReport.pendingDtcCount > 0 ? 'text-amber-400' : 'text-emerald-400', icon: <AlertCircle className="w-4 h-4" /> },
                  { label: 'Plausibility Anomalies', value: healthReport.plausibilityAnomalyCount, color: healthReport.plausibilityAnomalyCount > 0 ? 'text-amber-400' : 'text-emerald-400', icon: <Activity className="w-4 h-4" /> },
                  { label: 'System Voltage',     value: `${healthReport.systemVoltage.toFixed(2)} V`, color: healthReport.systemVoltage < 11.5 ? 'text-rose-400' : healthReport.systemVoltage < 12.5 ? 'text-amber-400' : 'text-emerald-400', icon: <Zap className="w-4 h-4" /> },
                  { label: 'Avg Response Latency', value: `${healthReport.averageResponseLatencyMs.toFixed(1)} ms`, color: healthReport.averageResponseLatencyMs > 1000 ? 'text-amber-400' : 'text-emerald-400', icon: <BarChart2 className="w-4 h-4" /> },
                  { label: 'NRC Count',           value: healthReport.totalNrcCount,             color: healthReport.totalNrcCount > 5 ? 'text-amber-400' : 'text-emerald-400', icon: <Info className="w-4 h-4" /> },
                ].map((metric, i) => (
                  <div key={i} className="p-3 rounded-lg bg-can-card border border-can-border space-y-1">
                    <div className={`flex items-center space-x-1.5 text-[10px] text-slate-400`}>
                      {metric.icon}
                      <span>{metric.label}</span>
                    </div>
                    <div className={`text-lg font-bold font-mono ${metric.color}`}>{metric.value}</div>
                  </div>
                ))}
              </div>

              {/* Observations */}
              {healthReport.healthObservations.length > 0 && (
                <div className="col-span-1 lg:col-span-3 p-4 rounded-lg bg-can-card border border-can-border space-y-2">
                  <div className="text-xs font-semibold text-slate-300 uppercase tracking-wide flex items-center space-x-2">
                    <Info className="w-4 h-4 text-sky-400" />
                    <span>Health Observations</span>
                  </div>
                  <ul className="space-y-1">
                    {healthReport.healthObservations.map((obs, i) => (
                      <li key={i} className="flex items-start space-x-2 text-xs text-slate-300">
                        <ChevronRight className="w-3.5 h-3.5 text-sky-500 flex-shrink-0 mt-0.5" />
                        <span>{obs}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* ── VIEW: Sensor Plausibility ─────────────────────────────────────── */}
      {activeView === 'plausibility' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-white">Sensor Plausibility Analysis</h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Multi-signal physical correlation checks: charging, airflow, thermal balance, fuel trim, driveline.
              </p>
            </div>
            <button
              onClick={loadPlausibility}
              disabled={loadingPlausibility}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded text-xs font-medium bg-purple-600 hover:bg-purple-500 text-white shadow transition-colors disabled:opacity-50"
            >
              <Activity className={`w-3.5 h-3.5 ${loadingPlausibility ? 'animate-spin' : ''}`} />
              <span>{loadingPlausibility ? 'Analyzing...' : 'Run Plausibility Checks'}</span>
            </button>
          </div>

          {!plausibilityReport ? (
            <div className="p-10 bg-can-panel border border-can-border rounded-lg text-center space-y-3">
              <Activity className="w-12 h-12 text-slate-600 mx-auto" />
              <p className="text-sm text-slate-300 font-semibold">No Plausibility Report</p>
              <p className="text-xs text-slate-500 max-w-sm mx-auto">
                Click <strong>Run Plausibility Checks</strong> to evaluate cross-sensor physical correlations.
              </p>
            </div>
          ) : (
            <div className="space-y-3">

              {/* Summary bar */}
              <div className={`p-4 rounded-lg border flex flex-wrap items-center gap-4 ${
                plausibilityReport.overallState === 'Plausible'
                  ? 'bg-emerald-500/10 border-emerald-500/30'
                  : plausibilityReport.overallState === 'Questionable'
                  ? 'bg-amber-500/10 border-amber-500/30'
                  : 'bg-rose-500/10 border-rose-500/30'
              }`}>
                <div>
                  <div className={`text-sm font-bold ${plausibilityColor(plausibilityReport.overallState)}`}>
                    Overall: {plausibilityReport.overallState}
                  </div>
                  <div className="text-[10px] text-slate-400 mt-0.5">
                    {plausibilityReport.ecuName} · {new Date(plausibilityReport.evaluatedUtc).toLocaleTimeString()}
                  </div>
                </div>
                <div className="flex items-center space-x-4 ml-auto text-xs font-mono">
                  <span className="text-emerald-400">{plausibilityReport.plausibleCount} Plausible</span>
                  <span className="text-slate-400">|</span>
                  <span className={plausibilityReport.anomalyCount > 0 ? 'text-rose-400' : 'text-slate-400'}>
                    {plausibilityReport.anomalyCount} Anomalies
                  </span>
                  <span className="text-slate-400">|</span>
                  <span className="text-slate-300">{plausibilityReport.totalChecksCount} Total</span>
                </div>
              </div>

              {/* Progress bar */}
              <div className="w-full h-2 bg-slate-700 rounded-full overflow-hidden">
                <div
                  className="h-2 bg-emerald-500 rounded-full transition-all"
                  style={{ width: `${(plausibilityReport.plausibleCount / plausibilityReport.totalChecksCount) * 100}%` }}
                />
              </div>

              {/* Check list */}
              <div className="space-y-2">
                {plausibilityReport.checks.map((check: SensorPlausibilityCheckResultDto, idx) => (
                  <div key={idx} className="border border-can-border rounded-lg overflow-hidden">
                    <button
                      onClick={() => setExpandedCheck(expandedCheck === idx ? null : idx)}
                      className="w-full flex items-center justify-between p-3 bg-can-card hover:bg-slate-800/50 transition-colors text-left"
                    >
                      <div className="flex items-center space-x-3">
                        <span className={`text-[10px] font-mono uppercase px-2 py-0.5 rounded border ${plausibilityBadgeBg(check.state)}`}>
                          {check.state}
                        </span>
                        <div>
                          <span className="text-xs font-semibold text-white">{check.checkName}</span>
                          <span className="text-[10px] text-slate-400 ml-2">[{check.category}]</span>
                        </div>
                      </div>
                      {expandedCheck === idx
                        ? <ChevronDown className="w-3.5 h-3.5 text-slate-400" />
                        : <ChevronRight className="w-3.5 h-3.5 text-slate-400" />
                      }
                    </button>

                    {expandedCheck === idx && (
                      <div className="p-3 bg-can-panel/50 border-t border-can-border space-y-3">
                        <p className="text-xs text-slate-300">{check.summary}</p>
                        {check.detailedEvidence && (
                          <p className="text-[11px] text-slate-400 italic">{check.detailedEvidence}</p>
                        )}

                        <div className="flex flex-wrap gap-1.5">
                          {check.involvedSignals.map((sig, si) => (
                            <span key={si} className="text-[10px] bg-sky-500/10 text-sky-400 border border-sky-500/30 px-1.5 py-0.5 rounded font-mono">
                              {sig}
                            </span>
                          ))}
                        </div>

                        {Object.keys(check.measuredValues).length > 0 && (
                          <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                            {Object.entries(check.measuredValues).map(([k, v]) => (
                              <div key={k} className="p-2 rounded bg-slate-900 border border-slate-700 text-[11px] font-mono">
                                <div className="text-slate-400">{k}</div>
                                <div className="text-sky-300 font-bold">{(v as number).toFixed(3)}</div>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* ── VIEW: Root-Cause Analysis ─────────────────────────────────────── */}
      {activeView === 'rootcause' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-white">Root-Cause Correlation</h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Evidence-based correlation tree. Physical checks only — no automated ECU modifications.
              </p>
            </div>
            <button
              onClick={loadHealthReport}
              disabled={loadingHealth}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded text-xs font-medium bg-purple-600 hover:bg-purple-500 text-white shadow transition-colors disabled:opacity-50"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${loadingHealth ? 'animate-spin' : ''}`} />
              <span>{loadingHealth ? 'Analyzing...' : 'Refresh Analysis'}</span>
            </button>
          </div>

          {/* Safety notice */}
          <div className="p-3 bg-amber-500/5 border border-amber-500/20 rounded-lg flex items-start space-x-2.5">
            <AlertTriangle className="w-4 h-4 text-amber-400 flex-shrink-0 mt-0.5" />
            <div className="text-[11px] text-amber-200/80">
              <strong className="text-amber-400">Advisory Only.</strong> All recommended checks target physical components
              (wiring, sensors, mechanical). No ECU code, calibration data, or binary images are modified by this system.
              Perform checks on vehicles you own or are authorized to diagnose.
            </div>
          </div>

          {!healthReport ? (
            <div className="p-10 bg-can-panel border border-can-border rounded-lg text-center space-y-3">
              <AlertCircle className="w-12 h-12 text-slate-600 mx-auto" />
              <p className="text-sm text-slate-300 font-semibold">No Analysis Available</p>
              <p className="text-xs text-slate-500 max-w-sm mx-auto">
                Run <strong>Health Assessment</strong> first to populate root-cause candidates.
              </p>
              <button
                onClick={loadHealthReport}
                disabled={loadingHealth}
                className="px-4 py-2 rounded text-xs font-medium bg-purple-600 hover:bg-purple-500 text-white shadow"
              >
                Run Health Assessment
              </button>
            </div>
          ) : healthReport.rootCauseCandidates.length === 0 ? (
            <div className="p-8 bg-can-panel border border-emerald-500/20 rounded-lg text-center space-y-2">
              <ShieldCheck className="w-10 h-10 text-emerald-400 mx-auto" />
              <p className="text-sm text-emerald-300 font-semibold">No Root Causes Identified</p>
              <p className="text-xs text-slate-400">ECU reports no significant fault patterns requiring investigation.</p>
            </div>
          ) : (
            <div className="space-y-2">
              <div className="text-xs text-slate-400 font-mono">
                {healthReport.rootCauseCandidates.length} candidate(s) identified — ranked by confidence
              </div>
              {healthReport.rootCauseCandidates.map((candidate, idx) => (
                <RootCauseRow key={idx} candidate={candidate} index={idx} />
              ))}
            </div>
          )}
        </div>
      )}

      {/* ── VIEW: OEM Normalization ───────────────────────────────────────── */}
      {activeView === 'normalization' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-white">OEM Normalization Comparison</h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Compare measured parameters against OEM target specifications. Read-only advisory — no ECU data is written.
              </p>
            </div>
          </div>

          {/* Safety attestation */}
          <div className="p-3 bg-sky-500/5 border border-sky-500/20 rounded-lg flex items-start space-x-2.5">
            <Info className="w-4 h-4 text-sky-400 flex-shrink-0 mt-0.5" />
            <div className="text-[11px] text-sky-200/70">
              Normalization recommendations address wiring, sensors, actuators, and mechanical components only.
              This system does NOT modify ECU calibration tables, binary images, or software.
            </div>
          </div>

          {/* Config */}
          <div className="p-4 bg-can-card border border-can-border rounded-lg space-y-3">
            <div className="text-xs font-semibold text-slate-300 uppercase tracking-wide">Optional — ECU Part Numbers</div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-[10px] text-slate-400 font-mono">Hardware Number (optional)</label>
                <input
                  type="text"
                  value={normHwNumber}
                  onChange={e => setNormHwNumber(e.target.value)}
                  placeholder="e.g. 8D0-907-551"
                  className="w-full bg-can-panel border border-can-border rounded px-2.5 py-1.5 text-xs font-mono text-sky-300 focus:outline-none focus:border-purple-500"
                />
              </div>
              <div className="space-y-1">
                <label className="text-[10px] text-slate-400 font-mono">Software Number (optional)</label>
                <input
                  type="text"
                  value={normSwNumber}
                  onChange={e => setNormSwNumber(e.target.value)}
                  placeholder="e.g. 8D0-907-551-M"
                  className="w-full bg-can-panel border border-can-border rounded px-2.5 py-1.5 text-xs font-mono text-sky-300 focus:outline-none focus:border-purple-500"
                />
              </div>
            </div>
            <button
              onClick={loadNormalization}
              disabled={loadingNorm}
              className="flex items-center space-x-1.5 px-4 py-2 rounded text-xs font-medium bg-purple-600 hover:bg-purple-500 text-white shadow transition-colors disabled:opacity-50"
            >
              <Target className={`w-3.5 h-3.5 ${loadingNorm ? 'animate-spin' : ''}`} />
              <span>{loadingNorm ? 'Generating Plan...' : 'Generate OEM Normalization Plan'}</span>
            </button>
          </div>

          {!normalizationPlan ? (
            <div className="p-8 bg-can-panel border border-can-border rounded-lg text-center space-y-2">
              <Target className="w-10 h-10 text-slate-600 mx-auto" />
              <p className="text-sm text-slate-300 font-semibold">No Plan Generated</p>
              <p className="text-xs text-slate-500">Click Generate to compare measured values against OEM targets.</p>
            </div>
          ) : (
            <div className="space-y-4">

              {/* Plan header */}
              <div className={`p-4 rounded-lg border ${normalizationPlan.meetsAllOemTargets ? 'bg-emerald-500/10 border-emerald-500/30' : 'bg-amber-500/10 border-amber-500/30'}`}>
                <div className="flex flex-wrap items-center gap-3">
                  <div className={`flex items-center space-x-2 text-sm font-bold ${normalizationPlan.meetsAllOemTargets ? 'text-emerald-400' : 'text-amber-400'}`}>
                    {normalizationPlan.meetsAllOemTargets
                      ? <CheckCircle2 className="w-5 h-5" />
                      : <AlertTriangle className="w-5 h-5" />
                    }
                    <span>{normalizationPlan.meetsAllOemTargets ? 'Meets All OEM Targets' : `${normalizationPlan.discrepancies.length} Discrepancies Found`}</span>
                  </div>
                  <div className="text-[10px] text-slate-400 font-mono ml-auto">
                    Plan: {normalizationPlan.planId.substring(0, 8)} · ECU Family: {normalizationPlan.ecuFamily}
                  </div>
                </div>
                <div className="text-[10px] text-slate-500 mt-1">
                  {new Date(normalizationPlan.createdUtc).toLocaleString()}
                </div>
                {normalizationPlan.safetyAttestation && (
                  <div className="mt-2 text-[11px] text-sky-300/70 italic">{normalizationPlan.safetyAttestation}</div>
                )}
              </div>

              {/* Parameters table */}
              <div className="bg-can-card border border-can-border rounded-lg overflow-hidden">
                <div className="p-3 border-b border-can-border">
                  <span className="text-xs font-semibold text-slate-300 uppercase tracking-wide">Evaluated Parameters ({normalizationPlan.evaluatedParameters.length})</span>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full text-left">
                    <thead className="text-[10px] font-semibold uppercase tracking-wider text-slate-400 bg-slate-800/60">
                      <tr>
                        <th className="py-2 px-3">Parameter</th>
                        <th className="py-2 px-3">OEM Target</th>
                        <th className="py-2 px-3">Measured</th>
                        <th className="py-2 px-3 w-40">Range</th>
                        <th className="py-2 px-3">Status</th>
                        <th className="py-2 px-3">Notes</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-can-border/30">
                      {normalizationPlan.evaluatedParameters.map((param, idx) => (
                        <OemParamRow key={idx} param={param} />
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Recommended service actions */}
              {normalizationPlan.recommendedServiceActions.length > 0 && (
                <div className="p-4 bg-can-card border border-can-border rounded-lg space-y-2">
                  <div className="text-xs font-semibold text-amber-400 uppercase tracking-wide flex items-center space-x-2">
                    <Wrench className="w-4 h-4" />
                    <span>Recommended Service Actions ({normalizationPlan.recommendedServiceActions.length})</span>
                  </div>
                  <ul className="space-y-1.5">
                    {normalizationPlan.recommendedServiceActions.map((action, i) => (
                      <li key={i} className="flex items-start space-x-2 text-xs text-slate-300">
                        <span className="text-amber-500 font-bold flex-shrink-0 font-mono">{i + 1}.</span>
                        <span>{action}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>
      )}

    </div>
  );
};
