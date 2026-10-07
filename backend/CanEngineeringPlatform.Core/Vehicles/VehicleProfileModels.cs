using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Vehicles;

public class EngineSpecs
{
    public string Code { get; set; } = "EA888.3";
    public double DisplacementLiters { get; set; } = 2.0;
    public int CylinderCount { get; set; } = 4;
    public string FuelType { get; set; } = "Gasoline";
    public string Aspiration { get; set; } = "Turbocharged"; // NA, Turbocharged, Supercharged, TwinTurbo
    public double MaxBoostBar { get; set; } = 1.35;
    public double CompressionRatio { get; set; } = 9.6;
    public double InjectorFlowCcPerMin { get; set; } = 550.0;
    public double RedlineRpm { get; set; } = 6800.0;
    public double RevLimitRpm { get; set; } = 7000.0;
    public double IdleRpm { get; set; } = 800.0;
}

public class EcuHardwareSoftware
{
    public string HardwarePartNumber { get; set; } = "06K907425B";
    public string HardwareVersion { get; set; } = "H13";
    public string SoftwareVersion { get; set; } = "0005";
    public string CalibrationId { get; set; } = "5G0906259L";
    public string DiagnosticProtocol { get; set; } = "UDS"; // UDS, KWP2000, OBD-II
    public uint RequestAddress { get; set; } = 0x7E0;
    public uint ResponseAddress { get; set; } = 0x7E8;
}

public class TransmissionSpecs
{
    public string Type { get; set; } = "Manual"; // Manual, Automatic, DCT, CVT
    public int ForwardGears { get; set; } = 6;
    public double FinalDriveRatio { get; set; } = 3.944;
    public double[] GearRatios { get; set; } = new double[] { 3.769, 2.087, 1.457, 1.109, 0.974, 0.733 };
}

public class TireSpecs
{
    public string SizeSpec { get; set; } = "235/40R18";
    public double WidthMm { get; set; } = 235.0;
    public double AspectRatioPct { get; set; } = 40.0;
    public double RimDiameterInches { get; set; } = 18.0;

    /// <summary>
    /// Calculated rolling radius in meters based on tire dimensions.
    /// </summary>
    public double RollingRadiusMeters
    {
        get
        {
            double sidewallMm = WidthMm * (AspectRatioPct / 100.0);
            double rimMm = RimDiameterInches * 25.4;
            double diameterMm = (sidewallMm * 2.0) + rimMm;
            return (diameterMm / 2.0) / 1000.0;
        }
    }

    /// <summary>
    /// Tire rolling circumference in meters (2 * PI * r).
    /// </summary>
    public double CircumferenceMeters => 2.0 * Math.PI * RollingRadiusMeters;
}

public class VehicleProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default Sports Vehicle";
    public string Vin { get; set; } = "WVWZZZAUZEP000001";
    public string Make { get; set; } = "Volkswagen";
    public string Model { get; set; } = "Golf R";
    public int Year { get; set; } = 2021;
    public double CurbWeightKg { get; set; } = 1450.0;
    public double TestWeightKg { get; set; } = 1530.0; // With driver and equipment
    public double OccupantsWeightKg { get; set; } = 80.0;

    // Aerodynamic and Road-Load Parameters for Road Dyno
    public double DragCoefficient { get; set; } = 0.31;
    public double FrontalAreaM2 { get; set; } = 2.20;
    public double RollingResistanceCoeff { get; set; } = 0.013;
    public string DrivetrainType { get; set; } = "AWD"; // FWD, RWD, AWD
    public double DrivetrainLossPct { get; set; } = 16.0; // AWD ~16-18%, RWD ~13-15%, FWD ~10-12%
    public double RotationalMassFactor { get; set; } = 1.08; // Equivalent rotating inertia factor (approx 7-10% in 3rd/4th gear)

    public double TotalMassKg => TestWeightKg > 0 ? TestWeightKg : (CurbWeightKg + OccupantsWeightKg);
    public double EffectiveMassKg => TotalMassKg * RotationalMassFactor;

    public EngineSpecs Engine { get; set; } = new();
    public EcuHardwareSoftware Ecu { get; set; } = new();
    public TransmissionSpecs Transmission { get; set; } = new();
    public TireSpecs Tires { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Calculates vehicle road speed (km/h) for a given transmission gear and engine RPM.
    /// </summary>
    public double CalculateSpeedAtRpm(int gear, double rpm)
    {
        if (gear < 1 || gear > Transmission.GearRatios.Length || rpm <= 0)
            return 0.0;

        double gearRatio = Transmission.GearRatios[gear - 1];
        double totalRatio = gearRatio * Transmission.FinalDriveRatio;
        if (totalRatio <= 0) return 0.0;

        double wheelRps = (rpm / 60.0) / totalRatio;
        double speedMps = wheelRps * Tires.CircumferenceMeters;
        return speedMps * 3.6; // m/s to km/h
    }

    /// <summary>
    /// Calculates engine RPM given road speed (km/h) and engaged gear.
    /// </summary>
    public double CalculateRpmAtSpeed(int gear, double speedKmh)
    {
        if (gear < 1 || gear > Transmission.GearRatios.Length || speedKmh <= 0)
            return Engine.IdleRpm;

        double gearRatio = Transmission.GearRatios[gear - 1];
        double totalRatio = gearRatio * Transmission.FinalDriveRatio;
        double speedMps = speedKmh / 3.6;
        double wheelRps = speedMps / Tires.CircumferenceMeters;
        double rpm = wheelRps * totalRatio * 60.0;
        return Math.Max(Engine.IdleRpm, Math.Round(rpm, 1));
    }
}

public interface IVehicleProfileService
{
    IReadOnlyList<VehicleProfile> GetAllProfiles();
    VehicleProfile? GetProfile(string id);
    VehicleProfile GetActiveProfile();
    void SetActiveProfile(string id);
    VehicleProfile SaveProfile(VehicleProfile profile);
    bool DeleteProfile(string id);
}

public class VehicleProfileService : IVehicleProfileService
{
    private readonly ConcurrentDictionary<string, VehicleProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private string _activeProfileId;
    private readonly object _lock = new();

    public VehicleProfileService()
    {
        var swiftDefault = new VehicleProfile
        {
            Id = "default-vehicle-profile",
            Name = "Maruti Suzuki Swift DDiS (Magneti Marelli MJD)",
            Vin = "MA3EFA12S00123456",
            Make = "Maruti Suzuki",
            Model = "Swift 1.3 DDiS (MJD 6F3/8F3)",
            Year = 2015,
            CurbWeightKg = 1065.0,
            TestWeightKg = 1150.0,
            DragCoefficient = 0.32,
            FrontalAreaM2 = 2.05,
            DrivetrainType = "FWD",
            DrivetrainLossPct = 11.0,
            Engine = new()
            {
                Code = "D13A (Fiat MultiJet)",
                DisplacementLiters = 1.248,
                CylinderCount = 4,
                FuelType = "Diesel",
                Aspiration = "Turbocharged",
                MaxBoostBar = 1.35,
                CompressionRatio = 17.6,
                RedlineRpm = 4500.0,
                RevLimitRpm = 5000.0,
                IdleRpm = 850.0
            },
            Ecu = new()
            {
                HardwarePartNumber = "MJD 6F3.S1 / 8F3",
                HardwareVersion = "Marelli MJD",
                SoftwareVersion = "3422S167",
                CalibrationId = "SWIFT_1.3_DDiS",
                DiagnosticProtocol = "UDS",
                RequestAddress = 0x7E0,
                ResponseAddress = 0x7E8
            }
        };

        var ea888Profile = new VehicleProfile
        {
            Id = "ea888-gen3-bench",
            Name = "Bench Test Profile (EA888 Gen 3 2.0T)",
            Vin = "1FA6P8CF5H5123456",
            Make = "Engineering Lab",
            Model = "Turbo 2.0L Testbench",
            Year = 2023,
            CurbWeightKg = 1450.0,
            TestWeightKg = 1530.0
        };

        _profiles[swiftDefault.Id] = swiftDefault;
        _profiles[ea888Profile.Id] = ea888Profile;
        _activeProfileId = swiftDefault.Id;
    }

    public IReadOnlyList<VehicleProfile> GetAllProfiles() => _profiles.Values.ToList();

    public VehicleProfile? GetProfile(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        _profiles.TryGetValue(id, out var profile);
        return profile;
    }

    public VehicleProfile GetActiveProfile()
    {
        lock (_lock)
        {
            if (_profiles.TryGetValue(_activeProfileId, out var active))
                return active;

            var first = _profiles.Values.FirstOrDefault() ?? new VehicleProfile();
            _activeProfileId = first.Id;
            return first;
        }
    }

    public void SetActiveProfile(string id)
    {
        lock (_lock)
        {
            if (_profiles.ContainsKey(id))
            {
                _activeProfileId = id;
            }
        }
    }

    public VehicleProfile SaveProfile(VehicleProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString("N");
            profile.CreatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            profile.UpdatedAtUtc = DateTime.UtcNow;
        }

        _profiles[profile.Id] = profile;
        return profile;
    }

    public bool DeleteProfile(string id)
    {
        if (_profiles.Count <= 1) return false; // Prevent deleting the sole remaining profile
        bool removed = _profiles.TryRemove(id, out _);
        if (removed && _activeProfileId == id)
        {
            _activeProfileId = _profiles.Keys.First();
        }
        return removed;
    }
}
