using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Dbc;

public static class SignalDecoder
{
    public static ulong ExtractRawBits(ReadOnlySpan<byte> data, int startBit, int length, bool isLittleEndian)
    {
        if (length <= 0 || length > 64)
        {
            return 0;
        }

        ulong result = 0;
        if (isLittleEndian)
        {
            for (int i = 0; i < length; i++)
            {
                int bitPos = startBit + i;
                int byteIndex = bitPos / 8;
                int bitInByte = bitPos % 8;
                if (byteIndex < data.Length)
                {
                    ulong bitVal = (ulong)((data[byteIndex] >> bitInByte) & 1);
                    result |= (bitVal << i);
                }
            }
        }
        else
        {
            // Standard CAN DBC Motorola format (StartBit is MSB)
            int currentByte = startBit / 8;
            int currentBit = startBit % 8;
            for (int i = 0; i < length; i++)
            {
                if (currentByte < data.Length)
                {
                    ulong bitVal = (ulong)((data[currentByte] >> currentBit) & 1);
                    result = (result << 1) | bitVal;
                }
                currentBit--;
                if (currentBit < 0)
                {
                    currentBit = 7;
                    currentByte++;
                }
            }
        }

        return result;
    }

    public static double DecodeRawToEngineering(ulong rawBits, int length, bool isSigned, double factor, double offset)
    {
        double rawNumber;
        if (isSigned && length < 64 && (rawBits & (1UL << (length - 1))) != 0)
        {
            ulong mask = ulong.MaxValue << length;
            long signedVal = (long)(rawBits | mask);
            rawNumber = signedVal;
        }
        else
        {
            rawNumber = rawBits;
        }

        return (rawNumber * factor) + offset;
    }

    public static List<DecodedSignal> DecodeFrame(in CanFrame frame, DbcMessage message)
    {
        var decoded = new List<DecodedSignal>(message.Signals.Count);
        int? activeMux = null;

        // Pass 1: Find multiplexer switch if present
        foreach (var sig in message.Signals)
        {
            if (sig.MultiplexerType == MultiplexerType.MultiplexerSwitch)
            {
                ulong rawBits = ExtractRawBits(frame.Data.AsSpan(0, frame.ActualLength), sig.StartBit, sig.Length, sig.IsLittleEndian);
                activeMux = (int)rawBits;
                double engVal = DecodeRawToEngineering(rawBits, sig.Length, sig.IsSigned, sig.Factor, sig.Offset);
                string? desc = sig.ValueTable.TryGetValue(activeMux.Value, out var valDesc) ? valDesc : null;

                decoded.Add(new DecodedSignal
                {
                    SignalName = sig.Name,
                    MessageName = message.Name,
                    CanId = frame.Id,
                    RawValue = rawBits,
                    EngineeringValue = engVal,
                    Unit = sig.Unit,
                    TimestampUs = frame.TimestampUs,
                    TransmitterNode = message.Transmitter,
                    ReceiverNodes = sig.Receivers,
                    ValueDescription = desc
                });
                break;
            }
        }

        // Pass 2: Decode all regular or active multiplexed signals
        foreach (var sig in message.Signals)
        {
            if (sig.MultiplexerType == MultiplexerType.MultiplexerSwitch)
            {
                continue; // already decoded in pass 1
            }

            if (sig.MultiplexerType == MultiplexerType.Multiplexed)
            {
                if (!activeMux.HasValue || sig.MultiplexerValue != activeMux.Value)
                {
                    continue; // Inactive multiplexed signal
                }
            }

            ulong rawBits = ExtractRawBits(frame.Data.AsSpan(0, frame.ActualLength), sig.StartBit, sig.Length, sig.IsLittleEndian);
            double engVal = DecodeRawToEngineering(rawBits, sig.Length, sig.IsSigned, sig.Factor, sig.Offset);
            string? desc = null;
            if (sig.ValueTable.Count > 0)
            {
                int intKey = (int)Math.Round(engVal);
                if (sig.ValueTable.TryGetValue(intKey, out var valDesc))
                {
                    desc = valDesc;
                }
            }

            decoded.Add(new DecodedSignal
            {
                SignalName = sig.Name,
                MessageName = message.Name,
                CanId = frame.Id,
                RawValue = rawBits,
                EngineeringValue = engVal,
                Unit = sig.Unit,
                TimestampUs = frame.TimestampUs,
                TransmitterNode = message.Transmitter,
                ReceiverNodes = sig.Receivers,
                ValueDescription = desc
            });
        }

        return decoded;
    }
}
