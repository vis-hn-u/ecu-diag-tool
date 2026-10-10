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
    event Action<CanFrame>? OnFlowControlRequired;
}

public class IsoTpRouter : IIsoTpRouter
{
    private readonly ConcurrentDictionary<uint, IsoTpSession> _sessions = new();
    private readonly IsoTpConfig _config;

    public event Action<IsoTpMessage>? OnMessageReceived;
    public event Action<string>? OnTransportError;
    public event Action<CanFrame>? OnFlowControlRequired;

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
            session.OnFlowControlRequired += fc => OnFlowControlRequired?.Invoke(fc);
            return session;
        });
    }

    public bool RouteCanFrame(CanFrame frame, out IsoTpMessage? completedMessage)
    {
        completedMessage = null;
        uint cleanId = frame.Id & 0x1FFFFFFFU;

        if (!_sessions.TryGetValue(cleanId, out var session))
        {
            // Only auto-register standard diagnostic address ranges (UDS, OBD, KWP2000, 29-bit UDS)
            bool isDiagnosticId = (cleanId >= 0x7E0 && cleanId <= 0x7EF) ||
                                  cleanId == 0x7DF ||
                                  (cleanId & 0xFFFF0000) == 0x18DA0000 ||
                                  (cleanId & 0xFFFF0000) == 0x18DB0000;
            if (!isDiagnosticId)
            {
                return false;
            }

            uint defaultTx = 0;
            if (cleanId >= 0x7E8 && cleanId <= 0x7EF)
            {
                defaultTx = cleanId - 8;
            }
            else if (cleanId >= 0x7E0 && cleanId <= 0x7E7)
            {
                defaultTx = cleanId + 8;
            }
            else if ((cleanId & 0xFFFF0000) == 0x18DA0000)
            {
                byte target = (byte)((cleanId >> 8) & 0xFF);
                byte source = (byte)(cleanId & 0xFF);
                defaultTx = 0x18DA0000U | ((uint)source << 8) | (uint)target;
            }

            session = _sessions.GetOrAdd(cleanId, id =>
            {
                var newSession = new IsoTpSession(cleanId, defaultTx, _config);
                newSession.OnMessageReceived += msg => OnMessageReceived?.Invoke(msg);
                newSession.OnTransportError += err => OnTransportError?.Invoke(err);
                newSession.OnFlowControlRequired += fc => OnFlowControlRequired?.Invoke(fc);
                return newSession;
            });
        }

        return session.ProcessIncomingCanFrame(frame, out completedMessage);
    }

    public List<CanFrame> SegmentMessage(byte[] payload, uint txCanId, uint timestampUs = 0)
    {
        var session = _sessions.GetOrAdd(txCanId, id =>
        {
            uint defaultRx = 0;
            if (txCanId >= 0x7E0 && txCanId <= 0x7E7) defaultRx = txCanId + 8;
            else if (txCanId == 0x7DF) defaultRx = 0x7E8;

            var newSession = new IsoTpSession(defaultRx, txCanId, _config);
            newSession.OnMessageReceived += msg => OnMessageReceived?.Invoke(msg);
            newSession.OnTransportError += err => OnTransportError?.Invoke(err);
            newSession.OnFlowControlRequired += fc => OnFlowControlRequired?.Invoke(fc);
            return newSession;
        });
        return session.SegmentMessage(payload, txCanId, timestampUs);
    }
}
