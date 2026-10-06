import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { HilTestFixture, HilChannelDescriptor } from '../../types';
import { 
  Zap, 
  Power, 
  RotateCcw, 
  ShieldAlert, 
  CheckCircle2, 
  Sliders, 
  Activity, 
  Radio, 
  Cpu, 
  AlertTriangle 
} from 'lucide-react';

export const HilBenchPanel: React.FC = () => {
  const [fixture, setFixture] = useState<HilTestFixture | null>(null);
  const [selectedChannel, setSelectedChannel] = useState<HilChannelDescriptor | null>(null);
  const [channelValue, setChannelValue] = useState<number>(0);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadFixture();
    const interval = setInterval(loadFixture, 2000);
    return () => clearInterval(interval);
  }, []);

  const loadFixture = async () => {
    try {
      const f = await api.getHilFixture();
      setFixture(f);
      if (!selectedChannel && f.channels.length > 0) {
        setSelectedChannel(f.channels[0]);
        setChannelValue(f.channels[0].currentValue);
      }
    } catch { }
  };

  const handleToggleIgnition = async () => {
    if (!fixture) return;
    await api.setHilIgnition(!fixture.ignitionRelayActive);
    await loadFixture();
  };

  const handleSetPower = async (voltage: number, enabled: boolean) => {
    await api.setHilMainPower(voltage, enabled);
    await loadFixture();
  };

  const handleUpdateChannelValue = async () => {
    if (!selectedChannel) return;
    await api.setHilChannelValue(selectedChannel.channelId, channelValue);
    await loadFixture();
  };

  const handleInjectFault = async (state: 'Active' | 'ShortToGround' | 'ShortToBattery' | 'OpenCircuit') => {
    if (!selectedChannel) return;
    await api.injectHilFault({
      channelId: selectedChannel.channelId,
      injectedState: state,
      overrideValue: state === 'ShortToGround' ? 0.0 : state === 'ShortToBattery' ? 13.8 : selectedChannel.currentValue
    });
    await loadFixture();
  };

  const handleResetFixture = async () => {
    await api.resetHilFixture();
    await loadFixture();
  };

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-200 font-mono text-xs overflow-hidden">
      {/* Top Bar */}
      <div className="bg-[#101726] border-b border-slate-800 p-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center space-x-2">
          <div className="p-1.5 bg-amber-500/10 border border-amber-500/30 rounded text-amber-400">
            <Radio className="w-4 h-4" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-white tracking-wide">HIL (HARDWARE-IN-THE-LOOP) BENCH CONTROLLER</h2>
            <p className="text-[11px] text-slate-400">Universal electrical stimulus, analog/digital/PWM synthesis, power relays & fault injection</p>
          </div>
        </div>

        <div className="flex items-center space-x-3">
          {/* Main Power & Ignition Status */}
          <div className="flex items-center space-x-2 bg-slate-900 border border-slate-800 px-3 py-1.5 rounded-lg">
            <span className="text-slate-400">SUPPLY:</span>
            <span className="text-white font-bold">{fixture?.mainSupplyVoltage.toFixed(1)}V</span>
            <span className="text-slate-500">|</span>
            <span className="text-slate-400">LOAD:</span>
            <span className="text-emerald-400 font-bold">{fixture?.totalCurrentAmps.toFixed(2)}A</span>
          </div>

          <button
            onClick={handleToggleIgnition}
            className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all ${
              fixture?.ignitionRelayActive
                ? 'bg-amber-600 hover:bg-amber-500 text-white'
                : 'bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700'
            }`}
          >
            <Power className="w-3.5 h-3.5" />
            <span>{fixture?.ignitionRelayActive ? 'TERMINAL 15: ON' : 'TERMINAL 15: OFF'}</span>
          </button>

          <button
            onClick={handleResetFixture}
            className="flex items-center space-x-1.5 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 rounded-lg text-xs"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Reset Bench</span>
          </button>
        </div>
      </div>

      {/* Main Grid */}
      <div className="flex-1 flex overflow-hidden">
        {/* Channel Table */}
        <div className="w-2/3 p-4 overflow-y-auto border-r border-slate-800">
          <table className="w-full text-left border-collapse font-mono text-xs">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 text-[11px]">
                <th className="p-2">CHANNEL ID</th>
                <th className="p-2">NAME</th>
                <th className="p-2">TYPE</th>
                <th className="p-2">VALUE</th>
                <th className="p-2">UNIT</th>
                <th className="p-2">STATE</th>
              </tr>
            </thead>
            <tbody>
              {fixture?.channels.map(ch => {
                const isSelected = ch.channelId === selectedChannel?.channelId;
                const isFaulted = ch.state !== 'Active' && ch.state !== 'Simulating' && ch.state !== 'Inactive';

                return (
                  <tr
                    key={ch.channelId}
                    onClick={() => {
                      setSelectedChannel(ch);
                      setChannelValue(ch.currentValue);
                    }}
                    className={`border-b border-slate-900 cursor-pointer transition-colors ${
                      isSelected ? 'bg-sky-950/40 text-sky-300' : 'hover:bg-slate-900/40 text-slate-300'
                    }`}
                  >
                    <td className="p-2 font-bold font-mono text-white">{ch.channelId}</td>
                    <td className="p-2">{ch.name}</td>
                    <td className="p-2 text-indigo-400">{ch.signalType}</td>
                    <td className="p-2 font-bold text-white">{ch.currentValue.toFixed(2)}</td>
                    <td className="p-2 text-slate-400">{ch.unit}</td>
                    <td className="p-2">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                        isFaulted
                          ? 'bg-rose-950 text-rose-300 border border-rose-800'
                          : ch.state === 'Active'
                          ? 'bg-emerald-950 text-emerald-300 border border-emerald-800'
                          : 'bg-slate-800 text-slate-400'
                      }`}>
                        {ch.state}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {/* Selected Channel Controller */}
        <div className="w-1/3 p-4 bg-[#0C101A] overflow-y-auto">
          {selectedChannel ? (
            <div className="space-y-4">
              <div className="border-b border-slate-800 pb-2">
                <h3 className="text-sm font-bold text-white">{selectedChannel.name}</h3>
                <p className="text-[11px] text-slate-400 font-mono">ID: {selectedChannel.channelId} • {selectedChannel.signalType}</p>
                <p className="text-[11px] text-slate-500 mt-1">{selectedChannel.description}</p>
              </div>

              {/* Adjust Value */}
              <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg space-y-2">
                <div className="flex justify-between text-xs font-semibold text-slate-300">
                  <span>Stimulus Output Value</span>
                  <span className="text-white font-bold">{channelValue.toFixed(2)} {selectedChannel.unit}</span>
                </div>
                <div className="flex space-x-2">
                  <input
                    type="number"
                    step="0.1"
                    value={channelValue}
                    onChange={e => setChannelValue(parseFloat(e.target.value) || 0)}
                    className="flex-1 bg-slate-950 border border-slate-800 rounded px-2.5 py-1 text-xs text-white font-mono"
                  />
                  <button
                    onClick={handleUpdateChannelValue}
                    className="px-3 py-1 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-semibold"
                  >
                    Set
                  </button>
                </div>
              </div>

              {/* Electrical Fault Injection */}
              <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg space-y-2">
                <span className="text-xs font-semibold text-slate-300 flex items-center space-x-1">
                  <ShieldAlert className="w-3.5 h-3.5 text-rose-400" />
                  <span>Hardware Fault Injection</span>
                </span>
                <p className="text-[11px] text-slate-400">Simulate harness / electrical circuit failures on this channel</p>

                <div className="grid grid-cols-2 gap-2 pt-1">
                  <button
                    onClick={() => handleInjectFault('ShortToGround')}
                    className="px-2 py-1.5 bg-rose-950/60 hover:bg-rose-900 text-rose-300 border border-rose-800/40 rounded text-[11px] font-semibold"
                  >
                    Short to GND (0V)
                  </button>
                  <button
                    onClick={() => handleInjectFault('ShortToBattery')}
                    className="px-2 py-1.5 bg-rose-950/60 hover:bg-rose-900 text-rose-300 border border-rose-800/40 rounded text-[11px] font-semibold"
                  >
                    Short to B+ (12V)
                  </button>
                  <button
                    onClick={() => handleInjectFault('OpenCircuit')}
                    className="px-2 py-1.5 bg-amber-950/60 hover:bg-amber-900 text-amber-300 border border-amber-800/40 rounded text-[11px] font-semibold"
                  >
                    Open Circuit (Float)
                  </button>
                  <button
                    onClick={() => handleInjectFault('Active')}
                    className="px-2 py-1.5 bg-emerald-950/60 hover:bg-emerald-900 text-emerald-300 border border-emerald-800/40 rounded text-[11px] font-semibold"
                  >
                    Restore Normal
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <div className="p-8 text-center text-slate-500">Select a HIL channel to configure stimulus.</div>
          )}
        </div>
      </div>
    </div>
  );
};
