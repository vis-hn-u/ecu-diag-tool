export type LiveParameterCategory =
  | 'Engine'
  | 'AirTurbo'
  | 'Fuel'
  | 'Egr'
  | 'Dpf'
  | 'ScrAdBlue'
  | 'Nox'
  | 'Thermal'
  | 'Vehicle'
  | 'Transmission'
  | 'Diagnostics'
  | 'Other';

export type ParameterProvenance =
  | 'Dbc'
  | 'ObservedCan'
  | 'DiagnosticResponse'
  | 'Simulation'
  | 'Unknown';

export interface LiveParameter {
  id: string;
  name: string;
  description: string;
  category: number | LiveParameterCategory;
  value: number;
  formattedValue: string;
  unit: string;
  provenance: number | ParameterProvenance;
  canId: number;
  messageName: string;
  signalName: string;
  updateRateHz: number;
  lastSeenUtc: string;
  isPinned: boolean;
  minObserved: number;
  maxObserved: number;
}

export interface RequestedVsActualPair {
  id: string;
  parameterName: string;
  category: number | LiveParameterCategory;
  unit: string;
  targetValue: number;
  actualValue: number;
  errorDelta: number;
  errorPercent: number;
  timestampUtc: string;
}

export interface CalibrationObject {
  id: string;
  name: string;
  description: string;
  category: number;
  objectType: string;
  fileOffset: number;
  logicalAddress?: number | null;
  sizeBytes: number;
  dataType: string;
  scalingFactor: number;
  offset: number;
  unit: string;
  minValue: number;
  maxValue: number;
  xBreakpoints: number[];
  yBreakpoints?: number[] | null;
  values: number[][];
  provenance: number; // 0=Confirmed, 1=ProjectDefined, 2=UserDefined, 3=Heuristic, 4=Unknown
}

export interface ChecksumInfo {
  regionName: string;
  algorithm: string;
  startOffset: number;
  endOffset: number;
  expectedChecksumHex: string;
  calculatedChecksumHex: string;
  isValid: boolean;
}

export interface EcuBinaryInfo {
  fileId: string;
  fileName: string;
  fileSizeBytes: number;
  sha256Hash: string;
  md5Hash: string;
  detectedEcuFamily: string;
  softwareNumber: string;
  hardwareNumber: string;
  importedUtc: string;
  objects: CalibrationObject[];
  checksums: ChecksumInfo[];
}

export interface BinaryDiffChunk {
  offset: number;
  length: number;
  hexOriginal: string;
  hexModified: string;
}

export interface BinaryDiffResult {
  originalFileName: string;
  modifiedFileName: string;
  originalSizeBytes: number;
  modifiedSizeBytes: number;
  totalDifferentBytes: number;
  differencePercentage: number;
  chunks: BinaryDiffChunk[];
}

export interface LiveFlashCorrelation {
  id: string;
  objectName: string;
  calibrationCategory: string;
  calibratedLimit: number;
  liveSignalName: string;
  liveObservedValue: number;
  unit: string;
  marginDelta: number;
  timestampUtc: string;
}
