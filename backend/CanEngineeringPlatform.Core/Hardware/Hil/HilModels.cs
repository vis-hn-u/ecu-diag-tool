using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Hardware.Hil;

public enum HilSignalType
{
    ClassicCan,
    CanFd,
    Lin,
    KLine,
    AutomotiveEthernet,
    DoIp,
    AnalogInput,
    AnalogOutput,
    DigitalInput,
    DigitalOutput,
    PwmInput,
    PwmOutput,
    PowerSupplyControl,
    RelayFaultInjection
}

public enum HilChannelState
{
    Inactive,
    Active,
    Simulating,
    ShortToGround,
    ShortToBattery,
    OpenCircuit,
    Faulted
}

public class HilChannelDescriptor
{
    public string ChannelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HilSignalType SignalType { get; set; }
    public HilChannelState State { get; set; } = HilChannelState.Inactive;
    public double CurrentValue { get; set; }
    public double TargetValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double MinAllowed { get; set; }
    public double MaxAllowed { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class HilTestFixture
{
    public string FixtureId { get; set; } = "HIL-BENCH-FIXTURE-01";
    public string Name { get; set; } = "Universal Automotive HIL Bench Fixture";
    public bool IsOnline { get; set; } = true;
    public double MainSupplyVoltage { get; set; } = 13.8;
    public double TotalCurrentAmps { get; set; } = 2.45;
    public bool IgnitionRelayActive { get; set; } = true;
    public bool Terminal30Active { get; set; } = true;
    public bool Terminal15Active { get; set; } = true;
    public List<HilChannelDescriptor> Channels { get; set; } = new();
}

public class HilFaultInjectionCommand
{
    public string ChannelId { get; set; } = string.Empty;
    public HilChannelState InjectedState { get; set; } = HilChannelState.Active;
    public double OverrideValue { get; set; }
}

public interface IHilService
{
    HilTestFixture GetFixtureStatus();
    void SetChannelValue(string channelId, double value);
    void InjectChannelFault(HilFaultInjectionCommand command);
    void SetIgnition(bool terminal15State);
    void SetMainPower(double voltage, bool enabled);
    void ResetAllChannels();
}

public class HilService : IHilService
{
    private readonly HilTestFixture _fixture = new();
    private readonly object _lock = new();

    public HilService()
    {
        InitializeDefaultChannels();
    }

    private void InitializeDefaultChannels()
    {
        _fixture.Channels = new List<HilChannelDescriptor>
        {
            new() { ChannelId = "PWR_MAIN", Name = "Terminal 30 Constant 12V", SignalType = HilSignalType.PowerSupplyControl, State = HilChannelState.Active, CurrentValue = 13.8, Unit = "V", MinAllowed = 8.0, MaxAllowed = 16.5, Description = "ECU permanent battery feed" },
            new() { ChannelId = "PWR_IGN", Name = "Terminal 15 Switched Ignition", SignalType = HilSignalType.PowerSupplyControl, State = HilChannelState.Active, CurrentValue = 13.8, Unit = "V", MinAllowed = 0.0, MaxAllowed = 16.5, Description = "Ignition switch relay feed" },
            new() { ChannelId = "CAN1_BUS", Name = "Primary Powertrain CAN", SignalType = HilSignalType.ClassicCan, State = HilChannelState.Active, CurrentValue = 500000, Unit = "bps", Description = "500 kbps high speed CAN" },
            new() { ChannelId = "CANFD1_BUS", Name = "Body / ADAS CAN FD", SignalType = HilSignalType.CanFd, State = HilChannelState.Active, CurrentValue = 2000000, Unit = "bps", Description = "CAN FD 500k nominal / 2M data" },
            new() { ChannelId = "LIN1_BUS", Name = "Alternator / Lin Bus", SignalType = HilSignalType.Lin, State = HilChannelState.Inactive, CurrentValue = 19200, Unit = "bps", Description = "LIN master/slave channel" },
            new() { ChannelId = "AIN_CRANK", Name = "Crankshaft Position Sensor (VR/Hall)", SignalType = HilSignalType.AnalogOutput, State = HilChannelState.Simulating, CurrentValue = 850, Unit = "RPM", MinAllowed = 0, MaxAllowed = 9000, Description = "60-2 tooth trigger wheel generator" },
            new() { ChannelId = "AIN_PEDAL1", Name = "Accelerator Pedal Position 1", SignalType = HilSignalType.AnalogOutput, State = HilChannelState.Simulating, CurrentValue = 0.75, Unit = "V", MinAllowed = 0.5, MaxAllowed = 4.8, Description = "Potentiometer 1 throttle command" },
            new() { ChannelId = "AIN_PEDAL2", Name = "Accelerator Pedal Position 2", SignalType = HilSignalType.AnalogOutput, State = HilChannelState.Simulating, CurrentValue = 0.38, Unit = "V", MinAllowed = 0.25, MaxAllowed = 2.4, Description = "Potentiometer 2 throttle command (half slope)" },
            new() { ChannelId = "DOUT_FUEL_PUMP", Name = "Fuel Pump Relay Feedback", SignalType = HilSignalType.DigitalInput, State = HilChannelState.Active, CurrentValue = 1.0, Unit = "Bool", Description = "ECU low-side drive feedback" },
            new() { ChannelId = "PWM_BOOST_SOLENOID", Name = "N75 Wastegate Duty Cycle", SignalType = HilSignalType.PwmInput, State = HilChannelState.Active, CurrentValue = 35.0, Unit = "%", MinAllowed = 0, MaxAllowed = 100, Description = "Turbocharger boost control solenoid" }
        };
    }

    public HilTestFixture GetFixtureStatus()
    {
        lock (_lock)
        {
            return new HilTestFixture
            {
                FixtureId = _fixture.FixtureId,
                Name = _fixture.Name,
                IsOnline = _fixture.IsOnline,
                MainSupplyVoltage = _fixture.MainSupplyVoltage,
                TotalCurrentAmps = _fixture.TotalCurrentAmps,
                IgnitionRelayActive = _fixture.IgnitionRelayActive,
                Terminal30Active = _fixture.Terminal30Active,
                Terminal15Active = _fixture.Terminal15Active,
                Channels = _fixture.Channels.Select(c => new HilChannelDescriptor
                {
                    ChannelId = c.ChannelId,
                    Name = c.Name,
                    SignalType = c.SignalType,
                    State = c.State,
                    CurrentValue = c.CurrentValue,
                    TargetValue = c.TargetValue,
                    Unit = c.Unit,
                    MinAllowed = c.MinAllowed,
                    MaxAllowed = c.MaxAllowed,
                    Description = c.Description
                }).ToList()
            };
        }
    }

    public void SetChannelValue(string channelId, double value)
    {
        lock (_lock)
        {
            var ch = _fixture.Channels.FirstOrDefault(c => c.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase));
            if (ch != null)
            {
                ch.TargetValue = value;
                ch.CurrentValue = value;
            }
        }
    }

    public void InjectChannelFault(HilFaultInjectionCommand command)
    {
        if (command == null) return;
        lock (_lock)
        {
            var ch = _fixture.Channels.FirstOrDefault(c => c.ChannelId.Equals(command.ChannelId, StringComparison.OrdinalIgnoreCase));
            if (ch != null)
            {
                ch.State = command.InjectedState;
                ch.CurrentValue = command.OverrideValue;
            }
        }
    }

    public void SetIgnition(bool terminal15State)
    {
        lock (_lock)
        {
            _fixture.Terminal15Active = terminal15State;
            _fixture.IgnitionRelayActive = terminal15State;
            var ch = _fixture.Channels.FirstOrDefault(c => c.ChannelId == "PWR_IGN");
            if (ch != null)
            {
                ch.CurrentValue = terminal15State ? _fixture.MainSupplyVoltage : 0.0;
                ch.State = terminal15State ? HilChannelState.Active : HilChannelState.Inactive;
            }
        }
    }

    public void SetMainPower(double voltage, bool enabled)
    {
        lock (_lock)
        {
            _fixture.Terminal30Active = enabled;
            _fixture.MainSupplyVoltage = enabled ? Math.Clamp(voltage, 0.0, 24.0) : 0.0;
            var ch30 = _fixture.Channels.FirstOrDefault(c => c.ChannelId == "PWR_MAIN");
            if (ch30 != null)
            {
                ch30.CurrentValue = _fixture.MainSupplyVoltage;
                ch30.State = enabled ? HilChannelState.Active : HilChannelState.Inactive;
            }
        }
    }

    public void ResetAllChannels()
    {
        lock (_lock)
        {
            _fixture.MainSupplyVoltage = 13.8;
            _fixture.Terminal30Active = true;
            _fixture.Terminal15Active = true;
            _fixture.IgnitionRelayActive = true;
            InitializeDefaultChannels();
        }
    }
}
