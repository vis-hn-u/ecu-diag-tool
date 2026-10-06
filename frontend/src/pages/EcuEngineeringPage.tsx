import React, { useState, useEffect } from 'react';
import { api, downloadBlobAsFile } from '../services/api';
import { 
  EcuIdentificationProfile, 
  EcuProtocolCapabilityMatrix, 
  EcuFamilyProfile, 
  FieldWithConfidence, 
  EvidenceSource, 
  IdentificationConfidence,
  EcuMemoryMap,
  MemoryRegion,
  EcuReadCapabilityMatrix,
  EcuMemoryImage,
  ReadManifest,
  MemoryImageAnalysis,
  ReadSessionStatus,
  ExecutionEnvironment
} from '../types';
import { 
  Cpu, 
  Search, 
  ShieldCheck, 
  ShieldAlert, 
  AlertTriangle, 
  HelpCircle, 
  CheckCircle2, 
  RefreshCw, 
  Layers, 
  Database, 
  Clock, 
  Lock, 
  FileCode, 
  ChevronRight, 
  Info, 
  Terminal, 
  ArrowRight,
  Sliders,
  Download,
  Flame,
  FileCheck,
  Eye,
  Activity,
  HardDrive,
  Stethoscope,
  Heart,
  Binary,
  Network,
  Zap,
  Sparkles
} from 'lucide-react';
import { DiagnosticEnginePanel } from '../components/diagnostics/DiagnosticEnginePanel';
import { EcuHealthPanel } from '../components/diagnostics/EcuHealthPanel';
import { ProjectWorkspaceHeader } from '../components/workspace/ProjectWorkspaceHeader';
import { CanReverseEngineeringPanel } from '../components/diagnostics/CanReverseEngineeringPanel';
import { VehicleDigitalTwinPanel } from '../components/simulation/VehicleDigitalTwinPanel';
import { HilBenchPanel } from '../components/hil/HilBenchPanel';
import { AsamXcpPanel } from '../components/asam/AsamXcpPanel';
import { EcuProgrammingPanel } from '../components/programming/EcuProgrammingPanel';
import { AiResearchCopilotPanel } from '../components/ai/AiResearchCopilotPanel';
import { UnifiedTimelinePanel } from '../components/timeline/UnifiedTimelinePanel';

export const EcuEngineeringPage: React.FC = () => {
  const [subTab, setSubTab] = useState<
    | 'identification' 
    | 'protocols' 
    | 'memory-map' 
    | 'safe-reader' 
    | 'image-forensics' 
    | 'catalog' 
    | 'diagnostics' 
    | 'ecu-health'
    | 'reversing'
    | 'digital-twin'
    | 'hil-bench'
    | 'asam-xcp'
    | 'programming'
    | 'ai-copilot'
    | 'timeline'
  >('identification');

  // Target inputs
  const [requestCanIdHex, setRequestCanIdHex] = useState('0x7E0');
  const [responseCanIdHex, setResponseCanIdHex] = useState('0x7E8');
  const [ecuName, setEcuName] = useState('ECM');

  // Loading states
  const [loadingIdentify, setLoadingIdentify] = useState(false);
  const [loadingProbe, setLoadingProbe] = useState(false);
  const [loadingMemory, setLoadingMemory] = useState(false);
  const [loadingRead, setLoadingRead] = useState(false);
  const [loadingCatalog, setLoadingCatalog] = useState(false);
  const [statusMessage, setStatusMessage] = useState<{ text: string; isError?: boolean } | null>(null);

  // Phase 1 Data states
  const [identificationProfile, setIdentificationProfile] = useState<EcuIdentificationProfile | null>(null);
  const [capabilityMatrix, setCapabilityMatrix] = useState<EcuProtocolCapabilityMatrix | null>(null);
  const [catalogProfiles, setCatalogProfiles] = useState<EcuFamilyProfile[]>([]);
  const [selectedCatalogId, setSelectedCatalogId] = useState<string>('BOSCH_MED17_5_5');
  const [catalogSearch, setCatalogSearch] = useState('');
  const [activeEvidenceModal, setActiveEvidenceModal] = useState<EvidenceSource[] | null>(null);

  // Phase 2 Data states
  const [memoryMap, setMemoryMap] = useState<EcuMemoryMap | null>(null);
  const [readCapabilities, setReadCapabilities] = useState<EcuReadCapabilityMatrix | null>(null);
  const [storedImages, setStoredImages] = useState<EcuMemoryImage[]>([]);
  const [selectedImage, setSelectedImage] = useState<EcuMemoryImage | null>(null);
  const [imageAnalysis, setImageAnalysis] = useState<MemoryImageAnalysis | null>(null);
  const [activeSessionStatus, setActiveSessionStatus] = useState<ReadSessionStatus | null>(null);

  // Safe Read parameters
  const [readStartAddressHex, setReadStartAddressHex] = useState('0x001C0000');
  const [readLengthBytes, setReadLengthBytes] = useState(1024);
  const [readRegionName, setReadRegionName] = useState('Calibration Sector');
  const [readChunkSize, setReadChunkSize] = useState(256);
  const [readTimeoutMs, setReadTimeoutMs] = useState(5000);
  const [readInterChunkDelayMs, setReadInterChunkDelayMs] = useState(10);
  const [readEnvironment, setReadEnvironment] = useState<ExecutionEnvironment>('Simulation');
  const [readUserConfirmed, setReadUserConfirmed] = useState(false);
  const [showConfirmModal, setShowConfirmModal] = useState(false);

  // Load initial data
  useEffect(() => {
    loadCatalog();
    loadStoredImages();
  }, []);

  const loadCatalog = async () => {
    setLoadingCatalog(true);
    try {
      const list = await api.getEcuCompatibilityCatalog();
      setCatalogProfiles(list);
      if (list.length > 0 && !selectedCatalogId) {
        setSelectedCatalogId(list[0].familyId);
      }
    } catch (err: any) {
      console.error('Failed to load compatibility catalog', err);
    } finally {
      setLoadingCatalog(false);
    }
  };

  const loadStoredImages = async () => {
    try {
      const imgs = await api.getAllImages();
      setStoredImages(imgs);
      if (imgs.length > 0 && !selectedImage) {
        setSelectedImage(imgs[0]);
      }
    } catch (err) {
      console.error('Failed to load stored images', err);
    }
  };

  const handleIdentifyEcu = async () => {
    setLoadingIdentify(true);
    setStatusMessage(null);
    try {
      const reqId = parseInt(requestCanIdHex, 16) || 0x7E0;
      const respId = parseInt(responseCanIdHex, 16) || 0x7E8;
      const profile = await api.identifyEcu({
        requestCanId: reqId,
        responseCanId: respId,
        ecuName: ecuName.trim() || 'ECM'
      });
      setIdentificationProfile(profile);
      setStatusMessage({ text: `ECU '${profile.ecuName}' identification completed with ${profile.overallConfidence} confidence.` });
    } catch (err: any) {
      setStatusMessage({ text: `Identification failed: ${err.message}`, isError: true });
    } finally {
      setLoadingIdentify(false);
    }
  };

  const handleProbeProtocol = async () => {
    setLoadingProbe(true);
    setStatusMessage(null);
    try {
      const reqId = parseInt(requestCanIdHex, 16) || 0x7E0;
      const respId = parseInt(responseCanIdHex, 16) || 0x7E8;
      const matrix = await api.probeEcuProtocol({
        requestCanId: reqId,
        responseCanId: respId,
        ecuName: ecuName.trim() || 'ECM'
      });
      setCapabilityMatrix(matrix);
      setSubTab('protocols');
      setStatusMessage({ text: `Protocol discovery completed: Primary protocol detected as ${matrix.primaryProtocol}.` });
    } catch (err: any) {
      setStatusMessage({ text: `Protocol probe failed: ${err.message}`, isError: true });
    } finally {
      setLoadingProbe(false);
    }
  };

  const handleDiscoverMemory = async () => {
    setLoadingMemory(true);
    setStatusMessage(null);
    try {
      const reqId = parseInt(requestCanIdHex, 16) || 0x7E0;
      const respId = parseInt(responseCanIdHex, 16) || 0x7E8;
      const name = ecuName.trim() || 'ECM';

      const map = await api.discoverMemoryMap({
        requestCanId: reqId,
        responseCanId: respId,
        ecuName: name
      });
      setMemoryMap(map);

      const caps = await api.getReadCapabilities(name);
      setReadCapabilities(caps);

      setSubTab('memory-map');
      setStatusMessage({ text: `Discovered memory map with ${map.regions.length} regions (${map.gaps.length} address gaps detected).` });
    } catch (err: any) {
      setStatusMessage({ text: `Memory map discovery failed: ${err.message}`, isError: true });
    } finally {
      setLoadingMemory(false);
    }
  };

  const handleSelectRegionForRead = (region: MemoryRegion) => {
    setReadStartAddressHex(`0x${region.startAddress.toString(16).toUpperCase()}`);
    setReadLengthBytes(Math.min(region.length, 4096)); // Default preview to first 4KB of region
    setReadRegionName(region.regionName);
    setSubTab('safe-reader');
  };

  const handleExecuteSafeRead = async () => {
    setShowConfirmModal(false);
    setLoadingRead(true);
    setStatusMessage(null);
    try {
      const reqId = parseInt(requestCanIdHex, 16) || 0x7E0;
      const respId = parseInt(responseCanIdHex, 16) || 0x7E8;
      const startAddr = parseInt(readStartAddressHex, 16) || 0;

      const image = await api.startSafeRead({
        requestCanId: reqId,
        responseCanId: respId,
        ecuName: ecuName.trim() || 'ECM',
        startAddress: startAddr,
        lengthBytes: readLengthBytes,
        regionName: readRegionName,
        chunkSize: readChunkSize,
        timeoutMs: readTimeoutMs,
        interChunkDelayMs: readInterChunkDelayMs,
        environment: readEnvironment,
        userConfirmed: true
      });

      setSelectedImage(image);
      await loadStoredImages();
      setStatusMessage({ text: `Safe read completed successfully! Image saved with SHA-256: ${image.sha256Hash}` });
    } catch (err: any) {
      setStatusMessage({ text: `Safe read failed: ${err.message}`, isError: true });
    } finally {
      setLoadingRead(false);
    }
  };

  const handleAnalyzeImage = async (img: EcuMemoryImage) => {
    try {
      const analysis = await api.analyzeImage(img.imageId);
      setImageAnalysis(analysis);
      setSelectedImage(img);
      setSubTab('image-forensics');
      setStatusMessage({ text: `Analyzed image '${img.imageId}' (Shannon Entropy: ${analysis.shannonEntropy})` });
    } catch (err: any) {
      setStatusMessage({ text: `Image analysis failed: ${err.message}`, isError: true });
    }
  };

  const handleSendToFlashAnalyzer = async (img: EcuMemoryImage) => {
    try {
      await api.sendToFlashAnalyzer(img.imageId);
      setStatusMessage({ text: `Image '${img.imageId}' successfully loaded into Flash Analyzer.` });
    } catch (err: any) {
      setStatusMessage({ text: `Failed to load into Flash Analyzer: ${err.message}`, isError: true });
    }
  };

  const handleDownloadManifest = async (img: EcuMemoryImage) => {
    try {
      const manifest = await api.getImageManifest(img.imageId);
      if (manifest) {
        const blob = new Blob([JSON.stringify(manifest, null, 2)], { type: 'application/json' });
        downloadBlobAsFile(blob, `${img.ecuName}_${img.regionName}_manifest.json`);
      }
    } catch (err: any) {
      setStatusMessage({ text: `Manifest download failed: ${err.message}`, isError: true });
    }
  };

  const handleDownloadRaw = async (img: EcuMemoryImage) => {
    try {
      const blob = await api.downloadImageRaw(img.imageId);
      downloadBlobAsFile(blob, `${img.ecuName}_${img.regionName}.bin`);
    } catch (err: any) {
      setStatusMessage({ text: `Raw download failed: ${err.message}`, isError: true });
    }
  };

  const renderConfidenceBadge = (confidence: IdentificationConfidence) => {
    switch (confidence) {
      case 'Confirmed':
        return (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-mono font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
            <CheckCircle2 className="w-3 h-3" /> CONFIRMED
          </span>
        );
      case 'Detected':
        return (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-mono font-medium bg-sky-500/10 text-sky-400 border border-sky-500/30">
            <ShieldCheck className="w-3 h-3" /> DETECTED
          </span>
        );
      case 'Inferred':
        return (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-mono font-medium bg-amber-500/10 text-amber-400 border border-amber-500/30">
            <AlertTriangle className="w-3 h-3" /> INFERRED
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-mono font-medium bg-slate-700/30 text-slate-400 border border-slate-700">
            <HelpCircle className="w-3 h-3" /> UNKNOWN
          </span>
        );
    }
  };

  const renderFieldRow = (label: string, field?: FieldWithConfidence<any>, fallbackUnit?: string) => {
    const val = field?.value;
    const confidence = field?.confidence ?? 'Unknown';
    const citations = field?.citations ?? [];

    return (
      <div className="flex items-center justify-between py-2 border-b border-can-border/40 hover:bg-slate-800/30 px-2 rounded transition-colors">
        <div className="flex items-center space-x-2">
          <span className="text-xs text-slate-400 font-medium">{label}</span>
          {citations.length > 0 && (
            <button
              onClick={() => setActiveEvidenceModal(citations)}
              className="text-[10px] text-sky-400 hover:text-sky-300 underline font-mono cursor-pointer"
              title="View evidence source citations"
            >
              [{citations.length} citation{citations.length > 1 ? 's' : ''}]
            </button>
          )}
        </div>
        <div className="flex items-center space-x-3">
          <span className="font-mono text-xs text-slate-200 font-semibold">
            {val !== undefined && val !== null && val !== '' ? `${val}${fallbackUnit ? ` ${fallbackUnit}` : ''}` : '—'}
          </span>
          {renderConfidenceBadge(confidence)}
        </div>
      </div>
    );
  };

  const selectedCatalogProfile = catalogProfiles.find(p => p.familyId === selectedCatalogId) ?? catalogProfiles[0];
  const filteredCatalog = catalogProfiles.filter(p => 
    p.name.toLowerCase().includes(catalogSearch.toLowerCase()) ||
    p.manufacturer.toLowerCase().includes(catalogSearch.toLowerCase()) ||
    p.familyId.toLowerCase().includes(catalogSearch.toLowerCase())
  );

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-100 overflow-y-auto p-4 space-y-4 font-sans">
      {/* Project Workspace & Automated 1-Click Sequence Header */}
      <ProjectWorkspaceHeader 
        onStartOneClick={() => {
          setStatusMessage({ text: 'One-Click Automated Diagnostic & Discovery sequence completed successfully.' });
          setSubTab('timeline');
        }}
        onOpenCopilot={() => setSubTab('ai-copilot')}
      />

      {/* Header bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 bg-can-panel border border-can-border p-4 rounded-lg shadow-sm">
        <div className="flex items-center space-x-3">
          <div className="w-10 h-10 rounded-lg bg-sky-500/10 border border-sky-500/30 flex items-center justify-center text-sky-400">
            <Cpu className="w-6 h-6" />
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <h1 className="text-base font-bold text-white tracking-wide">ECU Engineering & Safe Read Platform</h1>
              <span className="px-2 py-0.5 rounded text-[10px] font-mono bg-sky-500/10 text-sky-400 border border-sky-500/30 font-semibold">
                PHASE 2 VALIDATED
              </span>
            </div>
            <p className="text-xs text-slate-400">
              Evidence-based ECU discovery, memory map discovery, and safe chunked UDS memory reads.
            </p>
          </div>
        </div>

        {/* Scan / Target Inputs */}
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex items-center space-x-1 bg-can-card px-2.5 py-1.5 rounded border border-can-border">
            <span className="text-[11px] font-mono text-slate-400">REQ:</span>
            <input
              type="text"
              value={requestCanIdHex}
              onChange={e => setRequestCanIdHex(e.target.value)}
              className="w-16 bg-transparent text-xs font-mono text-sky-300 focus:outline-none"
              placeholder="0x7E0"
            />
          </div>
          <div className="flex items-center space-x-1 bg-can-card px-2.5 py-1.5 rounded border border-can-border">
            <span className="text-[11px] font-mono text-slate-400">RESP:</span>
            <input
              type="text"
              value={responseCanIdHex}
              onChange={e => setResponseCanIdHex(e.target.value)}
              className="w-16 bg-transparent text-xs font-mono text-sky-300 focus:outline-none"
              placeholder="0x7E8"
            />
          </div>
          <div className="flex items-center space-x-1 bg-can-card px-2.5 py-1.5 rounded border border-can-border">
            <span className="text-[11px] font-mono text-slate-400">MODULE:</span>
            <input
              type="text"
              value={ecuName}
              onChange={e => setEcuName(e.target.value)}
              className="w-16 bg-transparent text-xs font-mono text-sky-300 focus:outline-none"
              placeholder="ECM"
            />
          </div>

          <button
            onClick={handleIdentifyEcu}
            disabled={loadingIdentify}
            className="flex items-center space-x-1.5 px-3 py-1.5 rounded text-xs font-medium bg-sky-600 hover:bg-sky-500 text-white shadow transition-colors disabled:opacity-50"
          >
            <Search className={`w-3.5 h-3.5 ${loadingIdentify ? 'animate-spin' : ''}`} />
            <span>{loadingIdentify ? 'Identifying...' : 'Identify ECU'}</span>
          </button>

          <button
            onClick={handleDiscoverMemory}
            disabled={loadingMemory}
            className="flex items-center space-x-1.5 px-3 py-1.5 rounded text-xs font-medium bg-indigo-600 hover:bg-indigo-500 text-white shadow transition-colors disabled:opacity-50"
          >
            <Layers className={`w-3.5 h-3.5 ${loadingMemory ? 'animate-spin' : ''}`} />
            <span>{loadingMemory ? 'Mapping...' : 'Discover Memory'}</span>
          </button>
        </div>
      </div>

      {/* Status banner */}
      {statusMessage && (
        <div className={`p-3 rounded-lg border text-xs flex items-center justify-between ${
          statusMessage.isError 
            ? 'bg-rose-500/10 border-rose-500/30 text-rose-300' 
            : 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
        }`}>
          <span>{statusMessage.text}</span>
          <button onClick={() => setStatusMessage(null)} className="text-slate-400 hover:text-white">✕</button>
        </div>
      )}

      {/* Sub Tabs */}
      <div className="flex flex-wrap items-center gap-2 border-b border-can-border">
        <button
          onClick={() => setSubTab('identification')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'identification'
              ? 'border-sky-500 text-sky-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Cpu className="w-4 h-4" />
          <span>Identification</span>
          {identificationProfile && <span className="w-2 h-2 rounded-full bg-emerald-400" />}
        </button>

        <button
          onClick={() => setSubTab('protocols')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'protocols'
              ? 'border-sky-500 text-sky-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Terminal className="w-4 h-4" />
          <span>Protocols & Capabilities</span>
          {capabilityMatrix && <span className="w-2 h-2 rounded-full bg-sky-400" />}
        </button>

        <button
          onClick={() => setSubTab('memory-map')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'memory-map'
              ? 'border-indigo-500 text-indigo-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Layers className="w-4 h-4" />
          <span>Memory Map</span>
          {memoryMap && <span className="w-2 h-2 rounded-full bg-indigo-400" />}
        </button>

        <button
          onClick={() => setSubTab('safe-reader')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'safe-reader'
              ? 'border-emerald-500 text-emerald-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <HardDrive className="w-4 h-4" />
          <span>Safe Memory Reader</span>
        </button>

        <button
          onClick={() => setSubTab('image-forensics')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'image-forensics'
              ? 'border-purple-500 text-purple-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Activity className="w-4 h-4" />
          <span>Forensics & Images ({storedImages.length})</span>
        </button>

        <button
          onClick={() => setSubTab('catalog')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'catalog'
              ? 'border-sky-500 text-sky-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Database className="w-4 h-4" />
          <span>Compatibility Catalog ({catalogProfiles.length})</span>
        </button>

        <button
          onClick={() => setSubTab('diagnostics')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'diagnostics'
              ? 'border-rose-500 text-rose-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Stethoscope className="w-4 h-4" />
          <span>Diagnostic Engine</span>
        </button>

        <button
          onClick={() => setSubTab('ecu-health')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'ecu-health'
              ? 'border-purple-500 text-purple-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Heart className="w-4 h-4" />
          <span>ECU Health &amp; OEM</span>
        </button>

        <button
          onClick={() => setSubTab('reversing')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'reversing'
              ? 'border-amber-500 text-amber-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Binary className="w-4 h-4" />
          <span>CAN Reversing Lab</span>
        </button>

        <button
          onClick={() => setSubTab('digital-twin')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'digital-twin'
              ? 'border-cyan-500 text-cyan-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Network className="w-4 h-4" />
          <span>Digital Twin</span>
        </button>

        <button
          onClick={() => setSubTab('hil-bench')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'hil-bench'
              ? 'border-yellow-500 text-yellow-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Zap className="w-4 h-4" />
          <span>HIL &amp; Power Bench</span>
        </button>

        <button
          onClick={() => setSubTab('asam-xcp')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'asam-xcp'
              ? 'border-blue-500 text-blue-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <FileCode className="w-4 h-4" />
          <span>ASAM (A2L/ODX/XCP)</span>
        </button>

        <button
          onClick={() => setSubTab('programming')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'programming'
              ? 'border-rose-500 text-rose-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Flame className="w-4 h-4" />
          <span>12-Stage Flashing</span>
        </button>

        <button
          onClick={() => setSubTab('ai-copilot')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'ai-copilot'
              ? 'border-violet-500 text-violet-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Sparkles className="w-4 h-4" />
          <span>AI Copilot &amp; Research</span>
        </button>

        <button
          onClick={() => setSubTab('timeline')}
          className={`flex items-center space-x-2 px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
            subTab === 'timeline'
              ? 'border-emerald-500 text-emerald-400 font-semibold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Clock className="w-4 h-4" />
          <span>Forensic Timeline</span>
        </button>
      </div>

      {/* SubTab Panels */}
      {subTab === 'diagnostics' && (
        <DiagnosticEnginePanel
          requestCanId={parseInt(requestCanIdHex, 16) || 0x7E0}
          responseCanId={parseInt(responseCanIdHex, 16) || 0x7E8}
          ecuName={ecuName.trim() || 'ECM'}
        />
      )}

      {subTab === 'ecu-health' && (
        <EcuHealthPanel
          requestCanId={parseInt(requestCanIdHex, 16) || 0x7E0}
          responseCanId={parseInt(responseCanIdHex, 16) || 0x7E8}
          ecuName={ecuName.trim() || 'ECM'}
        />
      )}

      {subTab === 'reversing' && <CanReverseEngineeringPanel />}
      {subTab === 'digital-twin' && <VehicleDigitalTwinPanel />}
      {subTab === 'hil-bench' && <HilBenchPanel />}
      {subTab === 'asam-xcp' && <AsamXcpPanel />}
      {subTab === 'programming' && <EcuProgrammingPanel />}
      {subTab === 'ai-copilot' && <AiResearchCopilotPanel />}
      {subTab === 'timeline' && <UnifiedTimelinePanel />}

      {/* Tab 1: Identification */}
      {subTab === 'identification' && (

        <div className="space-y-4">
          {!identificationProfile ? (
            <div className="p-8 text-center bg-can-panel border border-can-border rounded-lg space-y-3">
              <Cpu className="w-12 h-12 text-slate-600 mx-auto" />
              <div className="text-slate-300 font-semibold text-sm">No Active Identification Performed</div>
              <p className="text-xs text-slate-500 max-w-md mx-auto">
                Click <strong>"Identify ECU"</strong> to send non-destructive UDS identification queries.
              </p>
              <button
                onClick={handleIdentifyEcu}
                disabled={loadingIdentify}
                className="px-4 py-2 rounded text-xs font-medium bg-sky-600 hover:bg-sky-500 text-white shadow"
              >
                Run Identification Probe
              </button>
            </div>
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              <div className="bg-can-panel border border-can-border rounded-lg p-4 space-y-4">
                <div className="flex items-center justify-between border-b border-can-border pb-3">
                  <div>
                    <h2 className="text-sm font-bold text-white">Module Identification: {identificationProfile.ecuName}</h2>
                    <p className="text-[11px] font-mono text-slate-400">
                      CAN ID: 0x{identificationProfile.requestCanId.toString(16).toUpperCase()} / 0x{identificationProfile.responseCanId.toString(16).toUpperCase()}
                    </p>
                  </div>
                  <div className="text-right">
                    <span className="text-[10px] text-slate-400 uppercase font-mono block">Overall Confidence</span>
                    {renderConfidenceBadge(identificationProfile.overallConfidence)}
                  </div>
                </div>

                <div className="space-y-1">
                  {renderFieldRow('Vehicle Identification Number (VIN)', identificationProfile.vin)}
                  {renderFieldRow('ECU Part Number', identificationProfile.ecuPartNumber)}
                  {renderFieldRow('ECU Hardware Number', identificationProfile.hardwareNumber)}
                  {renderFieldRow('ECU Software Number', identificationProfile.softwareNumber)}
                  {renderFieldRow('Software Version', identificationProfile.softwareVersion)}
                  {renderFieldRow('Calibration ID', identificationProfile.calibrationId)}
                  {renderFieldRow('ECU Family / Series', identificationProfile.ecuFamily)}
                  {renderFieldRow('Manufacturer', identificationProfile.manufacturer)}
                </div>
              </div>

              <div className="bg-can-panel border border-can-border rounded-lg p-4 space-y-4">
                <div className="border-b border-can-border pb-3">
                  <h2 className="text-sm font-bold text-white">Internal Architecture & Network Profile</h2>
                  <p className="text-[11px] font-mono text-slate-400">
                    Microcontroller architecture and memory characteristics.
                  </p>
                </div>

                <div className="space-y-1">
                  {renderFieldRow('Microcontroller (MCU)', identificationProfile.mcuArchitecture)}
                  {renderFieldRow('Memory Characteristics', identificationProfile.memoryCharacteristics)}
                  {renderFieldRow('Diagnostic Protocol', identificationProfile.diagnosticProtocol)}
                  {renderFieldRow('CAN Addressing Scheme', identificationProfile.canAddressing)}
                  {renderFieldRow('CAN Nominal Bitrate', identificationProfile.canBitrate, 'bps')}
                </div>

                <div className="p-3 bg-can-card rounded border border-can-border flex items-center justify-between text-xs">
                  <span className="text-slate-300">
                    Evidence Citations: <strong>{identificationProfile.allEvidence.length}</strong>
                  </span>
                  {identificationProfile.allEvidence.length > 0 && (
                    <button
                      onClick={() => setActiveEvidenceModal(identificationProfile.allEvidence)}
                      className="px-2.5 py-1 rounded bg-sky-600/20 text-sky-300 hover:bg-sky-600/30 font-medium"
                    >
                      Inspect Evidence Table
                    </button>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Tab 2: Protocols */}
      {subTab === 'protocols' && (
        <div className="space-y-4">
          {!capabilityMatrix ? (
            <div className="p-8 text-center bg-can-panel border border-can-border rounded-lg space-y-3">
              <Terminal className="w-12 h-12 text-slate-600 mx-auto" />
              <div className="text-slate-300 font-semibold text-sm">No Protocol Discovery Matrix Available</div>
              <button
                onClick={handleProbeProtocol}
                disabled={loadingProbe}
                className="px-4 py-2 rounded text-xs font-medium bg-emerald-600 hover:bg-emerald-500 text-white shadow"
              >
                Run Protocol Probe
              </button>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
                <div className="bg-can-panel border border-can-border p-3 rounded-lg">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">Primary Protocol</span>
                  <div className="text-sm font-bold text-sky-400 mt-1">{capabilityMatrix.primaryProtocol}</div>
                </div>
                <div className="bg-can-panel border border-can-border p-3 rounded-lg">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">ISO-TP Capabilities</span>
                  <div className="text-sm font-bold text-emerald-400 mt-1">
                    {capabilityMatrix.supportsIsoTp ? 'Supported (ISO 15765-2)' : 'Raw Only'}
                  </div>
                </div>
                <div className="bg-can-panel border border-can-border p-3 rounded-lg">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">Client Timing (P2 / P2*)</span>
                  <div className="text-sm font-bold text-amber-400 mt-1">
                    P2: {capabilityMatrix.timingP2ClientMs}ms | P2*: {capabilityMatrix.timingP2StarClientMs}ms
                  </div>
                </div>
                <div className="bg-can-panel border border-can-border p-3 rounded-lg">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">Security Constraint</span>
                  <div className="text-sm font-bold text-rose-400 mt-1">
                    {capabilityMatrix.securityAccessRequiredForCalibrations ? 'Security Access Required' : 'Open / Unlocked'}
                  </div>
                </div>
              </div>

              <div className="bg-can-panel border border-can-border rounded-lg overflow-hidden">
                <table className="w-full text-left text-xs">
                  <thead className="bg-slate-900/80 text-slate-400 font-mono text-[11px] border-b border-can-border">
                    <tr>
                      <th className="py-2.5 px-3">SID</th>
                      <th className="py-2.5 px-3">Service Name</th>
                      <th className="py-2.5 px-3">Status</th>
                      <th className="py-2.5 px-3">Notes</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-can-border/40 font-mono">
                    {Object.values(capabilityMatrix.services || {}).map(srv => (
                      <tr key={srv.serviceId} className="hover:bg-slate-800/20">
                        <td className="py-2 px-3 text-sky-400 font-bold">
                          0x{srv.serviceId.toString(16).toUpperCase().padStart(2, '0')}
                        </td>
                        <td className="py-2 px-3 text-slate-200 font-sans font-medium">{srv.serviceName}</td>
                        <td className="py-2 px-3">
                          {srv.isSupported ? (
                            <span className="inline-flex items-center gap-1 text-emerald-400 font-semibold text-[11px]">
                              <CheckCircle2 className="w-3 h-3" /> ACTIVE
                            </span>
                          ) : (
                            <span className="text-slate-500 text-[11px]">NOT SUPPORTED</span>
                          )}
                        </td>
                        <td className="py-2 px-3 text-slate-400 font-sans text-[11px]">{srv.notes || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Tab 3: Memory Map (Phase 2) */}
      {subTab === 'memory-map' && (
        <div className="space-y-4">
          {!memoryMap ? (
            <div className="p-8 text-center bg-can-panel border border-can-border rounded-lg space-y-3">
              <Layers className="w-12 h-12 text-indigo-500 mx-auto" />
              <div className="text-slate-300 font-semibold text-sm">No Discovered Memory Map Available</div>
              <p className="text-xs text-slate-500 max-w-md mx-auto">
                Discover the ECU memory map from authoritative OEM catalog specifications, diagnostic responses, and architectural analysis.
              </p>
              <button
                onClick={handleDiscoverMemory}
                disabled={loadingMemory}
                className="px-4 py-2 rounded text-xs font-medium bg-indigo-600 hover:bg-indigo-500 text-white shadow"
              >
                Discover Memory Map
              </button>
            </div>
          ) : (
            <div className="space-y-4">
              {/* Summary Stats */}
              <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
                <div className="bg-can-panel border border-can-border p-3 rounded-lg">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">ECU Architecture</span>
                  <div className="text-sm font-bold text-indigo-400 mt-1">{memoryMap.ecuFamily || 'Generic Automotive'}</div>
                  <div className="text-[11px] text-slate-500 font-mono mt-0.5">
                    Addressable: {(memoryMap.totalAddressableBytes / 1024 / 1024).toFixed(2)} MB
                  </div>
                </div>

                <div className="bg-can-panel border border-can-border p-3 rounded-lg">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">Memory Sectors</span>
                  <div className="text-sm font-bold text-white mt-1">{memoryMap.regions.length} Regions Bounded</div>
                  <div className="text-[11px] text-slate-500 font-mono mt-0.5">{memoryMap.gaps.length} Unmapped Gaps</div>
                </div>

                <div className="bg-can-panel border border-can-border p-3 rounded-lg">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">Read Access Mode</span>
                  <div className="text-sm font-bold text-emerald-400 mt-1">
                    {readCapabilities?.supportsReadMemoryByAddress0x23 ? 'UDS 0x23 Supported' : 'Read By DID / Upload'}
                  </div>
                  <div className="text-[11px] text-slate-500 font-mono mt-0.5">Chunk Size: 256 bytes safe</div>
                </div>

                <div className="bg-can-panel border border-can-border p-3 rounded-lg">
                  <span className="text-[10px] font-mono text-slate-400 uppercase">Security Boundary</span>
                  <div className="text-sm font-bold text-amber-400 mt-1">Protected Sectors</div>
                  <div className="text-[11px] text-slate-500 font-mono mt-0.5">Seed/Key 0x27 Enforced</div>
                </div>
              </div>

              {/* Memory Region Table */}
              <div className="bg-can-panel border border-can-border rounded-lg overflow-hidden">
                <div className="p-3 border-b border-can-border bg-slate-900/50 flex items-center justify-between">
                  <h3 className="text-xs font-bold text-white uppercase tracking-wider flex items-center gap-2">
                    <Layers className="w-4 h-4 text-indigo-400" />
                    <span>Discovered ECU Memory Regions & Access Privileges</span>
                  </h3>
                  <button
                    onClick={handleDiscoverMemory}
                    disabled={loadingMemory}
                    className="flex items-center space-x-1 text-xs text-sky-400 hover:text-sky-300 font-mono"
                  >
                    <RefreshCw className={`w-3 h-3 ${loadingMemory ? 'animate-spin' : ''}`} />
                    <span>Refresh</span>
                  </button>
                </div>

                <table className="w-full text-left text-xs font-mono">
                  <thead className="bg-slate-900/80 text-slate-400 text-[11px] border-b border-can-border">
                    <tr>
                      <th className="py-2.5 px-3">Region Name</th>
                      <th className="py-2.5 px-3">Address Range</th>
                      <th className="py-2.5 px-3">Size</th>
                      <th className="py-2.5 px-3">Type</th>
                      <th className="py-2.5 px-3">Read Privilege</th>
                      <th className="py-2.5 px-3">Session</th>
                      <th className="py-2.5 px-3">Provenance</th>
                      <th className="py-2.5 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-can-border/30">
                    {memoryMap.regions.map((reg, idx) => (
                      <tr key={idx} className="hover:bg-slate-800/30">
                        <td className="py-2 px-3 text-slate-200 font-sans font-semibold">
                          {reg.regionName}
                        </td>
                        <td className="py-2 px-3 text-sky-400">
                          0x{reg.startAddress.toString(16).toUpperCase().padStart(8, '0')} — 0x{reg.endAddress.toString(16).toUpperCase().padStart(8, '0')}
                        </td>
                        <td className="py-2 px-3 text-slate-300">
                          {(reg.length / 1024).toFixed(1)} KB
                        </td>
                        <td className="py-2 px-3">
                          <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300 border border-slate-700">
                            {reg.type}
                          </span>
                        </td>
                        <td className="py-2 px-3">
                          {reg.readCapability === 'ReadSupported' ? (
                            <span className="text-emerald-400 text-[11px] font-semibold">DIRECT READ</span>
                          ) : reg.readCapability === 'RequiresSecurityAccess' ? (
                            <span className="text-amber-400 text-[11px] font-semibold flex items-center gap-1">
                              <Lock className="w-3 h-3" /> AUTH REQUIRED
                            </span>
                          ) : (
                            <span className="text-sky-400 text-[11px]">DIAG SESSION</span>
                          )}
                        </td>
                        <td className="py-2 px-3 text-slate-400">
                          0x{reg.requiredSession.toString(16).toUpperCase().padStart(2, '0')}
                        </td>
                        <td className="py-2 px-3">
                          <span className="text-[10px] text-slate-400">
                            {reg.provenance}
                          </span>
                        </td>
                        <td className="py-2 px-3 text-right">
                          <button
                            onClick={() => handleSelectRegionForRead(reg)}
                            className="px-2.5 py-1 rounded bg-indigo-600 hover:bg-indigo-500 text-white text-[11px] font-sans font-medium transition-colors"
                          >
                            Read Sector
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Unmapped Gaps */}
              {memoryMap.gaps.length > 0 && (
                <div className="bg-can-panel border border-can-border rounded-lg p-3 space-y-2">
                  <div className="text-xs font-bold text-amber-400 uppercase tracking-wide flex items-center gap-1.5">
                    <AlertTriangle className="w-3.5 h-3.5" />
                    <span>Unmapped Address Gaps ({memoryMap.gaps.length})</span>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs font-mono">
                    {memoryMap.gaps.map((gap, i) => (
                      <div key={i} className="p-2 rounded bg-can-card border border-can-border text-slate-400 flex justify-between">
                        <span>0x{gap.startAddress.toString(16).toUpperCase()} — 0x{gap.endAddress.toString(16).toUpperCase()}</span>
                        <span className="text-slate-500">{(gap.lengthBytes / 1024).toFixed(1)} KB (Reserved / Empty)</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Tab 4: Safe Memory Reader (Phase 2) */}
      {subTab === 'safe-reader' && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          {/* Reader Configuration Panel */}
          <div className="bg-can-panel border border-can-border rounded-lg p-4 space-y-4">
            <div className="border-b border-can-border pb-3">
              <h2 className="text-sm font-bold text-white flex items-center gap-2">
                <HardDrive className="w-4 h-4 text-emerald-400" />
                <span>Safe Read Controller</span>
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">
                Deterministic chunked memory extraction with boundary checks and SHA-256 integrity verification.
              </p>
            </div>

            <div className="space-y-3 text-xs">
              <div>
                <label className="block text-slate-400 font-medium mb-1">Target Sector / Region Name:</label>
                <input
                  type="text"
                  value={readRegionName}
                  onChange={e => setReadRegionName(e.target.value)}
                  className="w-full bg-can-card border border-can-border rounded px-2.5 py-1.5 text-white font-mono"
                />
              </div>

              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="block text-slate-400 font-medium mb-1">Start Address (Hex):</label>
                  <input
                    type="text"
                    value={readStartAddressHex}
                    onChange={e => setReadStartAddressHex(e.target.value)}
                    className="w-full bg-can-card border border-can-border rounded px-2.5 py-1.5 text-sky-400 font-mono"
                    placeholder="0x001C0000"
                  />
                </div>
                <div>
                  <label className="block text-slate-400 font-medium mb-1">Length (Bytes):</label>
                  <input
                    type="number"
                    value={readLengthBytes}
                    onChange={e => setReadLengthBytes(parseInt(e.target.value) || 0)}
                    className="w-full bg-can-card border border-can-border rounded px-2.5 py-1.5 text-white font-mono"
                    min={1}
                    max={16777216}
                  />
                </div>
              </div>

              <div>
                <label className="block text-slate-400 font-medium mb-1">Execution Environment:</label>
                <select
                  value={readEnvironment}
                  onChange={e => setReadEnvironment(e.target.value as ExecutionEnvironment)}
                  className="w-full bg-can-card border border-can-border rounded px-2.5 py-1.5 text-white text-xs"
                >
                  <option value="Simulation">Simulation Mode (Non-invasive virtual ECU)</option>
                  <option value="Bench">Bench Interface (Direct harness / ECU pinout)</option>
                  <option value="Vehicle">Vehicle OBD-II Gateway</option>
                </select>
              </div>

              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="block text-slate-400 font-medium mb-1">Chunk Size:</label>
                  <select
                    value={readChunkSize}
                    onChange={e => setReadChunkSize(parseInt(e.target.value))}
                    className="w-full bg-can-card border border-can-border rounded px-2 py-1.5 text-slate-200"
                  >
                    <option value={64}>64 Bytes</option>
                    <option value={128}>128 Bytes</option>
                    <option value={256}>256 Bytes (Recommended)</option>
                    <option value={512}>512 Bytes</option>
                  </select>
                </div>
                <div>
                  <label className="block text-slate-400 font-medium mb-1">Inter-Chunk Delay:</label>
                  <select
                    value={readInterChunkDelayMs}
                    onChange={e => setReadInterChunkDelayMs(parseInt(e.target.value))}
                    className="w-full bg-can-card border border-can-border rounded px-2 py-1.5 text-slate-200"
                  >
                    <option value={0}>0 ms (Full speed)</option>
                    <option value={10}>10 ms (Standard)</option>
                    <option value={25}>25 ms (Protected)</option>
                  </select>
                </div>
              </div>

              <div className="pt-2 border-t border-can-border">
                <button
                  onClick={() => setShowConfirmModal(true)}
                  disabled={loadingRead}
                  className="w-full py-2.5 px-4 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs shadow flex items-center justify-center space-x-2 transition-colors disabled:opacity-50"
                >
                  <HardDrive className={`w-4 h-4 ${loadingRead ? 'animate-spin' : ''}`} />
                  <span>{loadingRead ? 'Reading Memory...' : 'Initiate Safe Memory Read'}</span>
                </button>
              </div>
            </div>
          </div>

          {/* Active Read & Image Result Panel */}
          <div className="lg:col-span-2 space-y-4">
            {selectedImage ? (
              <div className="bg-can-panel border border-can-border rounded-lg p-4 space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-2 border-b border-can-border pb-3">
                  <div>
                    <h3 className="text-sm font-bold text-white flex items-center gap-2">
                      <FileCheck className="w-4 h-4 text-emerald-400" />
                      <span>Extracted Memory Image: {selectedImage.imageId}</span>
                    </h3>
                    <p className="text-[11px] font-mono text-slate-400 mt-0.5">
                      Sector: {selectedImage.regionName} | Size: {selectedImage.length} bytes ({(selectedImage.length / 1024).toFixed(1)} KB)
                    </p>
                  </div>
                  <div className="flex items-center space-x-2">
                    <button
                      onClick={() => handleDownloadRaw(selectedImage)}
                      className="px-2.5 py-1.5 rounded bg-can-card hover:bg-slate-700 text-xs text-white border border-can-border flex items-center space-x-1"
                    >
                      <Download className="w-3.5 h-3.5" />
                      <span>Download .BIN</span>
                    </button>
                    <button
                      onClick={() => handleDownloadManifest(selectedImage)}
                      className="px-2.5 py-1.5 rounded bg-can-card hover:bg-slate-700 text-xs text-sky-400 border border-can-border flex items-center space-x-1"
                    >
                      <FileCode className="w-3.5 h-3.5" />
                      <span>Manifest</span>
                    </button>
                  </div>
                </div>

                <div className="p-3 bg-slate-950 rounded border border-slate-800 space-y-2 text-xs font-mono">
                  <div className="text-slate-400 text-[11px]">SHA-256 Integrity Checksum:</div>
                  <div className="text-emerald-400 break-all text-xs font-bold">{selectedImage.sha256Hash}</div>
                </div>

                <div className="grid grid-cols-2 md:grid-cols-4 gap-2 text-xs">
                  <div className="p-2.5 bg-can-card rounded border border-can-border">
                    <span className="text-[10px] text-slate-400 block uppercase">Protocol</span>
                    <span className="text-slate-200 font-semibold">{selectedImage.diagnosticProtocol}</span>
                  </div>
                  <div className="p-2.5 bg-can-card rounded border border-can-border">
                    <span className="text-[10px] text-slate-400 block uppercase">Method</span>
                    <span className="text-slate-200 font-semibold">{selectedImage.readMethod}</span>
                  </div>
                  <div className="p-2.5 bg-can-card rounded border border-can-border">
                    <span className="text-[10px] text-slate-400 block uppercase">Diagnostic Session</span>
                    <span className="text-slate-200 font-semibold">{selectedImage.diagnosticSession}</span>
                  </div>
                  <div className="p-2.5 bg-can-card rounded border border-can-border">
                    <span className="text-[10px] text-slate-400 block uppercase">Environment</span>
                    <span className="text-slate-200 font-semibold">{selectedImage.environment}</span>
                  </div>
                </div>

                {/* Integration Actions */}
                <div className="flex items-center space-x-3 pt-2">
                  <button
                    onClick={() => handleAnalyzeImage(selectedImage)}
                    className="px-4 py-2 rounded bg-purple-600 hover:bg-purple-500 text-white text-xs font-semibold flex items-center space-x-1.5 shadow"
                  >
                    <Activity className="w-4 h-4" />
                    <span>Run Image Forensics & Entropy Analysis</span>
                  </button>
                  <button
                    onClick={() => handleSendToFlashAnalyzer(selectedImage)}
                    className="px-4 py-2 rounded bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold flex items-center space-x-1.5 shadow"
                  >
                    <Flame className="w-4 h-4" />
                    <span>Send Image to Flash Analyzer</span>
                  </button>
                </div>
              </div>
            ) : (
              <div className="p-8 text-center bg-can-panel border border-can-border rounded-lg space-y-3">
                <HardDrive className="w-12 h-12 text-slate-600 mx-auto" />
                <div className="text-slate-300 font-semibold text-sm">No Active Memory Read Performed</div>
                <p className="text-xs text-slate-500 max-w-md mx-auto">
                  Configure the target start address and length on the left, then click <strong>"Initiate Safe Memory Read"</strong>.
                </p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Tab 5: Image Forensics (Phase 2) */}
      {subTab === 'image-forensics' && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          {/* Image List */}
          <div className="bg-can-panel border border-can-border rounded-lg p-3 space-y-3">
            <h3 className="text-xs font-bold text-white uppercase tracking-wider">Stored Memory Images</h3>
            <div className="space-y-1.5 max-h-[600px] overflow-y-auto">
              {storedImages.map(img => {
                const isSelected = selectedImage?.imageId === img.imageId;
                return (
                  <button
                    key={img.imageId}
                    onClick={() => {
                      setSelectedImage(img);
                      handleAnalyzeImage(img);
                    }}
                    className={`w-full text-left p-2.5 rounded border transition-all ${
                      isSelected
                        ? 'bg-purple-600/15 border-purple-500/40 shadow-sm'
                        : 'bg-can-card border-can-border/60 hover:bg-slate-800/40'
                    }`}
                  >
                    <div className="flex items-center justify-between text-xs font-semibold text-white">
                      <span>{img.regionName}</span>
                      <span className="text-[10px] font-mono text-purple-400">{(img.length / 1024).toFixed(1)} KB</span>
                    </div>
                    <div className="text-[11px] font-mono text-slate-400 mt-1 truncate">
                      SHA: {img.sha256Hash.substring(0, 16)}...
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Forensics Analysis Details */}
          <div className="lg:col-span-2 bg-can-panel border border-can-border rounded-lg p-4 space-y-4">
            {imageAnalysis ? (
              <div className="space-y-4">
                <div className="flex items-center justify-between border-b border-can-border pb-3">
                  <div>
                    <h3 className="text-sm font-bold text-white flex items-center gap-2">
                      <Activity className="w-4 h-4 text-purple-400" />
                      <span>Forensic Analysis: {selectedImage?.regionName}</span>
                    </h3>
                    <p className="text-[11px] font-mono text-slate-400 mt-0.5">
                      Image Size: {imageAnalysis.imageSizeBytes} bytes | Shannon Entropy: {imageAnalysis.shannonEntropy} / 8.000
                    </p>
                  </div>
                  <div className="text-right">
                    <span className="text-[11px] font-mono text-emerald-400 font-semibold block">
                      Empty / Zero: {imageAnalysis.zeroBytePercent}%
                    </span>
                    <span className="text-[11px] font-mono text-slate-400 block">
                      0xFF Fill: {imageAnalysis.ffBytePercent}%
                    </span>
                  </div>
                </div>

                {/* Printable ASCII Strings */}
                <div className="space-y-2">
                  <h4 className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                    Discovered ASCII Strings & Metadata ({imageAnalysis.identifiedAsciiStrings.length})
                  </h4>
                  <div className="border border-can-border rounded max-h-48 overflow-y-auto font-mono text-xs divide-y divide-can-border/30">
                    {imageAnalysis.identifiedAsciiStrings.length === 0 ? (
                      <div className="p-3 text-slate-500 text-center">No printable ASCII strings found.</div>
                    ) : (
                      imageAnalysis.identifiedAsciiStrings.map((s, idx) => (
                        <div key={idx} className="p-2 flex items-center justify-between hover:bg-slate-800/20">
                          <span className="text-sky-300 font-medium">{s.text}</span>
                          <span className="text-[11px] text-slate-500">Offset: 0x{s.offset.toString(16).toUpperCase()}</span>
                        </div>
                      ))
                    )}
                  </div>
                </div>

                {/* Potential Calibration Regions */}
                <div className="space-y-2">
                  <h4 className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                    Potential Calibration Maps & Tables ({imageAnalysis.potentialCalibrationRegions.length})
                  </h4>
                  <div className="border border-can-border rounded divide-y divide-can-border/30 text-xs">
                    {imageAnalysis.potentialCalibrationRegions.length === 0 ? (
                      <div className="p-3 text-slate-500 text-center">No distinct calibration curves detected.</div>
                    ) : (
                      imageAnalysis.potentialCalibrationRegions.map((cal, idx) => (
                        <div key={idx} className="p-2.5 flex items-center justify-between hover:bg-slate-800/20 font-mono">
                          <div>
                            <span className="text-purple-300 font-bold">
                              0x{cal.startOffset.toString(16).toUpperCase()} — 0x{cal.endOffset.toString(16).toUpperCase()}
                            </span>
                            <div className="text-[11px] text-slate-400 font-sans mt-0.5">{cal.evidence}</div>
                          </div>
                          <span className="px-2 py-0.5 rounded text-[10px] bg-purple-500/10 text-purple-300 border border-purple-500/30">
                            Entropy {cal.entropy.toFixed(2)}
                          </span>
                        </div>
                      ))
                    )}
                  </div>
                </div>

                {/* Checksum Candidates */}
                <div className="space-y-2">
                  <h4 className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                    Candidate Checksum Blocks ({imageAnalysis.checksumCandidates.length})
                  </h4>
                  <div className="border border-can-border rounded divide-y divide-can-border/30 text-xs font-mono">
                    {imageAnalysis.checksumCandidates.map((chk, idx) => (
                      <div key={idx} className="p-2.5 flex items-center justify-between hover:bg-slate-800/20">
                        <div>
                          <span className="text-white font-bold">{chk.algorithmName}</span>
                          <div className="text-[11px] text-slate-400 font-sans mt-0.5">{chk.description}</div>
                        </div>
                        <div className="text-right">
                          <span className="text-emerald-400 font-bold block">{chk.currentValueHex}</span>
                          <span className="text-[10px] text-slate-500 uppercase">Offset 0x{chk.offset.toString(16).toUpperCase()}</span>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            ) : (
              <div className="p-8 text-center text-slate-500 text-xs">
                Select an extracted image on the left to view Shannon entropy analysis, strings, and checksum candidates.
              </div>
            )}
          </div>
        </div>
      )}

      {/* Tab 6: Catalog (Phase 1) */}
      {subTab === 'catalog' && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          <div className="bg-can-panel border border-can-border rounded-lg p-3 space-y-3">
            <h3 className="text-xs font-bold text-white uppercase tracking-wider">Verified OEM Families</h3>
            <div className="relative">
              <Search className="w-3.5 h-3.5 absolute left-2.5 top-2.5 text-slate-500" />
              <input
                type="text"
                value={catalogSearch}
                onChange={e => setCatalogSearch(e.target.value)}
                placeholder="Filter by name, manufacturer, ID..."
                className="w-full bg-can-card border border-can-border rounded pl-8 pr-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-sky-500"
              />
            </div>

            <div className="space-y-1.5 max-h-[600px] overflow-y-auto">
              {filteredCatalog.map(profile => {
                const isSelected = profile.familyId === selectedCatalogId;
                return (
                  <button
                    key={profile.familyId}
                    onClick={() => setSelectedCatalogId(profile.familyId)}
                    className={`w-full text-left p-2.5 rounded border transition-all ${
                      isSelected
                        ? 'bg-sky-600/15 border-sky-500/40 shadow-sm'
                        : 'bg-can-card border-can-border/60 hover:bg-slate-800/40 hover:border-slate-600'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-semibold text-white">{profile.name}</span>
                      <span className="text-[10px] font-mono text-sky-400">{profile.manufacturer}</span>
                    </div>
                    <div className="flex items-center justify-between text-[11px] text-slate-400 font-mono mt-1">
                      <span>{profile.mcuModel}</span>
                      <span>{(profile.flashSizeBytes / 1024 / 1024).toFixed(1)} MB Flash</span>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          <div className="lg:col-span-2 bg-can-panel border border-can-border rounded-lg p-4 space-y-4">
            {selectedCatalogProfile ? (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-2 border-b border-can-border pb-3">
                  <div>
                    <h2 className="text-base font-bold text-white flex items-center gap-2">
                      <span>{selectedCatalogProfile.name}</span>
                      <span className="px-2 py-0.5 rounded text-[10px] font-mono bg-sky-500/10 text-sky-400 border border-sky-500/30">
                        {selectedCatalogProfile.category}
                      </span>
                    </h2>
                    <p className="text-xs text-slate-400 mt-0.5">
                      Family ID: <span className="font-mono text-sky-300">{selectedCatalogProfile.familyId}</span>
                    </p>
                  </div>
                  <div className="text-right">
                    <span className="text-[11px] font-mono text-emerald-400 block font-semibold">
                      Checksum: {selectedCatalogProfile.checksumAlgorithm}
                    </span>
                  </div>
                </div>

                <div className="space-y-2">
                  <h4 className="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                    <FileCode className="w-3.5 h-3.5 text-sky-400" />
                    <span>Memory Map & Protection Boundaries ({selectedCatalogProfile.memoryRegions.length} Regions)</span>
                  </h4>
                  <div className="border border-can-border rounded overflow-hidden">
                    <table className="w-full text-left text-xs font-mono">
                      <thead className="bg-slate-900/80 text-slate-400 text-[11px] border-b border-can-border">
                        <tr>
                          <th className="py-2 px-3">Region Name</th>
                          <th className="py-2 px-3">Start Address</th>
                          <th className="py-2 px-3">Length</th>
                          <th className="py-2 px-3">Writable</th>
                          <th className="py-2 px-3">Security Access</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-can-border/30">
                        {selectedCatalogProfile.memoryRegions.map((reg, idx) => (
                          <tr key={idx} className="hover:bg-slate-800/30">
                            <td className="py-2 px-3 text-slate-200 font-semibold">{reg.regionName}</td>
                            <td className="py-2 px-3 text-sky-400">0x{reg.startAddress.toString(16).toUpperCase()}</td>
                            <td className="py-2 px-3 text-slate-300">{(reg.lengthBytes / 1024).toFixed(1)} KB</td>
                            <td className="py-2 px-3">
                              {reg.isWritable ? (
                                <span className="text-emerald-400 font-semibold text-[11px]">YES</span>
                              ) : (
                                <span className="text-rose-400 text-[11px]">READ-ONLY</span>
                              )}
                            </td>
                            <td className="py-2 px-3">
                              {reg.requiresSecurityAccess ? (
                                <span className="text-amber-400 text-[11px]">REQUIRED</span>
                              ) : (
                                <span className="text-slate-400 text-[11px]">NONE</span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            ) : null}
          </div>
        </div>
      )}

      {/* Confirmation Modal */}
      {showConfirmModal && (
        <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-can-panel border border-can-border rounded-lg max-w-md w-full p-4 space-y-4 shadow-2xl">
            <div className="flex items-center space-x-2 text-amber-400 border-b border-can-border pb-2">
              <AlertTriangle className="w-5 h-5" />
              <h3 className="text-sm font-bold text-white">Operator Confirmation Required</h3>
            </div>

            <div className="text-xs text-slate-300 space-y-2">
              <p>You are about to initiate a chunked diagnostic memory read with the following parameters:</p>
              <div className="p-3 bg-can-card rounded border border-can-border font-mono space-y-1 text-[11px]">
                <div>ECU: <strong>{ecuName}</strong></div>
                <div>Region: <strong>{readRegionName}</strong></div>
                <div>Address: <strong>{readStartAddressHex}</strong></div>
                <div>Length: <strong>{readLengthBytes} bytes</strong> ({(readLengthBytes / 1024).toFixed(2)} KB)</div>
                <div>Environment: <strong>{readEnvironment}</strong></div>
              </div>
              <p className="text-slate-400 text-[11px]">
                All extracted data will be validated against bounds, integrity checked via SHA-256, and stored immutably.
              </p>
            </div>

            <div className="flex justify-end space-x-2 pt-2">
              <button
                onClick={() => setShowConfirmModal(false)}
                className="px-3 py-1.5 rounded bg-can-card hover:bg-slate-700 text-xs text-slate-300"
              >
                Cancel
              </button>
              <button
                onClick={handleExecuteSafeRead}
                className="px-4 py-1.5 rounded bg-emerald-600 hover:bg-emerald-500 text-xs font-semibold text-white shadow"
              >
                Confirm & Start Read
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Evidence Source Modal */}
      {activeEvidenceModal && (
        <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-can-panel border border-can-border rounded-lg max-w-2xl w-full p-4 space-y-3 shadow-xl">
            <div className="flex items-center justify-between border-b border-can-border pb-2">
              <div className="flex items-center space-x-2">
                <ShieldCheck className="w-4 h-4 text-sky-400" />
                <h3 className="text-sm font-bold text-white">Evidence Provenance & Citations</h3>
              </div>
              <button
                onClick={() => setActiveEvidenceModal(null)}
                className="text-slate-400 hover:text-white text-xs px-2 py-1 rounded"
              >
                ✕ Close
              </button>
            </div>

            <div className="space-y-2 max-h-96 overflow-y-auto">
              {activeEvidenceModal.map((ev, idx) => (
                <div key={idx} className="p-3 rounded bg-can-card border border-can-border text-xs space-y-1.5 font-mono">
                  <div className="flex items-center justify-between">
                    <span className="text-sky-400 font-bold">{ev.sourceType} ({ev.referenceCode})</span>
                    <span className="text-[10px] text-slate-400">{new Date(ev.timestampUtc).toLocaleTimeString()}</span>
                  </div>
                  <p className="text-slate-300 font-sans text-xs">{ev.description}</p>
                  {ev.rawBytesHex && (
                    <div className="p-2 rounded bg-slate-950 border border-slate-800 text-[11px] text-emerald-400 break-all">
                      Raw Payload: {ev.rawBytesHex}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
