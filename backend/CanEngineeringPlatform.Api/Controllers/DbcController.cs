using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Dbc;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Api.Controllers;

public class DbcLibraryItemDto
{
    public string Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EcuFamily { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Protocol { get; set; } = "CAN 2.0 (500 kbps)";
    public int MessageCount { get; set; }
    public int SignalCount { get; set; }
    public List<string> KeySignals { get; set; } = new();
    public List<string> Nodes { get; set; } = new();
    public bool IsActive { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class DbcController : ControllerBase
{
    private readonly ICanPipeline _pipeline;
    private static string? _activeLibraryKey = "magneti_marelli_mjd_ddis";

    public DbcController(ICanPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    private static string ResolveDbcLibraryPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "DbcLibrary"),
            Path.Combine(Directory.GetCurrentDirectory(), "DbcLibrary"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "DbcLibrary"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "CanEngineeringPlatform.Api", "DbcLibrary"),
            @"c:\Users\vishnu\Documents\ChatGPT\diag tool\backend\DbcLibrary"
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    [HttpGet("active")]
    public IActionResult GetActiveDbc()
    {
        var dbc = _pipeline.GetActiveDbc();
        if (dbc == null)
        {
            return Ok(new { isLoaded = false });
        }

        return Ok(new
        {
            isLoaded = true,
            version = dbc.Version,
            activeKey = _activeLibraryKey,
            nodeCount = dbc.Nodes.Count,
            messageCount = dbc.Messages.Count,
            nodes = dbc.Nodes,
            messages = dbc.Messages
        });
    }

    [HttpGet("library")]
    public IActionResult GetDbcLibrary()
    {
        var libraryPath = ResolveDbcLibraryPath();
        if (!Directory.Exists(libraryPath))
        {
            return Ok(new List<DbcLibraryItemDto>());
        }

        var files = Directory.GetFiles(libraryPath, "*.dbc");
        var catalog = new List<DbcLibraryItemDto>();

        var activeDbc = _pipeline.GetActiveDbc();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var key = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

            var meta = GetMetadataForDbc(key, fileName);

            try
            {
                var content = System.IO.File.ReadAllText(file);
                var parsed = DbcParser.Parse(content);
                meta.MessageCount = parsed.Messages.Count;
                meta.SignalCount = parsed.Messages.Sum(m => m.Signals.Count);
                if (meta.Nodes.Count == 0)
                {
                    meta.Nodes = parsed.Nodes.Select(n => n.Name).ToList();
                }
                if (meta.KeySignals.Count == 0)
                {
                    meta.KeySignals = parsed.Messages.SelectMany(m => m.Signals).Select(s => s.Name).Take(6).ToList();
                }
            }
            catch
            {
                // Fallback to static metadata if parse fails
            }

            meta.IsActive = string.Equals(_activeLibraryKey, key, StringComparison.OrdinalIgnoreCase);
            catalog.Add(meta);
        }

        // Return sorted by Category, then Title
        var sorted = catalog
            .OrderBy(c => GetCategorySortOrder(c.Category))
            .ThenBy(c => c.Title)
            .ToList();

        return Ok(sorted);
    }

    [HttpPost("load/{key}")]
    public IActionResult LoadDbcFromLibrary(string key)
    {
        var libraryPath = ResolveDbcLibraryPath();
        var files = Directory.GetFiles(libraryPath, "*.dbc");
        var targetFile = files.FirstOrDefault(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(f), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(f), key + ".dbc", StringComparison.OrdinalIgnoreCase));

        if (targetFile == null || !System.IO.File.Exists(targetFile))
        {
            return NotFound(new { success = false, error = $"DBC file '{key}' not found in library." });
        }

        try
        {
            var content = System.IO.File.ReadAllText(targetFile);
            var dbc = DbcParser.Parse(content);
            _pipeline.SetActiveDbc(dbc);
            _activeLibraryKey = Path.GetFileNameWithoutExtension(targetFile).ToLowerInvariant();

            return Ok(new
            {
                success = true,
                key = _activeLibraryKey,
                fileName = Path.GetFileName(targetFile),
                version = dbc.Version,
                nodeCount = dbc.Nodes.Count,
                messageCount = dbc.Messages.Count,
                signalCount = dbc.Messages.Sum(m => m.Signals.Count)
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = $"Failed to parse DBC: {ex.Message}" });
        }
    }

    [HttpGet("library/{key}/content")]
    public IActionResult GetDbcContent(string key)
    {
        var libraryPath = ResolveDbcLibraryPath();
        var files = Directory.GetFiles(libraryPath, "*.dbc");
        var targetFile = files.FirstOrDefault(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(f), key, StringComparison.OrdinalIgnoreCase));

        if (targetFile == null || !System.IO.File.Exists(targetFile))
        {
            return NotFound(new { error = "DBC file not found" });
        }

        var content = System.IO.File.ReadAllText(targetFile);
        return Content(content, "text/plain");
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDbc(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        using var streamReader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        string content = await streamReader.ReadToEndAsync();
        var dbc = DbcParser.Parse(content);

        _pipeline.SetActiveDbc(dbc);
        _activeLibraryKey = "custom_upload_" + Path.GetFileNameWithoutExtension(file.FileName).ToLowerInvariant();

        return Ok(new
        {
            success = true,
            fileName = file.FileName,
            version = dbc.Version,
            nodeCount = dbc.Nodes.Count,
            messageCount = dbc.Messages.Count
        });
    }

    [HttpPost("load-sample")]
    public IActionResult LoadSampleDbc()
    {
        _pipeline.ResetDefaultDbc();
        _activeLibraryKey = "magneti_marelli_mjd_ddis";
        var dbc = _pipeline.GetActiveDbc()!;

        return Ok(new
        {
            success = true,
            name = "magneti_marelli_mjd_ddis.dbc",
            version = dbc.Version,
            nodeCount = dbc.Nodes.Count,
            messageCount = dbc.Messages.Count
        });
    }

    [HttpPost("reset-default")]
    public IActionResult ResetDefaultDbc()
    {
        _pipeline.ResetDefaultDbc();
        _activeLibraryKey = "magneti_marelli_mjd_ddis";
        var dbc = _pipeline.GetActiveDbc()!;

        return Ok(new
        {
            success = true,
            name = "magneti_marelli_mjd_ddis.dbc",
            version = dbc.Version,
            nodeCount = dbc.Nodes.Count,
            messageCount = dbc.Messages.Count
        });
    }

    [HttpDelete("active")]
    public IActionResult ClearActiveDbc()
    {
        _pipeline.SetActiveDbc(null);
        _activeLibraryKey = null;
        return Ok(new { success = true });
    }

    private static int GetCategorySortOrder(string category) => category switch
    {
        "Bosch EDC Systems" => 1,
        "Magneti Marelli" => 2,
        "Denso Systems" => 3,
        "Siemens & Continental" => 4,
        "Indian OEM Specific" => 5,
        "Commercial Heavy Duty" => 6,
        "Global Platforms in India" => 7,
        "Standard Diagnostics" => 8,
        _ => 9
    };

    private static DbcLibraryItemDto GetMetadataForDbc(string key, string fileName)
    {
        return key switch
        {
            "magneti_marelli_mjd_ddis" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Magneti Marelli MJD Multijet (Maruti Swift DDiS / Tata Quadrajet / Fiat)",
                Category = "Magneti Marelli",
                EcuFamily = "Marelli 6F3 / 8F3 / 9DF / 9GF",
                Manufacturer = "Maruti Suzuki / Tata Motors / Fiat",
                Description = "Authoritative CAN network definitions for 1.3 Multijet diesel engines (Swift, Dzire, Ertiga, Baleno, Brezza, Vista, Manza, Punto). Live RPM (0x124), Rail Pressure (0x318), Coolant Temp (0x314), Vehicle Speed (0x310), BCM (0x73C).",
                KeySignals = new() { "EngineRPM", "FuelRailPressure", "BoostPressure", "EngineCoolantTemp", "VehicleSpeed", "ThrottlePosition" }
            },
            "bosch_edc16_crde" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Bosch EDC16C39 (Mahindra Scorpio/Bolero CRDe / Tata Safari 2.2 Dicor)",
                Category = "Bosch EDC Systems",
                EcuFamily = "Bosch EDC16C39",
                Manufacturer = "Mahindra & Mahindra / Tata Motors",
                Description = "Bosch EDC16 common rail diesel ECU network. Covers Mahindra Scorpio 2.6/2.2 CRDe, Bolero CRDe, Tata Safari 2.2 Dicor, Indigo Dicor, early Maruti 1.3 DDiS. Engine RPM (0x280), Rail Pressure (0x380), Boost (0x480), MIL/Battery (0x580).",
                KeySignals = new() { "EngineRPM", "CommonRailPressure", "BoostPressureActual", "EngineCoolantTemp", "MainInjectionQuantity", "BatteryVoltage" }
            },
            "bosch_edc17_mhawk_revotorq" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Bosch EDC17 (Mahindra Thar / Scorpio mHawk / Tata Nexon 1.5 Revotorq / Maruti DDiS)",
                Category = "Bosch EDC Systems",
                EcuFamily = "Bosch EDC17C53 / EDC17C55 / EDC17C65",
                Manufacturer = "Mahindra / Tata / Maruti / Hyundai / VW",
                Description = "Bosch EDC17 modern common rail platform. Used in Mahindra Thar CRDe / Scorpio mHawk 2.2, Tata Tiago / Altroz / Nexon 1.5 Revotorq, Maruti 1.3/1.6 DDiS, VW Polo 1.5 TDI EDC17C64, Hyundai 1.4/1.6 CRDi. Rail pressure up to 2200 bar, DPF soot, MAF.",
                KeySignals = new() { "EngineRPM", "RailPressureActual", "BoostPressureActual", "CoolantTemperature", "DpfDifferentialPressure", "DpfSootMass" }
            },
            "bosch_md1_mg1_bs6" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Bosch MD1 / MG1 BS6 (Mahindra Scorpio-N / Thar BS6 / Tata Safari 2.0 Kryotec)",
                Category = "Bosch EDC Systems",
                EcuFamily = "Bosch MD1CS018 / MD1CS006 / MG1",
                Manufacturer = "Mahindra / Tata / Hyundai / Maruti",
                Description = "Modern BS6 compliant diesel & petrol engine architecture. Covers Mahindra Scorpio-N, Thar BS6, XUV700 mHawk BS6, Tata Safari / Harrier 2.0 Kryotec BS6, Tata 1.5 Revotorq BS6. Real-time SCR AdBlue level, DPF regeneration, 2500+ bar rail pressure.",
                KeySignals = new() { "EngineRPM", "FuelRailPressure_Actual", "DefAdBlueTankLevel", "ScrCatalystTemp", "DpfSootLoadPercent", "EngineCoolantTemp" }
            },
            "denso_suzuki_toyota" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Denso Powertrain (Maruti Suzuki K-Series Petrol / Toyota Innova Fortuner D-4D)",
                Category = "Denso Systems",
                EcuFamily = "Denso 32-bit ECU",
                Manufacturer = "Maruti Suzuki / Toyota",
                Description = "Denso petrol & diesel powertrain networks. Covers Maruti Suzuki K-Series (K10B, K12M, K12N DualJet, K15B, K15C: Swift, Baleno, Dzire, Brezza, Ertiga, Ciaz) and Toyota Innova / Fortuner (1KD/2GD D-4D). VVT Cam Angle, Fuel Trim STFT/LTFT, MAP, Coolant.",
                KeySignals = new() { "EngineRPM", "VvtCamAngleIntake", "ShortTermFuelTrim_STFT", "LongTermFuelTrim_LTFT", "CoolantTemperature", "ManifoldAbsolutePressure_MAP" }
            },
            "siemens_continental_sid_pcr" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Siemens & Continental (Renault Duster 1.5 dCi / VW Polo 1.6 TDI PCR2.1 / Ford TDCi)",
                Category = "Siemens & Continental",
                EcuFamily = "Continental SID305 / SID307 / PCR2.1",
                Manufacturer = "Renault-Nissan / Volkswagen / Ford",
                Description = "Continental SID305/SID307/SID310 in Renault Duster / Nissan Terrano 1.5 dCi K9K, Continental PCR2.1 in VW Polo/Vento 1.2/1.5/1.6 TDI, Ford Figo/EcoSport 1.5 TDCi. Turbo boost, injection quantity, rail pressure, DPF delta.",
                KeySignals = new() { "EngineRPM", "RailPressureActual", "TurboBoostPressure", "InjectionQuantityMain", "CoolantTemperature", "DpfPressureDelta" }
            },
            "tata_motors_passenger" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Tata Motors Passenger Architecture (Safari / Harrier / Nexon / Altroz / Tiago)",
                Category = "Indian OEM Specific",
                EcuFamily = "Tata Motors EMS & Body Network",
                Manufacturer = "Tata Motors",
                Description = "Dedicated CAN network matrix for Tata Motors passenger fleet. Multi-drive modes (City, Eco, Sport, Rough Road on 0x114), Common Rail Pressure (0x324), ABS wheel speeds (0x214), DEF AdBlue & DPF soot (0x514).",
                KeySignals = new() { "EngineRPM", "DriveModeSelected", "CommonRailPressure", "TurboBoostPressure", "VehicleSpeed", "DefAdBlueLevel" }
            },
            "mahindra_passenger" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Mahindra Passenger Platform (Scorpio-N / Thar 4x4 / XUV700 / XUV300)",
                Category = "Indian OEM Specific",
                EcuFamily = "Mahindra Powertrain & BCM",
                Manufacturer = "Mahindra & Mahindra",
                Description = "Mahindra passenger vehicle network matrix. Reverse-engineered Scorpio-N BCM door and tailgate statuses (0x7ED / DID 0x037B), 4WD transfer case mode (2H/4H/4L) & differential lock (0x220), mHawk rail pressure (0x320), DEF level (0x520).",
                KeySignals = new() { "EngineRPM", "FourWheelDriveMode", "RearDiffLockEngaged", "RailPressure", "FrontRightDoorOpen", "AdBlueLevel" }
            },
            "sae_j1939_commercial" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "SAE J1939 Heavy Duty (Tata Prima / Ashok Leyland / Eicher / BharatBenz)",
                Category = "Commercial Heavy Duty",
                EcuFamily = "SAE J1939 Commercial Vehicle",
                Manufacturer = "Tata Trucks / Ashok Leyland / Eicher / BharatBenz",
                Protocol = "J1939 CAN (250/500 kbps, 29-bit)",
                Description = "Standardized SAE J1939 29-bit CAN database for Indian heavy commercial vehicles, buses, and industrial machinery. Electronic Engine Controller EEC1 (PGN 61444), Cruise & Speed CCVS (PGN 65265), Engine Temperature ET1 (PGN 65262), Fluid Level (PGN 65276).",
                KeySignals = new() { "Engine_Speed", "Driver_Demand_Torque", "Actual_Engine_Torque", "Engine_Coolant_Temp", "Wheel_Based_Vehicle_Speed", "Fuel_Level" }
            },
            "hyundai_kia_ccan" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Hyundai & Kia India C-CAN (Creta / Seltos / i20 / Venue / Verna)",
                Category = "Global Platforms in India",
                EcuFamily = "Hyundai-Kia C-CAN",
                Manufacturer = "Hyundai Motor India / Kia India",
                Description = "High-speed powertrain C-CAN matrix from open-source opendbc. Full coverage for Hyundai Creta, Venue, i20, Verna, Tucson and Kia Seltos, Sonet, Carens. Steering angle, wheel speeds, engine RPM, cruise control, brake pressure.",
                KeySignals = new() { "CR_Ems_EngSpd", "WHL_SPD_FL", "WHL_SPD_FR", "SAS_Angle", "CYL_PRES", "ACCELERATOR_PEDAL" }
            },
            "vw_skoda_pq25_edc17" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Volkswagen & Skoda PQ25/PQ35 (Polo / Vento / Rapid / Ameo TDI & TSI)",
                Category = "Global Platforms in India",
                EcuFamily = "VW PQ Powertrain",
                Manufacturer = "Volkswagen India / Skoda Auto India",
                Description = "VW PQ platform CAN database covering Polo 1.2/1.5/1.6 TDI, Vento, Ameo, Skoda Rapid. Motor 1 (0x280) RPM & temperature, Motor 2 (0x288) torque & pedal, Motor 3 (0x380), ABS 1 (0x1A0) wheel speeds.",
                KeySignals = new() { "MO_Drehzahl", "MO_Kuehlmitteltemperatur", "MO_Fahrpedalstellung", "KBI_km_Stand", "ESP_VL_Radgeschw", "ESP_VR_Radgeschw" }
            },
            "vw_skoda_mqb_india" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Volkswagen & Skoda MQB India 2.0 (Taigun / Kushaq / Slavia / Virtus)",
                Category = "Global Platforms in India",
                EcuFamily = "VW MQB Architecture",
                Manufacturer = "Skoda Auto Volkswagen India",
                Description = "Modern MQB-A0-IN platform CAN network for Taigun, Kushaq, Slavia, Virtus 1.0 TSI & 1.5 TSI. Motor 20 (0x120), ESP 21 (0xFD), Airbag, steering angle, turbo boost, electronic differential lock.",
                KeySignals = new() { "MO_Drehzahl", "MO_Fahrpedalstellung", "ESP_v_Signal", "KBI_km_Stand", "LWI_Lenkradwinkel", "MO_Kuehlmitteltemp" }
            },
            "toyota_innova_fortuner_pt" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Toyota Powertrain (Innova / Fortuner / Glanza / Urban Cruiser Hyryder)",
                Category = "Global Platforms in India",
                EcuFamily = "Toyota Ref Powertrain",
                Manufacturer = "Toyota Kirloskar Motor",
                Description = "Toyota primary powertrain CAN definitions. Covers Innova Crysta, Fortuner, Glanza, Urban Cruiser, and Toyota hybrids. Engine RPM, vehicle speed, steering angle, accelerator pedal position, brake pressure.",
                KeySignals = new() { "ENGINE_RPM", "VEHICLE_SPEED", "STEER_ANGLE", "GAS_PEDAL", "BRAKE_PRESSURE", "WHEEL_SPEED_FR" }
            },
            "ford_india_cgea_pt" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Ford India Powertrain (Figo / EcoSport / Aspire / Endeavour)",
                Category = "Global Platforms in India",
                EcuFamily = "Ford CGEA 1.2 PTCAN",
                Manufacturer = "Ford India",
                Description = "Ford CGEA 1.2 Powertrain CAN definitions. Figo, EcoSport, Aspire 1.5 TDCi & 1.5 Ti-VCT, Endeavour 2.0/3.2 Panther. Engine speed, driver torque demand, actual torque, vehicle speed, brake state.",
                KeySignals = new() { "EngAout_N_Actl", "Veh_V_ActlEng", "EngBout_Nd_Actl", "ApedPos_Pc_ActlArb", "BrkAct_B_Actl", "WhlRotDrctnl_No_Cnt" }
            },
            "mg_hector_astor" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "MG Motor India (Hector / Astor / ZS EV)",
                Category = "Global Platforms in India",
                EcuFamily = "MG Powertrain & ADAS",
                Manufacturer = "MG Motor India",
                Description = "MG vehicle CAN bus definitions covering MG Hector (1.5 Turbo / 2.0 Multijet diesel), Astor, and ZS EV. Powertrain RPM, speed, ADAS warnings, chassis status.",
                KeySignals = new() { "Engine_Speed", "Vehicle_Speed", "Throttle_Position", "Steering_Angle", "Brake_Pedal_Status", "Gear_Position" }
            },
            "universal_obd2_live_data" => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = "Universal ISO 15765-4 OBD-II / UDS Broadcast (All Indian Vehicles)",
                Category = "Standard Diagnostics",
                EcuFamily = "ISO 15765-4 OBD-II",
                Manufacturer = "Universal Standard",
                Description = "Standard OBD-II Parameter ID (PID) live stream receiver for CAN ID 0x7E8. Provides universal fallback decoding of RPM, Vehicle Speed, Coolant Temp, Throttle, Fuel Rail Pressure, MAP, MAF, and ECU Voltage for ANY OBD-II compliant vehicle.",
                KeySignals = new() { "EngineRPM", "VehicleSpeed", "CoolantTemperature", "ThrottlePosition", "FuelRailPressure", "ControlModuleVoltage" }
            },
            _ => new DbcLibraryItemDto
            {
                Key = key,
                FileName = fileName,
                Title = fileName,
                Category = "Custom DBC Files",
                EcuFamily = "Generic",
                Manufacturer = "Automotive",
                Description = $"Custom database file: {fileName}"
            }
        };
    }
}
