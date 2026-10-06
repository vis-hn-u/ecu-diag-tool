using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Hardware.Automotive;

/// <summary>
/// Hardware manager managing multi-protocol automotive physical and virtual adapters.
/// Extends IHardwareManager to support LIN, K-Line, KWP2000, J1850, Ethernet, DoIP, FlexRay, J1939, and OEM adapters.
/// </summary>
public interface IAutomotiveHardwareManager : IHardwareManager
{
    IReadOnlyList<IAutomotiveTransport> GetAllAutomotiveTransports();
    IReadOnlyList<TTransport> GetTransportsByProtocol<TTransport>(AutomotiveProtocol protocol) where TTransport : class, IAutomotiveTransport;
    IAutomotiveTransport? GetAutomotiveTransport(string transportId);
    void RegisterAutomotiveTransport(IAutomotiveTransport transport);

    event Action<IAutomotiveTransport>? OnAutomotiveTransportRegistered;
}
