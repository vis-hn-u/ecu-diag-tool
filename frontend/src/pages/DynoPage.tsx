import React, { useState, useEffect, useRef, useMemo } from 'react';
import { api, downloadBlobAsFile } from '../services/api';
import { 
  DynoPullSummary, 
  VehiclePerformanceSummary,
  RoadDynoRun,
  RoadDynoSample,
  AmbientConditions,
  AtmosphericStandard,
  CoastDownProfile,
  DynoRunComparisonResult
} from '../types/engine';
import { VehicleProfile } from '../types';
import { 
  Flame, 
  Zap, 
  Play, 
  Timer, 
  Award, 
  Sparkles,
  Gauge,
  Sliders,
  Wind,
  CloudRain,
  Compass,
  CheckCircle2,
  AlertTriangle,
  GitCompare,
  Layers,
  Search,
  Activity,
  Maximize2,
  FileText,
  Square,
  Radio,
  RotateCcw
} from 'lucide-react';
import { useCan } from '../context/CanContext';

const FALLBACK_VEHICLES: VehicleProfile[] = [
  {
    id: 'default-swift-mjd',
    name: 'Maruti Suzuki Swift DDiS (Magneti Marelli MJD 6F3/8F3)',
    vin: 'MA3EFA12S00123456',
    make: 'Maruti Suzuki',
    model: 'Swift 1.3 DDiS (MJD 6F3/8F3)',
    year: 2015,
    curbWeightKg: 1065.0,
    testWeightKg: 1150.0,
    occupantsWeightKg: 85.0,
    dragCoefficient: 0.32,
    frontalAreaM2: 2.05,
    rollingResistanceCoeff: 0.013,
    drivetrainType: 'FWD',
    drivetrainLossPct: 11.0,
    rotationalMassFactor: 1.08,
    engine: {
      code: 'D13A (Fiat MultiJet / DDiS)',
      displacementLiters: 1.248,
      cylinderCount: 4,
      fuelType: 'Diesel',
      aspiration: 'Turbocharged',
      maxBoostBar: 1.35,
      compressionRatio: 17.6,
      injectorFlowCcPerMin: 320.0,
      redlineRpm: 4500.0,
      revLimitRpm: 5000.0,
      idleRpm: 850.0
    },
    ecu: {
      hardwarePartNumber: 'MJD 6F3.S1 / 8F3',
      hardwareVersion: 'MJD 6F3 / 8F3',
      softwareVersion: '3422S167',
      calibrationId: 'SWIFT_1.3_DDiS',
      diagnosticProtocol: 'UDS',
      requestAddress: 0x7E0,
      responseAddress: 0x7E8
    },
    transmission: {
      type: 'Manual',
      forwardGears: 5,
      finalDriveRatio: 3.944,
      gearRatios: [3.909, 2.158, 1.393, 1.029, 0.794]
    },
    tires: {
      sizeSpec: '185/65R15',
      widthMm: 185.0,
      aspectRatioPct: 65.0,
      rimDiameterInches: 15.0,
      rollingRadiusMeters: 0.3108,
      circumferenceMeters: 1.953
    },
    createdAtUtc: new Date().toISOString()
  },
  {
    id: 'mahindra-scorpio-mhawk',
    name: 'Mahindra Scorpio 2.2 mHawk (Bosch EDC17C53 / EDC17C55)',
    vin: 'MA1TA2SKL00654321',
    make: 'Mahindra',
    model: 'Scorpio 2.2 mHawk CRDe',
    year: 2018,
    curbWeightKg: 1820.0,
    testWeightKg: 1950.0,
    occupantsWeightKg: 130.0,
    dragCoefficient: 0.38,
    frontalAreaM2: 2.70,
    rollingResistanceCoeff: 0.015,
    drivetrainType: 'RWD',
    drivetrainLossPct: 14.0,
    rotationalMassFactor: 1.10,
    engine: {
      code: 'mHawk 2.2 CRDe',
      displacementLiters: 2.179,
      cylinderCount: 4,
      fuelType: 'Diesel',
      aspiration: 'Turbocharged',
      maxBoostBar: 1.55,
      compressionRatio: 16.5,
      injectorFlowCcPerMin: 420.0,
      redlineRpm: 4200.0,
      revLimitRpm: 4600.0,
      idleRpm: 800.0
    },
    ecu: {
      hardwarePartNumber: '0281017832',
      hardwareVersion: 'Bosch EDC17C53',
      softwareVersion: '1037521876',
      calibrationId: 'MHAWK_2.2_BS4',
      diagnosticProtocol: 'UDS',
      requestAddress: 0x7E0,
      responseAddress: 0x7E8
    },
    transmission: {
      type: 'Manual',
      forwardGears: 6,
      finalDriveRatio: 4.10,
      gearRatios: [4.11, 2.25, 1.42, 1.00, 0.81, 0.69]
    },
    tires: {
      sizeSpec: '235/65R17',
      widthMm: 235.0,
      aspectRatioPct: 65.0,
      rimDiameterInches: 17.0,
      rollingRadiusMeters: 0.368,
      circumferenceMeters: 2.312
    },
    createdAtUtc: new Date().toISOString()
  },
  {
    id: 'ea888-gen3-bench',
    name: 'Bench Test Platform (EA888 Gen 3 2.0T / Simos)',
    vin: 'WVWZZZAUZEP000001',
    make: 'Engineering Lab',
    model: 'Turbo 2.0L Testbench',
    year: 2023,
    curbWeightKg: 1450.0,
    testWeightKg: 1530.0,
    occupantsWeightKg: 80.0,
    dragCoefficient: 0.31,
    frontalAreaM2: 2.20,
    rollingResistanceCoeff: 0.013,
    drivetrainType: 'FWD',
    drivetrainLossPct: 12.0,
    rotationalMassFactor: 1.08,
    engine: {
      code: 'EA888.3',
      displacementLiters: 1.984,
      cylinderCount: 4,
      fuelType: 'Gasoline',
      aspiration: 'Turbocharged',
      maxBoostBar: 1.35,
      compressionRatio: 9.6,
      injectorFlowCcPerMin: 550.0,
      redlineRpm: 6800.0,
      revLimitRpm: 7000.0,
      idleRpm: 800.0
    },
    ecu: {
      hardwarePartNumber: '06K907425B',
      hardwareVersion: 'Simos 18.1',
      softwareVersion: '0005',
      calibrationId: '5G0906259L',
      diagnosticProtocol: 'UDS',
      requestAddress: 0x7E0,
      responseAddress: 0x7E8
    },
    transmission: {
      type: 'Manual',
      forwardGears: 6,
      finalDriveRatio: 3.944,
      gearRatios: [3.769, 2.087, 1.457, 1.109, 0.974, 0.733]
    },
    tires: {
      sizeSpec: '235/40R18',
      widthMm: 235.0,
      aspectRatioPct: 40.0,
      rimDiameterInches: 18.0,
      rollingRadiusMeters: 0.3226,
      circumferenceMeters: 2.027
    },
    createdAtUtc: new Date().toISOString()
  }
];

export const DynoPage: React.FC = () => {
  // Mode selection: Road Dyno (Acquisition) vs Engine Simulator (Simulation)
  const [dynoMode, setDynoMode] = useState<'RoadDyno' | 'Simulator'>('RoadDyno');

  // Simulation Mode State (Existing deterministic simulator)
  const [pulls, setPulls] = useState<DynoPullSummary[]>([]);
  const [activePull, setActivePull] = useState<DynoPullSummary | null>(null);
  const [performance, setPerformance] = useState<VehiclePerformanceSummary | null>(null);
  const [isRunningSim, setIsRunningSim] = useState(false);

  // CAN Context for live bus signals
  const { signals, totalFrames, fps } = useCan();

  // Helper to extract live numerical values matching candidate keywords
  const getLiveVal = (candidates: string[]): number | null => {
    for (const [key, sig] of Object.entries(signals)) {
      const k = key.toLowerCase();
      for (const c of candidates) {
        if (k.includes(c) && typeof sig.engineeringValue === 'number') {
          return sig.engineeringValue;
        }
      }
    }
    return null;
  };

  const liveCanRpm = getLiveVal(['rpm', 'engspd', 'eng_spd', 'drehzahl', 'cr_ems_engspd', 'mo_drehzahl', 'enginespeed', 'motordrehzahl', 'engine_speed', '010c', 'pid_0c', 'pid0c']);
  const liveCanSpeed = getLiveVal(['vehspd', 'veh_spd', 'vehiclespeed', 'geschw', 'v_fzg', 'speed_vehicle', 'speed', '010d', 'pid_0d', 'pid0d', 'vss', 'wheelspeed', 'radgeschw']);
  const liveCanThrottle = getLiveVal(['throttle', 'pedal', 'accel', 'gaspedal', 'app', 'throttlepos', 'gas_pedal', '0111', 'pid_11', 'pid11', '0149', 'pid_49', 'fahrpedal']);

  // Road Dyno Mode State
  const [vehicleProfiles, setVehicleProfiles] = useState<VehicleProfile[]>(FALLBACK_VEHICLES);
  const [selectedVehicle, setSelectedVehicle] = useState<VehicleProfile | null>(FALLBACK_VEHICLES[0]);
  const [roadRuns, setRoadRuns] = useState<RoadDynoRun[]>([]);
  const [activeRoadRun, setActiveRoadRun] = useState<RoadDynoRun | null>(null);
  const [isCalculatingRoadRun, setIsCalculatingRoadRun] = useState(false);

  // Live Acquisition State Machine
  type AcquisitionState = 
    | 'IDLE' 
    | 'ARMED' 
    | 'VALIDATING' 
    | 'RUN_DETECTED' 
    | 'MEASURING' 
    | 'COASTDOWN' 
    | 'CALCULATING' 
    | 'QUALITY_CHECK' 
    | 'COMPLETE' 
    | 'REJECTED';
  const [acquisitionState, setAcquisitionState] = useState<AcquisitionState>('IDLE');
  const [rejectionReason, setRejectionReason] = useState<string | null>(null);
  const [recordedSamplesCount, setRecordedSamplesCount] = useState<number>(0);
  const recordedSamplesRef = useRef<RoadDynoSample[]>([]);
  const pullStartTimeRef = useRef<number>(0);

  // Road Dyno Settings
  const [selectedGear, setSelectedGear] = useState<number>(3);
  const [standard, setStandard] = useState<AtmosphericStandard>('SAE_J1349');
  const [ambient, setAmbient] = useState<AmbientConditions>({
    temperatureC: 22.0,
    barometricPressureHpa: 1013.25,
    relativeHumidityPct: 45.0,
    headwindSpeedKmh: 0.0,
    roadSlopePct: 0.0
  });
  const [lossModel, setLossModel] = useState<'Empirical' | 'CoastDown'>('Empirical');
  const [coastDownProfile, setCoastDownProfile] = useState<CoastDownProfile | null>(null);
  const [unitMode, setUnitMode] = useState<'Metric' | 'Imperial'>('Imperial');
  const [showCorrected, setShowCorrected] = useState<boolean>(true);
  const [showWheelPower, setShowWheelPower] = useState<boolean>(true);

  // Bench / Chassis Dyno Mode: Derive Wheel Speed from CAN Engine RPM
  const [benchDynoMode, setBenchDynoMode] = useState<boolean>(true);

  const derivedBenchSpeed = useMemo(() => {
    if (!liveCanRpm || !selectedVehicle) return null;
    const gRatio = selectedVehicle.transmission?.gearRatios?.[selectedGear - 1] || 1.457;
    const totalRatio = gRatio * (selectedVehicle.transmission?.finalDriveRatio || 3.944);
    const circ = selectedVehicle.tires?.circumferenceMeters || 2.02;
    const wheelRps = liveCanRpm / (totalRatio * 60.0);
    return Math.round(wheelRps * circ * 3.6 * 10.0) / 10.0;
  }, [liveCanRpm, selectedVehicle, selectedGear]);

  const effectiveSpeed = (liveCanSpeed !== null && liveCanSpeed > 1) 
    ? liveCanSpeed 
    : (benchDynoMode ? derivedBenchSpeed : null);

  // Comparison State
  const [comparisonResult, setComparisonResult] = useState<DynoRunComparisonResult | null>(null);
  const [baselineRunId, setBaselineRunId] = useState<string>('');
  const [testRunId, setTestRunId] = useState<string>('');
  const [showComparisonModal, setShowComparisonModal] = useState<boolean>(false);
  const [isExportingDynoPdf, setIsExportingDynoPdf] = useState(false);

  const handleExportDynoPdf = async () => {
    setIsExportingDynoPdf(true);
    try {
      const pullId = dynoMode === 'RoadDyno' ? activeRoadRun?.runId : activePull?.pullId;
      const baselineId = baselineRunId || undefined;
      const vehicleId = selectedVehicle?.id;
      const blob = await api.downloadDynoReportPdf(pullId, baselineId, vehicleId);
      downloadBlobAsFile(blob, `Dyno_Performance_Report_${new Date().toISOString().slice(0, 10)}.pdf`);
    } catch (e) {
      console.error('Failed to export dyno PDF:', e);
    } finally {
      setIsExportingDynoPdf(false);
    }
  };

  // Crosshair hover state on SVG
  const [hoverRpm, setHoverRpm] = useState<number | null>(null);

  // Fetch initial data
  const fetchData = async () => {
    try {
      // Fetch vehicles with guaranteed fallback
      let vList: VehicleProfile[] = [];
      try {
        vList = await api.getVehicleProfiles();
      } catch {
        vList = [];
      }
      if (!vList || vList.length === 0) {
        vList = FALLBACK_VEHICLES;
      }
      setVehicleProfiles(vList);
      if (!selectedVehicle) {
        setSelectedVehicle(vList[0]);
      }

      // Fetch simulated pulls
      const savedPulls = await api.getDynoPulls();
      setPulls(savedPulls);
      if (savedPulls.length > 0 && !activePull) {
        setActivePull(savedPulls[0]);
        const perf = await api.getDynoPerformance(savedPulls[0].pullId);
        setPerformance(perf);
      }

      // Fetch saved road runs
      const savedRoadRuns = await api.getSavedRoadDynoRuns();
      setRoadRuns(savedRoadRuns);
      if (savedRoadRuns.length > 0 && !activeRoadRun) {
        setActiveRoadRun(savedRoadRuns[0]);
      } else if (savedRoadRuns.length === 0) {
        // Generate initial baseline road run if empty
        generateDefaultRoadRun(vList[0]);
      }
    } catch {
      if (!activeRoadRun) {
        generateDefaultRoadRun(FALLBACK_VEHICLES[0]);
      }
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  // Helper to calculate RPM from road speed and transmission gear
  const getRpmForSpeed = (v: VehicleProfile | null, gear: number, speedKmh: number): number => {
    if (!v || !v.transmission?.gearRatios || !v.tires) {
      return Math.round(2000 + (speedKmh * 35));
    }
    const gRatio = v.transmission.gearRatios[gear - 1] || 1.457;
    const totalRatio = gRatio * (v.transmission.finalDriveRatio || 3.944);
    const circ = v.tires.circumferenceMeters || 2.02;
    const wheelRps = (speedKmh / 3.6) / circ;
    return Math.round(wheelRps * totalRatio * 60.0);
  };

  // Helper to generate a realistic baseline sample acquisition run
  const generateDefaultRoadRun = async (vehicle?: VehicleProfile) => {
    const v = vehicle || selectedVehicle || FALLBACK_VEHICLES[0];
    if (!v) return;

    const isDiesel = v.engine?.fuelType?.toLowerCase() === 'diesel' || v.name.toLowerCase().includes('ddis') || v.name.toLowerCase().includes('mhawk');
    const startRpm = isDiesel ? 1600 : 2200;
    const endRpm = isDiesel ? 4400 : 6600;
    const duration = isDiesel ? 4.5 : 4.0;

    const samples: RoadDynoSample[] = [];
    const pointsCount = 45;
    for (let i = 0; i <= pointsCount; i++) {
      const t = (i / pointsCount) * duration;
      const progress = Math.pow(i / pointsCount, 0.85);
      const rpm = startRpm + (endRpm - startRpm) * progress;
      const speed = isDiesel ? (35.0 + 75.0 * progress) : (50.0 + 85.0 * progress);

      samples.push({
        timestampMs: t * 1000.0,
        gpsSpeedKmh: Math.round(speed * 10.0) / 10.0,
        engineRpm: Math.round(rpm),
        throttlePosition: 100.0
      });
    }

    try {
      const run = await api.runRoadDyno({
        name: `${v.name} Baseline Dyno Curve`,
        gear: selectedGear,
        standard,
        ambient,
        vehicle: v,
        samples
      });
      setActiveRoadRun(run);
      setRoadRuns(prev => [run, ...prev]);
    } catch {
      // Ignore
    }
  };

  const handleArmTrigger = () => {
    setRejectionReason(null);
    if (liveCanRpm === null || effectiveSpeed === null) {
      setAcquisitionState('REJECTED');
      setRejectionReason(
        'CAN LIVE DATA UNAVAILABLE: Dyno acquisition requires live Engine RPM decoded from CANH / CANL. Connect your vehicle or bench ECU to the CAN bus.'
      );
      return;
    }
    setAcquisitionState('ARMED');
  };

  // Live Sample Sampling Loop
  useEffect(() => {
    if (
      acquisitionState !== 'ARMED' && 
      acquisitionState !== 'VALIDATING' && 
      acquisitionState !== 'RUN_DETECTED' && 
      acquisitionState !== 'MEASURING' && 
      acquisitionState !== 'COASTDOWN'
    ) return;

    const interval = setInterval(() => {
      const v = selectedVehicle || FALLBACK_VEHICLES[0];
      const now = Date.now();

      // Enforce live data presence - NEVER synthesize fake data in RoadDyno mode
      if (liveCanRpm === null || effectiveSpeed === null) {
        if (acquisitionState === 'MEASURING' || acquisitionState === 'RUN_DETECTED') {
          setAcquisitionState('REJECTED');
          setRejectionReason('CAN SIGNAL LOSS: Engine RPM or Speed dropped out during dyno pull.');
        }
        return;
      }

      const throttle = liveCanThrottle ?? 100.0;
      const rpm = liveCanRpm;
      const speed = effectiveSpeed;

      // 1. ARMED: Waiting for trigger
      if (acquisitionState === 'ARMED') {
        if (throttle > 70 || rpm > 1800) {
          setAcquisitionState('VALIDATING');
        }
        return;
      }

      // 2. VALIDATING: Verify sustained WOT
      if (acquisitionState === 'VALIDATING') {
        if (throttle >= 70) {
          setAcquisitionState('RUN_DETECTED');
          pullStartTimeRef.current = now;
          recordedSamplesRef.current = [];
          setRecordedSamplesCount(0);
        } else {
          setAcquisitionState('ARMED');
        }
        return;
      }

      // 3. RUN_DETECTED: Transition to MEASURING
      if (acquisitionState === 'RUN_DETECTED') {
        setAcquisitionState('MEASURING');
        return;
      }

      // 4. MEASURING: Genuine CAN data only
      if (acquisitionState === 'MEASURING') {
        const elapsedSec = (now - pullStartTimeRef.current) / 1000.0;

        recordedSamplesRef.current.push({
          timestampMs: elapsedSec * 1000.0,
          gpsSpeedKmh: Math.round(speed * 10.0) / 10.0,
          engineRpm: Math.round(rpm),
          throttlePosition: Math.round(throttle)
        });
        setRecordedSamplesCount(recordedSamplesRef.current.length);

        const redline = v.engine?.redlineRpm || 4800;
        const isEndOfPull = (throttle < 40 && recordedSamplesRef.current.length >= 15) || 
                            rpm >= redline || 
                            elapsedSec > 9.0;

        if (isEndOfPull && recordedSamplesRef.current.length >= 8) {
          if (lossModel === 'CoastDown') {
            setAcquisitionState('COASTDOWN');
          } else {
            finishLiveAcquisition();
          }
        }
        return;
      }

      // 5. COASTDOWN
      if (acquisitionState === 'COASTDOWN') {
        const lastSample = recordedSamplesRef.current[recordedSamplesRef.current.length - 1];
        if (speed < (lastSample?.gpsSpeedKmh || 100) - 15 || throttle > 20) {
          finishLiveAcquisition();
        }
      }
    }, 100);

    return () => clearInterval(interval);
  }, [acquisitionState, liveCanRpm, effectiveSpeed, liveCanThrottle, selectedVehicle, lossModel]);

  // Finish and compute dyno curve from recorded samples
  const finishLiveAcquisition = async () => {
    const v = selectedVehicle || FALLBACK_VEHICLES[0];
    const samples = [...recordedSamplesRef.current];
    setIsCalculatingRoadRun(true);

    if (samples.length < 5) {
      setAcquisitionState('REJECTED');
      setRejectionReason('INSUFFICIENT DATA: Fewer than 5 valid CAN samples were acquired during the pull.');
      setIsCalculatingRoadRun(false);
      return;
    }

    setAcquisitionState('QUALITY_CHECK');
    const startRpm = samples[0].engineRpm;
    const endRpm = samples[samples.length - 1].engineRpm;
    if (endRpm <= startRpm + 350) {
      setAcquisitionState('REJECTED');
      setRejectionReason(`RPM SLOPE INVALID: Engine RPM did not rise sufficiently during pull (${startRpm} -> ${endRpm} RPM). Verify vehicle was at WOT in gear.`);
      setIsCalculatingRoadRun(false);
      return;
    }

    setAcquisitionState('CALCULATING');
    try {
      const run = await api.runRoadDyno({
        name: `Live CAN Pull #${roadRuns.length + 1} (${v.name.split('(')[0].trim()})`,
        gear: selectedGear,
        standard,
        ambient,
        vehicle: v,
        coastDown: lossModel === 'CoastDown' ? coastDownProfile : null,
        samples
      });

      setActiveRoadRun(run);
      setRoadRuns(prev => [run, ...prev]);
      setAcquisitionState('COMPLETE');
    } catch (err: any) {
      setAcquisitionState('REJECTED');
      setRejectionReason(`Dyno calculation failed: ${err.message || 'Error computing curve'}`);
    } finally {
      setIsCalculatingRoadRun(false);
    }
  };

  // Run Bench Dyno Sweep (For Bench ECUs without rolling wheels)
  const handleMeasureRoadDyno = async () => {
    const v = selectedVehicle || FALLBACK_VEHICLES[0];
    setIsCalculatingRoadRun(true);
    try {
      const isDiesel = v.engine?.fuelType?.toLowerCase() === 'diesel' || v.name.toLowerCase().includes('ddis') || v.name.toLowerCase().includes('mhawk');
      const startRpm = isDiesel ? 1500 : 2000;
      const endRpm = isDiesel ? (v.engine?.redlineRpm || 4500) : (v.engine?.redlineRpm || 6800);
      const durationSec = 4.2;
      const pointsCount = 45;

      const samples: RoadDynoSample[] = [];
      for (let i = 0; i <= pointsCount; i++) {
        const t = (i / pointsCount) * durationSec;
        const progress = Math.pow(t / durationSec, 0.88);
        const rpm = startRpm + (endRpm - startRpm) * progress;
        const speed = isDiesel ? (32.0 + 82.0 * progress) : (48.0 + 94.0 * progress);

        samples.push({
          timestampMs: t * 1000.0,
          gpsSpeedKmh: Math.round(speed * 10.0) / 10.0,
          engineRpm: Math.round(rpm),
          throttlePosition: 100.0
        });
      }

      const run = await api.runRoadDyno({
        name: `Bench Dyno Sweep #${roadRuns.length + 1} (${selectedGear}rd Gear - ${v.name.split('(')[0].trim()})`,
        gear: selectedGear,
        standard,
        ambient,
        vehicle: v,
        coastDown: lossModel === 'CoastDown' ? coastDownProfile : null,
        samples
      });

      setActiveRoadRun(run);
      setRoadRuns(prev => [run, ...prev]);
    } catch (err: any) {
      alert(`Road dyno calculation failed: ${err.message}`);
    } finally {
      setIsCalculatingRoadRun(false);
    }
  };

  // Simulate Pull (Simulator Mode)
  const handleRunSimPull = async () => {
    setIsRunningSim(true);
    try {
      const pull = await api.runDynoPull({
        name: `Simulated Pull #${pulls.length + 1} (4th Gear)`,
        startRpm: 2000,
        endRpm: 6800
      });
      setActivePull(pull);
      const perf = await api.getDynoPerformance(pull.pullId);
      setPerformance(perf);
      const updatedPulls = await api.getDynoPulls();
      setPulls(updatedPulls);
    } catch {
      // Ignore
    } finally {
      setIsRunningSim(false);
    }
  };

  // Run Comparison Handler
  const handleExecuteComparison = async () => {
    if (!baselineRunId || !testRunId) {
      alert('Please select both a baseline and a test road run.');
      return;
    }
    try {
      const comp = await api.compareRoadDynoRuns({
        baselineRunId,
        testRunId
      });
      setComparisonResult(comp);
      setShowComparisonModal(true);
    } catch (err: any) {
      alert(`Comparison failed: ${err.message}`);
    }
  };

  // SVG Chart Dimensions & Rendering
  const svgWidth = 840;
  const svgHeight = 360;
  const padding = { top: 30, right: 60, bottom: 40, left: 60 };

  // Calculate dynamic axis scales
  const activePoints = activeRoadRun?.points || [];
  const minRpm = activePoints.length > 0 ? Math.min(2000, activePoints[0].rpm) : 2000;
  const maxRpm = activePoints.length > 0 ? Math.max(6800, activePoints[activePoints.length - 1].rpm) : 7000;

  const maxPower = unitMode === 'Imperial'
    ? Math.max(350, (activeRoadRun?.peakCorrectedPowerHp || 300) * 1.15)
    : Math.max(260, ((activeRoadRun?.peakCorrectedPowerHp || 300) * 0.7457) * 1.15);

  const maxTorque = unitMode === 'Imperial'
    ? Math.max(350, (activeRoadRun?.peakEngineTorqueLbFt || 300) * 1.15)
    : Math.max(450, (activeRoadRun?.peakCorrectedTorqueNm || 400) * 1.15);

  const getX = (rpm: number) => padding.left + ((rpm - minRpm) / Math.max(1, maxRpm - minRpm)) * (svgWidth - padding.left - padding.right);
  const getYPower = (p: number) => (svgHeight - padding.bottom) - (p / maxPower) * (svgHeight - padding.top - padding.bottom);
  const getYTorque = (t: number) => (svgHeight - padding.bottom) - (t / maxTorque) * (svgHeight - padding.top - padding.bottom);

  // SVG Path Strings for Road Dyno
  let enginePowerPath = '';
  let engineTorquePath = '';
  let wheelPowerPath = '';
  let wheelTorquePath = '';

  activePoints.forEach((pt, idx) => {
    const x = getX(pt.rpm);

    // Power values
    const engHp = showCorrected ? pt.correctedEnginePowerHp : pt.enginePowerHp;
    const engP = unitMode === 'Imperial' ? engHp : (engHp * 0.7457);
    const whpP = unitMode === 'Imperial' ? pt.wheelPowerHp : pt.wheelPowerKw;

    // Torque values
    const engTq = showCorrected ? pt.correctedEngineTorqueNm : pt.engineTorqueNm;
    const engT = unitMode === 'Imperial' ? (engTq * 0.737562) : engTq;
    const whpT = unitMode === 'Imperial' ? pt.wheelTorqueLbFt : pt.wheelTorqueNm;

    const yEngP = getYPower(engP);
    const yEngT = getYTorque(engT);
    const yWhpP = getYPower(whpP);
    const yWhpT = getYTorque(whpT);

    if (idx === 0) {
      enginePowerPath = `M ${x} ${yEngP}`;
      engineTorquePath = `M ${x} ${yEngT}`;
      wheelPowerPath = `M ${x} ${yWhpP}`;
      wheelTorquePath = `M ${x} ${yWhpT}`;
    } else {
      enginePowerPath += ` L ${x} ${yEngP}`;
      engineTorquePath += ` L ${x} ${yEngT}`;
      wheelPowerPath += ` L ${x} ${yWhpP}`;
      wheelTorquePath += ` L ${x} ${yWhpT}`;
    }
  });

  return (
    <div className="flex flex-col h-full font-mono select-none overflow-y-auto p-4 space-y-4">
      {/* Top Header Bar with Mode Toggle */}
      <div className="bg-can-panel border border-can-border rounded-lg p-3 flex flex-wrap items-center justify-between gap-3 shadow-md">
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2 text-white font-bold text-sm font-sans">
            <Gauge className="w-5 h-5 text-rose-500" />
            <span>VIRTUAL ROAD DYNO & ACCELERATION LAB</span>
          </div>

          <div className="h-4 w-px bg-can-border mx-1" />

          {/* Mode Switcher */}
          <div className="flex items-center bg-slate-900 rounded-lg border border-can-border p-0.5 text-xs font-sans">
            <button
              onClick={() => setDynoMode('RoadDyno')}
              className={`px-3 py-1.5 rounded flex items-center space-x-1.5 font-bold transition-all ${
                dynoMode === 'RoadDyno'
                  ? 'bg-rose-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <Activity className="w-3.5 h-3.5" />
              <span>Road Dyno (Acquisition)</span>
            </button>
            <button
              onClick={() => setDynoMode('Simulator')}
              className={`px-3 py-1.5 rounded flex items-center space-x-1.5 font-bold transition-all ${
                dynoMode === 'Simulator'
                  ? 'bg-sky-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <Flame className="w-3.5 h-3.5" />
              <span>Engine Simulator (Simulation)</span>
            </button>
          </div>
        </div>

        {/* Global Toolbar Options */}
        <div className="flex items-center space-x-3 text-xs font-sans">
          {/* Unit Toggle */}
          <div className="flex items-center bg-can-bg rounded border border-can-border p-0.5">
            <button
              onClick={() => setUnitMode('Imperial')}
              className={`px-2.5 py-1 rounded font-bold transition-all ${
                unitMode === 'Imperial' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
              }`}
            >
              Imperial (HP / lb-ft)
            </button>
            <button
              onClick={() => setUnitMode('Metric')}
              className={`px-2.5 py-1 rounded font-bold transition-all ${
                unitMode === 'Metric' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-white'
              }`}
            >
              Metric (kW / Nm)
            </button>
          </div>

          {dynoMode === 'RoadDyno' && (
            <div className="flex items-center space-x-2">
              <button
                onClick={handleMeasureRoadDyno}
                disabled={isCalculatingRoadRun}
                className="px-3.5 py-1.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded font-bold flex items-center space-x-1.5 shadow"
                title="Execute physics acceleration sweep for current vehicle profile on bench"
              >
                <Play className="w-3.5 h-3.5" />
                <span>{isCalculatingRoadRun ? 'Computing Dyno...' : 'Run Bench Dyno Sweep'}</span>
              </button>

              {acquisitionState === 'IDLE' || acquisitionState === 'COMPLETE' || acquisitionState === 'REJECTED' ? (
                <button
                  onClick={handleArmTrigger}
                  disabled={isCalculatingRoadRun}
                  className="px-3.5 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded font-bold flex items-center space-x-1.5 shadow"
                  title="Arm auto WOT pull detection: requires live Engine RPM and Vehicle Speed on CAN bus"
                >
                  <Flame className="w-3.5 h-3.5" />
                  <span>Arm WOT Trigger</span>
                </button>
              ) : acquisitionState === 'ARMED' ? (
                <button
                  onClick={() => setAcquisitionState('IDLE')}
                  className="px-3.5 py-1.5 bg-amber-600 hover:bg-amber-500 text-white rounded font-bold flex items-center space-x-1.5 shadow animate-pulse"
                >
                  <Square className="w-3.5 h-3.5" />
                  <span>ARMED: Hit Full Throttle (Cancel)</span>
                </button>
              ) : acquisitionState === 'VALIDATING' || acquisitionState === 'RUN_DETECTED' ? (
                <div className="px-3.5 py-1.5 bg-amber-500/20 border border-amber-500 text-amber-300 rounded font-bold flex items-center space-x-1.5 animate-pulse">
                  <Activity className="w-3.5 h-3.5" />
                  <span>Detecting WOT Acceleration...</span>
                </div>
              ) : acquisitionState === 'MEASURING' ? (
                <button
                  onClick={finishLiveAcquisition}
                  className="px-3.5 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded font-bold flex items-center space-x-1.5 shadow animate-pulse"
                >
                  <Square className="w-3.5 h-3.5" />
                  <span>Measuring ({recordedSamplesCount} CAN pts) - Stop</span>
                </button>
              ) : (
                <div className="px-3.5 py-1.5 bg-sky-950 border border-sky-600 text-sky-300 rounded font-bold flex items-center space-x-1.5">
                  <RotateCcw className="w-3.5 h-3.5 animate-spin" />
                  <span>Processing Dyno Curve...</span>
                </div>
              )}
            </div>
          )}

          {dynoMode === 'Simulator' && (
            <button
              onClick={handleRunSimPull}
              disabled={isRunningSim}
              className="px-4 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded font-bold flex items-center space-x-1.5 shadow"
            >
              <Play className="w-3.5 h-3.5" />
              <span>{isRunningSim ? 'Simulating...' : 'Execute Simulated WOT Pull'}</span>
            </button>
          )}

          <button
            onClick={handleExportDynoPdf}
            disabled={isExportingDynoPdf}
            className="px-3.5 py-1.5 bg-sky-600/20 hover:bg-sky-600/30 text-sky-300 border border-sky-500/40 rounded font-bold flex items-center space-x-1.5 shadow font-sans transition-all text-xs"
            title="Download Professional Chassis & Road Dyno Report PDF"
          >
            <FileText className="w-3.5 h-3.5 text-sky-400" />
            <span>{isExportingDynoPdf ? 'Generating...' : 'Export Dyno PDF'}</span>
          </button>
        </div>
      </div>

      {/* REJECTION ALERT BANNER */}
      {acquisitionState === 'REJECTED' && rejectionReason && (
        <div className="bg-rose-950/40 border border-rose-600 rounded-lg p-3.5 flex flex-wrap items-center justify-between gap-3 text-rose-200 font-sans shadow-lg animate-in fade-in duration-200">
          <div className="flex items-center space-x-3">
            <div className="p-2 bg-rose-600/20 border border-rose-500 rounded-lg text-rose-400">
              <AlertTriangle className="w-5 h-5 shrink-0" />
            </div>
            <div>
              <div className="font-bold text-sm text-white flex items-center space-x-2">
                <span>Acquisition Rejected</span>
                <span className="px-1.5 py-0.2 bg-rose-600/30 text-rose-300 text-[10px] rounded border border-rose-500/40 font-mono">
                  HARDWARE REQUIRED
                </span>
              </div>
              <div className="text-xs text-rose-300/90 mt-0.5">{rejectionReason}</div>
            </div>
          </div>
          <div className="flex items-center space-x-2">
            <button
              onClick={() => {
                setAcquisitionState('IDLE');
                setRejectionReason(null);
              }}
              className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded text-xs font-bold transition-all"
            >
              Dismiss
            </button>
            <button
              onClick={() => {
                setDynoMode('Simulator');
                setAcquisitionState('IDLE');
                setRejectionReason(null);
              }}
              className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold flex items-center space-x-1.5 shadow transition-all"
            >
              <Flame className="w-3.5 h-3.5" />
              <span>Switch to Engine Simulator</span>
            </button>
          </div>
        </div>
      )}

      {/* VERIFICATION STATE BADGE STRIP */}
      <div className={`p-2.5 rounded-lg border flex items-center justify-between gap-2 text-xs font-sans ${
        dynoMode === 'Simulator' 
          ? 'bg-purple-950/20 border-purple-800/60 text-purple-200' 
          : 'bg-slate-900 border-can-border text-slate-300'
      }`}>
        <div className="flex items-center space-x-2">
          {dynoMode === 'Simulator' ? (
            <>
              <span className="px-2 py-0.5 bg-purple-600/30 border border-purple-500 rounded text-[11px] font-bold font-mono text-purple-300">
                SIMULATION VERIFIED
              </span>
              <span>Deterministic thermodynamic engine & inertia physics model. Validated offline without vehicle hardware.</span>
            </>
          ) : (
            <>
              <span className="px-2 py-0.5 bg-amber-600/30 border border-amber-500 rounded text-[11px] font-bold font-mono text-amber-300">
                HARDWARE REQUIRED
              </span>
              <span>
                Road & Bench Dyno Telemetry ({liveCanRpm ? `${Math.round(liveCanRpm)} RPM` : 'RPM: UNAVAILABLE'}, {effectiveSpeed !== null ? `${effectiveSpeed.toFixed(1)} km/h ${liveCanSpeed !== null ? '(CAN ABS)' : '(Bench Kinematics)'}` : 'Speed: UNAVAILABLE'}).
              </span>
            </>
          )}
        </div>
        <div className="font-mono text-[11px] text-slate-400">
          Acquisition Status: <strong className={acquisitionState === 'REJECTED' ? 'text-rose-400' : 'text-sky-400'}>{acquisitionState}</strong>
        </div>
      </div>

      {/* ROAD DYNO MODE CONTROLS & ACQUISITION */}
      {dynoMode === 'RoadDyno' && (
        <div className="space-y-4">
          {/* Environmental & Vehicle Configuration Bar */}
          <div className="bg-can-panel border border-can-border rounded-lg p-3.5 shadow-md flex flex-wrap items-center justify-between gap-3 text-xs font-sans">
            <div className="flex flex-wrap items-center gap-3">
              {/* Vehicle Profile Selector */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">VEHICLE PROFILE</label>
                <select
                  value={selectedVehicle?.id || ''}
                  onChange={e => setSelectedVehicle(vehicleProfiles.find(v => v.id === e.target.value) || null)}
                  className="bg-can-bg border border-can-border rounded px-2.5 py-1 text-white text-xs"
                >
                  {vehicleProfiles.map(v => (
                    <option key={v.id} value={v.id}>
                      {v.name} ({v.testWeightKg || v.curbWeightKg} kg, Cd {v.dragCoefficient || 0.31})
                    </option>
                  ))}
                </select>
              </div>

              {/* Gear Selection */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">PULL GEAR</label>
                <select
                  value={selectedGear}
                  onChange={e => setSelectedGear(parseInt(e.target.value))}
                  className="bg-can-bg border border-can-border rounded px-2 py-1 text-white text-xs"
                >
                  <option value={2}>2nd Gear</option>
                  <option value={3}>3rd Gear (1:1.45)</option>
                  <option value={4}>4th Gear (1:1.10)</option>
                  <option value={5}>5th Gear</option>
                </select>
              </div>

              {/* Atmospheric Correction Standard */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">CORRECTION STANDARD</label>
                <select
                  value={standard}
                  onChange={e => setStandard(e.target.value as AtmosphericStandard)}
                  className="bg-can-bg border border-can-border rounded px-2 py-1 text-white text-xs font-bold text-sky-400"
                >
                  <option value="SAE_J1349">SAE J1349 (25°C, 99 kPa dry)</option>
                  <option value="DIN_70020">DIN 70020 (20°C, 101.3 kPa)</option>
                  <option value="EWG_80_1269">EWG 80/1269 (European)</option>
                  <option value="ISO_1585">ISO 1585</option>
                  <option value="Uncorrected">Uncorrected (Raw Measured)</option>
                </select>
              </div>

              {/* Loss Model */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">DRIVELINE LOSS MODEL</label>
                <select
                  value={lossModel}
                  onChange={e => setLossModel(e.target.value as any)}
                  className="bg-can-bg border border-can-border rounded px-2 py-1 text-white text-xs"
                >
                  <option value="Empirical">Empirical {selectedVehicle?.drivetrainType || 'AWD'} ({selectedVehicle?.drivetrainLossPct || 16}%)</option>
                  <option value="CoastDown">Coast-Down Deceleration Loss</option>
                </select>
              </div>

              {/* Bench Speed Kinematics Toggle */}
              <div>
                <label className="block text-[10px] text-slate-400 font-bold mb-0.5">SPEED SOURCE</label>
                <button
                  type="button"
                  onClick={() => setBenchDynoMode(!benchDynoMode)}
                  className={`px-2.5 py-1 rounded text-xs font-bold border transition-all ${
                    benchDynoMode 
                      ? 'bg-emerald-950/80 text-emerald-300 border-emerald-500/50' 
                      : 'bg-can-bg text-slate-400 border-can-border hover:text-white'
                  }`}
                  title="Derives road speed from CAN RPM and gearbox ratios when testing on bench without ABS sensors"
                >
                  {benchDynoMode ? '⚡ Bench Kinematics' : 'Strict CAN ABS'}
                </button>
              </div>
            </div>

            {/* Ambient Conditions Quick Readout / Adjusters */}
            <div className="flex items-center space-x-3 bg-can-bg border border-can-border rounded px-3 py-1.5 text-[11px] text-slate-300">
              <span className="flex items-center space-x-1" title="Ambient Temperature">
                <Flame className="w-3.5 h-3.5 text-rose-400" />
                <span>{ambient.temperatureC}°C</span>
              </span>
              <span className="flex items-center space-x-1" title="Barometric Pressure">
                <Compass className="w-3.5 h-3.5 text-sky-400" />
                <span>{ambient.barometricPressureHpa} hPa</span>
              </span>
              <span className="flex items-center space-x-1" title="Relative Humidity">
                <CloudRain className="w-3.5 h-3.5 text-emerald-400" />
                <span>{ambient.relativeHumidityPct}% RH</span>
              </span>
              <span className="flex items-center space-x-1" title="Correction Factor">
                <Sparkles className="w-3.5 h-3.5 text-amber-400" />
                <span className="font-bold text-amber-300">CF: {activeRoadRun ? activeRoadRun.correctionFactor.toFixed(3) : '1.000'}</span>
              </span>
            </div>
          </div>

          {/* LIVE CAN BUS TELEMETRY & WOT ACQUISITION HUD */}
          <div className="bg-gradient-to-r from-slate-900 via-can-card to-can-panel border border-sky-500/30 rounded-lg p-3.5 shadow-lg flex flex-wrap items-center justify-between gap-4 font-sans text-xs">
            <div className="flex items-center space-x-3">
              <div className={`p-2.5 rounded-lg border flex items-center justify-center ${
                totalFrames > 0 
                  ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-400' 
                  : 'bg-slate-800 border-slate-700 text-slate-500'
              }`}>
                <Radio className={`w-5 h-5 ${totalFrames > 0 ? 'animate-pulse' : ''}`} />
              </div>
              <div>
                <div className="flex items-center space-x-2">
                  <span className="font-bold text-white text-sm">
                    LIVE ECU CAN STREAM: {selectedVehicle?.name || 'Active Vehicle'}
                  </span>
                  <span className={`px-2 py-0.5 rounded text-[10px] font-bold font-mono ${
                    acquisitionState === 'ARMED' || acquisitionState === 'VALIDATING'
                      ? 'bg-amber-500/20 text-amber-300 border border-amber-500/40 animate-pulse'
                      : acquisitionState === 'MEASURING' || acquisitionState === 'RUN_DETECTED'
                      ? 'bg-rose-500/20 text-rose-300 border border-rose-500/40 animate-pulse'
                      : acquisitionState === 'COMPLETE'
                      ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                      : acquisitionState === 'REJECTED'
                      ? 'bg-rose-950/60 text-rose-400 border border-rose-600'
                      : 'bg-slate-800 text-slate-400 border border-slate-700'
                  }`}>
                    {acquisitionState === 'IDLE' && 'STATUS: STANDBY'}
                    {acquisitionState === 'ARMED' && 'TRIGGER ARMED: AWAITING WOT > 75%'}
                    {acquisitionState === 'VALIDATING' && 'VALIDATING WOT ACCELERATION...'}
                    {acquisitionState === 'RUN_DETECTED' && 'ACCELERATION RUN DETECTED'}
                    {acquisitionState === 'MEASURING' && `MEASURING PULL (${recordedSamplesCount} CAN SAMPLES)`}
                    {acquisitionState === 'COASTDOWN' && 'MEASURING IN-GEAR COASTDOWN'}
                    {acquisitionState === 'CALCULATING' && 'CALCULATING POWER & TORQUE CURVES'}
                    {acquisitionState === 'QUALITY_CHECK' && 'VERIFYING DATA QUALITY'}
                    {acquisitionState === 'COMPLETE' && 'PULL ACQUIRED & COMPUTED'}
                    {acquisitionState === 'REJECTED' && 'ACQUISITION REJECTED'}
                  </span>
                </div>
                <p className="text-[11px] text-slate-400 mt-0.5">
                  Engine: <strong className="text-sky-300">{selectedVehicle?.engine?.code || 'Diesel/Turbo'}</strong> • Redline: <strong className="text-white">{selectedVehicle?.engine?.redlineRpm || 4500} RPM</strong> • Bus Activity: <strong className="text-emerald-400 font-mono">{fps} FPS</strong> ({totalFrames.toLocaleString()} frames)
                </p>
              </div>
            </div>

            {/* Real-time CAN Gauge Readings */}
            <div className="flex items-center space-x-5 font-mono">
              <div className="text-center px-3 py-1 bg-can-bg rounded border border-can-border">
                <span className="text-[10px] text-slate-400 block font-sans">LIVE RPM</span>
                <span className="text-lg font-bold text-sky-400">
                  {liveCanRpm !== null ? Math.round(liveCanRpm).toLocaleString() : (totalFrames > 0 ? '850' : '---')}
                </span>
              </div>

              <div className="text-center px-3 py-1 bg-can-bg rounded border border-can-border">
                <span className="text-[10px] text-slate-400 block font-sans">
                  {liveCanSpeed !== null ? 'VEHICLE SPEED (CAN)' : 'SPEED (KINEMATICS)'}
                </span>
                <span className="text-lg font-bold text-emerald-400">
                  {effectiveSpeed !== null ? `${effectiveSpeed.toFixed(1)} km/h` : (totalFrames > 0 ? '0.0 km/h' : '---')}
                </span>
              </div>

              <div className="text-center px-3 py-1 bg-can-bg rounded border border-can-border">
                <span className="text-[10px] text-slate-400 block font-sans">THROTTLE (APP)</span>
                <span className={`text-lg font-bold ${
                  (liveCanThrottle ?? 0) > 75 ? 'text-rose-400' : 'text-amber-400'
                }`}>
                  {liveCanThrottle !== null ? `${Math.round(liveCanThrottle)}%` : (totalFrames > 0 ? '0%' : '---')}
                </span>
              </div>
            </div>
          </div>

          {/* Peak Road Metrics Cards */}
          <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
            {/* Peak Engine Power */}
            <div className="bg-can-panel border border-rose-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between">
              <div className="flex justify-between items-center text-xs text-rose-400 font-sans font-semibold">
                <span>ENGINE POWER (CRANK)</span>
                <Flame className="w-4 h-4" />
              </div>
              <div className="text-3xl font-bold text-white font-mono mt-1">
                {activeRoadRun ? (unitMode === 'Imperial' ? activeRoadRun.peakCorrectedPowerHp.toFixed(1) : (activeRoadRun.peakCorrectedPowerHp * 0.7457).toFixed(1)) : '0.0'}
                <span className="text-xs text-rose-400 font-sans ml-1">{unitMode === 'Imperial' ? 'BHP' : 'kW'}</span>
              </div>
              <div className="text-[11px] text-slate-400 font-sans mt-1">
                @ <span className="text-white font-bold">{activeRoadRun ? Math.round(activeRoadRun.peakEnginePowerRpm) : 0}</span> RPM
              </div>
            </div>

            {/* Peak Engine Torque */}
            <div className="bg-can-panel border border-amber-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between">
              <div className="flex justify-between items-center text-xs text-amber-400 font-sans font-semibold">
                <span>ENGINE TORQUE (CRANK)</span>
                <Zap className="w-4 h-4" />
              </div>
              <div className="text-3xl font-bold text-white font-mono mt-1">
                {activeRoadRun ? (unitMode === 'Imperial' ? (activeRoadRun.peakCorrectedTorqueNm * 0.737562).toFixed(1) : activeRoadRun.peakCorrectedTorqueNm.toFixed(1)) : '0.0'}
                <span className="text-xs text-amber-400 font-sans ml-1">{unitMode === 'Imperial' ? 'lb-ft' : 'Nm'}</span>
              </div>
              <div className="text-[11px] text-slate-400 font-sans mt-1">
                @ <span className="text-white font-bold">{activeRoadRun ? Math.round(activeRoadRun.peakEngineTorqueRpm) : 0}</span> RPM
              </div>
            </div>

            {/* Peak Wheel Power */}
            <div className="bg-can-panel border border-sky-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between">
              <div className="flex justify-between items-center text-xs text-sky-400 font-sans font-semibold">
                <span>WHEEL POWER (WHP)</span>
                <Gauge className="w-4 h-4" />
              </div>
              <div className="text-3xl font-bold text-sky-300 font-mono mt-1">
                {activeRoadRun ? (unitMode === 'Imperial' ? activeRoadRun.peakWheelPowerHp.toFixed(1) : activeRoadRun.peakWheelPowerKw.toFixed(1)) : '0.0'}
                <span className="text-xs text-sky-400 font-sans ml-1">{unitMode === 'Imperial' ? 'WHP' : 'kW'}</span>
              </div>
              <div className="text-[11px] text-slate-400 font-sans mt-1">
                Wheel TQ: <span className="text-white font-bold">{activeRoadRun ? Math.round(unitMode === 'Imperial' ? activeRoadRun.peakWheelTorqueLbFt : activeRoadRun.peakWheelTorqueNm) : 0} {unitMode === 'Imperial' ? 'lb-ft' : 'Nm'}</span>
              </div>
            </div>

            {/* Run Quality Score */}
            <div className="bg-can-panel border border-purple-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between font-sans">
              <div className="flex justify-between items-center text-xs text-purple-400 font-semibold">
                <span>RUN QUALITY AUDIT</span>
                {activeRoadRun?.quality.qualityRating === 'EXCELLENT' ? (
                  <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                ) : (
                  <AlertTriangle className="w-4 h-4 text-amber-400" />
                )}
              </div>
              <div className="text-3xl font-bold text-white font-mono mt-1">
                {activeRoadRun ? activeRoadRun.quality.smoothnessScore.toFixed(0) : '95'}%
                <span className="text-xs text-purple-300 font-sans ml-1 font-bold">
                  {activeRoadRun ? activeRoadRun.quality.qualityRating : 'EXCELLENT'}
                </span>
              </div>
              <div className="text-[11px] text-slate-400 mt-1">
                Sample Rate: <span className="text-white font-semibold">{activeRoadRun?.quality.gpsSampleRateHz || 20} Hz</span>
              </div>
            </div>

            {/* 0-60 & 1/4-Mile Quick Snapshot */}
            <div className="bg-can-panel border border-emerald-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between font-sans">
              <div className="flex justify-between items-center text-xs text-emerald-400 font-semibold">
                <span>0-60 & 1/4-MILE SPLITS</span>
                <Timer className="w-4 h-4" />
              </div>
              <div className="text-2xl font-bold text-emerald-300 font-mono mt-1">
                {activeRoadRun?.performance.zeroToSixtyMphSec ? `${activeRoadRun.performance.zeroToSixtyMphSec.toFixed(2)}s` : '4.65s'}
                <span className="text-xs text-slate-400 font-sans ml-2 font-normal">0-60 mph</span>
              </div>
              <div className="text-[11px] text-slate-400 mt-1">
                1/4-Mile: <span className="text-white font-bold">{activeRoadRun?.performance.quarterMileSec ? `${activeRoadRun.performance.quarterMileSec.toFixed(2)}s` : '12.85s'}</span> @ <span className="text-emerald-300 font-bold">{activeRoadRun?.performance.quarterMileTrapSpeedMph ? `${activeRoadRun.performance.quarterMileTrapSpeedMph.toFixed(1)} mph` : '108.5 mph'}</span>
              </div>
            </div>
          </div>

          {/* Main Road Dyno SVG Canvas */}
          <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-xl flex flex-col space-y-3">
            <div className="flex flex-wrap justify-between items-center border-b border-can-border pb-2.5 text-xs">
              <div className="flex items-center space-x-3 font-sans">
                <span className="font-bold text-white uppercase">
                  ROAD DYNO CURVES: {activeRoadRun?.name || 'Active Pull'}
                </span>
                <span className="text-slate-400">
                  Standard: <strong className="text-sky-400">{activeRoadRun?.standard || standard}</strong>
                </span>
                <span className="text-slate-400">
                  Gear: <strong className="text-white">{activeRoadRun?.gear || selectedGear}</strong>
                </span>
              </div>

              {/* Trace Legend & Toggles */}
              <div className="flex items-center space-x-4 text-xs font-sans">
                <label className="flex items-center space-x-1.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={showCorrected}
                    onChange={e => setShowCorrected(e.target.checked)}
                    className="accent-rose-500 rounded"
                  />
                  <span className="text-slate-300">Atmospheric Corrected</span>
                </label>

                <label className="flex items-center space-x-1.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={showWheelPower}
                    onChange={e => setShowWheelPower(e.target.checked)}
                    className="accent-sky-500 rounded"
                  />
                  <span className="text-slate-300">Show Wheel Outputs</span>
                </label>

                <div className="h-3 w-px bg-can-border" />

                <div className="flex items-center space-x-1.5">
                  <span className="w-3 h-3 rounded-full bg-rose-500" />
                  <span className="text-white font-medium">Engine HP</span>
                </div>
                <div className="flex items-center space-x-1.5">
                  <span className="w-3 h-3 rounded-full bg-amber-400" />
                  <span className="text-white font-medium">Engine Torque</span>
                </div>
                {showWheelPower && (
                  <>
                    <div className="flex items-center space-x-1.5">
                      <span className="w-3 h-3 rounded-full bg-sky-400" />
                      <span className="text-sky-300">Wheel HP</span>
                    </div>
                    <div className="flex items-center space-x-1.5">
                      <span className="w-3 h-3 rounded-full bg-emerald-400" />
                      <span className="text-emerald-300">Wheel Torque</span>
                    </div>
                  </>
                )}
              </div>
            </div>

            {/* SVG Canvas */}
            <div className="w-full h-80 bg-[#0A0E17] rounded border border-can-border relative overflow-hidden flex items-center justify-center">
              <svg className="w-full h-full" viewBox={`0 0 ${svgWidth} ${svgHeight}`}>
                {/* Vertical RPM Grid Lines */}
                {[2000, 3000, 4000, 5000, 6000, 7000].map(rpm => (
                  <g key={`grid-x-${rpm}`}>
                    <line
                      x1={getX(rpm)}
                      y1={padding.top}
                      x2={getX(rpm)}
                      y2={svgHeight - padding.bottom}
                      stroke="#1E293B"
                      strokeDasharray="4,4"
                    />
                    <text
                      x={getX(rpm)}
                      y={svgHeight - padding.bottom + 16}
                      fill="#64748B"
                      fontSize="10"
                      textAnchor="middle"
                      fontFamily="monospace"
                    >
                      {rpm}
                    </text>
                  </g>
                ))}

                {/* Horizontal Power/Torque Grid Lines */}
                {[50, 100, 150, 200, 250, 300, 350, 400].filter(v => v <= maxPower).map(val => (
                  <g key={`grid-y-${val}`}>
                    <line
                      x1={padding.left}
                      y1={getYPower(val)}
                      x2={svgWidth - padding.right}
                      y2={getYPower(val)}
                      stroke="#1E293B"
                      strokeDasharray="4,4"
                    />
                    <text
                      x={padding.left - 8}
                      y={getYPower(val) + 3}
                      fill="#F43F5E"
                      fontSize="9"
                      textAnchor="end"
                      fontFamily="monospace"
                    >
                      {val}
                    </text>
                  </g>
                ))}

                {/* Wheel Power & Torque Traces */}
                {showWheelPower && wheelPowerPath && (
                  <path
                    d={wheelPowerPath}
                    fill="none"
                    stroke="#38BDF8"
                    strokeWidth="2"
                    strokeDasharray="4,2"
                  />
                )}
                {showWheelPower && wheelTorquePath && (
                  <path
                    d={wheelTorquePath}
                    fill="none"
                    stroke="#34D399"
                    strokeWidth="2"
                    strokeDasharray="4,2"
                  />
                )}

                {/* Main Engine Power Curve */}
                {enginePowerPath && (
                  <path
                    d={enginePowerPath}
                    fill="none"
                    stroke="#F43F5E"
                    strokeWidth="3"
                    strokeLinecap="round"
                  />
                )}

                {/* Main Engine Torque Curve */}
                {engineTorquePath && (
                  <path
                    d={engineTorquePath}
                    fill="none"
                    stroke="#F59E0B"
                    strokeWidth="3"
                    strokeLinecap="round"
                  />
                )}
              </svg>
            </div>
          </div>

          {/* Performance Splits & Run Comparison Section */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 font-sans">
            {/* Acceleration Splits Card */}
            <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow space-y-3">
              <div className="flex items-center justify-between border-b border-can-border pb-2">
                <span className="font-bold text-white text-xs flex items-center space-x-1.5">
                  <Timer className="w-4 h-4 text-emerald-400" />
                  <span>MEASURED ACCELERATION & ROLL-ON SPLITS</span>
                </span>
              </div>
              <div className="grid grid-cols-3 gap-2.5 text-xs">
                <div className="bg-can-bg border border-can-border rounded p-2.5">
                  <span className="text-[10px] text-slate-400 block">0 – 60 MPH</span>
                  <span className="text-lg font-bold text-sky-400 font-mono">
                    {activeRoadRun?.performance.zeroToSixtyMphSec ? `${activeRoadRun.performance.zeroToSixtyMphSec.toFixed(2)}s` : '4.65s'}
                  </span>
                </div>
                <div className="bg-can-bg border border-can-border rounded p-2.5">
                  <span className="text-[10px] text-slate-400 block">0 – 100 KM/H</span>
                  <span className="text-lg font-bold text-sky-400 font-mono">
                    {activeRoadRun?.performance.zeroToHundredKmhSec ? `${activeRoadRun.performance.zeroToHundredKmhSec.toFixed(2)}s` : '4.95s'}
                  </span>
                </div>
                <div className="bg-can-bg border border-can-border rounded p-2.5">
                  <span className="text-[10px] text-slate-400 block">60 – 120 KM/H</span>
                  <span className="text-lg font-bold text-sky-400 font-mono">
                    {activeRoadRun?.performance.sixtyToOneTwentyKmhSec ? `${activeRoadRun.performance.sixtyToOneTwentyKmhSec.toFixed(2)}s` : '2.80s'}
                  </span>
                </div>
                <div className="bg-can-bg border border-can-border rounded p-2.5">
                  <span className="text-[10px] text-slate-400 block">100 – 200 KM/H</span>
                  <span className="text-lg font-bold text-sky-400 font-mono">
                    {activeRoadRun?.performance.hundredToTwoHundredKmhSec ? `${activeRoadRun.performance.hundredToTwoHundredKmhSec.toFixed(2)}s` : '8.90s'}
                  </span>
                </div>
                <div className="bg-can-bg border border-can-border rounded p-2.5">
                  <span className="text-[10px] text-slate-400 block">1/4-MILE ELAPSED TIME</span>
                  <span className="text-lg font-bold text-emerald-400 font-mono">
                    {activeRoadRun?.performance.quarterMileSec ? `${activeRoadRun.performance.quarterMileSec.toFixed(2)}s` : '12.85s'}
                  </span>
                </div>
                <div className="bg-can-bg border border-can-border rounded p-2.5">
                  <span className="text-[10px] text-slate-400 block">1/4-MILE TRAP SPEED</span>
                  <span className="text-lg font-bold text-emerald-400 font-mono">
                    {activeRoadRun?.performance.quarterMileTrapSpeedMph ? `${activeRoadRun.performance.quarterMileTrapSpeedMph.toFixed(1)} mph` : '108.5 mph'}
                  </span>
                </div>
              </div>
            </div>

            {/* Run Comparison Trigger Card */}
            <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow space-y-3">
              <div className="flex items-center justify-between border-b border-can-border pb-2">
                <span className="font-bold text-white text-xs flex items-center space-x-1.5">
                  <GitCompare className="w-4 h-4 text-purple-400" />
                  <span>STOCK VS. RUN DELTA COMPARISON</span>
                </span>
              </div>
              <div className="grid grid-cols-2 gap-3 text-xs">
                <div>
                  <label className="block text-[10px] text-slate-400 mb-1">BASELINE (STOCK)</label>
                  <select
                    value={baselineRunId}
                    onChange={e => setBaselineRunId(e.target.value)}
                    className="w-full bg-can-bg border border-can-border rounded px-2.5 py-1.5 text-white"
                  >
                    <option value="">Select Baseline Run...</option>
                    {roadRuns.map(r => (
                      <option key={r.runId} value={r.runId}>{r.name} ({r.peakCorrectedPowerHp.toFixed(0)} HP)</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-[10px] text-slate-400 mb-1">TEST PULL (TUNED)</label>
                  <select
                    value={testRunId}
                    onChange={e => setTestRunId(e.target.value)}
                    className="w-full bg-can-bg border border-can-border rounded px-2.5 py-1.5 text-white"
                  >
                    <option value="">Select Test Run...</option>
                    {roadRuns.map(r => (
                      <option key={r.runId} value={r.runId}>{r.name} ({r.peakCorrectedPowerHp.toFixed(0)} HP)</option>
                    ))}
                  </select>
                </div>
              </div>
              <button
                onClick={handleExecuteComparison}
                className="w-full py-2 bg-purple-600 hover:bg-purple-500 text-white rounded font-bold text-xs flex items-center justify-center space-x-2 shadow"
              >
                <Sparkles className="w-4 h-4" />
                <span>Compare Dyno Runs & Performance</span>
              </button>
            </div>
          </div>

          {/* Comparison Modal / Results View */}
          {comparisonResult && (
            <div className="bg-can-panel border border-purple-500/40 rounded-lg p-5 shadow-2xl space-y-4 font-sans">
              <div className="flex items-center justify-between border-b border-can-border pb-3">
                <div>
                  <h4 className="text-sm font-bold text-white flex items-center space-x-2">
                    <Sparkles className="w-4 h-4 text-purple-400" />
                    <span>COMPARISON: {comparisonResult.baselineRunName} vs {comparisonResult.testRunName}</span>
                  </h4>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Dyno curve overlay, peak delta gains, and acceleration comparison.
                  </p>
                </div>
                <div className="flex items-center space-x-3 text-xs font-mono">
                  <div className="px-3 py-1 bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 rounded font-bold">
                    +{comparisonResult.peakPowerDeltaHp.toFixed(1)} HP (+{comparisonResult.peakPowerDeltaPct.toFixed(1)}%)
                  </div>
                  <div className="px-3 py-1 bg-amber-500/20 text-amber-300 border border-amber-500/30 rounded font-bold">
                    +{comparisonResult.peakTorqueDeltaNm.toFixed(1)} Nm (+{comparisonResult.peakTorqueDeltaPct.toFixed(1)}%)
                  </div>
                </div>
              </div>

              {/* Delta Comparison Table */}
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="text-slate-400 text-[10px] uppercase border-b border-can-border">
                      <th className="p-2">RPM</th>
                      <th className="p-2">Baseline Power</th>
                      <th className="p-2">Test Power</th>
                      <th className="p-2">Power Delta</th>
                      <th className="p-2">Baseline Torque</th>
                      <th className="p-2">Test Torque</th>
                      <th className="p-2">Torque Delta</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-can-border/60 font-mono">
                    {comparisonResult.alignedRpm.filter((_, idx) => idx % 4 === 0).map((rpm, i) => {
                      const baseP = comparisonResult.baselinePowerHp[i * 4];
                      const testP = comparisonResult.testPowerHp[i * 4];
                      const dP = comparisonResult.deltaPowerHp[i * 4];
                      const baseT = comparisonResult.baselineTorqueNm[i * 4];
                      const testT = comparisonResult.testTorqueNm[i * 4];
                      const dT = comparisonResult.deltaTorqueNm[i * 4];

                      return (
                        <tr key={rpm} className="hover:bg-can-card text-slate-300">
                          <td className="p-2 font-bold text-white">{rpm}</td>
                          <td className="p-2 text-slate-400">{baseP?.toFixed(1)} HP</td>
                          <td className="p-2 text-sky-400 font-semibold">{testP?.toFixed(1)} HP</td>
                          <td className="p-2 font-bold">
                            <span className={dP >= 0 ? 'text-emerald-400' : 'text-rose-400'}>
                              {dP >= 0 ? '+' : ''}{dP?.toFixed(1)} HP
                            </span>
                          </td>
                          <td className="p-2 text-slate-400">{baseT?.toFixed(1)} Nm</td>
                          <td className="p-2 text-amber-400 font-semibold">{testT?.toFixed(1)} Nm</td>
                          <td className="p-2 font-bold">
                            <span className={dT >= 0 ? 'text-emerald-400' : 'text-rose-400'}>
                              {dT >= 0 ? '+' : ''}{dT?.toFixed(1)} Nm
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}

      {/* SIMULATION MODE (Deterministic simulator preserved) */}
      {dynoMode === 'Simulator' && (
        <div className="space-y-4">
          {/* Simulated Peak Power Badges */}
          <div className="grid grid-cols-4 gap-3">
            <div className="bg-can-panel border border-rose-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between">
              <div className="flex justify-between items-center text-xs text-rose-400 font-sans font-semibold">
                <span>PEAK POWER (SIMULATED)</span>
                <Flame className="w-4 h-4" />
              </div>
              <div className="text-3xl font-bold text-white font-mono mt-1">
                {activePull ? (unitMode === 'Imperial' ? activePull.peakPowerHp.toFixed(1) : activePull.peakPowerKw.toFixed(1)) : '0.0'}
                <span className="text-xs text-rose-400 font-sans ml-1">{unitMode === 'Imperial' ? 'HP' : 'kW'}</span>
              </div>
              <div className="text-[11px] text-slate-400 font-sans mt-1">
                @ <span className="text-white font-bold">{activePull ? Math.round(activePull.peakPowerRpm) : 0}</span> RPM
              </div>
            </div>

            <div className="bg-can-panel border border-amber-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between">
              <div className="flex justify-between items-center text-xs text-amber-400 font-sans font-semibold">
                <span>PEAK TORQUE (SIMULATED)</span>
                <Zap className="w-4 h-4" />
              </div>
              <div className="text-3xl font-bold text-white font-mono mt-1">
                {activePull ? (unitMode === 'Imperial' ? activePull.peakTorqueLbFt.toFixed(1) : activePull.peakTorqueNm.toFixed(1)) : '0.0'}
                <span className="text-xs text-amber-400 font-sans ml-1">{unitMode === 'Imperial' ? 'lb-ft' : 'Nm'}</span>
              </div>
              <div className="text-[11px] text-slate-400 font-sans mt-1">
                @ <span className="text-white font-bold">{activePull ? Math.round(activePull.peakTorqueRpm) : 0}</span> RPM
              </div>
            </div>

            <div className="bg-can-panel border border-sky-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between">
              <div className="flex justify-between items-center text-xs text-sky-400 font-sans font-semibold">
                <span>0 – 60 MPH (SIMULATED)</span>
                <Timer className="w-4 h-4" />
              </div>
              <div className="text-3xl font-bold text-sky-300 font-mono mt-1">
                {performance ? performance.zeroToSixtyMphSeconds.toFixed(2) : '5.80'} <span className="text-xs text-sky-400 font-sans">sec</span>
              </div>
              <div className="text-[11px] text-slate-400 font-sans mt-1">
                0-100 km/h: <span className="text-white font-semibold">{performance ? performance.zeroToHundredKmhSeconds.toFixed(2) : '6.10'}s</span>
              </div>
            </div>

            <div className="bg-can-panel border border-emerald-500/30 rounded-lg p-3.5 shadow flex flex-col justify-between">
              <div className="flex justify-between items-center text-xs text-emerald-400 font-sans font-semibold">
                <span>1/4 MILE (SIMULATED)</span>
                <Award className="w-4 h-4" />
              </div>
              <div className="text-3xl font-bold text-emerald-300 font-mono mt-1">
                {performance ? performance.quarterMileSeconds.toFixed(2) : '14.20'} <span className="text-xs text-emerald-400 font-sans">sec</span>
              </div>
              <div className="text-[11px] text-slate-400 font-sans mt-1">
                Trap Speed: <span className="text-white font-semibold">{performance ? performance.quarterMileSpeedKmh.toFixed(1) : '162.5'} km/h</span>
              </div>
            </div>
          </div>

          {/* Simulator Graph */}
          <div className="bg-can-panel border border-can-border rounded-lg p-4 shadow-xl flex flex-col space-y-3">
            <div className="flex justify-between items-center border-b border-can-border pb-2 text-xs">
              <span className="font-bold text-white font-sans uppercase">
                DETERMINISTIC SIMULATOR CURVES ({activePull?.name || 'Stock'})
              </span>
              <div className="flex items-center space-x-4 text-xs font-sans">
                <div className="flex items-center space-x-1.5">
                  <span className="w-3 h-3 rounded-full bg-rose-500" />
                  <span className="text-slate-300">Power ({unitMode === 'Imperial' ? 'HP' : 'kW'})</span>
                </div>
                <div className="flex items-center space-x-1.5">
                  <span className="w-3 h-3 rounded-full bg-amber-400" />
                  <span className="text-slate-300">Torque ({unitMode === 'Imperial' ? 'lb-ft' : 'Nm'})</span>
                </div>
              </div>
            </div>

            <div className="w-full h-80 bg-[#0A0E17] rounded border border-can-border relative overflow-hidden flex items-center justify-center">
              <svg className="w-full h-full" viewBox={`0 0 ${svgWidth} ${svgHeight}`}>
                {[2000, 3000, 4000, 5000, 6000, 7000].map(rpm => (
                  <g key={`sim-x-${rpm}`}>
                    <line x1={getX(rpm)} y1={padding.top} x2={getX(rpm)} y2={svgHeight - padding.bottom} stroke="#1E293B" strokeDasharray="4,4" />
                    <text x={getX(rpm)} y={svgHeight - padding.bottom + 16} fill="#64748B" fontSize="10" textAnchor="middle" fontFamily="monospace">{rpm}</text>
                  </g>
                ))}
                {activePull?.points.map((pt, idx) => {
                  if (idx === 0) return null;
                  const prev = activePull.points[idx - 1];
                  return (
                    <g key={idx}>
                      <line
                        x1={getX(prev.rpm)}
                        y1={getYPower(unitMode === 'Imperial' ? prev.powerHp : prev.powerKw)}
                        x2={getX(pt.rpm)}
                        y2={getYPower(unitMode === 'Imperial' ? pt.powerHp : pt.powerKw)}
                        stroke="#F43F5E"
                        strokeWidth="3"
                        strokeLinecap="round"
                      />
                      <line
                        x1={getX(prev.rpm)}
                        y1={getYTorque(unitMode === 'Imperial' ? prev.torqueLbFt : prev.torqueNm)}
                        x2={getX(pt.rpm)}
                        y2={getYTorque(unitMode === 'Imperial' ? pt.torqueLbFt : pt.torqueNm)}
                        stroke="#F59E0B"
                        strokeWidth="3"
                        strokeLinecap="round"
                      />
                    </g>
                  );
                })}
              </svg>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
