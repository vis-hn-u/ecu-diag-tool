export type ManufacturerOem = 'Generic' | 'MarutiMarelli' | 'VAG' | 'BMW' | 'Ford' | 'GM' | 'Toyota' | 'Mercedes';

export type LiveDataIdentifierType = 'UdsDid' | 'KwpLocalId' | 'ObdPid' | 'BroadcastCan';

export interface ManufacturerLiveDataDefinition {
  id: string;
  oem: ManufacturerOem;
  category: string;
  name: string;
  description: string;
  identifierType: LiveDataIdentifierType;
  identifier: number;
  identifierHex: string;
  byteOffset: number;
  byteLength: number;
  isBigEndian: boolean;
  isSigned: boolean;
  factor: number;
  offset: number;
  unit: string;
  minValue: number;
  maxValue: number;
  decimalFormat: string;
  stateLabels?: Record<number, string>;
}

export interface DecodedManufacturerValue {
  definitionId: string;
  oem: ManufacturerOem;
  name: string;
  category: string;
  rawValue: number;
  scaledValue: number;
  formattedValue: string;
  unit: string;
  stateLabel?: string;
  timestampUtc: string;
  isWarning: boolean;
}

export type VagFormulaType =
  | 'Rpm'
  | 'AbsoluteLoad'
  | 'ThrottleAngle'
  | 'Temperature'
  | 'Voltage'
  | 'Speed'
  | 'AirFlow'
  | 'TimingAngle'
  | 'VoltageFine'
  | 'PressureMbar'
  | 'DutyCycle'
  | 'LambdaRatio'
  | 'MassAirFlow'
  | 'Counter'
  | 'BinaryStatus'
  | 'Custom';

export interface VagMeasuringBlockZone {
  zoneNumber: number;
  label: string;
  formulaType: VagFormulaType;
  rawA: number;
  rawB: number;
  value: number;
  formattedValue: string;
  unit: string;
  minNominal?: number;
  maxNominal?: number;
  isOutOfRange: boolean;
}

export interface VagMeasuringGroup {
  groupNumber: number;
  groupKey: string;
  name: string;
  description: string;
  ecuSubsystem: string;
  zones: VagMeasuringBlockZone[];
  lastUpdatedUtc: string;
}

export interface FreezeFrameSensorValue {
  parameterId: string;
  name: string;
  category: string;
  rawValue: number;
  scaledValue: number;
  formattedValue: string;
  unit: string;
}

export interface DiagnosticFreezeFrame {
  id: string;
  dtcCode: string;
  recordNumber: number;
  timestampUtc: string;
  timestampUs: number;
  sourceEcu: string;
  protocol: string;
  statusByte: number;
  faultTriggerReason: string;
  sensors: FreezeFrameSensorValue[];
  engineRpm?: number;
  vehicleSpeedKmh?: number;
  coolantTempC?: number;
  engineLoadPct?: number;
}

export type DtcSeverity = 'Minor' | 'Moderate' | 'Major' | 'Critical';
export type DtcFaultCategory = 
  | 'FuelSystem' 
  | 'IgnitionCombustion' 
  | 'AirInductionTurbo' 
  | 'EmissionsCatalyst' 
  | 'ElectricalSensors' 
  | 'TransmissionDriveline' 
  | 'NetworkCommunication';

export interface DtcMetadata {
  code: string;
  title: string;
  severity: DtcSeverity;
  category: DtcFaultCategory;
  technicalExplanation: string;
  commonSymptoms: string[];
  likelyCauses: string[];
  diagnosticSteps: string[];
  associatedVagGroups: number[];
  associatedUdsDids: string[];
  oemCrossReferences: Record<string, string>;
}

export interface SignalAnomalyMetric {
  signalName: string;
  unit: string;
  baselineMean: number;
  faultMomentValue: number;
  deviationZScore: number;
  deviationPct: number;
  correlationConfidence: number;
  isPrimarySuspect: boolean;
  anomalyDescription: string;
}

export interface DtcCorrelationResult {
  dtcCode: string;
  dtcTitle: string;
  faultTimestampUtc: string;
  analysisWindowSeconds: number;
  anomalies: SignalAnomalyMetric[];
  topSuspectSignal: string;
  rootCauseHypothesis: string;
}

export type DashboardWidgetType = 'RadialGauge' | 'DigitalValue' | 'MiniGraph' | 'LevelBar' | 'StatusAnnunciator';
export type WidgetSignalSource = 
  | 'CanSignal' 
  | 'ObdPid' 
  | 'UdsDid' 
  | 'VagMeasuringBlock' 
  | 'ManufacturerLiveData' 
  | 'LiveParameter' 
  | 'CalculatedSignal';

export interface WidgetGridPosition {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface DashboardWidget {
  id: string;
  type: DashboardWidgetType;
  title: string;
  subtitle: string;
  sourceType: WidgetSignalSource;
  sourceIdentifier: string;
  minValue: number;
  maxValue: number;
  warnThreshold?: number;
  dangerThreshold?: number;
  unit: string;
  colorScheme: 'sky' | 'emerald' | 'amber' | 'rose' | 'purple';
  decimalPlaces: number;
  position: WidgetGridPosition;
}

export interface DashboardLayout {
  id: string;
  name: string;
  description: string;
  presetKey?: string;
  gridColumns: number;
  theme: string;
  lastModifiedUtc: string;
  widgets: DashboardWidget[];
}

export type MapDimension = 'Scalar' | 'OneDimensional' | 'TwoDimensional';
export type MapCategory = 
  | 'Fuel' 
  | 'Ignition' 
  | 'Boost' 
  | 'Airflow' 
  | 'Torque' 
  | 'Protection' 
  | 'VariableValveTiming' 
  | 'Idle';

export interface ReadonlyMapAxis {
  name: string;
  unit: string;
  breakpoints: number[];
}

export interface ReadonlyEcuMap {
  mapId: string;
  name: string;
  description: string;
  category: MapCategory;
  dimension: MapDimension;
  memoryAddressHex: string;
  xAxis: ReadonlyMapAxis;
  yAxis?: ReadonlyMapAxis;
  zData: number[][];
  zUnit: string;
  minValue: number;
  maxValue: number;
  decimalPrecision: number;
  heatmapPalette: string;
  isReadOnly: boolean;
}

export interface EcuDefinitionPack {
  id: string;
  name: string;
  targetPlatform: string;
  hardwarePartNumber: string;
  softwareVersion: string;
  calibrationId: string;
  author: string;
  version: string;
  createdAtUtc: string;
  supportedProtocols: string[];
  maps: ReadonlyEcuMap[];
  isReadOnly: boolean;
}

export interface OperatingPointTracer {
  currentRpm: number;
  currentLoad: number;
  nearestXIndex: number;
  nearestYIndex: number;
  interpolatedZValue: number;
  activeMapId: string;
}
