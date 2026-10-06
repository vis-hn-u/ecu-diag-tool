import React, { useState, useEffect } from 'react';
import { api } from '../services/api';
import { 
  EcuBinaryInfo, 
  CalibrationObject, 
  ChecksumInfo, 
  BinaryDiffResult, 
  LiveFlashCorrelation 
} from '../types/flash';
import { 
  Cpu, 
  Lock, 
  FileCode, 
  Binary, 
  GitCompare, 
  ShieldCheck, 
  Activity, 
  Search, 
  Upload, 
  RotateCcw, 
  CheckCircle2, 
  Layers, 
  Sliders, 
  AlertTriangle,
  Info
} from 'lucide-react';

type FlashTab = 'project' | 'objects' | 'hex' | 'diff' | 'checksums' | 'correlation';

export const FlashAnalyzerPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<FlashTab>('project');
  const [binaryInfo, setBinaryInfo] = useState<EcuBinaryInfo | null>(null);
  const [selectedObjectId, setSelectedObjectId] = useState<string>('obj_driver_demand_torque');
  const [selectedObject, setSelectedObject] = useState<CalibrationObject | null>(null);
  const [hexOffset, setHexOffset] = useState<number>(0x1A240);
  const [hexDump, setHexDump] = useState<string>('');
  const [diffResult, setDiffResult] = useState<BinaryDiffResult | null>(null);
  const [correlations, setCorrelations] = useState<LiveFlashCorrelation[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);

  const fetchBinaryData = async () => {
    try {
      const bin = await api.getActiveBinary();
      setBinaryInfo(bin);
      if (bin.objects.length > 0 && !selectedObject) {
        setSelectedObject(bin.objects[0]);
      }
      const corrs = await api.getFlashCorrelations();
      setCorrelations(corrs);
      fetchHexDump(hexOffset);
    } catch {
      // Ignore
    }
  };

  const fetchHexDump = async (offset: number) => {
    try {
      const res = await api.getHexDump(offset, 256);
      setHexDump(res.dump);
    } catch {
      // Ignore
    }
  };

  useEffect(() => {
    fetchBinaryData();
  }, []);

  const handleSelectObject = async (objId: string) => {
    setSelectedObjectId(objId);
    try {
      const obj = await api.getFlashObject(objId);
      setSelectedObject(obj);
    } catch {
      // Ignore
    }
  };

  const handleRunDiff = async () => {
    setIsLoading(true);
    try {
      const res = await api.compareSampleBinaries();
      setDiffResult(res);
      setActiveTab('diff');
    } catch {
      // Ignore
    } finally {
      setIsLoading(false);
    }
  };

  const handleLoadSample = async () => {
    try {
      const res = await api.loadSampleBinary();
      setBinaryInfo(res);
      if (res.objects.length > 0) {
        setSelectedObject(res.objects[0]);
      }
      fetchHexDump(0x1A240);
    } catch {
      // Ignore
    }
  };

  const getProvenanceLabel = (prov: number) => {
    return ['CONFIRMED', 'PROJECT DEFINED', 'USER DEFINED', 'HEURISTIC', 'UNKNOWN'][prov] || 'CONFIRMED';
  };

  return (
    <div className="flex flex-col h-full font-mono select-none overflow-hidden p-4 space-y-4">
      {/* Top Header Bar */}
      <div className="bg-can-panel border border-can-border rounded-lg p-3 flex items-center justify-between shadow-md">
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2 text-white font-bold text-sm font-sans">
            <Cpu className="w-5 h-5 text-indigo-400" />
            <span>ECU FLASH ANALYZER</span>
          </div>

          <div className="h-4 w-px bg-can-border mx-1" />

          {/* Safety Read-Only Badge */}
          <span className="px-3 py-0.5 rounded text-[11px] font-sans font-bold flex items-center space-x-1.5 bg-purple-500/20 text-purple-300 border border-purple-500/30">
            <Lock className="w-3.5 h-3.5" />
            <span>READ-ONLY FLASH ANALYSIS — EDITING DISABLED</span>
          </span>
        </div>

        <div className="flex items-center space-x-2">
          <button
            onClick={handleLoadSample}
            className="px-3 py-1.5 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border text-xs flex items-center space-x-1.5"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Load Reference Stock Flash</span>
          </button>
        </div>
      </div>

      {/* Navigation Sub-Tabs */}
      <div className="flex items-center space-x-1 bg-can-panel border border-can-border p-1 rounded-lg text-xs font-sans">
        <button
          onClick={() => setActiveTab('project')}
          className={`px-3.5 py-1.5 rounded flex items-center space-x-1.5 font-bold transition-all ${
            activeTab === 'project' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
          }`}
        >
          <FileCode className="w-3.5 h-3.5" />
          <span>Project & Binary</span>
        </button>

        <button
          onClick={() => setActiveTab('objects')}
          className={`px-3.5 py-1.5 rounded flex items-center space-x-1.5 font-bold transition-all ${
            activeTab === 'objects' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
          }`}
        >
          <Layers className="w-3.5 h-3.5" />
          <span>Calibration Maps</span>
        </button>

        <button
          onClick={() => setActiveTab('hex')}
          className={`px-3.5 py-1.5 rounded flex items-center space-x-1.5 font-bold transition-all ${
            activeTab === 'hex' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
          }`}
        >
          <Binary className="w-3.5 h-3.5" />
          <span>Hex & ASCII</span>
        </button>

        <button
          onClick={() => setActiveTab('diff')}
          className={`px-3.5 py-1.5 rounded flex items-center space-x-1.5 font-bold transition-all ${
            activeTab === 'diff' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
          }`}
        >
          <GitCompare className="w-3.5 h-3.5" />
          <span>Binary Diff</span>
        </button>

        <button
          onClick={() => setActiveTab('checksums')}
          className={`px-3.5 py-1.5 rounded flex items-center space-x-1.5 font-bold transition-all ${
            activeTab === 'checksums' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
          }`}
        >
          <ShieldCheck className="w-3.5 h-3.5" />
          <span>Checksums</span>
        </button>

        <button
          onClick={() => setActiveTab('correlation')}
          className={`px-3.5 py-1.5 rounded flex items-center space-x-1.5 font-bold transition-all ${
            activeTab === 'correlation' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
          }`}
        >
          <Activity className="w-3.5 h-3.5" />
          <span>Live CAN ↔ Flash Correlation</span>
        </button>
      </div>

      {/* Main Tab Views */}
      <div className="flex-1 bg-can-panel border border-can-border rounded-lg p-4 overflow-hidden flex flex-col shadow-xl">
        {/* Tab 1: Project Overview */}
        {activeTab === 'project' && (
          <div className="space-y-4 overflow-y-auto">
            <div className="flex items-center space-x-2 border-b border-can-border pb-2.5 text-white font-bold text-xs font-sans">
              <Info className="w-4 h-4 text-sky-400" />
              <span>ECU BINARY IMAGE METADATA</span>
            </div>

            <div className="grid grid-cols-2 gap-4 text-xs">
              <div className="bg-[#0A0E17] border border-can-border rounded p-4 space-y-2.5">
                <div className="flex justify-between">
                  <span className="text-slate-400">File Name:</span>
                  <span className="text-white font-bold">{binaryInfo?.fileName}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">File Size:</span>
                  <span className="text-sky-300 font-bold">{((binaryInfo?.fileSizeBytes || 0) / 1024 / 1024).toFixed(2)} MB ({binaryInfo?.fileSizeBytes} bytes)</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Detected ECU Family:</span>
                  <span className="text-emerald-400 font-bold">{binaryInfo?.detectedEcuFamily}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Hardware Part Number:</span>
                  <span className="text-slate-200">{binaryInfo?.hardwareNumber}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Software Calibration ID:</span>
                  <span className="text-slate-200">{binaryInfo?.softwareNumber}</span>
                </div>
              </div>

              <div className="bg-[#0A0E17] border border-can-border rounded p-4 space-y-2.5">
                <span className="text-slate-400 block font-bold">Cryptographic Hashes:</span>
                <div>
                  <span className="text-[11px] text-slate-500 block">SHA-256:</span>
                  <span className="text-[11px] text-purple-300 break-all">{binaryInfo?.sha256Hash}</span>
                </div>
                <div>
                  <span className="text-[11px] text-slate-500 block">MD5:</span>
                  <span className="text-[11px] text-purple-300 break-all">{binaryInfo?.md5Hash}</span>
                </div>
                <div className="pt-2">
                  <button
                    onClick={handleRunDiff}
                    disabled={isLoading}
                    className="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded text-xs font-bold font-sans flex items-center justify-center space-x-1.5"
                  >
                    <GitCompare className="w-3.5 h-3.5" />
                    <span>Run Read-Only Binary Comparison vs Stage Diff</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Tab 2: Read-Only Calibration Objects & Maps */}
        {activeTab === 'objects' && (
          <div className="flex-1 flex gap-4 overflow-hidden">
            {/* Object List */}
            <div className="w-64 border-r border-can-border pr-3 flex flex-col space-y-2 overflow-y-auto">
              <span className="text-[10px] text-slate-500 font-bold uppercase tracking-wider">IDENTIFIED OBJECTS</span>
              {binaryInfo?.objects.map(obj => (
                <button
                  key={obj.id}
                  onClick={() => handleSelectObject(obj.id)}
                  className={`text-left p-2 rounded text-xs transition-all flex flex-col space-y-0.5 ${
                    selectedObjectId === obj.id
                      ? 'bg-sky-600/20 text-sky-400 border border-sky-500/30 font-bold'
                      : 'text-slate-400 hover:text-white hover:bg-can-card border border-transparent'
                  }`}
                >
                  <span className="font-sans">{obj.name}</span>
                  <div className="flex justify-between text-[10px] font-mono text-slate-500">
                    <span>{obj.objectType}</span>
                    <span>{obj.unit}</span>
                  </div>
                </button>
              ))}
            </div>

            {/* Read-Only Object Details & Map */}
            <div className="flex-1 flex flex-col overflow-hidden space-y-3">
              <div className="flex justify-between items-center border-b border-can-border pb-2">
                <div>
                  <h3 className="text-sm font-bold text-white font-sans">{selectedObject?.name}</h3>
                  <p className="text-[11px] text-slate-400 font-sans">{selectedObject?.description}</p>
                </div>

                <div className="flex items-center space-x-2">
                  <span className="px-2 py-0.5 rounded text-[10px] bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-bold">
                    {getProvenanceLabel(selectedObject?.provenance || 0)}
                  </span>
                  <span className="text-xs text-slate-400">
                    Offset: <span className="text-white font-bold">0x{selectedObject?.fileOffset.toString(16).toUpperCase()}</span>
                  </span>
                </div>
              </div>

              {/* Read-Only Matrix Display */}
              <div className="flex-1 overflow-auto border border-can-border rounded bg-[#0A0E17]">
                <table className="w-full text-center text-xs border-collapse font-mono">
                  <thead>
                    <tr className="bg-[#0B0F17] text-slate-400 border-b border-can-border">
                      <th className="p-2 border-r border-can-border text-[10px] uppercase">Y \ X (RPM)</th>
                      {selectedObject?.xBreakpoints.map((bp, idx) => (
                        <th key={idx} className="p-2 border-r border-can-border text-[11px] text-sky-300 font-bold">
                          {bp}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {selectedObject?.values.map((row, yIdx) => {
                      const yBp = selectedObject.yBreakpoints?.[yIdx] ?? yIdx;
                      return (
                        <tr key={yIdx} className="border-b border-can-border/60">
                          <td className="p-2 bg-[#0B0F17] text-amber-300 font-bold text-[11px] border-r border-can-border">
                            {yBp}
                          </td>
                          {row.map((val, xIdx) => (
                            <td key={xIdx} className="p-2 border-r border-can-border/40 text-slate-200">
                              {val.toFixed(1)}
                            </td>
                          ))}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}

        {/* Tab 3: Hex & ASCII Viewer */}
        {activeTab === 'hex' && (
          <div className="flex-1 flex flex-col space-y-3 overflow-hidden">
            <div className="flex justify-between items-center text-xs">
              <div className="flex items-center space-x-2">
                <span className="text-slate-400 font-sans">Jump to Offset:</span>
                <input
                  type="text"
                  value={`0x${hexOffset.toString(16).toUpperCase()}`}
                  onChange={e => {
                    const val = parseInt(e.target.value, 16);
                    if (!isNaN(val)) {
                      setHexOffset(val);
                      fetchHexDump(val);
                    }
                  }}
                  className="bg-can-panel border border-can-border rounded px-2 py-1 text-xs text-white font-mono w-28"
                />
              </div>

              <div className="flex space-x-2">
                <button
                  onClick={() => {
                    const next = Math.max(0, hexOffset - 256);
                    setHexOffset(next);
                    fetchHexDump(next);
                  }}
                  className="px-2.5 py-1 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border"
                >
                  -256 Bytes
                </button>
                <button
                  onClick={() => {
                    const next = hexOffset + 256;
                    setHexOffset(next);
                    fetchHexDump(next);
                  }}
                  className="px-2.5 py-1 bg-can-card hover:bg-slate-700 text-slate-300 rounded border border-can-border"
                >
                  +256 Bytes
                </button>
              </div>
            </div>

            <div className="flex-1 bg-[#0A0E17] border border-can-border rounded p-3 font-mono text-xs overflow-auto text-emerald-400 whitespace-pre">
              {hexDump}
            </div>
          </div>
        )}

        {/* Tab 4: Binary Diff */}
        {activeTab === 'diff' && (
          <div className="flex-1 flex flex-col space-y-3 overflow-hidden">
            <div className="flex justify-between items-center border-b border-can-border pb-2 text-xs">
              <span className="font-sans font-bold text-white uppercase">
                BYTE-BY-BYTE BINARY COMPARISON RESULT
              </span>
              <span className="text-slate-400">
                Differences: <span className="text-amber-400 font-bold">{diffResult?.totalDifferentBytes || 0} bytes</span> ({diffResult?.differencePercentage.toFixed(3)}%)
              </span>
            </div>

            {diffResult ? (
              <div className="flex-1 overflow-auto border border-can-border rounded bg-[#0A0E17]">
                <table className="w-full text-left text-xs font-mono">
                  <thead className="bg-[#0B0F17] text-slate-400 border-b border-can-border sticky top-0 uppercase text-[10px]">
                    <tr>
                      <th className="p-2.5">Offset</th>
                      <th className="p-2.5">Length</th>
                      <th className="p-2.5">Original Hex ({diffResult.originalFileName})</th>
                      <th className="p-2.5">Modified Hex ({diffResult.modifiedFileName})</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-can-border/60">
                    {diffResult.chunks.map((chunk, idx) => (
                      <tr key={idx} className="hover:bg-can-card/60">
                        <td className="p-2.5 font-bold text-sky-400">0x{chunk.offset.toString(16).toUpperCase()}</td>
                        <td className="p-2.5 text-slate-400">{chunk.length} B</td>
                        <td className="p-2.5 text-slate-300">{chunk.hexOriginal}</td>
                        <td className="p-2.5 text-amber-300 font-bold">{chunk.hexModified}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="flex-1 flex flex-col items-center justify-center text-slate-500 text-xs">
                <span>No binary diff loaded.</span>
                <button
                  onClick={handleRunDiff}
                  className="mt-3 px-4 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-sans font-bold"
                >
                  Execute Sample Comparison
                </button>
              </div>
            )}
          </div>
        )}

        {/* Tab 5: Checksums */}
        {activeTab === 'checksums' && (
          <div className="space-y-4 overflow-y-auto">
            <div className="flex items-center space-x-2 border-b border-can-border pb-2.5 text-white font-bold text-xs font-sans">
              <ShieldCheck className="w-4 h-4 text-emerald-400" />
              <span>READ-ONLY CHECKSUM & INTEGRITY VALIDATION</span>
            </div>

            <div className="space-y-3">
              {binaryInfo?.checksums.map((chk, idx) => (
                <div key={idx} className="bg-[#0A0E17] border border-can-border rounded p-4 flex justify-between items-center text-xs">
                  <div className="space-y-1">
                    <span className="font-sans font-bold text-white block">{chk.regionName}</span>
                    <div className="flex space-x-4 text-[11px] text-slate-400">
                      <span>Algorithm: <span className="text-slate-200">{chk.algorithm}</span></span>
                      <span>Range: <span className="text-slate-200">0x{chk.startOffset.toString(16).toUpperCase()} - 0x{chk.endOffset.toString(16).toUpperCase()}</span></span>
                      <span>Expected: <span className="text-purple-300 font-mono">{chk.expectedChecksumHex}</span></span>
                      <span>Calculated: <span className="text-purple-300 font-mono">{chk.calculatedChecksumHex}</span></span>
                    </div>
                  </div>

                  <span className="px-3 py-1 rounded text-xs font-sans font-bold flex items-center space-x-1 bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                    <CheckCircle2 className="w-3.5 h-3.5" />
                    <span>VALID</span>
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Tab 6: Live CAN ↔ Flash Correlation */}
        {activeTab === 'correlation' && (
          <div className="space-y-4 overflow-y-auto">
            <div className="flex items-center space-x-2 border-b border-can-border pb-2.5 text-white font-bold text-xs font-sans">
              <Activity className="w-4 h-4 text-sky-400" />
              <span>READ-ONLY LIVE CAN ↔ FLASH CALIBRATION CORRELATION</span>
            </div>

            <div className="grid grid-cols-3 gap-4">
              {correlations.map(corr => (
                <div key={corr.id} className="bg-[#0A0E17] border border-can-border rounded p-4 space-y-3 text-xs">
                  <div>
                    <span className="text-[10px] text-slate-500 uppercase font-sans font-bold block">{corr.calibrationCategory}</span>
                    <span className="text-white font-bold font-sans text-sm">{corr.objectName}</span>
                  </div>

                  <div className="space-y-1.5 pt-2 border-t border-can-border">
                    <div className="flex justify-between">
                      <span className="text-slate-400">Calibrated Flash Ceiling:</span>
                      <span className="text-purple-300 font-bold font-mono">{corr.calibratedLimit.toFixed(1)} {corr.unit}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-400">Live Observed CAN:</span>
                      <span className="text-emerald-300 font-bold font-mono">{corr.liveObservedValue.toFixed(1)} {corr.unit}</span>
                    </div>
                    <div className="flex justify-between pt-1 border-t border-can-border/60">
                      <span className="text-slate-400">Available Headroom:</span>
                      <span className="text-sky-300 font-bold font-mono">{corr.marginDelta.toFixed(1)} {corr.unit}</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
