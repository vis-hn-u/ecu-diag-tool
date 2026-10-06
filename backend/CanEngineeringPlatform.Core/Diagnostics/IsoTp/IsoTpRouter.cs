using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Diagnostics.IsoTp;

public interface IIsoTpRouter
{
    void RegisterChannel(uint rxCanId, uint txCanId);
    bool RouteCanFrame(CanFrame frame, out IsoTpMessage? completedMessage);
    List<CanFrame> SegmentMessage(byte[] payload, uint txCanId, uint timestampUs = 0);
    event Action<IsoTpMessage>? OnMessageReceived;
    event Action<string>? OnTransportError;
}

public class IsoTpRouter : IIsoTpRouter
{
    private readonly ConcurrentDictionary<uint, IsoTpSession> _sessions = new();
    private readonly IsoTpConfig _config;

    public event Action<IsoTpMessage>? OnMessageReceived;
    public event Action<string>? OnTransportError;

    public IsoTpRouter(IsoTpConfig? config = null)
    {
        _config = config ?? new IsoTpConfig();

        // Pre-register standard automotive diagnostic channels:
        // Broadcast: 0x7DF -> 0x7E8..0x7EF
        // ECM: Req 0x7E0, Resp 0x7E8
        // TCM/ABS: Req 0x7E1, Resp 0x7E9
        // BCM: Req 0x7E2, Resp 0x7EA
        // SRS/Airbag: Req 0x7E3, Resp 0x7EB
        // Cluster: Req 0x7E4, Resp 0x7EC
        RegisterChannel(0x7E8, 0x7E0);
        RegisterChannel(0x7E9, 0x7E1);
        RegisterChannel(0x7EA, 0x7E2);
        RegisterChannel(0x7EB, 0x7E3);
        RegisterChannel(0x7EC, 0x7E4);
        RegisterChannel(0x7E0, 0x7E8);
        RegisterChannel(0x7E1, 0x7E9);
        RegisterChannel(0x7E2, 0x7EA);
        RegisterChannel(0x7DF, 0x7E8);
    }

    public void RegisterChannel(uint rxCanId, uint txCanId)
    {
        _sessions.GetOrAdd(rxCanId, id =>
        {
            var session = new IsoTpSession(rxCanId, txCanId, _config);
            session.OnMessageReceived += msg => OnMessageReceived?.Invoke(msg);
            session.OnTransportError += err => OnTransportError?.Invoke(err);
            return session;
        });
    }

    public bool RouteCanFrame(CanFrame frame, out IsoTpMessage? completedMessage)
    {
        completedMessage = null;
        uint cleanId = frame.Id & 0x1FFFFFFFU;

        // Auto-register session if not already registered
        var session = _sessions.GetOrAdd(cleanId, id =>
        {
            var newSession = new IsoTpSession(cleanId, 0, _config);
            newSession.OnMessageReceived += msg => OnMessageReceived?.Invoke(msg);
            newSession.OnTransportError += err => OnTransportError?.Invoke(err);
            return newSession;
        });

        return session.ProcessIncomingCanFrame(frame, out completedMessage);
    }

    public List<CanFrame> SegmentMessage(byte[] payload, uint txCanId, uint timestampUs = 0)
    {
        var session = _sessions.GetOrAdd(txCanId, id => new IsoTpSession(id, 0, _config));
        return session.SegmentMessage(payload, txCanId, timestampUs);
    }
}
