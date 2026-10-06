import React, { useState, useEffect } from 'react';
import { useCan } from '../context/CanContext';
import { api } from '../services/api';
import { EngineOutputs, EngineParameters, EngineInputs } from '../types/engine';
import { 
  Gauge, 
  Flame, 
  Activity, 
  Play, 
  Square, 
  RotateCcw, 
  Zap, 
  Wind, 
  Thermometer, 
  Compass, 
  Layers, 
  Sliders, 
  ShieldCheck,
  Radio
} from 'lucide-react';

export const EnginePage: React.FC = () => {
  const { operatingMode } = useCan();
  const [engineData, setEngineData] = useState<{
    parameters: EngineParameters;
    inputs: EngineInputs;
    outputs: EngineOutputs;
    isSimulationRunning: boolean;
  } | null>(null);

  const [tps, setTps] = useState<number>(0);
  const [gear, setGear] = useState<number>(1);

  const fetchStatus = async () => {
    try {
      const status = await api.getEngineStatus();
      setEngineData(status);
    } catch {
      // Ignore
    }
  };

  useEffect(() => {
    fetchStatus();
    const interval = setInterval(fetchStatus, 200);
    return () => clearInterval(interval);
  }, []);

  const handleThrottleChange = async (val: number) => {
    setTps(val);
    await api.setEngineControl({ throttlePositionPct: val });
    fetchStatus();
  };

  const handleGearChange = async (newGear: number) => {
    setGear(newGear);
    await api.setEngineControl({ gear: newGear });
    fetchStatus();
  };

  const handleToggleSimulation = async () => {
    if (engineData?.isSimulationRunning) {
      await api.stopEngineSimulation();
    } else {
      await api.startEngineSimulation();
    }
    fetchStatus();
  };

  const handleReset = async () => {
    setTps(0);
    setGear(1);
    await api.resetEngine();
    fetchStatus();
  };

  const out = engineData?.outputs;
  const isSimActive = engineData?.isSimulationRunning ?? false;

  return (
    <div className="flex flex-col h-full font-mono select-none overflow-y-auto p-4 space-y-4">
      {/* Top Header Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-3 flex items-center justify-between shadow-md">
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2 text-white font-bold text-sm font-sans">
            <Flame className="w-5 h-5 text-amber-400" />
            <span>REAL-TIME ENGINE CONTROL & TELEMETRY</span>
          </div>

          <div className="h-4 w-px bg-can-border mx-1" />

          {/* Mode Badge */}
          <span className={`px-2.5 py-0.5 rounded text-[11px] font-sans font-bold flex items-center space-x-1 ${
            operatingMode === 'Live Hardware'
              ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'
              : 'bg-sky-500/20 text-sky-300 border border-sky-500/30'
          }`}>
            <Radio className="w-3 h-3" />
            <span>{operatingMode.toUpperCase()}</span>
          </span>

          <span className="text-xs text-slate-400 font-sans">
            State: <span className="text-white font-bold">{out?.operatingState || 'Idle'}</span>
          </span>
        </div>

        <div className="flex items-center space-x-3">
          <button
            onClick={handleToggleSimulation}
            className={`px-3.5 py-1.5 rounded text-xs font-sans font-bold flex items-center space-x-1.5 shadow transition-all ${
              isSimActive
                ? 'bg-rose-600 hover:bg-rose-500 text-white'
                : 'bg-emerald-600 hover:bg-emerald-500 text-white'
            }`}
          >
            {isSimActive ? <Square className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5" />}
            <span>{isSimActive ? 'Stop CAN Stream' : 'Stream to CAN Bus'}</span>
          </button>

          <button
            onClick={handleReset}
            className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded border border-can-border text-xs flex items-center space-x-1.5"
            title="Reset Engine Dynamics"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Reset</span>
          </button>
        </div>
      </div>

      {/* Primary Big Gauges Grid */}
      <div className="grid grid-cols-4 gap-3">
        {/* RPM Gauge */}
        <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-lg flex flex-col justify-between">
          <div className="flex justify-between items-center text-xs text-slate-400 font-sans font-semibold">
            <span>ENGINE SPEED</span>
            <Activity className="w-4 h-4 text-sky-400" />
          </div>
          <div className="text-3xl font-bold text-white font-mono my-2">
            {out ? Math.round(out.rpm) : 850} <span className="text-xs text-sky-400 font-sans">RPM</span>
          </div>
          <div className="w-full bg-can-bg rounded-full h-2 overflow-hidden border border-can-border">
            <div
              className={`h-full transition-all duration-75 ${
                (out?.rpm || 0) > 6000 ? 'bg-rose-500' : (out?.rpm || 0) > 4500 ? 'bg-amber-400' : 'bg-sky-500'
              }`}
              style={{ width: `${Math.min(100, ((out?.rpm || 850) / 7000) * 100)}%` }}
            />
          </div>
        </div>

        {/* MAP / Boost Gauge */}
        <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-lg flex flex-col justify-between">
          <div className="flex justify-between items-center text-xs text-slate-400 font-sans font-semibold">
            <span>MANIFOLD PRESSURE & BOOST</span>
            <Wind className="w-4 h-4 text-indigo-400" />
          </div>
          <div className="text-3xl font-bold text-white font-mono my-2">
            {out ? out.boostBar.toFixed(2) : '0.00'} <span className="text-xs text-indigo-400 font-sans">BAR ({out ? out.mapKpa.toFixed(0) : 101} kPa)</span>
          </div>
          <div className="w-full bg-can-bg rounded-full h-2 overflow-hidden border border-can-border">
            <div
              className="h-full bg-indigo-500 transition-all duration-75"
              style={{ width: `${Math.min(100, ((out?.boostBar || 0) / 1.5) * 100)}%` }}
            />
          </div>
        </div>

        {/* Torque Gauge */}
        <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-lg flex flex-col justify-between">
          <div className="flex justify-between items-center text-xs text-slate-400 font-sans font-semibold">
            <span>ESTIMATED TORQUE</span>
            <Zap className="w-4 h-4 text-amber-400" />
          </div>
          <div className="text-3xl font-bold text-amber-300 font-mono my-2">
            {out ? out.torqueNm.toFixed(1) : '45.0'} <span className="text-xs text-amber-400 font-sans">Nm</span>
          </div>
          <div className="w-full bg-can-bg rounded-full h-2 overflow-hidden border border-can-border">
            <div
              className="h-full bg-amber-500 transition-all duration-75"
              style={{ width: `${Math.min(100, ((out?.torqueNm || 0) / 400.0) * 100)}%` }}
            />
          </div>
        </div>

        {/* Power Gauge */}
        <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-lg flex flex-col justify-between">
          <div className="flex justify-between items-center text-xs text-slate-400 font-sans font-semibold">
            <span>CALCULATED POWER</span>
            <Flame className="w-4 h-4 text-rose-400" />
          </div>
          <div className="text-3xl font-bold text-rose-400 font-mono my-2">
            {out ? out.powerHp.toFixed(1) : '5.4'} <span className="text-xs text-rose-400 font-sans">HP ({out ? out.powerKw.toFixed(1) : '4.0'} kW)</span>
          </div>
          <div className="w-full bg-can-bg rounded-full h-2 overflow-hidden border border-can-border">
            <div
              className="h-full bg-rose-500 transition-all duration-75"
              style={{ width: `${Math.min(100, ((out?.powerHp || 0) / 320.0) * 100)}%` }}
            />
          </div>
        </div>
      </div>

      {/* Middle Interactive Cockpit & Secondary Parameters */}
      <div className="grid grid-cols-3 gap-4">
        {/* Interactive Throttle & Gear Cockpit */}
        <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow space-y-4">
          <div className="flex items-center space-x-2 border-b border-can-border pb-2 text-white font-bold text-xs font-sans">
            <Sliders className="w-4 h-4 text-sky-400" />
            <span>INTERACTIVE ENGINE CONTROLS</span>
          </div>

          {/* Throttle Slider */}
          <div className="space-y-2">
            <div className="flex justify-between text-xs">
              <span className="text-slate-400 font-sans">Throttle Position (TPS):</span>
              <span className="text-white font-bold font-mono">{tps}%</span>
            </div>
            <input
              type="range"
              min="0"
              max="100"
              step="1"
              value={tps}
              onChange={e => handleThrottleChange(Number(e.target.value))}
              className="w-full h-2 bg-can-bg rounded-lg appearance-none cursor-pointer accent-sky-500"
            />
            <div className="flex justify-between text-[10px] text-slate-500">
              <button onClick={() => handleThrottleChange(0)} className="hover:text-white">Idle (0%)</button>
              <button onClick={() => handleThrottleChange(25)} className="hover:text-white">Cruise (25%)</button>
              <button onClick={() => handleThrottleChange(50)} className="hover:text-white">Half (50%)</button>
              <button onClick={() => handleThrottleChange(100)} className="hover:text-rose-400 font-bold">WOT (100%)</button>
            </div>
          </div>

          {/* Transmission Gear Selector */}
          <div className="space-y-2 pt-2 border-t border-can-border">
            <span className="text-xs text-slate-400 font-sans block">Selected Gear:</span>
            <div className="grid grid-cols-6 gap-1.5">
              {[1, 2, 3, 4, 5, 6].map(g => (
                <button
                  key={g}
                  onClick={() => handleGearChange(g)}
                  className={`py-1.5 rounded text-xs font-bold transition-all ${
                    gear === g
                      ? 'bg-sky-600 text-white shadow'
                      : 'bg-can-card text-slate-400 hover:text-white hover:bg-slate-700'
                  }`}
                >
                  {g}
                </button>
              ))}
            </div>
          </div>

          {/* Speed Indicator */}
          <div className="bg-can-bg p-3 rounded border border-can-border flex justify-between items-center">
            <span className="text-xs text-slate-400 font-sans">Vehicle Speed:</span>
            <span className="text-xl font-bold text-white font-mono">{out ? out.vehicleSpeedKmh.toFixed(1) : 0} <span className="text-xs text-slate-400">km/h</span></span>
          </div>
        </div>

        {/* Combustion & Airflow Dynamics */}
        <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow space-y-3">
          <div className="flex items-center space-x-2 border-b border-can-border pb-2 text-white font-bold text-xs font-sans">
            <Activity className="w-4 h-4 text-emerald-400" />
            <span>COMBUSTION & AIRFLOW DYNAMICS</span>
          </div>

          <div className="space-y-2.5 text-xs">
            <div className="flex justify-between">
              <span className="text-slate-400">Engine Load:</span>
              <span className="text-white font-bold">{out ? out.engineLoadPct.toFixed(1) : 0} %</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Airflow Rate (MAF):</span>
              <span className="text-sky-300 font-bold">{out ? out.mafGramsPerSec.toFixed(2) : 0} g/s</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Target Lambda:</span>
              <span className={`font-bold ${(out?.lambda || 1) < 0.9 ? 'text-amber-400' : 'text-emerald-400'}`}>
                {out ? out.lambda.toFixed(3) : 1.0} λ ({out ? out.afr.toFixed(1) : 14.7} AFR)
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Ignition Advance:</span>
              <span className="text-purple-300 font-bold">{out ? out.ignitionAdvanceDeg.toFixed(1) : 12.0} ° BTDC</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Fuel Consumption:</span>
              <span className="text-slate-200">{out ? out.fuelFlowLitersPerHour.toFixed(2) : 0} L/h</span>
            </div>
          </div>
        </div>

        {/* Thermal State & Fuel Pressures */}
        <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow space-y-3">
          <div className="flex items-center space-x-2 border-b border-can-border pb-2 text-white font-bold text-xs font-sans">
            <Thermometer className="w-4 h-4 text-rose-400" />
            <span>THERMAL STATE & HYDRAULICS</span>
          </div>

          <div className="space-y-2.5 text-xs">
            <div className="flex justify-between">
              <span className="text-slate-400">Coolant Temperature:</span>
              <span className="text-emerald-400 font-bold">{out ? out.coolantTempC.toFixed(0) : 88} °C</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Engine Oil Temperature:</span>
              <span className="text-emerald-400 font-bold">{out ? out.oilTempC.toFixed(0) : 92} °C</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Intake Air Temp (IAT):</span>
              <span className="text-sky-300 font-bold">{out ? out.intakeAirTempC.toFixed(1) : 25} °C</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">High-Pressure Rail:</span>
              <span className="text-slate-200">{out ? out.railPressureMpa.toFixed(1) : 15} MPa</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Low-Pressure Fuel:</span>
              <span className="text-slate-200">{out ? out.fuelPressureKpa.toFixed(0) : 400} kPa</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
