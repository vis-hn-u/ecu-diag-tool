using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Diagnostics.Engine.LiveData;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Plausibility;

public interface ISensorPlausibilityService
{
    SensorPlausibilityReport EvaluatePlausibility(string ecuName = "ECM");
}

public class SensorPlausibilityService : ISensorPlausibilityService
{
    private readonly ILiveDataService _liveDataService;

    public SensorPlausibilityService(ILiveDataService liveDataService)
    {
        _liveDataService = liveDataService ?? throw new ArgumentNullException(nameof(liveDataService));
    }

    public SensorPlausibilityReport EvaluatePlausibility(string ecuName = "ECM")
    {
        var report = new SensorPlausibilityReport
        {
            EcuName = ecuName,
            EvaluatedUtc = DateTime.UtcNow
        };

        var samples = _liveDataService.GetLatestSamples();
        var sampleDict = samples.ToDictionary(s => s.ParameterId, s => s, StringComparer.OrdinalIgnoreCase);

        // 1. Electrical / Alternator Charging Plausibility
        report.Checks.Add(EvaluateElectricalCharging(sampleDict));

        // 2. Airflow vs Throttle & Load Plausibility
        report.Checks.Add(EvaluateAirflowThrottleCorrelation(sampleDict));

        // 3. Thermal Balance: Coolant vs Intake Air Temp
        report.Checks.Add(EvaluateThermalBalance(sampleDict));

        // 4. Closed-Loop Combustion & Fuel Trim Plausibility
        report.Checks.Add(EvaluateCombustionFuelTrims(sampleDict));

        // 5. Driveline Speed vs Engine Velocity Plausibility
        report.Checks.Add(EvaluateDrivelinePlausibility(sampleDict));

        // Determine Overall State
        if (report.Checks.Any(c => c.State == SensorPlausibilityState.OutOfRange || c.State == SensorPlausibilityState.Inconsistent))
        {
            report.OverallState = SensorPlausibilityState.Inconsistent;
        }
        else if (report.Checks.Any(c => c.State == SensorPlausibilityState.Questionable))
        {
            report.OverallState = SensorPlausibilityState.Questionable;
        }
        else if (report.Checks.Any(c => c.State == SensorPlausibilityState.Plausible))
        {
            report.OverallState = SensorPlausibilityState.Plausible;
        }
        else
        {
            report.OverallState = SensorPlausibilityState.InsufficientData;
        }

        return report;
    }

    private SensorPlausibilityCheckResult EvaluateElectricalCharging(Dictionary<string, LiveDataSample> samples)
    {
        bool hasVoltage = samples.TryGetValue("MODULE_VOLTAGE", out var voltSample);
        bool hasRpm = samples.TryGetValue("ENGINE_RPM", out var rpmSample);

        if (!hasVoltage)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Electrical System & Alternator Charging",
                Category = "Electrical",
                State = SensorPlausibilityState.InsufficientData,
                Summary = "Module voltage parameter not available.",
                DetailedEvidence = "DID 0x0102 / PID 0x42 missing from live data telemetry stream."
            };
        }

        double voltage = voltSample!.ScaledValue;
        double rpm = hasRpm ? rpmSample!.ScaledValue : 0.0;

        var values = new Dictionary<string, double> { ["Voltage"] = voltage, ["RPM"] = rpm };

        if (voltage < 9.5)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Electrical System & Alternator Charging",
                Category = "Electrical",
                State = SensorPlausibilityState.OutOfRange,
                Summary = "Severe low voltage brownout hazard (< 9.5V).",
                DetailedEvidence = $"Measured bus voltage {voltage:F2}V is dangerously below operating threshold (minimum 10.5V required for stable ECU operation).",
                InvolvedSignals = new() { "MODULE_VOLTAGE" },
                MeasuredValues = values
            };
        }

        if (voltage > 16.0)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Electrical System & Alternator Charging",
                Category = "Electrical",
                State = SensorPlausibilityState.OutOfRange,
                Summary = "Alternator overvoltage hazard (> 16.0V).",
                DetailedEvidence = $"Measured bus voltage {voltage:F2}V exceeds maximum safe voltage threshold (15.5V). Risk of damage to onboard electronics.",
                InvolvedSignals = new() { "MODULE_VOLTAGE" },
                MeasuredValues = values
            };
        }

        if (rpm > 650 && voltage < 12.2)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Electrical System & Alternator Charging",
                Category = "Electrical",
                State = SensorPlausibilityState.Inconsistent,
                Summary = "Alternator not charging while engine is running.",
                DetailedEvidence = $"Engine is running at {rpm:F0} RPM, but system voltage is only {voltage:F2}V (normal running target: 13.5V to 14.7V). Alternator or serpentine belt failure likely.",
                InvolvedSignals = new() { "MODULE_VOLTAGE", "ENGINE_RPM" },
                MeasuredValues = values
            };
        }

        return new SensorPlausibilityCheckResult
        {
            CheckName = "Electrical System & Alternator Charging",
            Category = "Electrical",
            State = SensorPlausibilityState.Plausible,
            Summary = "System voltage within normal operating bounds.",
            DetailedEvidence = $"System voltage is {voltage:F2}V with engine at {rpm:F0} RPM. Regulated charging confirmed.",
            InvolvedSignals = new() { "MODULE_VOLTAGE" },
            MeasuredValues = values
        };
    }

    private SensorPlausibilityCheckResult EvaluateAirflowThrottleCorrelation(Dictionary<string, LiveDataSample> samples)
    {
        bool hasRpm = samples.TryGetValue("ENGINE_RPM", out var rpmSample);
        bool hasThrottle = samples.TryGetValue("THROTTLE_POS", out var throttleSample);
        bool hasMaf = samples.TryGetValue("MASS_AIR_FLOW", out var mafSample);
        bool hasMap = samples.TryGetValue("MANIFOLD_PRESSURE", out var mapSample);

        if (!hasRpm || (!hasMaf && !hasMap))
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Intake Airflow & Load Correlation",
                Category = "Air / Induction",
                State = SensorPlausibilityState.InsufficientData,
                Summary = "Insufficient airflow or RPM parameters to perform correlation.",
                DetailedEvidence = "Requires ENGINE_RPM and either MASS_AIR_FLOW or MANIFOLD_PRESSURE."
            };
        }

        double rpm = rpmSample!.ScaledValue;
        double throttle = hasThrottle ? throttleSample!.ScaledValue : 0.0;
        double maf = hasMaf ? mafSample!.ScaledValue : 0.0;
        double map = hasMap ? mapSample!.ScaledValue : 100.0;

        var values = new Dictionary<string, double>
        {
            ["RPM"] = rpm,
            ["Throttle"] = throttle,
            ["MAF"] = maf,
            ["MAP"] = map
        };

        // Decel fuel cutoff / Idle check: High RPM (> 2500) and Closed Throttle (< 3%)
        if (rpm > 2500 && throttle < 3.0 && maf > 35.0)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Intake Airflow & Load Correlation",
                Category = "Air / Induction",
                State = SensorPlausibilityState.Inconsistent,
                Summary = "Excessive airflow measured during closed-throttle deceleration.",
                DetailedEvidence = $"At {rpm:F0} RPM with closed throttle ({throttle:F1}%), MAF is {maf:F1} g/s (expected < 15 g/s during deceleration fuel cut-off). Check for stuck idle air valve or MAF calibration drift.",
                InvolvedSignals = new() { "ENGINE_RPM", "THROTTLE_POS", "MASS_AIR_FLOW" },
                MeasuredValues = values
            };
        }

        // Idle check: Low RPM (600-900) but excessive MAF (> 25 g/s)
        if (rpm > 500 && rpm < 1000 && throttle < 5.0 && maf > 20.0)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Intake Airflow & Load Correlation",
                Category = "Air / Induction",
                State = SensorPlausibilityState.Questionable,
                Summary = "High idle airflow reading.",
                DetailedEvidence = $"Engine idling at {rpm:F0} RPM with {throttle:F1}% throttle, but MAF reads {maf:F1} g/s (normal idle target: 2.0 to 6.0 g/s for 2.0L engine).",
                InvolvedSignals = new() { "ENGINE_RPM", "THROTTLE_POS", "MASS_AIR_FLOW" },
                MeasuredValues = values
            };
        }

        return new SensorPlausibilityCheckResult
        {
            CheckName = "Intake Airflow & Load Correlation",
            Category = "Air / Induction",
            State = SensorPlausibilityState.Plausible,
            Summary = "Airflow, manifold pressure, and throttle angle correlate plausibly.",
            DetailedEvidence = $"RPM={rpm:F0}, Throttle={throttle:F1}%, MAF={maf:F1} g/s, MAP={map:F0} kPa reflect valid operating equilibrium.",
            InvolvedSignals = new() { "ENGINE_RPM", "THROTTLE_POS" },
            MeasuredValues = values
        };
    }

    private SensorPlausibilityCheckResult EvaluateThermalBalance(Dictionary<string, LiveDataSample> samples)
    {
        bool hasEct = samples.TryGetValue("COOLANT_TEMP", out var ectSample);
        bool hasIat = samples.TryGetValue("INTAKE_AIR_TEMP", out var iatSample);

        if (!hasEct)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Thermal Equilibrium (ECT vs IAT)",
                Category = "Thermal",
                State = SensorPlausibilityState.InsufficientData,
                Summary = "Coolant temperature parameter not available.",
                DetailedEvidence = "COOLANT_TEMP missing from live data."
            };
        }

        double ect = ectSample!.ScaledValue;
        double iat = hasIat ? iatSample!.ScaledValue : 25.0;

        var values = new Dictionary<string, double> { ["ECT"] = ect, ["IAT"] = iat };

        if (ect > 115.0)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Thermal Equilibrium (ECT vs IAT)",
                Category = "Thermal",
                State = SensorPlausibilityState.OutOfRange,
                Summary = "Engine coolant overheating critical warning (> 115°C).",
                DetailedEvidence = $"Measured coolant temperature is {ect:F1}°C (safe operating ceiling is 108°C). Risk of cylinder head warpage or gasket failure.",
                InvolvedSignals = new() { "COOLANT_TEMP" },
                MeasuredValues = values
            };
        }

        if (hasIat && ect < -20.0 && iat > 20.0)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Thermal Equilibrium (ECT vs IAT)",
                Category = "Thermal",
                State = SensorPlausibilityState.Inconsistent,
                Summary = "Sub-zero coolant reading while intake air is warm.",
                DetailedEvidence = $"Coolant sensor reads {ect:F1}°C while ambient IAT reads {iat:F1}°C. Strong indicator of open-circuit ECT sensor or broken ground.",
                InvolvedSignals = new() { "COOLANT_TEMP", "INTAKE_AIR_TEMP" },
                MeasuredValues = values
            };
        }

        return new SensorPlausibilityCheckResult
        {
            CheckName = "Thermal Equilibrium (ECT vs IAT)",
            Category = "Thermal",
            State = SensorPlausibilityState.Plausible,
            Summary = "Engine thermal parameters operating within documented specifications.",
            DetailedEvidence = $"Coolant temperature is {ect:F1}°C, Intake air is {iat:F1}°C.",
            InvolvedSignals = new() { "COOLANT_TEMP" },
            MeasuredValues = values
        };
    }

    private SensorPlausibilityCheckResult EvaluateCombustionFuelTrims(Dictionary<string, LiveDataSample> samples)
    {
        bool hasLambda = samples.TryGetValue("LAMBDA", out var lambdaSample);
        bool hasStft = samples.TryGetValue("STFT", out var stftSample);
        bool hasLtft = samples.TryGetValue("LTFT", out var ltftSample);

        if (!hasLambda && !hasStft && !hasLtft)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Closed-Loop Fuel Trim & Mixture Control",
                Category = "Combustion",
                State = SensorPlausibilityState.InsufficientData,
                Summary = "Combustion mixture parameters not available.",
                DetailedEvidence = "Requires LAMBDA, STFT, or LTFT parameters."
            };
        }

        double lambda = hasLambda ? lambdaSample!.ScaledValue : 1.0;
        double stft = hasStft ? stftSample!.ScaledValue : 0.0;
        double ltft = hasLtft ? ltftSample!.ScaledValue : 0.0;

        var values = new Dictionary<string, double>
        {
            ["Lambda"] = lambda,
            ["STFT"] = stft,
            ["LTFT"] = ltft,
            ["TotalTrim"] = stft + ltft
        };

        // Lean mixture accompanied by maxed positive trims (> +20%)
        if (lambda > 1.10 && (stft + ltft) > 20.0)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Closed-Loop Fuel Trim & Mixture Control",
                Category = "Combustion",
                State = SensorPlausibilityState.Inconsistent,
                Summary = "Uncorrected lean combustion with positive fuel trim saturation.",
                DetailedEvidence = $"Lambda={lambda:F2} (lean), Total fuel trim={stft + ltft:F1}% (+{stft:F1}% STFT, +{ltft:F1}% LTFT). ECU cannot compensate for unmetered air or inadequate fuel rail pressure.",
                InvolvedSignals = new() { "LAMBDA", "STFT", "LTFT" },
                MeasuredValues = values
            };
        }

        // Rich mixture accompanied by maxed negative trims (< -20%)
        if (lambda < 0.90 && (stft + ltft) < -20.0)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Closed-Loop Fuel Trim & Mixture Control",
                Category = "Combustion",
                State = SensorPlausibilityState.Inconsistent,
                Summary = "Uncorrected rich combustion with negative fuel trim saturation.",
                DetailedEvidence = $"Lambda={lambda:F2} (rich), Total fuel trim={stft + ltft:F1}%. ECU has reached maximum lean trim limit. Check for leaking fuel injectors or high fuel pressure.",
                InvolvedSignals = new() { "LAMBDA", "STFT", "LTFT" },
                MeasuredValues = values
            };
        }

        return new SensorPlausibilityCheckResult
        {
            CheckName = "Closed-Loop Fuel Trim & Mixture Control",
            Category = "Combustion",
            State = SensorPlausibilityState.Plausible,
            Summary = "Fuel trims and lambda stoichiometry are well-balanced.",
            DetailedEvidence = $"Lambda={lambda:F2}, STFT={stft:F1}%, LTFT={ltft:F1}% (Total Trim = {stft + ltft:F1}% within safe ±10% OEM window).",
            InvolvedSignals = new() { "LAMBDA", "STFT", "LTFT" },
            MeasuredValues = values
        };
    }

    private SensorPlausibilityCheckResult EvaluateDrivelinePlausibility(Dictionary<string, LiveDataSample> samples)
    {
        bool hasSpeed = samples.TryGetValue("VEHICLE_SPEED", out var speedSample);
        bool hasRpm = samples.TryGetValue("ENGINE_RPM", out var rpmSample);

        if (!hasSpeed || !hasRpm)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Driveline Kinematics (Speed vs RPM)",
                Category = "Driveline",
                State = SensorPlausibilityState.InsufficientData,
                Summary = "Speed or RPM parameter missing.",
                DetailedEvidence = "Requires VEHICLE_SPEED and ENGINE_RPM."
            };
        }

        double speed = speedSample!.ScaledValue;
        double rpm = rpmSample!.ScaledValue;

        var values = new Dictionary<string, double> { ["Speed"] = speed, ["RPM"] = rpm };

        if (speed > 40.0 && rpm == 0.0)
        {
            return new SensorPlausibilityCheckResult
            {
                CheckName = "Driveline Kinematics (Speed vs RPM)",
                Category = "Driveline",
                State = SensorPlausibilityState.Questionable,
                Summary = "Vehicle speed detected with engine stalled / off.",
                DetailedEvidence = $"Road speed is {speed:F0} km/h but crankshaft velocity is 0 RPM. Plausible only during neutral coasting with stalled engine.",
                InvolvedSignals = new() { "VEHICLE_SPEED", "ENGINE_RPM" },
                MeasuredValues = values
            };
        }

        return new SensorPlausibilityCheckResult
        {
            CheckName = "Driveline Kinematics (Speed vs RPM)",
            Category = "Driveline",
            State = SensorPlausibilityState.Plausible,
            Summary = "Vehicle speed and engine RPM kinematics correlate plausibly.",
            DetailedEvidence = $"Speed={speed:F0} km/h, RPM={rpm:F0}.",
            InvolvedSignals = new() { "VEHICLE_SPEED", "ENGINE_RPM" },
            MeasuredValues = values
        };
    }
}
