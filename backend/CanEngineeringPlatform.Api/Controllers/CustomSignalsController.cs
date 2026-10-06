using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Signals;
using CanEngineeringPlatform.Core.Signals.Calculated;

namespace CanEngineeringPlatform.Api.Controllers;

public class EvaluateCalculatedRequest
{
    public Dictionary<string, double> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

[ApiController]
[Route("api/[controller]")]
public class CustomSignalsController : ControllerBase
{
    private readonly ICalculatedSignalEngine _calculatedEngine;

    public CustomSignalsController(ICalculatedSignalEngine calculatedEngine)
    {
        _calculatedEngine = calculatedEngine;
    }

    [HttpGet("calculated")]
    public ActionResult<IEnumerable<CalculatedSignalDefinition>> GetCalculatedSignals()
    {
        return Ok(_calculatedEngine.GetAllSignals());
    }

    [HttpGet("calculated/order")]
    public ActionResult<IEnumerable<string>> GetEvaluationOrder()
    {
        return Ok(_calculatedEngine.GetEvaluationOrder());
    }

    [HttpPost("calculated")]
    public ActionResult<CalculatedSignalDefinition> RegisterCalculatedSignal([FromBody] CalculatedSignalDefinition signal)
    {
        if (signal == null || string.IsNullOrWhiteSpace(signal.Expression))
            return BadRequest("Signal definition with valid expression is required.");

        try
        {
            _calculatedEngine.RegisterSignal(signal);
            return Ok(signal);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("calculated/evaluate")]
    public ActionResult<Dictionary<string, double>> EvaluateCalculatedSignals([FromBody] EvaluateCalculatedRequest request)
    {
        var results = _calculatedEngine.EvaluateAll(request.Inputs ?? new Dictionary<string, double>());
        return Ok(results);
    }

    [HttpGet("metadata")]
    public ActionResult<IEnumerable<ParameterMetadata>> GetStandardMetadata()
    {
        var metadata = new List<ParameterMetadata>
        {
            new() { ParameterId = "EngineRpm", StandardLabel = "RPM", DisplayName = "Engine Speed", Category = "Powertrain", Units = "rpm", ExpectedFrequencyHz = 50, Confidence = ParameterConfidence.FactoryCalibrated },
            new() { ParameterId = "VehicleSpeedKmh", StandardLabel = "VSS", DisplayName = "Vehicle Road Speed", Category = "Dynamics", Units = "km/h", ExpectedFrequencyHz = 50, Confidence = ParameterConfidence.Observed },
            new() { ParameterId = "MapKpa", StandardLabel = "MAP", DisplayName = "Manifold Absolute Pressure", Category = "Powertrain", Units = "kPa", ExpectedFrequencyHz = 50, Confidence = ParameterConfidence.Observed },
            new() { ParameterId = "BoostBar", StandardLabel = "BOOST", DisplayName = "Gauge Boost Pressure", Category = "Powertrain", Units = "bar", ExpectedFrequencyHz = 50, Confidence = ParameterConfidence.Estimated },
            new() { ParameterId = "CoolantTempC", StandardLabel = "ECT", DisplayName = "Engine Coolant Temperature", Category = "Thermal", Units = "°C", ExpectedFrequencyHz = 10, Confidence = ParameterConfidence.FactoryCalibrated },
            new() { ParameterId = "IntakeAirTempC", StandardLabel = "IAT", DisplayName = "Intake Air Temperature", Category = "Thermal", Units = "°C", ExpectedFrequencyHz = 10, Confidence = ParameterConfidence.FactoryCalibrated },
            new() { ParameterId = "OilTempC", StandardLabel = "EOT", DisplayName = "Engine Oil Temperature", Category = "Thermal", Units = "°C", ExpectedFrequencyHz = 5, Confidence = ParameterConfidence.Observed },
            new() { ParameterId = "ThrottlePositionPct", StandardLabel = "TPS", DisplayName = "Throttle Position", Category = "Powertrain", Units = "%", ExpectedFrequencyHz = 100, Confidence = ParameterConfidence.FactoryCalibrated },
            new() { ParameterId = "TorqueNm", StandardLabel = "TRQ", DisplayName = "Engine Flywheel Torque", Category = "Powertrain", Units = "Nm", ExpectedFrequencyHz = 50, Confidence = ParameterConfidence.Estimated },
            new() { ParameterId = "PowerHp", StandardLabel = "PWR", DisplayName = "Calculated Engine Power", Category = "Powertrain", Units = "hp", ExpectedFrequencyHz = 50, Confidence = ParameterConfidence.Estimated },
            new() { ParameterId = "Lambda", StandardLabel = "LAMBDA", DisplayName = "Air-Fuel Equivalence Ratio", Category = "Fuel", Units = "λ", ExpectedFrequencyHz = 50, Confidence = ParameterConfidence.FactoryCalibrated },
            new() { ParameterId = "Afr", StandardLabel = "AFR", DisplayName = "Air-Fuel Ratio", Category = "Fuel", Units = "AFR", ExpectedFrequencyHz = 50, Confidence = ParameterConfidence.Estimated }
        };

        return Ok(metadata);
    }
}
