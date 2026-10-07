export interface CanFrame {
  timestampUs: number;
  id: number;
  dlc: number;
  flags: number;
  data: number[];
  isExtended: boolean;
  isRtr: boolean;
  isCanFd?: boolean;
  bitRateSwitch?: boolean;
  errorStateIndicator?: boolean;
  direction?: 'Rx' | 'Tx' | 0 | 1;
  channel?: number;
  sourceId?: string;
  actualLength?: number;
}

export type TransportType = 'Esp8266' | 'WaveshareCanFd' | 'Simulation' | 'Elm327' | 'Esp8266UsbSerial' | 'GvretSerial' | number;

export interface HardwareCapabilities {
  supportsCanFd: boolean;
  supportsBitRateSwitch: boolean;
  supportsClassicCan: boolean;
  channelCount: number;
  maxPayloadBytes: number;
  supportsTransmit: boolean;
  supportsHardwareTimestamps: boolean;
  supportedNominalBaudRates: number[];
  supportedDataBaudRates: number[];
}

export interface HardwareTransportInfo {
  transportId: string;
  name: string;
  type: TransportType;
  isConnected: boolean;
  capabilities: HardwareCapabilities;
}

export interface HardwareStatus {
  transportId: string;
  name: string;
  type: TransportType;
  isConnected: boolean;
  connectionDetails: string;
  totalFramesReceived: number;
  totalFramesTransmitted: number;
  rxFrameRate: number;
  txFrameRate: number;
  lastSeenUtc?: string | null;
  lastError?: string | null;
  capabilities: HardwareCapabilities;
  extendedTelemetry?: any;
}

export interface DecodedSignal {
  signalName: string;
  messageName: string;
  canId: number;
  rawValue: number;
  engineeringValue: number;
  formattedValue: string;
  unit: string;
  timestampUs: number;
  timestampUtc: string;
  transmitterNode: string;
  receiverNodes: string[];
  valueDescription?: string | null;
}

export interface CanFrameBatch {
  frames: CanFrame[];
  signals: DecodedSignal[];
  frameRate: number;
}

export type TopologyMode = 'Live' | 'Paused' | 'Simulation';

export interface NetworkNode {
  id: string;
  name: string;
  address: string;
  type: string;
  status: 'Active' | 'Inactive';
  source: 'Dbc' | 'Observed';
  lastSeenUtc?: string | null;
  messageCount: number;
  txRateHz: number;
}

export interface CanMessageInfo {
  id: number;
  hexId: string;
  name: string;
  transmitter: string;
  receivers: string[];
  dlc: number;
  lastSeenUtc?: string | null;
  frequencyHz: number;
  frameCount: number;
  signals: string[];
  isKnown: boolean;
}

export interface TopologyEdge {
  source: string;
  target: string;
  messageId: number;
  messageName: string;
  signalName: string;
  lastSeenUtc?: string | null;
  state: 'Active' | 'Idle';
  updateRateHz: number;
}

export interface NetworkTopologyState {
  nodes: NetworkNode[];
  messages: CanMessageInfo[];
  edges: TopologyEdge[];
  mode: TopologyMode;
  totalFramesObserved: number;
  activeNodeCount: number;
  lastUpdatedUtc: string;
}

export interface DbcSignal {
  name: string;
  startBit: number;
  length: number;
  isLittleEndian: boolean;
  isSigned: boolean;
  factor: number;
  offset: number;
  minimum: number;
  maximum: number;
  unit: string;
  receivers: string[];
  multiplexerType: number;
  multiplexerValue?: number | null;
  comment: string;
  valueTable: Record<number, string>;
}

export interface DbcMessage {
  id: number;
  cleanId: number;
  name: string;
  dlc: number;
  transmitter: string;
  signals: DbcSignal[];
  comment: string;
  isExtended: boolean;
}

export interface DbcDatabaseInfo {
  isLoaded: boolean;
  version?: string;
  nodeCount?: number;
  messageCount?: number;
  nodes?: { name: string; comment: string }[];
  messages?: DbcMessage[];
}

export interface LogSession {
  id: string;
  name: string;
  startTimeUtc: string;
  endTimeUtc?: string | null;
  frameCount: number;
  isActive: boolean;
  isPaused?: boolean;
}

export interface ReplayStatus {
  isLoaded: boolean;
  isPlaying: boolean;
  isPaused: boolean;
  currentIndex: number;
  totalFrames: number;
  speedMultiplier: number;
  name: string;
  progressPercent: number;
}

export interface GatewayStatus {
  isConnected: boolean;
  gatewayAddress: string;
  totalPacketsReceived: number;
  ingestFps: number;
  lastPacketTimeUtc?: string | null;
  lastError?: string | null;
  espTelemetry?: {
    rx_frames?: number;
    rx_fps?: number;
    dropped_frames?: number;
    spi_errors?: number;
    can_errors?: number;
    queue_usage?: number;
    queue_high_water?: number;
    ws_failures?: number;
    ws_broadcasts?: number;
    eflg?: number;
    tec?: number;
    rec?: number;
    free_heap?: number;
    max_loop_latency_us?: number;
    clients?: number;
  } | null;
}

export type AutomotiveProtocol =
  | 'Can'
  | 'Lin'
  | 'KLine'
  | 'Kwp2000'
  | 'J1850Vpw'
  | 'J1850Pwm'
  | 'AutomotiveEthernet'
  | 'DoIp'
  | 'FlexRay'
  | 'J1939'
  | 'OemSpecific';

export type TransportState = 'Disconnected' | 'Connecting' | 'Connected' | 'Suspended' | 'Error';

export interface AutomotiveProtocolInfo {
  protocol: AutomotiveProtocol;
  name: string;
  status: 'Active' | 'Architecture Ready';
  hardwareSupported: boolean;
  category: 'CAN / CAN FD' | 'Sub-Bus' | 'Serial Legacy' | 'High-Speed / IP' | 'Commercial' | 'OEM Proprietary';
  description: string;
}

export interface LinFrame {
  pid: number;
  frameId: number;
  data: number[];
  checksum: number;
  checksumType: 'Classic' | 'Enhanced';
  frameType: 'Unconditional' | 'EventTriggered' | 'Sporadic' | 'DiagnosticMasterRequest' | 'DiagnosticSlaveResponse';
  direction: 'Rx' | 'Tx';
  channel: number;
  timestampUs: number;
}

export interface KLineMessage {
  data: number[];
  initType: 'None' | 'FiveBaudInit' | 'FastInit';
  checksum: number;
  isChecksumValid: boolean;
  channel: number;
  timestampUs: number;
}

export interface Kwp2000Message {
  serviceId: number;
  payload: number[];
  targetAddress?: number;
  sourceAddress?: number;
  physicalLayer: 'KLine' | 'Can';
  checksum: number;
  channel: number;
  timestampUs: number;
}

export interface J1850Frame {
  variant: 'Vpw_10_4K' | 'Pwm_41_6K';
  header: number[];
  data: number[];
  crc: number;
  hasInFrameResponse: boolean;
  channel: number;
  timestampUs: number;
}

export interface AutomotiveEthernetFrame {
  destinationMac: string;
  sourceMac: string;
  etherType: number;
  vlanId?: number;
  vlanPriority?: number;
  payload: number[];
  phyType: 'BaseT1_100' | 'BaseT1_1000' | 'BaseTx_100';
  portIndex: number;
  timestampUs: number;
}

export interface DoIpMessage {
  protocolVersion: number;
  payloadType: number;
  sourceLogicalAddress?: number;
  targetLogicalAddress?: number;
  diagnosticPayload: number[];
  timestampUs: number;
}

export interface FlexRayFrame {
  channel: 'ChannelA' | 'ChannelB' | 'Both';
  frameId: number;
  payloadLengthWords: number;
  payloadLengthBytes: number;
  cycleCount: number;
  payload: number[];
  timestampUs: number;
}

export interface J1939Message {
  pgn: number;
  priority: number;
  dataPage: number;
  pduFormat: number;
  pduSpecific: number;
  sourceAddress: number;
  destinationAddress: number;
  isBroadcast: boolean;
  data: number[];
  protocolType: 'ClassicJ1939' | 'J1939_22_CanFd';
  channel: number;
  timestampUs: number;
}

// Vehicle Domain Profile
export interface EngineSpecs {
  code: string;
  displacementLiters: number;
  cylinderCount: number;
  fuelType: string;
  aspiration: string;
  maxBoostBar: number;
  compressionRatio: number;
  injectorFlowCcPerMin: number;
  redlineRpm: number;
  revLimitRpm: number;
  idleRpm: number;
}

export interface EcuHardwareSoftware {
  hardwarePartNumber: string;
  hardwareVersion: string;
  softwareVersion: string;
  calibrationId: string;
  diagnosticProtocol: string;
  requestAddress: number;
  responseAddress: number;
}

export interface TransmissionSpecs {
  type: string;
  forwardGears: number;
  finalDriveRatio: number;
  gearRatios: number[];
}

export interface TireSpecs {
  sizeSpec: string;
  widthMm: number;
  aspectRatioPct: number;
  rimDiameterInches: number;
  rollingRadiusMeters: number;
  circumferenceMeters: number;
}

export interface VehicleProfile {
  id: string;
  name: string;
  vin: string;
  make: string;
  model: string;
  year: number;
  curbWeightKg: number;
  testWeightKg: number;
  occupantsWeightKg?: number;
  dragCoefficient?: number;
  frontalAreaM2?: number;
  rollingResistanceCoeff?: number;
  drivetrainType?: string;
  drivetrainLossPct?: number;
  rotationalMassFactor?: number;
  engine: EngineSpecs;
  ecu: EcuHardwareSoftware;
  transmission: TransmissionSpecs;
  tires: TireSpecs;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

// Custom Signals & Parameter Metadata
export type SignalDataSource = 'CanMessage' | 'ObdPid' | 'UdsDid' | 'Kwp2000' | 'VagBlock' | 'Calculated';
export type SignalEndianness = 'LittleEndian' | 'BigEndian';

export interface CustomSignalDefinition {
  id: string;
  name: string;
  description: string;
  category: string;
  sourceType: SignalDataSource;
  sourceIdentifier: number;
  startBit: number;
  bitLength: number;
  byteOffset: number;
  byteOrder: SignalEndianness;
  isSigned: boolean;
  factor: number;
  offset: number;
  units: string;
  minLimit?: number | null;
  maxLimit?: number | null;
  customEquation?: string | null;
}

export type ParameterConfidence = 'Unverified' | 'Estimated' | 'Observed' | 'FactoryCalibrated';

export interface ParameterMetadata {
  parameterId: string;
  standardLabel: string;
  displayName: string;
  category: string;
  primarySource: SignalDataSource;
  confidence: ParameterConfidence;
  confidenceScore: number;
  sourceDetails: string;
  units: string;
  expectedFrequencyHz: number;
  lastUpdatedUtc?: string | null;
}

// Calculated Signals
export interface CalculatedSignalDefinition {
  id: string;
  name: string;
  expression: string;
  units: string;
  category: string;
  dependencies: string[];
}

// Engineering Sessions & Runs
export type RunType = 'DynoPull' | 'AccelerationTest' | 'IdleMonitoring' | 'DiagnosticScan' | 'SteadyCruise' | 'TrackLap' | 'Custom';

export interface EngineeringRun {
  runId: string;
  runNumber: number;
  name: string;
  type: RunType;
  startTimeUtc: string;
  endTimeUtc?: string | null;
  durationSeconds: number;
  startFrameIndex: number;
  endFrameIndex: number;
  summaryMetrics: Record<string, number>;
  notes: string;
}

export interface EngineeringSession {
  sessionId: string;
  name: string;
  description: string;
  technician: string;
  vehicleProfileId: string;
  vehicleProfileSnapshot?: VehicleProfile | null;
  hardwareTransportId: string;
  hardwareAdapterName: string;
  activeProtocol: AutomotiveProtocol;
  startTimeUtc: string;
  endTimeUtc?: string | null;
  isActive: boolean;
  logSessionId?: string | null;
  runs: EngineeringRun[];
  tags: string[];
}

// Time-Series and Analysis Models
export type LogFileFormat =
  | 'GenericCanCsv'
  | 'TorqueProCsv'
  | 'VcdsCsv'
  | 'LinuxCandump'
  | 'VectorAsc'
  | 'GenericTimeSeriesCsv'
  | 'JsonLog';

export interface TimeSeriesChannel {
  name: string;
  units: string;
  source: string;
  timestampsMs: number[];
  values: number[];
  sampleCount: number;
  min: number;
  max: number;
  mean: number;
  stdDev: number;
}

export interface GpsPoint {
  timestampMs: number;
  timestampUtc?: string | null;
  latitude: number;
  longitude: number;
  altitudeMeters?: number | null;
  speedKmh?: number | null;
  headingDegrees?: number | null;
  satellites?: number | null;
  fixQuality?: number | null;
}

export interface CanonicalLogDataSet {
  datasetId: string;
  name: string;
  sourceFormat: LogFileFormat;
  startTimeUtc: string;
  endTimeUtc: string;
  durationSeconds: number;
  channels: Record<string, TimeSeriesChannel>;
  gpsTrack: GpsPoint[];
  metadata: Record<string, string>;
}

export interface LogImportResult {
  success: boolean;
  detectedFormat: LogFileFormat;
  message: string;
  dataSet?: CanonicalLogDataSet | null;
  canSession?: any;
}

export type ValidationSeverity = 'Info' | 'Warning' | 'Error';
export type ValidationIssueType =
  | 'MissingSamples'
  | 'TimestampJumps'
  | 'DuplicateTimestamps'
  | 'SpikesOutliers'
  | 'ImpossibleValues'
  | 'NoiseJitter';

export interface LogValidationIssue {
  type: ValidationIssueType;
  severity: ValidationSeverity;
  channelName: string;
  timestampMs: number;
  description: string;
  observedValue?: number | null;
  expectedRange?: string | null;
}

export interface LogValidationReport {
  totalChannels: number;
  totalSamples: number;
  durationMs: number;
  issues: LogValidationIssue[];
  missingGapsCount: number;
  timestampJumpsCount: number;
  duplicatesCount: number;
  spikesCount: number;
  impossibleValuesCount: number;
  channelNoiseScore: Record<string, number>;
  isValid: boolean;
  summary: string;
}

export type InterpolationMode = 'Linear' | 'StepHold' | 'Nearest';

export interface ProcessingOptions {
  resampleFrequencyHz?: number | null;
  interpolation: InterpolationMode;
  movingAverageWindow?: number | null;
  medianFilterWindow?: number | null;
  emaAlpha?: number | null;
  lowPassCutoffHz?: number | null;
}

export type ComparisonAlignmentMode = 'TimeTriggered' | 'RpmDomain' | 'SpeedDomain';

export interface ChannelComparisonResult {
  channelName: string;
  units: string;
  alignedAxisValues: number[];
  runAValues: number[];
  runBValues: number[];
  deltaValues: number[];
  deltaPercent: number[];
  peakRunA: number;
  peakRunB: number;
  peakDelta: number;
  peakDeltaPercent: number;
  meanDelta: number;
}

export interface RunComparisonResult {
  runAId: string;
  runAName: string;
  runBId: string;
  runBName: string;
  alignment: ComparisonAlignmentMode;
  channels: Record<string, ChannelComparisonResult>;
  summaryNotes: string;
}

// ==========================================
// ECU Engineering & Protocol Discovery (Phase 1)
// ==========================================

export type IdentificationConfidence = 'Confirmed' | 'Detected' | 'Inferred' | 'Unknown';

export interface EvidenceSource {
  sourceType: string;
  referenceCode: string;
  rawBytesHex?: string | null;
  description: string;
  timestampUtc: string;
}

export interface FieldWithConfidence<T> {
  value?: T | null;
  confidence: IdentificationConfidence;
  citations: EvidenceSource[];
}

export interface MemoryRegionDefinition {
  regionName: string;
  startAddress: number;
  lengthBytes: number;
  isWritable: boolean;
  requiresSecurityAccess: boolean;
  description: string;
}

export interface EcuFamilyProfile {
  familyId: string;
  name: string;
  manufacturer: string;
  category: string;
  typicalPlatforms: string[];
  mcu: string;
  mcuModel: string;
  flashSizeBytes: number;
  eepromSizeBytes: number;
  memoryRegions: MemoryRegionDefinition[];
  defaultBaudRate: number;
  primaryDiagnosticProtocol: string;
  defaultRequestCanId: number;
  defaultResponseCanId: number;
  checksumAlgorithm: string;
  knownConstraints: string[];
  recoveryProcedures: string[];
  oemNormalLimits: Record<string, { min?: number; max?: number; unit?: string; item1?: number; item2?: number; item3?: string }>;
}

export interface ServiceCapability {
  serviceId: number;
  serviceName: string;
  isSupported: boolean;
  requiresSecurityAccess: boolean;
  notes: string;
}

export interface EcuProtocolCapabilityMatrix {
  ecuName: string;
  primaryProtocol: string;
  addressingScheme: string;
  requestCanId: number;
  responseCanId: number;
  supportsIsoTp: boolean;
  supportsCanFd: boolean;
  maxPduSizeBytes: number;
  timingP2ClientMs: number;
  timingP2StarClientMs: number;
  timingP3ClientMs: number;
  supportedDiagnosticSessions: string[];
  securityAccessRequiredForCalibrations: boolean;
  services: Record<number, ServiceCapability>;
  discoveryTimestampUtc: string;
}

export interface EcuIdentificationProfile {
  ecuName: string;
  requestCanId: number;
  responseCanId: number;
  manufacturer: FieldWithConfidence<string>;
  vehiclePlatform: FieldWithConfidence<string>;
  ecuFamily: FieldWithConfidence<string>;
  ecuPartNumber: FieldWithConfidence<string>;
  hardwareNumber: FieldWithConfidence<string>;
  softwareNumber: FieldWithConfidence<string>;
  softwareVersion: FieldWithConfidence<string>;
  calibrationId: FieldWithConfidence<string>;
  vin: FieldWithConfidence<string>;
  diagnosticProtocol: FieldWithConfidence<string>;
  canBitrate: FieldWithConfidence<number>;
  canAddressing: FieldWithConfidence<string>;
  bootloaderInfo: FieldWithConfidence<string>;
  applicationInfo: FieldWithConfidence<string>;
  mcuArchitecture: FieldWithConfidence<string>;
  memoryCharacteristics: FieldWithConfidence<string>;
  supportedServices: string[];
  extendedParameters: Record<string, string>;
  overallConfidence: IdentificationConfidence;
  identificationTimestampUtc: string;
  allEvidence: EvidenceSource[];
}

// ==========================================
// Safe ECU Memory Architecture & Discovery (Phase 2)
// ==========================================

export type MemoryType = 
  | 'Bootloader' 
  | 'Application' 
  | 'Calibration' 
  | 'Eeprom' 
  | 'Nvm' 
  | 'Configuration' 
  | 'Data' 
  | 'Reserved' 
  | 'Unknown';

export type ReadPermission = 
  | 'ReadSupported' 
  | 'ReadNotSupported' 
  | 'RequiresDiagnosticSession' 
  | 'RequiresAuthorization' 
  | 'RequiresSecurityAccess' 
  | 'RequiresBenchInterface' 
  | 'RequiresSpecialHardware' 
  | 'Unknown';

export type DiagnosticReadMethod = 
  | 'ReadMemoryByAddress_0x23' 
  | 'RequestUpload_0x35' 
  | 'ReadDataByIdentifier_0x22' 
  | 'VendorSpecific' 
  | 'None' 
  | 'Unknown';

export type ReadSessionState = 
  | 'Disconnected' 
  | 'Connected' 
  | 'Identified' 
  | 'ProtocolConfirmed' 
  | 'DiagnosticSessionActive' 
  | 'ReadAuthorized' 
  | 'Reading' 
  | 'Verifying' 
  | 'Complete' 
  | 'Failed';

export type ExecutionEnvironment = 'Bench' | 'Vehicle' | 'Simulation';

export type ImageClassification = 
  | 'OriginalRead' 
  | 'Baseline' 
  | 'WorkingCopy' 
  | 'Modified' 
  | 'Verified' 
  | 'Backup';

export interface MemoryRegion {
  regionName: string;
  startAddress: number;
  endAddress: number;
  length: number;
  type: MemoryType;
  addressWidth: number;
  readCapability: ReadPermission;
  writeCapability: string;
  eraseCapability: string;
  preferredReadMethod: DiagnosticReadMethod;
  requiredSession: number;
  requiredSecurityLevel?: number | null;
  maxReadLengthBytes: number;
  addressConstraints: string;
  knownRestrictions: string;
  confidence: IdentificationConfidence;
  provenance: 'EcuObserved' | 'CatalogDerived' | 'UserProvided' | 'Inferred';
  citations: EvidenceSource[];
}

export interface MemoryAddressGap {
  startAddress: number;
  endAddress: number;
  lengthBytes: number;
}

export interface EcuMemoryMap {
  ecuName: string;
  hardwareNumber?: string | null;
  softwareNumber?: string | null;
  ecuFamily?: string | null;
  totalAddressableBytes: number;
  regions: MemoryRegion[];
  gaps: MemoryAddressGap[];
  discoveryTimestampUtc: string;
}

export interface RegionReadCapability {
  regionName: string;
  startAddress: number;
  lengthBytes: number;
  readMethod: DiagnosticReadMethod;
  permission: ReadPermission;
  requiredSession: number;
  sessionName: string;
  requiresSecurityAccess: boolean;
  securityLevel?: number | null;
  requiresAuthorization: boolean;
  maxChunkLengthBytes: number;
  addressConstraints: string;
  knownRestrictions: string;
}

export interface EcuReadCapabilityMatrix {
  ecuName: string;
  supportsReadMemoryByAddress0x23: boolean;
  supportsRequestUpload0x35: boolean;
  supportsReadDataByIdentifier0x22: boolean;
  securityAccessRequiredOverall: boolean;
  regionCapabilities: RegionReadCapability[];
  evaluatedTimestampUtc: string;
}

export interface SafeReadOptions {
  chunkSize: number;
  timeoutMs: number;
  interChunkDelayMs: number;
  maxTotalReadBytes: number;
  retryCount: number;
  environment: ExecutionEnvironment;
  requireUserConfirmation: boolean;
  userConfirmed: boolean;
}

export interface StateTransitionRecord {
  state: ReadSessionState;
  timestampUtc: string;
  message: string;
}

export interface ReadSessionStatus {
  sessionId: string;
  ecuName: string;
  requestCanId: number;
  responseCanId: number;
  regionName: string;
  startAddress: number;
  totalBytesRequested: number;
  bytesReadSoFar: number;
  progressPercent: number;
  currentChunk: number;
  totalChunks: number;
  state: ReadSessionState;
  stateTransitions: StateTransitionRecord[];
  lastError?: string | null;
  sha256Result?: string | null;
  startedUtc: string;
  completedUtc?: string | null;
}

export interface EcuMemoryImage {
  imageId: string;
  ecuName: string;
  hardwareNumber: string;
  softwareNumber: string;
  calibrationId: string;
  vin: string;
  regionName: string;
  startAddress: number;
  length: number;
  sha256Hash: string;
  timestampUtc: string;
  transport: string;
  diagnosticProtocol: string;
  readMethod: string;
  diagnosticSession: string;
  authorizationState: string;
  classification: ImageClassification;
  environment: ExecutionEnvironment;
  toolVersion: string;
  evidence: EvidenceSource[];
}

export interface ReadManifest {
  ecu: string;
  hardwareNumber: string;
  softwareNumber: string;
  calibrationId: string;
  protocol: string;
  addressing: string;
  memoryRegion: string;
  startAddress: string;
  length: number;
  readMethod: string;
  authorizationStatus: string;
  sha256: string;
  timestamp: string;
  toolVersion: string;
}

export interface AsciiStringMatch {
  offset: number;
  text: string;
}

export interface EntropyBlock {
  startOffset: number;
  endOffset: number;
  entropy: number;
  likelyType: string;
}

export interface PotentialCalibrationRegion {
  startOffset: number;
  endOffset: number;
  lengthBytes: number;
  confidence: IdentificationConfidence;
  evidence: string;
  entropy: number;
  patternDescription: string;
  potentialAxesCount: number;
  potentialTablesCount: number;
}

export interface ChecksumCandidateRegion {
  offset: number;
  lengthBytes: number;
  algorithmName: string;
  classification: string;
  currentValueHex: string;
  calculatedValueHex: string;
  isValid: boolean;
  description: string;
}

export interface MemoryImageAnalysis {
  imageSizeBytes: number;
  shannonEntropy: number;
  zeroByteCount: number;
  zeroBytePercent: number;
  ffByteCount: number;
  ffBytePercent: number;
  repeatedPatternsCount: number;
  identifiedAsciiStrings: AsciiStringMatch[];
  potentialCodeRegions: EntropyBlock[];
  potentialDataRegions: EntropyBlock[];
  potentialCalibrationRegions: PotentialCalibrationRegion[];
  checksumCandidates: ChecksumCandidateRegion[];
  detectedGaps: MemoryAddressGap[];
}

// ==========================================
// PHASE 3 DIAGNOSTIC ENGINE & ECU HEALTH
// ==========================================

export type DiagnosticSessionStateType = 
  | 'Disconnected' 
  | 'Connected' 
  | 'Identified' 
  | 'ProtocolConfirmed' 
  | 'DefaultSession' 
  | 'ExtendedSession' 
  | 'ProgrammingSession' 
  | 'Terminated' 
  | 'Faulted';

export type DiagnosticSessionTypeValue = 'DefaultSession' | 'ProgrammingSession' | 'ExtendedDiagnosticSession' | 'SafetySystemDiagnosticSession';

export interface SessionTransitionRecord {
  fromState: DiagnosticSessionStateType;
  toState: DiagnosticSessionStateType;
  timestampUtc: string;
  reason: string;
}

export interface DiagnosticSessionStatusDto {
  sessionId: string;
  ecuName: string;
  requestCanId: number;
  responseCanId: number;
  currentState: DiagnosticSessionStateType;
  activeUdsSession?: DiagnosticSessionTypeValue | null;
  p2ServerMs: number;
  p2StarServerMs: number;
  isTesterPresentActive: boolean;
  testerPresentIntervalMs: number;
  suppressTesterPresentPositiveResponse: boolean;
  lastCommunicationUtc?: string | null;
  history: SessionTransitionRecord[];
  lastError?: string | null;
}

export interface DtcRecordDto {
  code: string;
  rawDtcNumber: number;
  statusByte: number;
  description: string;
  descriptionSource: string;
  ecuName: string;
  canId: number;
  detectedUtc: string;
  sessionWhenRead: DiagnosticSessionStateType;
  testFailed: boolean;
  testFailedThisOperationCycle: boolean;
  pendingDtc: boolean;
  confirmedDtc: boolean;
  testNotCompletedSinceLastClear: boolean;
  testFailedSinceLastClear: boolean;
  testNotCompletedThisOperationCycle: boolean;
  warningIndicatorRequested: boolean;
  formattedFlags: string[];
}

export interface DtcClearRequestDto {
  ecuName: string;
  requestCanId: number;
  groupOfDtc?: number;
  requireUserConfirmation: boolean;
  userConfirmed: boolean;
  confirmationReason: string;
}

export interface DtcClearResultDto {
  operationId: string;
  ecuName: string;
  clearedUtc: string;
  success: boolean;
  errorMessage?: string | null;
  preClearDtcs: DtcRecordDto[];
  postClearDtcs: DtcRecordDto[];
  clearedCodes: string[];
  returningCodes: string[];
}

export type DataQualityType = 'Valid' | 'Invalid' | 'Stale' | 'OutOfRange' | 'Unavailable' | 'Unknown';

export interface LiveDataParameterDefinitionDto {
  id: string;
  name: string;
  category: string;
  unit: string;
  scaling: number;
  offset: number;
  minExpectedValue: number;
  maxExpectedValue: number;
  description: string;
}

export interface LiveDataSampleDto {
  parameterId: string;
  name: string;
  category: string;
  rawBytes: number[];
  rawValue: number;
  scaledValue: number;
  formattedValue: string;
  unit: string;
  timestampUtc: string;
  quality: DataQualityType;
  qualityReason?: string | null;
  ecuName: string;
}

export type SensorPlausibilityStateType = 'Plausible' | 'Questionable' | 'OutOfRange' | 'Inconsistent' | 'InsufficientData' | 'Unknown';

export interface SensorPlausibilityCheckResultDto {
  checkName: string;
  category: string;
  state: SensorPlausibilityStateType;
  summary: string;
  detailedEvidence: string;
  involvedSignals: string[];
  measuredValues: Record<string, number>;
  evaluatedUtc: string;
}

export interface SensorPlausibilityReportDto {
  ecuName: string;
  evaluatedUtc: string;
  overallState: SensorPlausibilityStateType;
  totalChecksCount: number;
  plausibleCount: number;
  anomalyCount: number;
  checks: SensorPlausibilityCheckResultDto[];
}

export interface RoutineDescriptorDto {
  routineId: number;
  name: string;
  description: string;
  requiredSession: string;
  requiresSecurityAccess: boolean;
  safetyPreconditions: string[];
  expectedDurationSeconds: number;
  recoveryBehavior: string;
  supportsStop: boolean;
  supportsResultsRequest: boolean;
}

export interface RoutineExecutionRequestDto {
  ecuName: string;
  requestCanId: number;
  routineId: number;
  subFunction: number;
  optionRecord?: number[];
  requireUserConfirmation: boolean;
  userConfirmed: boolean;
  confirmationReason: string;
}

export interface RoutineExecutionResultDto {
  executionId: string;
  routineId: number;
  routineName: string;
  subFunction: number;
  success: boolean;
  state: string;
  routineStatusRecord: number;
  errorMessage?: string | null;
  executionLatencyMs: number;
  timestampUtc: string;
}

export interface EcuResetRequestDto {
  ecuName: string;
  requestCanId: number;
  resetType: number; // 1: Hard, 2: KeyOffOn, 3: Soft
  requireUserConfirmation: boolean;
  userConfirmed: boolean;
  confirmationReason: string;
}

export interface EcuResetResultDto {
  resetId: string;
  ecuName: string;
  resetType: number;
  success: boolean;
  errorMessage?: string | null;
  postResetPowerDownDelaySeconds: number;
  communicationRestored: boolean;
  initiatedUtc: string;
  restoredUtc?: string | null;
}

export interface DiagnosticTraceEntryDto {
  id: string;
  timestampUtc: string;
  transportId: string;
  requestCanId: number;
  responseCanId: number;
  direction: 'Tx' | 'Rx';
  protocol: string;
  serviceId: number;
  subFunction?: number | null;
  dataIdentifier?: number | null;
  payload: number[];
  formattedHex: string;
  result: 'PositiveResponse' | 'NegativeResponse' | 'Timeout' | 'TransportError' | 'Pending';
  negativeResponseCode?: number | null;
  nrcDescription?: string | null;
  retryClassification: string;
  latencyMs: number;
  activeSession: DiagnosticSessionStateType;
  notes?: string | null;
}

export type EcuHealthStatusType = 'Normal' | 'Warning' | 'Fault' | 'Unknown';

export interface DiagnosticRootCauseCandidateDto {
  suspectComponent: string;
  description: string;
  confidence: 'Confirmed' | 'Likely' | 'Possible' | 'InsufficientData' | 'Unknown';
  supportingEvidence: string[];
  recommendedPhysicalChecks: string[];
}

export interface EcuHealthReportDto {
  ecuName: string;
  evaluatedUtc: string;
  overallHealth: EcuHealthStatusType;
  healthScorePercent: number;
  activeDtcCount: number;
  confirmedDtcCount: number;
  pendingDtcCount: number;
  plausibilityAnomalyCount: number;
  systemVoltage: number;
  averageResponseLatencyMs: number;
  totalNrcCount: number;
  healthObservations: string[];
  rootCauseCandidates: DiagnosticRootCauseCandidateDto[];
}

export interface OemTargetParameterDto {
  parameterName: string;
  unit: string;
  oemTargetMin: number;
  oemTargetMax: number;
  measuredValue: number;
  isWithinTarget: boolean;
  deviation: number;
  source: string;
  notes: string;
}

export interface OemNormalizationPlanDto {
  planId: string;
  ecuName: string;
  ecuFamily: string;
  createdUtc: string;
  meetsAllOemTargets: boolean;
  evaluatedParameters: OemTargetParameterDto[];
  discrepancies: OemTargetParameterDto[];
  recommendedServiceActions: string[];
  safetyAttestation: string;
}

// ============================================================================
// FINAL PRODUCTIZATION & COMPLETE AUTOMOTIVE WORKSPACE TYPES
// ============================================================================

export interface ProjectVehicleInfo {
  vin: string;
  make: string;
  model: string;
  year: number;
  engineCode: string;
  transmission: string;
  architecture: string;
}

export interface ProjectEcuTarget {
  ecuName: string;
  family: string;
  hardwareNumber: string;
  softwareNumber: string;
  requestCanId: number;
  responseCanId: number;
  protocol: string;
  mcuFamily: string;
  flashSizeBytes: number;
}

export interface ProjectHardwareConfig {
  preferredTransportId: string;
  nominalBaudRate: number;
  dataBaudRate?: number;
  listenOnly: boolean;
  serialPort: string;
}

export interface ProjectNote {
  id: string;
  timestampUtc: string;
  author: string;
  title: string;
  content: string;
  category: string;
}

export interface ProjectEvidenceItem {
  id: string;
  timestampUtc: string;
  category: string;
  description: string;
  source: string;
  sha256Hash: string;
  rawUri: string;
}

export interface EngineeringProject {
  id: string;
  name: string;
  description: string;
  createdBy: string;
  createdUtc: string;
  lastModifiedUtc: string;
  projectHash: string;
  vehicle: ProjectVehicleInfo;
  ecuTarget: ProjectEcuTarget;
  hardware: ProjectHardwareConfig;
  associatedDbcFiles: string[];
  associatedA2lFiles: string[];
  associatedOdxFiles: string[];
  firmwareImages: string[];
  calibrationFiles: string[];
  logCaptures: string[];
  diagnosticReports: string[];
  notes: ProjectNote[];
  evidenceRecords: ProjectEvidenceItem[];
  tags: string[];
  isActive: boolean;
}

// Timeline & One-Click
export type TimelineCategory = 
  | 'CanFrame'
  | 'CanFdFrame'
  | 'DtcEvent'
  | 'UdsExchange'
  | 'ObdEvent'
  | 'SignalChange'
  | 'EcuStateChange'
  | 'UserAction'
  | 'Annotation'
  | 'SimulationEvent'
  | 'FlashOperation'
  | 'CalibrationChange'
  | 'TestResult'
  | 'SecurityAlert'
  | 'HardwareEvent';

export type TimelineSeverity = 'Trace' | 'Debug' | 'Info' | 'Notice' | 'Warning' | 'Error' | 'Critical';

export interface TimelineEntry {
  sequenceId: number;
  timestampUtc: string;
  category: TimelineCategory;
  severity: TimelineSeverity;
  sourceId: string;
  title: string;
  description: string;
  rawHex?: string;
  correlationId?: string;
  metadata: Record<string, string>;
}

export interface OneClickSessionRequest {
  projectName: string;
  preferredTransportId?: string;
  ecuName: string;
  requestCanId: number;
  responseCanId: number;
  vin?: string;
}

export interface OneClickSessionStepResult {
  stepName: string;
  success: boolean;
  summary: string;
  durationMs: number;
}

export interface OneClickSessionReport {
  sessionId: string;
  startedUtc: string;
  overallSuccess: boolean;
  activeTransportId: string;
  identifiedEcuProfile: string;
  detectedProtocol: string;
  activeDtcCount: number;
  steps: OneClickSessionStepResult[];
}

// Reverse Engineering
export interface CounterCandidate {
  startBit: number;
  bitLength: number;
  modulo: number;
  monotonicityConfidence: number;
}

export interface ChecksumCandidate {
  byteIndex: number;
  algorithm: string;
  matchRatio: number;
}

export interface DiscoveredSignalCandidate {
  name: string;
  physicalCategory: string;
  startBit: number;
  bitLength: number;
  isLittleEndian: boolean;
  minObservedRaw: number;
  maxObservedRaw: number;
  plausibleFactor: number;
  plausibleOffset: number;
  unit: string;
  confidence: 'Candidate' | 'Probable' | 'Verified';
  rationale: string;
}

export interface CanIdDeepMetrics {
  canId: number;
  formattedCanId: string;
  messageCount: number;
  frequencyHz: number;
  periodMs: number;
  minIntervalMs: number;
  maxIntervalMs: number;
  averageIntervalMs: number;
  jitterMs: number;
  dlc: number;
  payloadEntropy: number;
  byteChangeCounts: number[];
  bitChangeMask: number;
  hasBurstPatterns: boolean;
  missingMessageCount: number;
  counterCandidates: CounterCandidate[];
  checksumCandidates: ChecksumCandidate[];
  discoveredSignals: DiscoveredSignalCandidate[];
  lastPayload: number[];
  lastSeenUtc: string;
}

// Digital Twin
export interface DigitalTwinNode {
  nodeId: string;
  name: string;
  type: string;
  isOnline: boolean;
  requestCanId: number;
  responseCanId: number;
  broadcastCanIds: number[];
  broadcastIntervalMs: number;
  voltage: number;
  activeDtcs: string[];
  simulatedSignals: Record<string, number>;
  injectedBusDrop: boolean;
}

export interface DigitalTwinFaultRequest {
  nodeId: string;
  dtcCode?: string;
  setDtcActive?: boolean;
  voltageOverride?: number;
  busDrop?: boolean;
  signalName?: string;
  signalValue?: number;
}

// HIL
export interface HilChannelDescriptor {
  channelId: string;
  name: string;
  signalType: string;
  state: 'Inactive' | 'Active' | 'Simulating' | 'ShortToGround' | 'ShortToBattery' | 'OpenCircuit' | 'Faulted';
  currentValue: number;
  targetValue: number;
  unit: string;
  minAllowed: number;
  maxAllowed: number;
  description: string;
}

export interface HilTestFixture {
  fixtureId: string;
  name: string;
  isOnline: boolean;
  mainSupplyVoltage: number;
  totalCurrentAmps: number;
  ignitionRelayActive: boolean;
  terminal30Active: boolean;
  terminal15Active: boolean;
  channels: HilChannelDescriptor[];
}

export interface HilFaultInjectionCommand {
  channelId: string;
  injectedState: 'Active' | 'ShortToGround' | 'ShortToBattery' | 'OpenCircuit';
  overrideValue: number;
}

// ASAM / XCP
export interface A2lMeasurementDescriptor {
  name: string;
  description: string;
  dataType: string;
  ecuAddress: number;
  conversionMethod: string;
  resolution: number;
  offset: number;
  lowerLimit: number;
  upperLimit: number;
  unit: string;
}

export interface A2lCharacteristicDescriptor {
  name: string;
  description: string;
  type: string;
  ecuAddress: number;
  sizeBytes: number;
  recordLayout: string;
  lowerLimit: number;
  upperLimit: number;
  unit: string;
}

export interface A2lDatabase {
  ecuProjectName: string;
  epk: string;
  measurements: A2lMeasurementDescriptor[];
  characteristics: A2lCharacteristicDescriptor[];
}

export interface OdxDiagServiceDescriptor {
  semanticId: string;
  shortName: string;
  requestSid: number;
  subFunction?: number;
  description: string;
}

export interface OdxEcuVariant {
  ecuVariantId: string;
  variantName: string;
  services: OdxDiagServiceDescriptor[];
}

export interface XcpSessionStatus {
  isConnected: boolean;
  transport: string;
  resourceProtectionMask: number;
  maxCtoSize: number;
  maxDtoSize: number;
  protocolVersion: string;
  activeDaqListCount: number;
}

// Programming
export type ProgrammingLifecycleState = 
  | 'Disconnected'
  | 'Connected'
  | 'Identified'
  | 'Diagnostic'
  | 'EngineeringAuthorized'
  | 'ProgrammingReady'
  | 'Programming'
  | 'Verifying'
  | 'Complete'
  | 'Fault'
  | 'RecoveryRequired';

export interface ProgrammingStageRecord {
  stageIndex: number;
  stageName: string;
  success: boolean;
  summary: string;
  timestampUtc: string;
  durationMs: number;
}

export interface EcuProgrammingSessionStatus {
  sessionId: string;
  state: ProgrammingLifecycleState;
  targetEcuName: string;
  requestCanId: number;
  supplyVoltage: number;
  isVoltageSafe: boolean;
  originalImageHash: string;
  targetImageHash: string;
  checksumVerified: boolean;
  operatorConfirmed: boolean;
  progressPercent: number;
  completedStages: ProgrammingStageRecord[];
  lastError?: string;
}

// Research & AI Copilot
export interface ResearchDocument {
  id: string;
  title: string;
  category: string;
  authority: string;
  sourceName: string;
  sourceUrl: string;
  license: string;
  targetEcuFamily: string;
  targetVehicle: string;
  contentSummary: string;
  keywords: string[];
  indexedUtc: string;
}

export interface KnowledgeGraphNode {
  id: string;
  label: string;
  type: string;
  attributes: Record<string, string>;
}

export interface KnowledgeGraphEdge {
  sourceId: string;
  targetId: string;
  relationship: string;
}

export interface GlobalSearchResult {
  category: string;
  title: string;
  subtitle: string;
  id: string;
  relevanceScore: number;
}

export interface CopilotQueryRequest {
  domain: string;
  userPrompt: string;
  ecuName?: string;
  contextDataJson?: string;
}

export interface CopilotResponse {
  queryId: string;
  answeredUtc: string;
  domain: string;
  conclusion: string;
  supportingEvidence: string[];
  confidence: 'Confirmed' | 'High' | 'Medium' | 'Low';
  sourceProvenance: string;
  alternativeExplanation: string;
  verificationProcedure: string;
  requiresOperatorPhysicalVerification: boolean;
  safetyAdvisory: string;
}




