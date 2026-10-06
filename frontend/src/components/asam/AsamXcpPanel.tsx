import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { A2lDatabase, OdxEcuVariant, XcpSessionStatus } from '../../types';
import { 
  FileCode, 
  Layers, 
  Cpu, 
  Link, 
  Unlink, 
  Upload, 
  Download, 
  Search, 
  CheckCircle2, 
  Sliders, 
  Activity 
} from 'lucide-react';

export const AsamXcpPanel: React.FC = () => {
  const [a2l, setA2l] = useState<A2lDatabase | null>(null);
  const [odx, setOdx] = useState<OdxEcuVariant | null>(null);
  const [xcpStatus, setXcpStatus] = useState<XcpSessionStatus | null>(null);
  const [activeTab, setActiveTab] = useState<'a2l' | 'odx' | 'xcp'>('a2l');
  const [uploadAddressHex, setUploadAddressHex] = useState('0x810000');
  const [uploadedHex, setUploadedHex] = useState<string>('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [a, o, x] = await Promise.all([
        api.getAsamA2l(),
        api.getAsamOdx(),
        api.getXcpStatus()
      ]);
      setA2l(a);
      setOdx(o);
      setXcpStatus(x);
    } catch { }
  };

  const handleToggleXcp = async () => {
    setLoading(true);
    try {
      if (xcpStatus?.isConnected) {
        await api.disconnectXcp();
      } else {
        await api.connectXcp('XcpOnCan');
      }
      await loadData();
    } catch { }
    finally { setLoading(false); }
  };

  const handleUpload = async () => {
    try {
      const addr = parseInt(uploadAddressHex, 16);
      const res = await api.xcpUpload(addr, 16);
      setUploadedHex(res.dataHex);
    } catch { }
  };

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-200 font-mono text-xs overflow-hidden">
      {/* Top Bar */}
      <div className="bg-[#101726] border-b border-slate-800 p-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center space-x-2">
          <div className="p-1.5 bg-purple-500/10 border border-purple-500/30 rounded text-purple-400">
            <FileCode className="w-4 h-4" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-white tracking-wide">ASAM MCD-2 (A2L / ODX) & XCP PROTOCOL LAB</h2>
            <p className="text-[11px] text-slate-400">Calibration description, diagnostic specifications & Universal Measurement/Calibration Protocol (XCP)</p>
          </div>
        </div>

        {/* XCP Connection */}
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2 bg-slate-900 border border-slate-800 px-3 py-1.5 rounded-lg">
            <span className={`w-2 h-2 rounded-full ${xcpStatus?.isConnected ? 'bg-emerald-400' : 'bg-slate-600'}`} />
            <span className="text-[11px] text-slate-300">
              XCP: {xcpStatus?.isConnected ? 'CONNECTED (XCP on CAN)' : 'DISCONNECTED'}
            </span>
          </div>

          <button
            onClick={handleToggleXcp}
            disabled={loading}
            className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all ${
              xcpStatus?.isConnected
                ? 'bg-rose-600 hover:bg-rose-500 text-white'
                : 'bg-indigo-600 hover:bg-indigo-500 text-white'
            }`}
          >
            {xcpStatus?.isConnected ? <Unlink className="w-3.5 h-3.5" /> : <Link className="w-3.5 h-3.5" />}
            <span>{xcpStatus?.isConnected ? 'Disconnect XCP' : 'Connect XCP'}</span>
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-slate-800 bg-[#0D121F] px-4 space-x-4">
        <button
          onClick={() => setActiveTab('a2l')}
          className={`py-2 text-xs font-semibold border-b-2 transition-all ${
            activeTab === 'a2l' ? 'text-sky-400 border-sky-500' : 'text-slate-400 border-transparent hover:text-slate-200'
          }`}
        >
          A2L Characteristics & Measurements
        </button>
        <button
          onClick={() => setActiveTab('odx')}
          className={`py-2 text-xs font-semibold border-b-2 transition-all ${
            activeTab === 'odx' ? 'text-sky-400 border-sky-500' : 'text-slate-400 border-transparent hover:text-slate-200'
          }`}
        >
          ODX Diagnostic Services
        </button>
        <button
          onClick={() => setActiveTab('xcp')}
          className={`py-2 text-xs font-semibold border-b-2 transition-all ${
            activeTab === 'xcp' ? 'text-sky-400 border-sky-500' : 'text-slate-400 border-transparent hover:text-slate-200'
          }`}
        >
          XCP Memory Transfer
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-4">
        {activeTab === 'a2l' && (
          <div className="space-y-6">
            <div>
              <h3 className="text-xs font-bold text-sky-400 uppercase tracking-wider mb-2">
                Calibration Maps & Characteristics ({a2l?.characteristics.length || 0})
              </h3>
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 text-[11px]">
                    <th className="p-2">NAME</th>
                    <th className="p-2">TYPE</th>
                    <th className="p-2">ECU ADDRESS</th>
                    <th className="p-2">SIZE</th>
                    <th className="p-2">RANGE</th>
                    <th className="p-2">UNIT</th>
                    <th className="p-2">DESCRIPTION</th>
                  </tr>
                </thead>
                <tbody>
                  {a2l?.characteristics.map((c, i) => (
                    <tr key={i} className="border-b border-slate-900 hover:bg-slate-900/40">
                      <td className="p-2 font-bold text-white font-mono">{c.name}</td>
                      <td className="p-2 text-indigo-400">{c.type}</td>
                      <td className="p-2 font-mono text-slate-300">0x{c.ecuAddress.toString(16).toUpperCase()}</td>
                      <td className="p-2">{c.sizeBytes} B</td>
                      <td className="p-2 text-slate-400">{c.lowerLimit} to {c.upperLimit}</td>
                      <td className="p-2 text-emerald-400">{c.unit || '--'}</td>
                      <td className="p-2 text-slate-400 text-[11px]">{c.description}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div>
              <h3 className="text-xs font-bold text-indigo-400 uppercase tracking-wider mb-2">
                Live DAQ Measurements ({a2l?.measurements.length || 0})
              </h3>
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 text-[11px]">
                    <th className="p-2">MEASUREMENT</th>
                    <th className="p-2">DATA TYPE</th>
                    <th className="p-2">ECU ADDRESS</th>
                    <th className="p-2">RESOLUTION</th>
                    <th className="p-2">UNIT</th>
                    <th className="p-2">DESCRIPTION</th>
                  </tr>
                </thead>
                <tbody>
                  {a2l?.measurements.map((m, i) => (
                    <tr key={i} className="border-b border-slate-900 hover:bg-slate-900/40">
                      <td className="p-2 font-bold text-white font-mono">{m.name}</td>
                      <td className="p-2 text-sky-400">{m.dataType}</td>
                      <td className="p-2 font-mono text-slate-300">0x{m.ecuAddress.toString(16).toUpperCase()}</td>
                      <td className="p-2">{m.resolution}</td>
                      <td className="p-2 text-emerald-400">{m.unit}</td>
                      <td className="p-2 text-slate-400 text-[11px]">{m.description}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {activeTab === 'odx' && (
          <div>
            <div className="mb-3 text-[11px] text-slate-400">
              ECU Variant: <span className="text-white font-bold">{odx?.variantName}</span> ({odx?.ecuVariantId})
            </div>
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="border-b border-slate-800 text-slate-400 text-[11px]">
                  <th className="p-2">SEMANTIC ID</th>
                  <th className="p-2">SHORT NAME</th>
                  <th className="p-2">SID</th>
                  <th className="p-2">SUBFUNCTION</th>
                  <th className="p-2">DESCRIPTION</th>
                </tr>
              </thead>
              <tbody>
                {odx?.services.map((s, i) => (
                  <tr key={i} className="border-b border-slate-900 hover:bg-slate-900/40">
                    <td className="p-2 font-bold text-sky-400">{s.semanticId}</td>
                    <td className="p-2 text-white">{s.shortName}</td>
                    <td className="p-2 font-mono text-indigo-300">0x{s.requestSid.toString(16).padStart(2, '0').toUpperCase()}</td>
                    <td className="p-2 font-mono">{s.subFunction ? `0x${s.subFunction.toString(16).padStart(2, '0').toUpperCase()}` : '--'}</td>
                    <td className="p-2 text-slate-400 text-[11px]">{s.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {activeTab === 'xcp' && (
          <div className="max-w-xl space-y-4">
            <div className="bg-slate-900/60 border border-slate-800 p-4 rounded-lg space-y-3">
              <h3 className="text-xs font-bold text-white">XCP Short Upload (Read Memory by Address)</h3>
              <div className="flex space-x-2">
                <input
                  type="text"
                  value={uploadAddressHex}
                  onChange={e => setUploadAddressHex(e.target.value)}
                  placeholder="Address Hex (e.g. 0x810000)"
                  className="flex-1 bg-slate-950 border border-slate-800 rounded px-2.5 py-1.5 text-xs text-white font-mono"
                />
                <button
                  onClick={handleUpload}
                  disabled={!xcpStatus?.isConnected}
                  className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded text-xs font-semibold"
                >
                  Upload Bytes
                </button>
              </div>

              {uploadedHex && (
                <div className="p-3 bg-slate-950 border border-slate-800 rounded font-mono text-xs text-emerald-400">
                  <div className="text-[10px] text-slate-500 mb-1">RECEIVED DATA:</div>
                  {uploadedHex}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
