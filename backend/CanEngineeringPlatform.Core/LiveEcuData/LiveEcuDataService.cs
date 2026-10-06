using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.LiveEcuData;

public interface ILiveEcuDataService
{
    IReadOnlyList<LiveParameter> GetAllParameters();
    IReadOnlyList<LiveParameter> GetParametersByCategory(LiveParameterCategory category);
    IReadOnlyList<RequestedVsActualPair> GetRequestedVsActualPairs();
    void SetPinned(string parameterId, bool isPinned);
    void ClearParameters();
    void IngestDecodedSignals(IReadOnlyList<DecodedSignal> signals);
    event Action<IReadOnlyList<LiveParameter>>? OnParametersUpdated;
}

public class LiveEcuDataService : ILiveEcuDataService, IDisposable
{
    private readonly ICanPipeline _pipeline;
    private readonly IDiagnosticSessionService _diagnosticService;

    private readonly ConcurrentDictionary<string, LiveParameter> _parameters = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _updateTimestamps = new();
    private readonly ConcurrentDictionary<string, RequestedVsActualPair> _reqVsActPairs = new();

    public event Action<IReadOnlyList<LiveParameter>>? OnParametersUpdated;

    public LiveEcuDataService(
        ICanPipeline pipeline,
        IDiagnosticSessionService diagnosticService)
    {
        _pipeline = pipeline;
        _diagnosticService = diagnosticService;

        _pipeline.OnSignalsDecoded += HandleSignalsDecoded;
        _pipeline.OnFrameReceived += HandleRawFrame;
    }

    private void HandleRawFrame(CanFrame frame)
    {
        // Track raw CAN parameters if unmapped
        string rawKey = $"CAN_0x{frame.Id:X3}";
        if (!_parameters.ContainsKey(rawKey))
        {
            var param = new LiveParameter
            {
                Id = rawKey,
                Name = $"CAN ID 0x{frame.Id:X3}",
                Description = "Observed CAN Frame Traffic",
                Category = LiveParameterCategory.Other,
                Value = frame.Dlc,
                FormattedValue = $"DLC: {frame.Dlc} [{BitConverter.ToString(frame.Data)}]",
                Unit = "bytes",
                Provenance = ParameterProvenance.ObservedCan,
                CanId = frame.Id,
                MessageName = $"MSG_0x{frame.Id:X3}",
                SignalName = "RawPayload",
                LastSeenUtc = DateTime.UtcNow
            };
            _parameters[rawKey] = param;
        }
    }

    private void HandleSignalsDecoded(IReadOnlyList<DecodedSignal> signals)
    {
        IngestDecodedSignals(signals);
    }

    public void IngestDecodedSignals(IReadOnlyList<DecodedSignal> signals)
    {
        var now = DateTime.UtcNow;

        foreach (var sig in signals)
        {
            string key = $"{sig.CanId}_{sig.SignalName}";
            var category = CategorizeSignal(sig.SignalName);

            var tsList = _updateTimestamps.GetOrAdd(key, _ => new List<DateTime>());
            lock (tsList)
            {
                tsList.Add(now);
                tsList.RemoveAll(t => (now - t).TotalSeconds > 2.0);
            }
            double rateHz = Math.Round(tsList.Count / 2.0, 1);

            _parameters.AddOrUpdate(key, _ => new LiveParameter
            {
                Id = key,
                Name = sig.SignalName,
                Description = $"{sig.MessageName} -> {sig.SignalName}",
                Category = category,
                Value = sig.EngineeringValue,
                FormattedValue = $"{sig.EngineeringValue:F2} {sig.Unit}",
                Unit = sig.Unit,
                Provenance = ParameterProvenance.Dbc,
                CanId = sig.CanId,
                MessageName = sig.MessageName,
                SignalName = sig.SignalName,
                UpdateRateHz = rateHz,
                LastSeenUtc = now,
                MinObserved = sig.EngineeringValue,
                MaxObserved = sig.EngineeringValue
            }, (_, existing) =>
            {
                existing.Value = sig.EngineeringValue;
                existing.FormattedValue = $"{sig.EngineeringValue:F2} {sig.Unit}";
                existing.UpdateRateHz = rateHz;
                existing.LastSeenUtc = now;
                existing.MinObserved = Math.Min(existing.MinObserved, sig.EngineeringValue);
                existing.MaxObserved = Math.Max(existing.MaxObserved, sig.EngineeringValue);
                return existing;
            });
        }

        UpdateRequestedVsActual();
        OnParametersUpdated?.Invoke(GetAllParameters());
    }

    private LiveParameterCategory CategorizeSignal(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("rpm") || n.Contains("engine") || n.Contains("torque") || n.Contains("throttle") || n.Contains("tps") || n.Contains("oil"))
            return LiveParameterCategory.Engine;
        if (n.Contains("boost") || n.Contains("map") || n.Contains("maf") || n.Contains("air") || n.Contains("turbo") || n.Contains("vgt") || n.Contains("wastegate"))
            return LiveParameterCategory.AirTurbo;
        if (n.Contains("fuel") || n.Contains("rail") || n.Contains("lambda") || n.Contains("afr") || n.Contains("injection"))
            return LiveParameterCategory.Fuel;
        if (n.Contains("egr"))
            return LiveParameterCategory.Egr;
        if (n.Contains("dpf") || n.Contains("soot") || n.Contains("ash") || n.Contains("regen"))
            return LiveParameterCategory.Dpf;
        if (n.Contains("adblue") || n.Contains("scr") || n.Contains("urea") || n.Contains("dosing"))
            return LiveParameterCategory.ScrAdBlue;
        if (n.Contains("nox"))
            return LiveParameterCategory.Nox;
        if (n.Contains("temp") || n.Contains("coolant") || n.Contains("egt") || n.Contains("thermal"))
            return LiveParameterCategory.Thermal;
        if (n.Contains("speed") || n.Contains("wheel") || n.Contains("gear") || n.Contains("brake") || n.Contains("clutch") || n.Contains("voltage") || n.Contains("battery"))
            return LiveParameterCategory.Vehicle;
        if (n.Contains("transmission") || n.Contains("tcm") || n.Contains("prndl"))
            return LiveParameterCategory.Transmission;
        if (n.Contains("dtc") || n.Contains("mil") || n.Contains("fault") || n.Contains("diag"))
            return LiveParameterCategory.Diagnostics;

        return LiveParameterCategory.Other;
    }

    private void UpdateRequestedVsActual()
    {
        // 1. Torque Comparison (Requested vs Actual)
        var reqTorque = _parameters.Values.FirstOrDefault(p => p.Name.Contains("ReqTorque", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("DriverDemand", StringComparison.OrdinalIgnoreCase));
        var actTorque = _parameters.Values.FirstOrDefault(p => p.Name.Equals("EngineTorque", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("ActualTorque", StringComparison.OrdinalIgnoreCase));
        if (reqTorque != null && actTorque != null)
        {
            _reqVsActPairs["torque"] = new RequestedVsActualPair
            {
                Id = "torque",
                ParameterName = "Engine Torque",
                Category = LiveParameterCategory.Engine,
                Unit = "Nm",
                TargetValue = reqTorque.Value,
                ActualValue = actTorque.Value,
                TimestampUtc = DateTime.UtcNow
            };
        }

        // 2. Boost Comparison (Target vs Actual)
        var targetBoost = _parameters.Values.FirstOrDefault(p => p.Name.Contains("BoostTarget", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("TargetBoost", StringComparison.OrdinalIgnoreCase));
        var actBoost = _parameters.Values.FirstOrDefault(p => p.Name.Equals("Boost", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("ActualBoost", StringComparison.OrdinalIgnoreCase));
        if (targetBoost != null && actBoost != null)
        {
            _reqVsActPairs["boost"] = new RequestedVsActualPair
            {
                Id = "boost",
                ParameterName = "Boost Pressure",
                Category = LiveParameterCategory.AirTurbo,
                Unit = "bar",
                TargetValue = targetBoost.Value,
                ActualValue = actBoost.Value,
                TimestampUtc = DateTime.UtcNow
            };
        }

        // 3. Rail Pressure Comparison (Target vs Actual)
        var targetRail = _parameters.Values.FirstOrDefault(p => p.Name.Contains("TargetRail", StringComparison.OrdinalIgnoreCase));
        var actRail = _parameters.Values.FirstOrDefault(p => p.Name.Contains("RailPressure", StringComparison.OrdinalIgnoreCase));
        if (targetRail != null && actRail != null)
        {
            _reqVsActPairs["rail_pressure"] = new RequestedVsActualPair
            {
                Id = "rail_pressure",
                ParameterName = "Common Rail Fuel Pressure",
                Category = LiveParameterCategory.Fuel,
                Unit = "MPa",
                TargetValue = targetRail.Value,
                ActualValue = actRail.Value,
                TimestampUtc = DateTime.UtcNow
            };
        }
    }

    public IReadOnlyList<LiveParameter> GetAllParameters() => new List<LiveParameter>(_parameters.Values);

    public IReadOnlyList<LiveParameter> GetParametersByCategory(LiveParameterCategory category)
    {
        return _parameters.Values.Where(p => p.Category == category).ToList();
    }

    public IReadOnlyList<RequestedVsActualPair> GetRequestedVsActualPairs() => new List<RequestedVsActualPair>(_reqVsActPairs.Values);

    public void SetPinned(string parameterId, bool isPinned)
    {
        if (_parameters.TryGetValue(parameterId, out var param))
        {
            param.IsPinned = isPinned;
        }
    }

    public void ClearParameters()
    {
        _parameters.Clear();
        _updateTimestamps.Clear();
        _reqVsActPairs.Clear();
    }

    public void Dispose()
    {
        _pipeline.OnSignalsDecoded -= HandleSignalsDecoded;
        _pipeline.OnFrameReceived -= HandleRawFrame;
    }
}
