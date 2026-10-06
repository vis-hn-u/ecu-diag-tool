import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { EcuProgrammingSessionStatus } from '../../types';
import { 
  Cpu, 
  ShieldAlert, 
  ShieldCheck, 
  CheckCircle2, 
  AlertTriangle, 
  Play, 
  Square, 
  RefreshCw, 
  Lock, 
  Zap, 
  Database,
  Flame
} from 'lucide-react';

export const EcuProgrammingPanel: React.FC = () => {
  const [status, setStatus] = useState<EcuProgrammingSessionStatus | null>(null);
  const [operatorConfirmed, setOperatorConfirmed] = useState(false);
  const [operatorRationale, setOperatorRationale] = useState('Authorized OEM calibration update following bench bench-test verification');
  const [voltage, setVoltage] = useState(13.8);
  const [isFlashing, setIsFlashing] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    loadStatus();
  }, []);

  const loadStatus = async () => {
    try {
      const s = await api.getProgrammingStatus();
      setStatus(s);
    } catch { }
  };

  const handleStartProgramming = async () => {
    if (!operatorConfirmed) {
      setErrorMsg('Operator confirmation is required before entering programming session.');
      return;
    }
    setErrorMsg(null);
    setIsFlashing(true);
    try {
      // 1KB dummy image for verification
      const dummyImage = btoa(String.fromCharCode(...new Array(1024).fill(0xAA)));
      const res = await api.executeProgramming({
        ecuName: 'ECM',
        requestCanId: 0x7E0,
        targetImageBase64: dummyImage,
        operatorConfirmed: true,
        operatorRationale: operatorRationale,
        currentSupplyVoltage: voltage
      });
      setStatus(res);
    } catch (e: any) {
      setErrorMsg(e.message || 'Programming sequence failed.');
    } finally {
      setIsFlashing(false);
      await loadStatus();
    }
  };

  const handleAbort = async () => {
    await api.abortProgramming('Operator manual abort');
    await loadStatus();
  };

  const isVoltageSafe = voltage >= 12.5 && voltage <= 15.0;

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-200 font-mono text-xs overflow-hidden">
      {/* Top Bar */}
      <div className="bg-[#101726] border-b border-slate-800 p-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center space-x-2">
          <div className="p-1.5 bg-rose-500/10 border border-rose-500/30 rounded text-rose-400">
            <Flame className="w-4 h-4" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-white tracking-wide">SAFE ECU FLASH PROGRAMMING ARCHITECTURE</h2>
            <p className="text-[11px] text-slate-400">12-stage fail-safe programming state machine with immutable root backups & power verification</p>
          </div>
        </div>

        {/* State Badge */}
        <div className="flex items-center space-x-2">
          <div className="text-[11px] text-slate-400 font-bold">STATE:</div>
          <span className={`px-2.5 py-1 rounded text-xs font-bold border ${
            status?.state === 'Complete'
              ? 'bg-emerald-950 text-emerald-300 border-emerald-800'
              : status?.state === 'Programming' || status?.state === 'Verifying'
              ? 'bg-sky-950 text-sky-300 border-sky-800 animate-pulse'
              : status?.state === 'Fault' || status?.state === 'RecoveryRequired'
              ? 'bg-rose-950 text-rose-300 border-rose-800'
              : 'bg-slate-800 text-slate-300 border-slate-700'
          }`}>
            {status?.state || 'DISCONNECTED'}
          </span>
        </div>
      </div>

      <div className="flex-1 flex overflow-hidden">
        {/* Stages Checklist */}
        <div className="w-1/2 p-4 border-r border-slate-800 overflow-y-auto space-y-3">
          <h3 className="text-xs font-bold text-white uppercase tracking-wider mb-2">12-Stage Programming Protocol</h3>

          <div className="space-y-2">
            {[
              { idx: 1, title: 'ECU Identification', desc: 'Read VIN, Part Number, HW & SW numbers via UDS 0x22' },
              { idx: 2, title: 'Compatibility Verification', desc: 'Cross-reference hardware revision and memory map' },
              { idx: 3, title: 'Original Memory Backup', desc: 'Read and archive immutable original ECU image' },
              { idx: 4, title: 'Backup Hash Integrity Check', desc: 'Compute SHA-256 hash of original backup' },
              { idx: 5, title: 'Target Image Validation', desc: 'Verify format (BIN/HEX), length, and address layout' },
              { idx: 6, title: 'Checksum Verification', desc: 'Calculate and match modular 32-bit CRC block checksum' },
              { idx: 7, title: 'Power Stability Verification', desc: 'Verify voltage > 12.5V (clean bench power)' },
              { idx: 8, title: 'Operator Authorization Gate', desc: 'Verify explicit operator sign-off and rationale' },
              { idx: 9, title: 'Flash Programming', desc: 'Sector erase & ISO-TP block data transfer (UDS 0x34/0x36)' },
              { idx: 10, title: 'Post-Flash Verification', desc: 'Full image readback and cryptographic hash comparison' },
              { idx: 11, title: 'ECU Synchronization Reset', desc: 'Hard power-on reset via UDS Service 0x11' },
              { idx: 12, title: 'Diagnostic Re-identification', desc: 'Verify restored communication and zero persistent DTCs' }
            ].map(step => {
              const completed = status?.completedStages.find(s => s.stageIndex === step.idx);
              const isPassed = completed?.success;

              return (
                <div
                  key={step.idx}
                  className={`p-2.5 rounded-lg border transition-all ${
                    isPassed
                      ? 'bg-emerald-950/20 border-emerald-500/30'
                      : completed && !isPassed
                      ? 'bg-rose-950/30 border-rose-500/40'
                      : 'bg-slate-900/40 border-slate-800'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-white text-xs">
                      {step.idx}. {step.title}
                    </span>
                    {isPassed ? (
                      <span className="text-emerald-400 flex items-center space-x-1 text-[11px]">
                        <CheckCircle2 className="w-3.5 h-3.5" />
                        <span>{completed?.durationMs.toFixed(0)} ms</span>
                      </span>
                    ) : completed && !isPassed ? (
                      <span className="text-rose-400 text-[11px] font-bold">FAILED</span>
                    ) : (
                      <span className="text-slate-600 text-[11px]">Pending</span>
                    )}
                  </div>
                  <div className="text-[11px] text-slate-400 mt-0.5">{step.desc}</div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Safety Pre-Checks & Authorization */}
        <div className="w-1/2 p-4 bg-[#0C101A] overflow-y-auto space-y-4">
          <div className="border-b border-slate-800 pb-2">
            <h3 className="text-sm font-bold text-white">Pre-Flight Safety Controls</h3>
            <p className="text-[11px] text-slate-400">All pre-conditions must be satisfied before programming authorization can unlock</p>
          </div>

          {/* Voltage Guard */}
          <div className={`p-3 rounded-lg border ${isVoltageSafe ? 'bg-emerald-950/20 border-emerald-500/30' : 'bg-rose-950/40 border-rose-500/50'}`}>
            <div className="flex items-center justify-between">
              <span className="font-semibold text-white flex items-center space-x-1.5">
                <Zap className="w-4 h-4 text-amber-400" />
                <span>Supply Voltage Verification</span>
              </span>
              <span className={`font-bold ${isVoltageSafe ? 'text-emerald-400' : 'text-rose-400'}`}>
                {voltage.toFixed(1)} V
              </span>
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Required window: <strong>12.5V – 15.0V</strong>. {isVoltageSafe ? 'Voltage verified safe for flash writing.' : 'UNSAFE VOLTAGE. Programming will be rejected.'}
            </div>
            <div className="mt-2 flex items-center space-x-2">
              <span className="text-[10px] text-slate-500">Bench voltage:</span>
              <input
                type="range"
                min="10.0"
                max="16.0"
                step="0.1"
                value={voltage}
                onChange={e => setVoltage(parseFloat(e.target.value))}
                className="flex-1 accent-sky-500"
              />
            </div>
          </div>

          {/* Operator Authorization Gate */}
          <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg space-y-2">
            <span className="font-semibold text-white flex items-center space-x-1.5">
              <Lock className="w-4 h-4 text-rose-400" />
              <span>Operator Authorization Gate (Required)</span>
            </span>

            <div className="flex items-start space-x-2 pt-1">
              <input
                type="checkbox"
                id="opConfirm"
                checked={operatorConfirmed}
                onChange={e => setOperatorConfirmed(e.target.checked)}
                className="mt-0.5 rounded accent-sky-500"
              />
              <label htmlFor="opConfirm" className="text-slate-300 cursor-pointer">
                I explicitly confirm that the target ECU is connected to a regulated clean power supply, the correct calibration image has been selected, and original backup is preserved.
              </label>
            </div>

            <div className="pt-2">
              <label className="text-[10px] text-slate-400 uppercase font-semibold">Operator Rationale / Work Order:</label>
              <input
                type="text"
                value={operatorRationale}
                onChange={e => setOperatorRationale(e.target.value)}
                className="w-full mt-1 bg-slate-950 border border-slate-800 rounded px-2.5 py-1 text-xs text-white"
              />
            </div>
          </div>

          {errorMsg && (
            <div className="p-3 bg-rose-950/60 border border-rose-500/50 rounded-lg text-rose-300 text-xs">
              <strong>Error:</strong> {errorMsg}
            </div>
          )}

          {/* Action Trigger */}
          <div className="pt-2 flex space-x-3">
            <button
              onClick={handleStartProgramming}
              disabled={isFlashing || !operatorConfirmed || !isVoltageSafe}
              className="flex-1 py-2.5 bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white rounded-lg text-xs font-bold shadow-lg shadow-rose-900/30 flex items-center justify-center space-x-2"
            >
              {isFlashing ? (
                <>
                  <RefreshCw className="w-4 h-4 animate-spin" />
                  <span>Executing 12-Stage Workflow... ({status?.progressPercent || 0}%)</span>
                </>
              ) : (
                <>
                  <Play className="w-4 h-4 fill-current" />
                  <span>AUTHORIZE & EXECUTE PROGRAMMING</span>
                </>
              )}
            </button>

            {isFlashing && (
              <button
                onClick={handleAbort}
                className="px-4 py-2.5 bg-slate-800 hover:bg-slate-700 text-rose-400 border border-rose-800/40 rounded-lg text-xs font-bold"
              >
                Abort
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
