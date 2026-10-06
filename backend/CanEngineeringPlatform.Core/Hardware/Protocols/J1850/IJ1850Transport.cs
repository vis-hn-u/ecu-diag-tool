using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.J1850;

/// <summary>
/// Abstraction for SAE J1850 (VPW 10.4 kbps / PWM 41.6 kbps) hardware adapters.
/// </summary>
public interface IJ1850Transport : IAutomotiveTransport
{
    new J1850Capabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => Capabilities.SupportsVpw ? AutomotiveProtocol.J1850Vpw : AutomotiveProtocol.J1850Pwm;

    ValueTask<bool> SendFrameAsync(J1850Frame frame, CancellationToken cancellationToken = default);

    event Action<J1850Frame>? OnJ1850FrameReceived;
}
