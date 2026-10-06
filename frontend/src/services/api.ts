import { 
  DbcDatabaseInfo, GatewayStatus, HardwareStatus, HardwareTransportInfo, LogSession, NetworkTopologyState, ReplayStatus, TopologyMode,
  VehicleProfile, CalculatedSignalDefinition, ParameterMetadata, EngineeringSession, EngineeringRun,
  CanonicalLogDataSet, LogImportResult, LogValidationReport, ProcessingOptions, TimeSeriesChannel, RunComparisonResult, ComparisonAlignmentMode,
  EcuFamilyProfile, EcuProtocolCapabilityMatrix, EcuIdentificationProfile,
  EcuMemoryMap, EcuReadCapabilityMatrix, SafeReadOptions, ReadSessionStatus, EcuMemoryImage, ReadManifest, MemoryImageAnalysis,
  DiagnosticSessionStatusDto, DiagnosticSessionTypeValue,
  DtcRecordDto, DtcClearRequestDto, DtcClearResultDto,
  LiveDataParameterDefinitionDto, LiveDataSampleDto,
  SensorPlausibilityReportDto,
  RoutineDescriptorDto, RoutineExecutionRequestDto, RoutineExecutionResultDto,
  EcuResetRequestDto, EcuResetResultDto,
  DiagnosticTraceEntryDto,
  EcuHealthReportDto,
  OemNormalizationPlanDto,
  EngineeringProject,
  ProjectEvidenceItem,
  ProjectNote,
  TimelineEntry,
  OneClickSessionRequest,
  OneClickSessionReport,
  CanIdDeepMetrics,
  DiscoveredSignalCandidate,
  DigitalTwinNode,
  DigitalTwinFaultRequest,
  HilTestFixture,
  HilFaultInjectionCommand,
  A2lDatabase,
  OdxEcuVariant,
  XcpSessionStatus,
  EcuProgrammingSessionStatus,
  ResearchDocument,
  KnowledgeGraphNode,
  KnowledgeGraphEdge,
  GlobalSearchResult,
  CopilotQueryRequest,
  CopilotResponse
} from '../types';
import { DiagnosticExchange, DiagnosticTroubleCode, EcuIdentification, ObdPidValue, ObdPidDefinition, VirtualEcu } from '../types/diagnostics';
import { 
  EngineOutputs, EngineParameters, EngineInputs, DynoPullSummary, VehiclePerformanceSummary, 
  CalibrationProject, CalibrationTable, CalibrationDiffCell, CalibrationEditRecord,
  RoadDynoRun, RoadDynoSample, AmbientConditions, AtmosphericStandard, CoastDownProfile, DynoRunComparisonResult
} from '../types/engine';
import { LiveParameter, RequestedVsActualPair, EcuBinaryInfo, CalibrationObject, ChecksumInfo, BinaryDiffResult, LiveFlashCorrelation } from '../types/flash';
import { 
  ManufacturerLiveDataDefinition, DecodedManufacturerValue, ManufacturerOem,
  VagMeasuringGroup, VagMeasuringBlockZone, DiagnosticFreezeFrame,
  DtcMetadata, DtcCorrelationResult, DashboardLayout, DashboardWidget,
  EcuDefinitionPack, ReadonlyEcuMap, OperatingPointTracer
} from '../types/diagnosticsExtended';

const API_BASE = '/api';

export const FALLBACK_HARDWARE_TRANSPORTS: HardwareTransportInfo[] = [
  {
    transportId: 'waveshare-usb-can-fd',
    name: 'Waveshare USB-CAN-FD-B (Dual Channel)',
    type: 'WaveshareCanFd',
    isConnected: false,
    capabilities: {
      channelCount: 2,
      supportsCanFd: true,
      supportsBitRateSwitch: true,
      supportsClassicCan: true,
      supportsTransmit: true,
      maxPayloadBytes: 64,
      supportsHardwareTimestamps: true,
      supportedNominalBaudRates: [100000, 250000, 500000, 1000000],
      supportedDataBaudRates: [1000000, 2000000, 4000000, 5000000]
    }
  },
  {
    transportId: 'esp8266-gateway',
    name: 'ESP8266 + MCP2515 Gateway',
    type: 'Esp8266',
    isConnected: false,
    capabilities: {
      channelCount: 1,
      supportsCanFd: false,
      supportsBitRateSwitch: false,
      supportsClassicCan: true,
      supportsTransmit: false,
      maxPayloadBytes: 8,
      supportsHardwareTimestamps: false,
      supportedNominalBaudRates: [500000],
      supportedDataBaudRates: []
    }
  },
  {
    transportId: 'virtual-simulation',
    name: 'Virtual CAN / CAN FD Simulation',
    type: 'Simulation',
    isConnected: true,
    capabilities: {
      channelCount: 2,
      supportsCanFd: true,
      supportsBitRateSwitch: true,
      supportsClassicCan: true,
      supportsTransmit: true,
      maxPayloadBytes: 64,
      supportsHardwareTimestamps: true,
      supportedNominalBaudRates: [100000, 250000, 500000, 1000000],
      supportedDataBaudRates: [1000000, 2000000, 4000000, 5000000]
    }
  },
  {
    transportId: 'elm327-adapter',
    name: 'ELM327 OBD-II / CAN Adapter',
    type: 'Elm327',
    isConnected: false,
    capabilities: {
      channelCount: 1,
      supportsCanFd: false,
      supportsBitRateSwitch: false,
      supportsClassicCan: true,
      supportsTransmit: true,
      maxPayloadBytes: 8,
      supportsHardwareTimestamps: false,
      supportedNominalBaudRates: [250000, 500000],
      supportedDataBaudRates: []
    }
  },
  {
    transportId: 'esp8266-usb-serial',
    name: 'ESP8266 + MCP2515 Gateway (USB Serial)',
    type: 'Esp8266UsbSerial',
    isConnected: false,
    capabilities: {
      channelCount: 1,
      supportsCanFd: false,
      supportsBitRateSwitch: false,
      supportsClassicCan: true,
      supportsTransmit: false,
      maxPayloadBytes: 8,
      supportsHardwareTimestamps: true,
      supportedNominalBaudRates: [500000],
      supportedDataBaudRates: []
    }
  }
];

export const api = {
  // Hardware Manager & Transports (Multi-hardware abstraction)
  getHardwareTransports: async (): Promise<HardwareTransportInfo[]> => {
    try {
      const res = await fetch(`${API_BASE}/hardware/transports`);
      if (!res.ok) return FALLBACK_HARDWARE_TRANSPORTS;
      const data = await res.json();
      return Array.isArray(data) && data.length > 0 ? data : FALLBACK_HARDWARE_TRANSPORTS;
    } catch {
      return FALLBACK_HARDWARE_TRANSPORTS;
    }
  },
  getHardwareStatus: async (): Promise<HardwareStatus | null> => {
    try {
      const res = await fetch(`${API_BASE}/hardware/status`);
      if (!res.ok) return null;
      return await res.json();
    } catch {
      return null;
    }
  },
  selectHardwareTransport: async (transportId: string): Promise<{ success: boolean; activeTransportId?: string; error?: string }> => {
    const res = await fetch(`${API_BASE}/hardware/select`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ transportId })
    });
    return res.json();
  },
  transmitCanFrame: async (frame: { id: number; data: number[]; isCanFd?: boolean; bitRateSwitch?: boolean; channel?: number }): Promise<{ success: boolean; error?: string }> => {
    const res = await fetch(`${API_BASE}/hardware/transmit`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(frame)
    });
    return res.json();
  },
  configureHardwareTransport: async (config: {
    transportId: string;
    nominalBaudRate?: number;
    dataBaudRate?: number;
    activeChannel?: number;
    gatewayAddress?: string;
    portName?: string;
    serialBaudRate?: number;
  }): Promise<HardwareStatus> => {
    const res = await fetch(`${API_BASE}/hardware/configure`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config)
    });
    return res.json();
  },
  getAutomotiveProtocols: async (): Promise<any[]> => {
    const res = await fetch(`${API_BASE}/hardware/protocols`);
    return res.json();
  },
  getElm327Ports: async (): Promise<string[]> => {
    try {
      const res = await fetch(`${API_BASE}/hardware/elm327/ports`);
      if (!res.ok) return ['Loopback (Simulated)'];
      return await res.json();
    } catch {
      return ['Loopback (Simulated)'];
    }
  },
  executeElm327Command: async (command: string, timeoutSeconds = 2): Promise<any> => {
    const res = await fetch(`${API_BASE}/hardware/elm327/command`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ command, timeoutSeconds })
    });
    return res.json();
  },
  getEsp8266UsbPorts: async (): Promise<string[]> => {
    try {
      const res = await fetch(`${API_BASE}/hardware/esp8266usb/ports`);
      if (!res.ok) return [];
      return await res.json();
    } catch {
      return [];
    }
  },

  // Vehicle Profile Management
  getVehicleProfiles: async (): Promise<VehicleProfile[]> => {
    const res = await fetch(`${API_BASE}/vehicleprofile`);
    return res.json();
  },
  getActiveVehicleProfile: async (): Promise<VehicleProfile> => {
    const res = await fetch(`${API_BASE}/vehicleprofile/active`);
    return res.json();
  },
  saveVehicleProfile: async (profile: Partial<VehicleProfile>): Promise<VehicleProfile> => {
    const res = await fetch(`${API_BASE}/vehicleprofile`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(profile)
    });
    return res.json();
  },
  setActiveVehicleProfile: async (id: string): Promise<VehicleProfile> => {
    const res = await fetch(`${API_BASE}/vehicleprofile/active/${id}`, { method: 'POST' });
    return res.json();
  },
  deleteVehicleProfile: async (id: string): Promise<{ success: boolean }> => {
    const res = await fetch(`${API_BASE}/vehicleprofile/${id}`, { method: 'DELETE' });
    return res.json();
  },

  // Engineering Sessions & Runs
  getEngineeringSessions: async (): Promise<EngineeringSession[]> => {
    const res = await fetch(`${API_BASE}/engineeringsession`);
    return res.json();
  },
  getActiveEngineeringSession: async (): Promise<EngineeringSession> => {
    const res = await fetch(`${API_BASE}/engineeringsession/active`);
    return res.json();
  },
  startEngineeringSession: async (req: { name: string; description?: string; technician?: string; tags?: string[] }): Promise<EngineeringSession> => {
    const res = await fetch(`${API_BASE}/engineeringsession/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    return res.json();
  },
  stopEngineeringSession: async (sessionId?: string): Promise<EngineeringSession> => {
    const url = sessionId ? `${API_BASE}/engineeringsession/stop?sessionId=${sessionId}` : `${API_BASE}/engineeringsession/stop`;
    const res = await fetch(url, { method: 'POST' });
    return res.json();
  },
  startEngineeringRun: async (sessionId: string, req: { name: string; type: string }): Promise<EngineeringRun> => {
    const res = await fetch(`${API_BASE}/engineeringsession/${sessionId}/runs/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    return res.json();
  },
  stopEngineeringRun: async (sessionId: string, runId: string, metrics?: Record<string, number>): Promise<EngineeringRun> => {
    const res = await fetch(`${API_BASE}/engineeringsession/${sessionId}/runs/${runId}/stop`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ summaryMetrics: metrics })
    });
    return res.json();
  },

  // Calculated & Custom Signals
  getCalculatedSignals: async (): Promise<CalculatedSignalDefinition[]> => {
    const res = await fetch(`${API_BASE}/customsignals/calculated`);
    return res.json();
  },
  registerCalculatedSignal: async (signal: Partial<CalculatedSignalDefinition>): Promise<CalculatedSignalDefinition> => {
    const res = await fetch(`${API_BASE}/customsignals/calculated`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(signal)
    });
    return res.json();
  },
  evaluateCalculatedSignals: async (inputs: Record<string, number>): Promise<Record<string, number>> => {
    const res = await fetch(`${API_BASE}/customsignals/calculated/evaluate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ inputs })
    });
    return res.json();
  },
  getParameterMetadata: async (): Promise<ParameterMetadata[]> => {
    const res = await fetch(`${API_BASE}/customsignals/metadata`);
    return res.json();
  },

  // Gateway (ESP8266 compatibility wrapper)
  getGatewayStatus: async (): Promise<GatewayStatus> => {
    const res = await fetch(`${API_BASE}/gateway/status`);
    return res.json();
  },
  setGatewayAddress: async (address: string): Promise<GatewayStatus> => {
    const res = await fetch(`${API_BASE}/gateway/address`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ address })
    });
    return res.json();
  },
  pollGatewayStatus: async (): Promise<GatewayStatus> => {
    const res = await fetch(`${API_BASE}/gateway/poll-status`, { method: 'POST' });
    return res.json();
  },

  // DBC
  getActiveDbc: async (): Promise<DbcDatabaseInfo> => {
    const res = await fetch(`${API_BASE}/dbc/active`);
    return res.json();
  },
  uploadDbc: async (file: File): Promise<any> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await fetch(`${API_BASE}/dbc/upload`, {
      method: 'POST',
      body: formData
    });
    return res.json();
  },
  loadSampleDbc: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/dbc/load-sample`, { method: 'POST' });
    return res.json();
  },
  clearActiveDbc: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/dbc/active`, { method: 'DELETE' });
    return res.json();
  },

  // Logger
  getCurrentLogSession: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/log/current`);
    return res.json();
  },
  getLogSessions: async (): Promise<LogSession[]> => {
    const res = await fetch(`${API_BASE}/log/sessions`);
    return res.json();
  },
  startLogging: async (name?: string): Promise<LogSession> => {
    const res = await fetch(`${API_BASE}/log/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name })
    });
    return res.json();
  },
  pauseLogging: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/log/pause`, { method: 'POST' });
    return res.json();
  },
  resumeLogging: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/log/resume`, { method: 'POST' });
    return res.json();
  },
  stopLogging: async (): Promise<LogSession> => {
    const res = await fetch(`${API_BASE}/log/stop`, { method: 'POST' });
    return res.json();
  },
  getExportUrl: (sessionId: string) => `${API_BASE}/log/export/${sessionId}`,

  // Replay
  getReplayStatus: async (): Promise<ReplayStatus> => {
    const res = await fetch(`${API_BASE}/replay/status`);
    return res.json();
  },
  loadSessionForReplay: async (sessionId: string): Promise<ReplayStatus> => {
    const res = await fetch(`${API_BASE}/replay/load-session/${sessionId}`, { method: 'POST' });
    return res.json();
  },
  uploadCsvForReplay: async (file: File): Promise<ReplayStatus> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await fetch(`${API_BASE}/replay/upload-csv`, {
      method: 'POST',
      body: formData
    });
    return res.json();
  },
  generateSampleReplay: async (): Promise<ReplayStatus> => {
    const res = await fetch(`${API_BASE}/replay/generate-sample`, { method: 'POST' });
    return res.json();
  },
  playReplay: async (): Promise<ReplayStatus> => {
    const res = await fetch(`${API_BASE}/replay/play`, { method: 'POST' });
    return res.json();
  },
  pauseReplay: async (): Promise<ReplayStatus> => {
    const res = await fetch(`${API_BASE}/replay/pause`, { method: 'POST' });
    return res.json();
  },
  stopReplay: async (): Promise<ReplayStatus> => {
    const res = await fetch(`${API_BASE}/replay/stop`, { method: 'POST' });
    return res.json();
  },
  seekReplay: async (index: number): Promise<ReplayStatus> => {
    const res = await fetch(`${API_BASE}/replay/seek?index=${index}`, { method: 'POST' });
    return res.json();
  },
  setReplaySpeed: async (multiplier: number): Promise<ReplayStatus> => {
    const res = await fetch(`${API_BASE}/replay/speed?multiplier=${multiplier}`, { method: 'POST' });
    return res.json();
  },

  // Topology
  getTopology: async (): Promise<NetworkTopologyState> => {
    const res = await fetch(`${API_BASE}/topology`);
    return res.json();
  },
  setTopologyMode: async (mode: TopologyMode): Promise<NetworkTopologyState> => {
    const res = await fetch(`${API_BASE}/topology/mode`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ mode })
    });
    return res.json();
  },
  resetTopology: async (): Promise<NetworkTopologyState> => {
    const res = await fetch(`${API_BASE}/topology/reset`, { method: 'POST' });
    return res.json();
  },

  // Diagnostics (Phase 3)
  getRecentExchanges: async (count: number = 100): Promise<DiagnosticExchange[]> => {
    const res = await fetch(`${API_BASE}/diagnostics/exchanges?count=${count}`);
    return res.json();
  },
  sendDiagnosticRequest: async (req: { requestCanId: number; ecuName: string; hexPayload: string; isSimulation: boolean }): Promise<DiagnosticExchange> => {
    const res = await fetch(`${API_BASE}/diagnostics/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    return res.json();
  },
  getActiveDtcs: async (): Promise<DiagnosticTroubleCode[]> => {
    const res = await fetch(`${API_BASE}/diagnostics/dtcs`);
    return res.json();
  },
  clearDtcs: async (requestCanId: number = 0x7E0, ecuName: string = "ECM", isSimulation: boolean = true): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostics/clear-dtcs?requestCanId=${requestCanId}&ecuName=${ecuName}&isSimulation=${isSimulation}`, {
      method: 'POST'
    });
    return res.json();
  },
  getEcuIdentifications: async (): Promise<EcuIdentification[]> => {
    const res = await fetch(`${API_BASE}/diagnostics/ecu-info`);
    return res.json();
  },
  getObdLiveValues: async (): Promise<{ values: ObdPidValue[]; supportedDefinitions: ObdPidDefinition[] }> => {
    const res = await fetch(`${API_BASE}/diagnostics/obd-live`);
    return res.json();
  },
  getSimulatorStatus: async (): Promise<{ isEnabled: boolean; virtualEcus: VirtualEcu[] }> => {
    const res = await fetch(`${API_BASE}/diagnostics/simulator/status`);
    return res.json();
  },
  toggleSimulator: async (enabled: boolean): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostics/simulator/toggle?enabled=${enabled}`, {
      method: 'POST'
    });
    return res.json();
  },
  setSimulatorDtcFault: async (ecuName: string, dtcCode: string, active: boolean): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostics/simulator/dtc-fault`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ecuName, dtcCode, active })
    });
    return res.json();
  },
  resetSimulator: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostics/simulator/reset`, { method: 'POST' });
    return res.json();
  },
  clearDiagnosticHistory: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostics/history`, { method: 'DELETE' });
    return res.json();
  },

  // Engine (Phase 4)
  getEngineStatus: async (): Promise<{ parameters: EngineParameters; inputs: EngineInputs; outputs: EngineOutputs; isSimulationRunning: boolean }> => {
    const res = await fetch(`${API_BASE}/engine/status`);
    return res.json();
  },
  setEngineControl: async (control: { throttlePositionPct?: number; gear?: number }): Promise<EngineOutputs> => {
    const res = await fetch(`${API_BASE}/engine/control`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(control)
    });
    return res.json();
  },
  startEngineSimulation: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/engine/start-simulation`, { method: 'POST' });
    return res.json();
  },
  stopEngineSimulation: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/engine/stop-simulation`, { method: 'POST' });
    return res.json();
  },
  resetEngine: async (): Promise<EngineOutputs> => {
    const res = await fetch(`${API_BASE}/engine/reset`, { method: 'POST' });
    return res.json();
  },

  // Dyno (Phase 4)
  getDynoPulls: async (): Promise<DynoPullSummary[]> => {
    const res = await fetch(`${API_BASE}/dyno/pulls`);
    return res.json();
  },
  runDynoPull: async (req: { name: string; startRpm: number; endRpm: number }): Promise<DynoPullSummary> => {
    const res = await fetch(`${API_BASE}/dyno/run-pull`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    return res.json();
  },
  getDynoPerformance: async (pullId?: string): Promise<VehiclePerformanceSummary> => {
    const url = pullId ? `${API_BASE}/dyno/performance?pullId=${pullId}` : `${API_BASE}/dyno/performance`;
    const res = await fetch(url);
    return res.json();
  },
  clearDynoPulls: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/dyno/pulls`, { method: 'DELETE' });
    return res.json();
  },

  // Measurement-Based Road Dyno System
  runRoadDyno: async (payload: {
    name?: string;
    gear: number;
    standard: AtmosphericStandard;
    ambient?: AmbientConditions;
    vehicle?: any;
    vehicleProfileId?: string;
    coastDown?: CoastDownProfile | null;
    samples: RoadDynoSample[];
  }): Promise<RoadDynoRun> => {
    const res = await fetch(`${API_BASE}/dyno/road-run`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    return res.json();
  },
  detectRoadDynoPulls: async (payload: {
    samples: RoadDynoSample[];
    vehicle?: any;
    vehicleProfileId?: string;
    minThrottlePct?: number;
    minDurationSec?: number;
  }): Promise<RoadDynoSample[][]> => {
    const res = await fetch(`${API_BASE}/dyno/detect-pulls`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    return res.json();
  },
  analyzeCoastDown: async (payload: {
    samples: RoadDynoSample[];
    vehicle?: any;
    vehicleProfileId?: string;
    ambient?: AmbientConditions;
  }): Promise<CoastDownProfile> => {
    const res = await fetch(`${API_BASE}/dyno/coastdown`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    return res.json();
  },
  compareRoadDynoRuns: async (payload: {
    baselineRunId?: string;
    testRunId?: string;
    baselineRun?: RoadDynoRun;
    testRun?: RoadDynoRun;
  }): Promise<DynoRunComparisonResult> => {
    const res = await fetch(`${API_BASE}/dyno/compare`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    return res.json();
  },
  getSavedRoadDynoRuns: async (): Promise<RoadDynoRun[]> => {
    const res = await fetch(`${API_BASE}/dyno/road-runs`);
    return res.json();
  },
  getSavedRoadDynoRun: async (id: string): Promise<RoadDynoRun> => {
    const res = await fetch(`${API_BASE}/dyno/road-run/${id}`);
    return res.json();
  },

  // Calibration (Phase 4)
  getCalibrationProject: async (): Promise<CalibrationProject> => {
    const res = await fetch(`${API_BASE}/calibration/project`);
    return res.json();
  },
  getCalibrationTable: async (id: string): Promise<CalibrationTable> => {
    const res = await fetch(`${API_BASE}/calibration/table/${id}`);
    return res.json();
  },
  updateCalibrationCell: async (tableId: string, xIndex: number, yIndex: number, newValue: number): Promise<any> => {
    const res = await fetch(`${API_BASE}/calibration/cell`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tableId, xIndex, yIndex, newValue })
    });
    return res.json();
  },
  bulkOffsetCalibration: async (tableId: string, value: number): Promise<any> => {
    const res = await fetch(`${API_BASE}/calibration/offset`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tableId, value })
    });
    return res.json();
  },
  bulkScaleCalibration: async (tableId: string, value: number): Promise<any> => {
    const res = await fetch(`${API_BASE}/calibration/scale`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tableId, value })
    });
    return res.json();
  },
  undoCalibration: async (tableId: string): Promise<any> => {
    const res = await fetch(`${API_BASE}/calibration/undo/${tableId}`, { method: 'POST' });
    return res.json();
  },
  redoCalibration: async (tableId: string): Promise<any> => {
    const res = await fetch(`${API_BASE}/calibration/redo/${tableId}`, { method: 'POST' });
    return res.json();
  },
  getCalibrationDiff: async (tableId: string): Promise<CalibrationDiffCell[]> => {
    const res = await fetch(`${API_BASE}/calibration/diff/${tableId}`);
    return res.json();
  },
  getCalibrationHistory: async (tableId: string): Promise<CalibrationEditRecord[]> => {
    const res = await fetch(`${API_BASE}/calibration/history/${tableId}`);
    return res.json();
  },
  resetCalibrationTable: async (tableId: string): Promise<any> => {
    const res = await fetch(`${API_BASE}/calibration/reset-table/${tableId}`, { method: 'POST' });
    return res.json();
  },
  resetAllCalibrationToStock: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/calibration/reset-all`, { method: 'POST' });
    return res.json();
  },

  // Live ECU Data (Phase 5 - Domain A)
  getLiveParameters: async (category?: string): Promise<LiveParameter[]> => {
    const url = category ? `${API_BASE}/live-ecu-data/parameters?category=${category}` : `${API_BASE}/live-ecu-data/parameters`;
    const res = await fetch(url);
    return res.json();
  },
  getRequestedVsActual: async (): Promise<RequestedVsActualPair[]> => {
    const res = await fetch(`${API_BASE}/live-ecu-data/requested-vs-actual`);
    return res.json();
  },
  setLiveParameterPin: async (parameterId: string, isPinned: boolean): Promise<any> => {
    const res = await fetch(`${API_BASE}/live-ecu-data/pin`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ parameterId, isPinned })
    });
    return res.json();
  },
  clearLiveParameters: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/live-ecu-data/clear`, { method: 'POST' });
    return res.json();
  },

  // ECU Flash Analyzer (Phase 5 - Domain B - READ ONLY)
  getActiveBinary: async (): Promise<EcuBinaryInfo> => {
    const res = await fetch(`${API_BASE}/flash-analyzer/binary`);
    return res.json();
  },
  getFlashObjects: async (): Promise<CalibrationObject[]> => {
    const res = await fetch(`${API_BASE}/flash-analyzer/objects`);
    return res.json();
  },
  getFlashObject: async (id: string): Promise<CalibrationObject> => {
    const res = await fetch(`${API_BASE}/flash-analyzer/object/${id}`);
    return res.json();
  },
  getFlashChecksums: async (): Promise<ChecksumInfo[]> => {
    const res = await fetch(`${API_BASE}/flash-analyzer/checksums`);
    return res.json();
  },
  getFlashCorrelations: async (): Promise<LiveFlashCorrelation[]> => {
    const res = await fetch(`${API_BASE}/flash-analyzer/correlations`);
    return res.json();
  },
  getHexDump: async (offset: number = 0, length: number = 256): Promise<{ offset: number; dump: string }> => {
    const res = await fetch(`${API_BASE}/flash-analyzer/hexdump?offset=${offset}&length=${length}`);
    return res.json();
  },
  compareSampleBinaries: async (): Promise<BinaryDiffResult> => {
    const res = await fetch(`${API_BASE}/flash-analyzer/compare-sample`, { method: 'POST' });
    return res.json();
  },
  loadSampleBinary: async (): Promise<EcuBinaryInfo> => {
    const res = await fetch(`${API_BASE}/flash-analyzer/load-sample`, { method: 'POST' });
    return res.json();
  },

  // Log Analysis, Validation, Processing, Comparison & GPS Sync
  importLogFile: async (file: File): Promise<LogImportResult> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await fetch(`${API_BASE}/analysis/import`, {
      method: 'POST',
      body: formData,
    });
    return res.json();
  },
  importLogContent: async (content: string, fileName?: string): Promise<LogImportResult> => {
    const res = await fetch(`${API_BASE}/analysis/import`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content, fileName }),
    });
    return res.json();
  },
  validateDataSet: async (dataset: CanonicalLogDataSet): Promise<LogValidationReport> => {
    const res = await fetch(`${API_BASE}/analysis/validate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dataset),
    });
    return res.json();
  },
  processChannel: async (channel: TimeSeriesChannel, options: ProcessingOptions): Promise<TimeSeriesChannel> => {
    const res = await fetch(`${API_BASE}/analysis/process`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ channel, options }),
    });
    return res.json();
  },
  compareRuns: async (
    runA: CanonicalLogDataSet,
    runB: CanonicalLogDataSet,
    mode: ComparisonAlignmentMode = 'TimeTriggered',
    triggerChannel?: string,
    triggerThreshold: number = 0
  ): Promise<RunComparisonResult> => {
    const res = await fetch(`${API_BASE}/analysis/compare`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ runA, runB, mode, triggerChannel, triggerThreshold }),
    });
    return res.json();
  },
  synchronizeGps: async (dataset: CanonicalLogDataSet, canMonotonicOffsetMs: number = 0): Promise<CanonicalLogDataSet> => {
    const res = await fetch(`${API_BASE}/analysis/gps-sync`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ dataSet: dataset, canMonotonicOffsetMs }),
    });
    return res.json();
  },
  getCachedDataSets: async (): Promise<any[]> => {
    const res = await fetch(`${API_BASE}/analysis/datasets`);
    return res.json();
  },
  getCachedDataSet: async (id: string): Promise<CanonicalLogDataSet> => {
    const res = await fetch(`${API_BASE}/analysis/dataset/${id}`);
    return res.json();
  },
  convertSessionToCanonical: async (sessionId: string): Promise<CanonicalLogDataSet> => {
    const res = await fetch(`${API_BASE}/analysis/session/${sessionId}/canonical`);
    return res.json();
  },

  // Extended Diagnostics (Phase 6)
  getFreezeFrames: async (): Promise<DiagnosticFreezeFrame[]> => {
    const res = await fetch(`${API_BASE}/diagnostics/freeze-frames`);
    return res.json();
  },
  getFreezeFrameById: async (id: string): Promise<DiagnosticFreezeFrame> => {
    const res = await fetch(`${API_BASE}/diagnostics/freeze-frames/${id}`);
    return res.json();
  },
  getFreezeFrameForDtc: async (code: string): Promise<DiagnosticFreezeFrame> => {
    const res = await fetch(`${API_BASE}/diagnostics/freeze-frames/dtc/${code}`);
    return res.json();
  },
  getAllDtcMetadata: async (): Promise<DtcMetadata[]> => {
    const res = await fetch(`${API_BASE}/diagnostics/dtc-metadata`);
    return res.json();
  },
  getDtcMetadata: async (code: string): Promise<DtcMetadata> => {
    const res = await fetch(`${API_BASE}/diagnostics/dtc-metadata/${code}`);
    return res.json();
  },
  correlateDtc: async (dtcCode: string): Promise<DtcCorrelationResult> => {
    const res = await fetch(`${API_BASE}/diagnostics/correlate-dtc`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ dtcCode })
    });
    return res.json();
  },
  getVagGroups: async (): Promise<VagMeasuringGroup[]> => {
    const res = await fetch(`${API_BASE}/diagnostics/vag/groups`);
    return res.json();
  },
  getVagGroup: async (groupNumber: number): Promise<VagMeasuringGroup> => {
    const res = await fetch(`${API_BASE}/diagnostics/vag/groups/${groupNumber}`);
    return res.json();
  },
  readVagGroup: async (groupNumber: number, rpm: number = 800, boostBar: number = 0.0, loadPct: number = 15.0, rawHexPayload?: string): Promise<VagMeasuringGroup> => {
    const res = await fetch(`${API_BASE}/diagnostics/vag/read-group`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ groupNumber, rpm, boostBar, loadPct, rawHexPayload })
    });
    return res.json();
  },
  getManufacturerDefinitions: async (oem?: ManufacturerOem): Promise<ManufacturerLiveDataDefinition[]> => {
    const url = oem ? `${API_BASE}/diagnostics/manufacturer/definitions?oem=${oem}` : `${API_BASE}/diagnostics/manufacturer/definitions`;
    const res = await fetch(url);
    return res.json();
  },
  getManufacturerSnapshot: async (oem: ManufacturerOem = 'VAG', rpm: number = 2500, boostBar: number = 0.5, loadPct: number = 45.0): Promise<DecodedManufacturerValue[]> => {
    const res = await fetch(`${API_BASE}/diagnostics/manufacturer/snapshot?oem=${oem}&rpm=${rpm}&boostBar=${boostBar}&loadPct=${loadPct}`);
    return res.json();
  },

  // Dashboard Designer
  getDashboardLayouts: async (): Promise<DashboardLayout[]> => {
    const res = await fetch(`${API_BASE}/dashboarddesigner/layouts`);
    return res.json();
  },
  getDashboardLayout: async (id: string): Promise<DashboardLayout> => {
    const res = await fetch(`${API_BASE}/dashboarddesigner/layouts/${id}`);
    return res.json();
  },
  saveDashboardLayout: async (layout: DashboardLayout): Promise<DashboardLayout> => {
    const res = await fetch(`${API_BASE}/dashboarddesigner/layouts`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(layout)
    });
    return res.json();
  },
  deleteDashboardLayout: async (id: string): Promise<{ success: boolean; message: string }> => {
    const res = await fetch(`${API_BASE}/dashboarddesigner/layouts/${id}`, { method: 'DELETE' });
    return res.json();
  },
  getDashboardPreset: async (key: string): Promise<DashboardLayout> => {
    const res = await fetch(`${API_BASE}/dashboarddesigner/presets/${key}`);
    return res.json();
  },

  // ECU Definition Packs (Mappacks) - Read-Only
  getMappacks: async (): Promise<EcuDefinitionPack[]> => {
    const res = await fetch(`${API_BASE}/mappack/packs`);
    return res.json();
  },
  getMappack: async (id: string): Promise<EcuDefinitionPack> => {
    const res = await fetch(`${API_BASE}/mappack/packs/${id}`);
    return res.json();
  },
  matchMappack: async (hw?: string, sw?: string, calId?: string): Promise<EcuDefinitionPack> => {
    const params = new URLSearchParams();
    if (hw) params.set('hwPartNumber', hw);
    if (sw) params.set('swVersion', sw);
    if (calId) params.set('calibrationId', calId);
    const res = await fetch(`${API_BASE}/mappack/match?${params.toString()}`);
    return res.json();
  },
  getMappackMap: async (packId: string, mapId: string): Promise<ReadonlyEcuMap> => {
    const res = await fetch(`${API_BASE}/mappack/packs/${packId}/maps/${mapId}`);
    return res.json();
  },
  getOperatingPointTracer: async (packId: string, mapId: string, rpm: number, load: number): Promise<OperatingPointTracer> => {
    const res = await fetch(`${API_BASE}/mappack/packs/${packId}/maps/${mapId}/trace?rpm=${rpm}&load=${load}`);
    return res.json();
  },

  // Centralized Reporting & PDF Generation
  getReportTypes: async (): Promise<{ id: string; name: string; description: string }[]> => {
    const res = await fetch(`${API_BASE}/reports/types`);
    return res.json();
  },
  downloadDiagnosticReportPdf: async (vehicleId?: string, notes?: string): Promise<Blob> => {
    const params = new URLSearchParams();
    if (vehicleId) params.set('vehicleId', vehicleId);
    if (notes) params.set('notes', notes);
    const res = await fetch(`${API_BASE}/reports/diagnostic/pdf?${params.toString()}`);
    if (!res.ok) throw new Error(`Report generation failed: ${res.statusText}`);
    return res.blob();
  },
  downloadDynoReportPdf: async (pullId?: string, baselinePullId?: string, vehicleId?: string): Promise<Blob> => {
    const params = new URLSearchParams();
    if (pullId) params.set('pullId', pullId);
    if (baselinePullId) params.set('baselinePullId', baselinePullId);
    if (vehicleId) params.set('vehicleId', vehicleId);
    const res = await fetch(`${API_BASE}/reports/dyno/pdf?${params.toString()}`);
    if (!res.ok) throw new Error(`Report generation failed: ${res.statusText}`);
    return res.blob();
  },
  downloadEcuFlashReportPdf: async (fileHash?: string, packId?: string, vehicleId?: string): Promise<Blob> => {
    const params = new URLSearchParams();
    if (fileHash) params.set('fileHash', fileHash);
    if (packId) params.set('packId', packId);
    if (vehicleId) params.set('vehicleId', vehicleId);
    const res = await fetch(`${API_BASE}/reports/ecu-flash/pdf?${params.toString()}`);
    if (!res.ok) throw new Error(`Report generation failed: ${res.statusText}`);
    return res.blob();
  },
  downloadSessionLogReportPdf: async (sessionId?: string, vehicleId?: string): Promise<Blob> => {
    const params = new URLSearchParams();
    if (sessionId) params.set('sessionId', sessionId);
    if (vehicleId) params.set('vehicleId', vehicleId);
    const res = await fetch(`${API_BASE}/reports/session-log/pdf?${params.toString()}`);
    if (!res.ok) throw new Error(`Report generation failed: ${res.statusText}`);
    return res.blob();
  },
  downloadVehicleHealthReportPdf: async (vehicleId?: string, notes?: string): Promise<Blob> => {
    const params = new URLSearchParams();
    if (vehicleId) params.set('vehicleId', vehicleId);
    if (notes) params.set('notes', notes);
    const res = await fetch(`${API_BASE}/reports/vehicle-health/pdf?${params.toString()}`);
    if (!res.ok) throw new Error(`Report generation failed: ${res.statusText}`);
    return res.blob();
  },
  getReportPreview: async (reportType: string, vehicleId?: string): Promise<any> => {
    const params = new URLSearchParams();
    if (vehicleId) params.set('vehicleId', vehicleId);
    const res = await fetch(`${API_BASE}/reports/preview/${reportType}?${params.toString()}`);
    if (!res.ok) throw new Error(`Failed to load report preview: ${res.statusText}`);
    return res.json();
  },

  // ECU Engineering & Identification (Phase 1)
  getEcuCompatibilityCatalog: async (): Promise<EcuFamilyProfile[]> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/compatibility/catalog`);
    if (!res.ok) return [];
    return res.json();
  },
  getEcuCompatibilityProfile: async (familyId: string): Promise<EcuFamilyProfile | null> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/compatibility/${encodeURIComponent(familyId)}`);
    if (!res.ok) return null;
    return res.json();
  },
  probeEcuProtocol: async (req: { requestCanId?: number; responseCanId?: number; ecuName?: string }): Promise<EcuProtocolCapabilityMatrix> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/discovery/probe`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    if (!res.ok) throw new Error(`Protocol probe failed: ${res.statusText}`);
    return res.json();
  },
  getDiscoveredProtocolMatrices: async (): Promise<EcuProtocolCapabilityMatrix[]> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/discovery/matrices`);
    if (!res.ok) return [];
    return res.json();
  },
  identifyEcu: async (req: { requestCanId?: number; responseCanId?: number; ecuName?: string }): Promise<EcuIdentificationProfile> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/identification/identify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    if (!res.ok) throw new Error(`ECU identification failed: ${res.statusText}`);
    return res.json();
  },
  getAllIdentifiedEcus: async (): Promise<EcuIdentificationProfile[]> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/identification/profiles`);
    if (!res.ok) return [];
    return res.json();
  },
  getIdentifiedEcuProfile: async (ecuName: string): Promise<EcuIdentificationProfile | null> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/identification/profiles/${encodeURIComponent(ecuName)}`);
    if (!res.ok) return null;
    return res.json();
  },

  // Safe ECU Memory Architecture & Discovery (Phase 2)
  discoverMemoryMap: async (req: { requestCanId?: number; responseCanId?: number; ecuName?: string }): Promise<EcuMemoryMap> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/memory/discover`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    if (!res.ok) throw new Error(`Memory discovery failed: ${res.statusText}`);
    return res.json();
  },
  getMemoryMap: async (ecuName: string): Promise<EcuMemoryMap | null> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/memory/map/${encodeURIComponent(ecuName)}`);
    if (!res.ok) return null;
    return res.json();
  },
  getReadCapabilities: async (ecuName: string): Promise<EcuReadCapabilityMatrix | null> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/memory/capabilities/${encodeURIComponent(ecuName)}`);
    if (!res.ok) return null;
    return res.json();
  },
  startSafeRead: async (req: {
    requestCanId?: number;
    responseCanId?: number;
    ecuName?: string;
    startAddress: number;
    lengthBytes: number;
    regionName?: string;
    chunkSize?: number;
    timeoutMs?: number;
    interChunkDelayMs?: number;
    environment?: 'Bench' | 'Vehicle' | 'Simulation';
    userConfirmed?: boolean;
  }): Promise<EcuMemoryImage> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/read/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: res.statusText }));
      throw new Error(err.message || `Safe read failed: ${res.statusText}`);
    }
    return res.json();
  },
  getAllReadSessions: async (): Promise<ReadSessionStatus[]> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/read/sessions`);
    if (!res.ok) return [];
    return res.json();
  },
  getReadSession: async (sessionId: string): Promise<ReadSessionStatus | null> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/read/sessions/${sessionId}`);
    if (!res.ok) return null;
    return res.json();
  },
  getAllImages: async (): Promise<EcuMemoryImage[]> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/images`);
    if (!res.ok) return [];
    return res.json();
  },
  getImage: async (imageId: string): Promise<EcuMemoryImage | null> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/images/${encodeURIComponent(imageId)}`);
    if (!res.ok) return null;
    return res.json();
  },
  getImageManifest: async (imageId: string): Promise<ReadManifest | null> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/images/${encodeURIComponent(imageId)}/manifest`);
    if (!res.ok) return null;
    return res.json();
  },
  downloadImageRaw: async (imageId: string): Promise<Blob> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/images/${encodeURIComponent(imageId)}/raw`);
    if (!res.ok) throw new Error(`Raw binary download failed: ${res.statusText}`);
    return res.blob();
  },
  analyzeImage: async (imageId: string): Promise<MemoryImageAnalysis> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/images/${encodeURIComponent(imageId)}/analyze`, {
      method: 'POST'
    });
    if (!res.ok) throw new Error(`Image analysis failed: ${res.statusText}`);
    return res.json();
  },
  sendToFlashAnalyzer: async (imageId: string): Promise<EcuBinaryInfo> => {
    const res = await fetch(`${API_BASE}/ecu-engineering/images/${encodeURIComponent(imageId)}/send-to-flash-analyzer`, {
      method: 'POST'
    });
    if (!res.ok) throw new Error(`Sending to flash analyzer failed: ${res.statusText}`);
    return res.json();
  },

  // ==========================================
  // PHASE 3 DIAGNOSTIC ENGINE & HEALTH APIs
  // ==========================================
  getDiagnosticSessionStatus: async (): Promise<DiagnosticSessionStatusDto> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/session/status`);
    if (!res.ok) throw new Error('Failed to retrieve diagnostic session status');
    return res.json();
  },
  switchDiagnosticSession: async (requestCanId: number, sessionType: DiagnosticSessionTypeValue, ecuName = 'ECM'): Promise<boolean> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/session/switch`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ requestCanId, sessionType, ecuName })
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },
  setTesterPresent: async (requestCanId: number, ecuName = 'ECM', intervalMs = 2000, suppressResponse = false, enable = true): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/session/tester-present`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ requestCanId, ecuName, intervalMs, suppressResponse, enable })
    });
    if (!res.ok) throw new Error('Failed to toggle TesterPresent');
    return res.json();
  },
  getDiagnosticDtcs: async (ecuName?: string, historical = false): Promise<DtcRecordDto[]> => {
    const q = new URLSearchParams();
    if (ecuName) q.append('ecuName', ecuName);
    if (historical) q.append('historical', 'true');
    const res = await fetch(`${API_BASE}/diagnostic-engine/dtcs?${q.toString()}`);
    if (!res.ok) return [];
    return res.json();
  },
  readDiagnosticDtcs: async (requestCanId = 0x7E0, statusMask = 0xFF, ecuName = 'ECM'): Promise<DtcRecordDto[]> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/dtcs/read`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ requestCanId, statusMask, ecuName })
    });
    if (!res.ok) throw new Error('Failed to read DTCs from ECU');
    return res.json();
  },
  clearDiagnosticDtcsSafe: async (request: DtcClearRequestDto): Promise<DtcClearResultDto> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/dtcs/clear-safe`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },
  getDtcClearHistory: async (ecuName?: string): Promise<DtcClearResultDto[]> => {
    const q = ecuName ? `?ecuName=${encodeURIComponent(ecuName)}` : '';
    const res = await fetch(`${API_BASE}/diagnostic-engine/dtcs/clear-history${q}`);
    if (!res.ok) return [];
    return res.json();
  },
  getLiveDataDefinitions: async (): Promise<LiveDataParameterDefinitionDto[]> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/live-data/definitions`);
    if (!res.ok) return [];
    return res.json();
  },
  getLiveDataSamples: async (): Promise<LiveDataSampleDto[]> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/live-data/samples`);
    if (!res.ok) return [];
    return res.json();
  },
  pollLiveDataParameter: async (requestCanId = 0x7E0, parameterId = 'ENGINE_RPM', ecuName = 'ECM'): Promise<LiveDataSampleDto | null> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/live-data/poll`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ requestCanId, parameterId, ecuName })
    });
    if (!res.ok) return null;
    return res.json();
  },
  startLiveDataPolling: async (requestCanId = 0x7E0, parameterIds: string[] = [], intervalMs = 250, ecuName = 'ECM'): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/live-data/start-polling`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ requestCanId, parameterIds, intervalMs, ecuName })
    });
    return res.json();
  },
  stopLiveDataPolling: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/live-data/stop-polling`, { method: 'POST' });
    return res.json();
  },
  getSensorPlausibilityReport: async (ecuName = 'ECM'): Promise<SensorPlausibilityReportDto> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/plausibility?ecuName=${encodeURIComponent(ecuName)}`);
    if (!res.ok) throw new Error('Failed to retrieve sensor plausibility report');
    return res.json();
  },
  getDiagnosticRoutines: async (): Promise<RoutineDescriptorDto[]> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/routines`);
    if (!res.ok) return [];
    return res.json();
  },
  executeDiagnosticRoutine: async (request: RoutineExecutionRequestDto): Promise<RoutineExecutionResultDto> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/routines/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },
  getRoutineExecutionHistory: async (): Promise<RoutineExecutionResultDto[]> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/routines/history`);
    if (!res.ok) return [];
    return res.json();
  },
  executeEcuReset: async (request: EcuResetRequestDto): Promise<EcuResetResultDto> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/reset`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },
  getEcuResetHistory: async (): Promise<EcuResetResultDto[]> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/reset/history`);
    if (!res.ok) return [];
    return res.json();
  },
  getDiagnosticTraces: async (count = 200): Promise<DiagnosticTraceEntryDto[]> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/traces?count=${count}`);
    if (!res.ok) return [];
    return res.json();
  },
  clearDiagnosticTraces: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/traces`, { method: 'DELETE' });
    return res.json();
  },
  getEcuHealthReport: async (ecuName = 'ECM'): Promise<EcuHealthReportDto> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/health?ecuName=${encodeURIComponent(ecuName)}`);
    if (!res.ok) throw new Error('Failed to retrieve ECU health report');
    return res.json();
  },
  getOemNormalizationPlan: async (ecuName = 'ECM', hardwareNumber?: string, softwareNumber?: string): Promise<OemNormalizationPlanDto> => {
    const q = new URLSearchParams({ ecuName });
    if (hardwareNumber) q.append('hardwareNumber', hardwareNumber);
    if (softwareNumber) q.append('softwareNumber', softwareNumber);
    const res = await fetch(`${API_BASE}/diagnostic-engine/oem-normalization?${q.toString()}`);
    if (!res.ok) throw new Error('Failed to generate OEM normalization plan');
    return res.json();
  },
  injectSimulatorFault: async (dto: { ecuName: string; dtcCode?: string; dtcActive?: boolean; voltage?: number }): Promise<any> => {
    const res = await fetch(`${API_BASE}/diagnostic-engine/simulator/fault`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dto)
    });
    return res.json();
  },

  // --- Projects Workspace ---
  getProjects: async (): Promise<EngineeringProject[]> => {
    const res = await fetch(`${API_BASE}/projects`);
    return res.json();
  },
  getActiveProject: async (): Promise<EngineeringProject> => {
    const res = await fetch(`${API_BASE}/projects/active`);
    return res.json();
  },
  createProject: async (name: string, description = '', createdBy = 'Engineer'): Promise<EngineeringProject> => {
    const res = await fetch(`${API_BASE}/projects`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, description, createdBy })
    });
    return res.json();
  },
  setActiveProject: async (id: string): Promise<any> => {
    const res = await fetch(`${API_BASE}/projects/${id}/activate`, { method: 'POST' });
    return res.json();
  },
  addProjectEvidence: async (id: string, category: string, description: string, source: string, rawDataBase64?: string): Promise<ProjectEvidenceItem> => {
    const res = await fetch(`${API_BASE}/projects/${id}/evidence`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ category, description, source, rawDataBase64 })
    });
    return res.json();
  },
  addProjectNote: async (id: string, title: string, content: string, category = 'General', author = 'Engineer'): Promise<ProjectNote> => {
    const res = await fetch(`${API_BASE}/projects/${id}/notes`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, content, category, author })
    });
    return res.json();
  },

  // --- Unified Timeline & One-Click ---
  getTimelineEntries: async (count = 200, category?: string, minSeverity?: string): Promise<TimelineEntry[]> => {
    const q = new URLSearchParams({ count: count.toString() });
    if (category) q.append('category', category);
    if (minSeverity) q.append('minSeverity', minSeverity);
    const res = await fetch(`${API_BASE}/timeline/entries?${q.toString()}`);
    return res.json();
  },
  searchTimeline: async (query: string, count = 100): Promise<TimelineEntry[]> => {
    const res = await fetch(`${API_BASE}/timeline/search?query=${encodeURIComponent(query)}&count=${count}`);
    return res.json();
  },
  clearTimeline: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/timeline/clear`, { method: 'DELETE' });
    return res.json();
  },
  startOneClickSession: async (req: OneClickSessionRequest): Promise<OneClickSessionReport> => {
    const res = await fetch(`${API_BASE}/timeline/start-one-click-session`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    return res.json();
  },

  // --- CAN Reverse Engineering ---
  getReverseEngineeringMetrics: async (): Promise<CanIdDeepMetrics[]> => {
    const res = await fetch(`${API_BASE}/reverse-engineering/metrics`);
    return res.json();
  },
  getDiscoveredSignals: async (canId?: number): Promise<DiscoveredSignalCandidate[]> => {
    const url = canId ? `${API_BASE}/reverse-engineering/signals?canId=${canId}` : `${API_BASE}/reverse-engineering/signals`;
    const res = await fetch(url);
    return res.json();
  },
  setReverseEngineeringBaseline: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/reverse-engineering/baseline`, { method: 'POST' });
    return res.json();
  },
  resetReverseEngineering: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/reverse-engineering/reset`, { method: 'POST' });
    return res.json();
  },

  // --- Digital Twin ---
  getDigitalTwinNodes: async (): Promise<DigitalTwinNode[]> => {
    const res = await fetch(`${API_BASE}/digital-twin/nodes`);
    return res.json();
  },
  getDigitalTwinStatus: async (): Promise<{ isRunning: boolean; activeNodeCount: number; totalNodeCount: number }> => {
    const res = await fetch(`${API_BASE}/digital-twin/status`);
    return res.json();
  },
  startDigitalTwinSimulation: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/digital-twin/start`, { method: 'POST' });
    return res.json();
  },
  stopDigitalTwinSimulation: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/digital-twin/stop`, { method: 'POST' });
    return res.json();
  },
  setDigitalTwinFault: async (req: DigitalTwinFaultRequest): Promise<any> => {
    const res = await fetch(`${API_BASE}/digital-twin/fault`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    return res.json();
  },
  clearDigitalTwinFaults: async (nodeId?: string): Promise<any> => {
    const url = nodeId ? `${API_BASE}/digital-twin/clear-faults?nodeId=${encodeURIComponent(nodeId)}` : `${API_BASE}/digital-twin/clear-faults`;
    const res = await fetch(url, { method: 'POST' });
    return res.json();
  },

  // --- HIL Bench ---
  getHilFixture: async (): Promise<HilTestFixture> => {
    const res = await fetch(`${API_BASE}/hil/fixture`);
    return res.json();
  },
  setHilChannelValue: async (channelId: string, value: number): Promise<any> => {
    const res = await fetch(`${API_BASE}/hil/channel/value`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ channelId, value })
    });
    return res.json();
  },
  injectHilFault: async (cmd: HilFaultInjectionCommand): Promise<any> => {
    const res = await fetch(`${API_BASE}/hil/channel/fault`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(cmd)
    });
    return res.json();
  },
  setHilMainPower: async (voltage: number, enabled: boolean): Promise<any> => {
    const res = await fetch(`${API_BASE}/hil/power/main`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ voltage, enabled })
    });
    return res.json();
  },
  setHilIgnition: async (active: boolean): Promise<any> => {
    const res = await fetch(`${API_BASE}/hil/power/ignition?active=${active}`, { method: 'POST' });
    return res.json();
  },
  resetHilFixture: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/hil/reset`, { method: 'POST' });
    return res.json();
  },

  // --- ASAM / XCP ---
  getAsamA2l: async (): Promise<A2lDatabase> => {
    const res = await fetch(`${API_BASE}/asam/a2l`);
    return res.json();
  },
  getAsamOdx: async (): Promise<OdxEcuVariant> => {
    const res = await fetch(`${API_BASE}/asam/odx`);
    return res.json();
  },
  getXcpStatus: async (): Promise<XcpSessionStatus> => {
    const res = await fetch(`${API_BASE}/asam/xcp/status`);
    return res.json();
  },
  connectXcp: async (transport: string): Promise<any> => {
    const res = await fetch(`${API_BASE}/asam/xcp/connect`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ transport })
    });
    return res.json();
  },
  disconnectXcp: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/asam/xcp/disconnect`, { method: 'POST' });
    return res.json();
  },
  xcpUpload: async (address: number, length = 8): Promise<{ address: number; length: number; dataHex: string }> => {
    const res = await fetch(`${API_BASE}/asam/xcp/upload?address=${address}&length=${length}`);
    return res.json();
  },

  // --- Safe ECU Programming ---
  getProgrammingStatus: async (): Promise<EcuProgrammingSessionStatus> => {
    const res = await fetch(`${API_BASE}/ecu-programming/status`);
    return res.json();
  },
  executeProgramming: async (dto: {
    ecuName: string;
    requestCanId: number;
    targetImageBase64: string;
    operatorConfirmed: boolean;
    operatorRationale: string;
    currentSupplyVoltage: number;
  }): Promise<EcuProgrammingSessionStatus> => {
    const res = await fetch(`${API_BASE}/ecu-programming/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dto)
    });
    return res.json();
  },
  abortProgramming: async (reason = 'Operator abort'): Promise<any> => {
    const res = await fetch(`${API_BASE}/ecu-programming/abort?reason=${encodeURIComponent(reason)}`, { method: 'POST' });
    return res.json();
  },

  // --- Research & Global Knowledge Graph ---
  getResearchDocuments: async (ecuFamily?: string): Promise<ResearchDocument[]> => {
    const url = ecuFamily ? `${API_BASE}/research/documents?ecuFamily=${encodeURIComponent(ecuFamily)}` : `${API_BASE}/research/documents`;
    const res = await fetch(url);
    return res.json();
  },
  getKnowledgeGraphNodes: async (): Promise<KnowledgeGraphNode[]> => {
    const res = await fetch(`${API_BASE}/research/graph/nodes`);
    return res.json();
  },
  getKnowledgeGraphEdges: async (): Promise<KnowledgeGraphEdge[]> => {
    const res = await fetch(`${API_BASE}/research/graph/edges`);
    return res.json();
  },
  searchGlobal: async (query: string): Promise<GlobalSearchResult[]> => {
    const res = await fetch(`${API_BASE}/research/search?query=${encodeURIComponent(query)}`);
    return res.json();
  },

  // --- AI Engineering Copilot ---
  queryCopilot: async (req: CopilotQueryRequest): Promise<CopilotResponse> => {
    const res = await fetch(`${API_BASE}/copilot/analyze`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    });
    return res.json();
  }
};

export function downloadBlobAsFile(blob: Blob, filename: string): void {
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => {
    window.URL.revokeObjectURL(url);
  }, 1000);
}

