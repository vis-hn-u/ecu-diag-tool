export interface EngineParameters {
  displacementLiters: number;
  cylinderCount: number;
  idleRpm: number;
  redlineRpm: number;
  revLimitRpm: number;
  isTurbocharged: boolean;
  maxBoostBar: number;
  compressionRatio: number;
  injectorFlowCcPerMin: number;
  vehicleWeightKg: number;
}

export interface EngineInputs {
  throttlePositionPct: number;
  ambientTempC: number;
  ambientPressureKpa: number;
  requestedTorqueNm: number;
  gear: number;
  brakePressed: boolean;
  clutchPressed: boolean;
}

export interface EngineOutputs {
  rpm: number;
  mapKpa: number;
  boostBar: number;
  mafGramsPerSec: number;
  engineLoadPct: number;
  torqueNm: number;
  powerKw: number;
  powerHp: number;
  lambda: number;
  afr: number;
  ignitionAdvanceDeg: number;
  fuelFlowLitersPerHour: number;
  coolantTempC: number;
  intakeAirTempC: number;
  oilTempC: number;
  fuelPressureKpa: number;
  railPressureMpa: number;
  vehicleSpeedKmh: number;
  operatingState: string;
  timestampUtc: string;
}

export interface DynoDataPoint {
  rpm: number;
  torqueNm: number;
  torqueLbFt: number;
  powerKw: number;
  powerHp: number;
  boostBar: number;
  afr: number;
  engineLoadPct: number;
  vehicleSpeedKmh: number;
  timestampMs: number;
}

export interface DynoPullSummary {
  pullId: string;
  name: string;
  timestampUtc: string;
  type: string; // 'SIMULATED', 'MEASURED', 'CALCULATED'
  peakTorqueNm: number;
  peakTorqueLbFt: number;
  peakTorqueRpm: number;
  peakPowerKw: number;
  peakPowerHp: number;
  peakPowerRpm: number;
  startRpm: number;
  endRpm: number;
  durationSeconds: number;
  points: DynoDataPoint[];
}

export interface VehiclePerformanceSummary {
  zeroToSixtyMphSeconds: number;
  zeroToHundredKmhSeconds: number;
  sixtyToHundredKmhSeconds: number;
  eightyToOneTwentyKmhSeconds: number;
  quarterMileSeconds: number;
  quarterMileSpeedKmh: number;
  sourceType: string;
}

export interface CalibrationAxis {
  name: string;
  unit: string;
  breakpoints: number[];
}

export interface CalibrationTable {
  id: string;
  name: string;
  description: string;
  type: number;
  xAxis: CalibrationAxis;
  yAxis?: CalibrationAxis | null;
  data: number[][];
  originalData: number[][];
  minValue: number;
  maxValue: number;
  unit: string;
}

export interface CalibrationDiffCell {
  xIndex: number;
  yIndex: number;
  xBreakpoint: number;
  yBreakpoint: number;
  originalValue: number;
  modifiedValue: number;
  absoluteDiff: number;
  percentDiff: number;
  state: 'UNCHANGED' | 'INCREASED' | 'DECREASED';
}

export interface CalibrationEditRecord {
  id: string;
  tableId: string;
  timestampUtc: string;
  xIndex: number;
  yIndex: number;
  previousValue: number;
  newValue: number;
  operation: string;
}

export interface CalibrationProject {
  id: string;
  name: string;
  vehicleModel: string;
  ecuType: string;
  version: string;
  lastModifiedUtc: string;
  tables: CalibrationTable[];
}

// Measurement-Based Road Dyno Models
export type AtmosphericStandard = 'Uncorrected' | 'SAE_J1349' | 'DIN_70020' | 'EWG_80_1269' | 'ISO_1585';

export interface AmbientConditions {
  temperatureC: number;
  barometricPressureHpa: number;
  relativeHumidityPct: number;
  headwindSpeedKmh: number;
  roadSlopePct: number;
}

export interface RoadDynoSample {
  timestampMs: number;
  gpsSpeedKmh: number;
  engineRpm: number;
  throttlePosition?: number;
  latitude?: number | null;
  longitude?: number | null;
  altitudeMeters?: number | null;
  slopePct?: number | null;
}

export interface RoadDynoDataPoint {
  timestampMs: number;
  speedKmh: number;
  rpm: number;
  accelerationMps2: number;
  forceInertialN: number;
  forceAeroN: number;
  forceRollingN: number;
  forceSlopeN: number;
  forceTotalN: number;
  wheelPowerKw: number;
  wheelPowerHp: number;
  wheelTorqueNm: number;
  wheelTorqueLbFt: number;
  lossPowerHp: number;
  enginePowerKw: number;
  enginePowerHp: number;
  engineTorqueNm: number;
  engineTorqueLbFt: number;
  correctionFactor: number;
  correctedEnginePowerHp: number;
  correctedEngineTorqueNm: number;
}

export interface CoastDownProfile {
  samples: RoadDynoSample[];
  coeffA: number;
  coeffB: number;
  meanDecelerationMps2: number;
  isMeasured: boolean;
}

export interface RunQualityMetrics {
  smoothnessScore: number;
  jerkRms: number;
  gpsSampleRateHz: number;
  averageSlopePct: number;
  maxSlopeDeltaPct: number;
  gearRatioVariancePct: number;
  detectedGear: number;
  qualityRating: 'EXCELLENT' | 'GOOD' | 'MARGINAL' | 'REJECTED';
  warnings: string[];
}

export interface AccelerationPerformanceMetrics {
  zeroToSixtyMphSec?: number | null;
  zeroToHundredKmhSec?: number | null;
  sixtyToOneTwentyKmhSec?: number | null;
  hundredToTwoHundredKmhSec?: number | null;
  quarterMileSec?: number | null;
  quarterMileTrapSpeedKmh?: number | null;
  quarterMileTrapSpeedMph?: number | null;
  fiftyToEightyKmhSec?: number | null;
  eightyToOneTwentyKmhSec?: number | null;
}

export interface RoadDynoRun {
  runId: string;
  name: string;
  timestampUtc: string;
  gear: number;
  standard: AtmosphericStandard;
  correctionFactor: number;

  peakWheelPowerHp: number;
  peakWheelPowerKw: number;
  peakWheelPowerRpm: number;
  peakWheelTorqueNm: number;
  peakWheelTorqueLbFt: number;
  peakWheelTorqueRpm: number;

  peakEnginePowerHp: number;
  peakEnginePowerKw: number;
  peakEnginePowerRpm: number;
  peakEngineTorqueNm: number;
  peakEngineTorqueLbFt: number;
  peakEngineTorqueRpm: number;

  peakCorrectedPowerHp: number;
  peakCorrectedTorqueNm: number;

  startRpm: number;
  endRpm: number;
  durationSeconds: number;

  quality: RunQualityMetrics;
  performance: AccelerationPerformanceMetrics;
  points: RoadDynoDataPoint[];

  vehicleSnapshot?: any;
  ambientSnapshot?: AmbientConditions;
  coastDown?: CoastDownProfile | null;
}

export interface DynoRunComparisonResult {
  baselineRunId: string;
  baselineRunName: string;
  testRunId: string;
  testRunName: string;
  alignedRpm: number[];

  baselinePowerHp: number[];
  testPowerHp: number[];
  deltaPowerHp: number[];
  deltaPowerPct: number[];

  baselineTorqueNm: number[];
  testTorqueNm: number[];
  deltaTorqueNm: number[];
  deltaTorquePct: number[];

  peakPowerDeltaHp: number;
  peakPowerDeltaPct: number;
  peakTorqueDeltaNm: number;
  peakTorqueDeltaPct: number;

  zeroToSixtyDeltaSec?: number | null;
  zeroToHundredDeltaSec?: number | null;
  quarterMileDeltaSec?: number | null;
  quarterMileTrapSpeedDeltaKmh?: number | null;
}

