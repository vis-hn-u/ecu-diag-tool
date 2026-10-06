using System;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Images;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;
using CanEngineeringPlatform.Core.EcuEngineering.Protocols;
using CanEngineeringPlatform.Core.EcuEngineering.Reading;

namespace CanEngineeringPlatform.Core.EcuEngineering.Core;

/// <summary>
/// Unified ECU Engineering Provider orchestrating Identification, Protocol Discovery,
/// Compatibility, Memory Discovery, Safe Reading, and Image Management.
/// </summary>
public interface IEcuEngineeringProvider
{
    IEcuIdentificationService Identification { get; }
    IProtocolDiscoveryService ProtocolDiscovery { get; }
    IEcuCompatibilityManager Compatibility { get; }
    IMemoryDiscoveryService MemoryDiscovery { get; }
    IEcuMemoryReader? MemoryReader { get; }
    IEcuMemoryImageStore ImageStore { get; }
}

public class EcuEngineeringProvider : IEcuEngineeringProvider
{
    public IEcuIdentificationService Identification { get; }
    public IProtocolDiscoveryService ProtocolDiscovery { get; }
    public IEcuCompatibilityManager Compatibility { get; }
    public IMemoryDiscoveryService MemoryDiscovery { get; }
    public IEcuMemoryReader? MemoryReader { get; }
    public IEcuMemoryImageStore ImageStore { get; }

    public EcuEngineeringProvider(
        IEcuIdentificationService identification,
        IProtocolDiscoveryService protocolDiscovery,
        IEcuCompatibilityManager compatibility,
        IMemoryDiscoveryService memoryDiscovery,
        IEcuMemoryReader memoryReader,
        IEcuMemoryImageStore imageStore)
    {
        Identification = identification ?? throw new ArgumentNullException(nameof(identification));
        ProtocolDiscovery = protocolDiscovery ?? throw new ArgumentNullException(nameof(protocolDiscovery));
        Compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
        MemoryDiscovery = memoryDiscovery ?? throw new ArgumentNullException(nameof(memoryDiscovery));
        MemoryReader = memoryReader ?? throw new ArgumentNullException(nameof(memoryReader));
        ImageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
    }

    public EcuEngineeringProvider(
        IEcuIdentificationService identification,
        IProtocolDiscoveryService protocolDiscovery,
        IEcuCompatibilityManager compatibility)
    {
        Identification = identification ?? throw new ArgumentNullException(nameof(identification));
        ProtocolDiscovery = protocolDiscovery ?? throw new ArgumentNullException(nameof(protocolDiscovery));
        Compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
        MemoryDiscovery = new MemoryDiscoveryService(compatibility);
        ImageStore = new EcuMemoryImageStore();
    }
}
