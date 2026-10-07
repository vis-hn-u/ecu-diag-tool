export interface DiagnosticExchange {
  id: string;
  timestampUtc: string;
  timestampUs: number;
  ecuName: string;
  requestCanId: number;
  responseCanId: number;
  protocol: 'UDS' | 'OBD-II';
  serviceId: number;
  serviceName: string;
  subFunction?: number | null;
  dataIdentifier?: number | null;
  requestDataHex: string;
  responseDataHex?: string | null;
  isComplete: boolean;
  isSuccess: boolean;
  nrcHex?: string | null;
  nrcDescription?: string | null;
  summary: string;
  responseTimeMs: number;
}

export interface DiagnosticTroubleCode {
  code: string;
  rawDtcNumber: number;
  statusByte: number;
  description: string;
  ecuName: string;
  canId: number;
  timestampUtc: string;
  isActive: boolean;
  isPending: boolean;
  isConfirmed: boolean;
  isMilRequested: boolean;
  isFailedThisCycle?: boolean;
  isFailedSinceClear?: boolean;
  isTestNotCompleted?: boolean;
  rawEvidenceHex?: string;
  activeStatusFlags: string[];
}

export type DtcClearStatus = 
  | 'VerifiedCleared'
  | 'StillPresent'
  | 'ConditionsNotCorrect'
  | 'SecurityRequired'
  | 'RequestOutOfRange'
  | 'GeneralReject'
  | 'Timeout'
  | 'TransportError';

export interface DtcClearVerificationResult {
  status: DtcClearStatus;
  clearedSuccessfully: boolean;
  preClearDtcCount: number;
  postClearDtcCount: number;
  clearedCodes: string[];
  persistingCodes: string[];
  reTriggeredCodes: string[];
  message: string;
  verifiedAtUtc: string;
}

export interface EcuIdentification {
  ecuName: string;
  requestCanId: number;
  responseCanId: number;
  vin?: string | null;
  vinStatus: 'Supported' | 'NotAvailable' | 'Unknown';
  hardwareNumber?: string | null;
  hardwareNumberStatus: 'Supported' | 'NotAvailable' | 'Unknown';
  softwareNumber?: string | null;
  softwareNumberStatus: 'Supported' | 'NotAvailable' | 'Unknown';
  softwareVersion?: string | null;
  softwareVersionStatus: 'Supported' | 'NotAvailable' | 'Unknown';
  supplierInfo?: string | null;
  bootVersion?: string | null;
  serialNumber?: string | null;
  systemName?: string | null;
  activeSession?: string | null;
  lastQueriedUtc: string;
  customDids: Record<number, string>;
}

export interface ObdPidValue {
  pid: number;
  name: string;
  value?: number | null;
  formattedValue: string;
  unit: string;
  rawBytes: number[];
  isSupported: boolean;
  timestampUtc: string;
}

export interface ObdPidDefinition {
  pid: number;
  name: string;
  description: string;
  returnBytes: number;
  unit: string;
  minValue: number;
  maxValue: number;
}

export interface VirtualEcu {
  name: string;
  requestCanId: number;
  responseCanId: number;
  description: string;
  vin: string;
  hardwareNumber: string;
  softwareVersion: string;
  systemName: string;
  activeSession: number;
  securityUnlocked: boolean;
  troubleCodes: DiagnosticTroubleCode[];
}
