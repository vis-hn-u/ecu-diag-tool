using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CanEngineeringPlatform.Core.Dbc;

namespace CanEngineeringPlatform.Core.Signals;

public enum SignalDataSource
{
    CanMessage, // Broadcast CAN/CAN-FD frame
    ObdPid,     // SAE J1979 / ISO 15031-5 Mode 01/02 PID
    UdsDid,     // ISO 14229-1 Service 0x22 ReadDataByIdentifier
    Kwp2000,    // ISO 14230 Service 0x21 ReadDataByLocalIdentifier
    VagBlock,   // VAG Measuring Value Block (MWB / Group)
    Calculated  // Derived mathematical expression
}

public enum SignalEndianness
{
    LittleEndian, // Intel format (LSB first)
    BigEndian     // Motorola format (MSB first)
}

public class CustomSignalDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // Powertrain, Thermal, Dynamics, Fuel, Diagnostics
    public SignalDataSource SourceType { get; set; } = SignalDataSource.CanMessage;
    public uint SourceIdentifier { get; set; } // CAN ID (e.g. 0x280), OBD PID (0x0C), UDS DID (0xF190), VAG Block (0x003)
    
    public int StartBit { get; set; } = 0;
    public int BitLength { get; set; } = 16;
    public int ByteOffset { get; set; } = 0;
    public SignalEndianness ByteOrder { get; set; } = SignalEndianness.LittleEndian;
    public bool IsSigned { get; set; } = false;

    public double Factor { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string Units { get; set; } = string.Empty;
    public double? MinLimit { get; set; }
    public double? MaxLimit { get; set; }

    /// <summary>
    /// Optional algebraic equation using variable 'raw' or 'x'. E.g. "(raw * 0.25) - 40"
    /// </summary>
    public string? CustomEquation { get; set; }

    public double Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty) return 0.0;

        // Apply byte offset if specified
        ReadOnlySpan<byte> slice = (ByteOffset > 0 && ByteOffset < payload.Length)
            ? payload.Slice(ByteOffset)
            : payload;

        bool isLittle = (ByteOrder == SignalEndianness.LittleEndian);
        ulong rawBits = SignalDecoder.ExtractRawBits(slice, StartBit, BitLength, isLittle);

        double engineeringValue;
        if (!string.IsNullOrWhiteSpace(CustomEquation))
        {
            engineeringValue = EvaluateSimpleEquation(CustomEquation, (double)rawBits);
        }
        else
        {
            engineeringValue = SignalDecoder.DecodeRawToEngineering(rawBits, BitLength, IsSigned, Factor, Offset);
        }

        if (MinLimit.HasValue && engineeringValue < MinLimit.Value) engineeringValue = MinLimit.Value;
        if (MaxLimit.HasValue && engineeringValue > MaxLimit.Value) engineeringValue = MaxLimit.Value;

        return engineeringValue;
    }

    private static double EvaluateSimpleEquation(string equation, double rawVal)
    {
        try
        {
            // Simple linear expression evaluator supporting basic tokens
            string expr = equation.Replace("raw", rawVal.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                                  .Replace("x", rawVal.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

            // Compute using DataTable.Compute equivalent arithmetic or basic tokenization
            return ExpressionTokenEvaluator.EvaluateArithmetic(expr);
        }
        catch
        {
            return rawVal;
        }
    }
}

public enum ParameterConfidence
{
    Unverified = 1,      // 0.2 confidence - Guess or inferred
    Estimated = 2,       // 0.5 confidence - Standard OBD-II or calculated
    Observed = 3,        // 0.8 confidence - Verified CAN bus reverse engineering
    FactoryCalibrated = 4 // 1.0 confidence - OEM DBC, A2L/ASAP2, or ECU flash map verified
}

public class ParameterMetadata
{
    public string ParameterId { get; set; } = string.Empty;
    public string StandardLabel { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "Powertrain";
    public SignalDataSource PrimarySource { get; set; } = SignalDataSource.CanMessage;
    public ParameterConfidence Confidence { get; set; } = ParameterConfidence.Observed;
    public double ConfidenceScore => (int)Confidence / 4.0;
    public string SourceDetails { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public double ExpectedFrequencyHz { get; set; } = 50.0;
    public DateTime? LastUpdatedUtc { get; set; }
}

/// <summary>
/// Lightweight arithmetic evaluator for inline custom signal equations without external dependencies.
/// </summary>
internal static class ExpressionTokenEvaluator
{
    public static double EvaluateArithmetic(string expr)
    {
        expr = expr.Replace(" ", "");
        int pos = 0;
        return ParseExpression(expr, ref pos);
    }

    private static double ParseExpression(string s, ref int pos)
    {
        double left = ParseTerm(s, ref pos);
        while (pos < s.Length)
        {
            char op = s[pos];
            if (op != '+' && op != '-') break;
            pos++;
            double right = ParseTerm(s, ref pos);
            if (op == '+') left += right;
            else left -= right;
        }
        return left;
    }

    private static double ParseTerm(string s, ref int pos)
    {
        double left = ParseFactor(s, ref pos);
        while (pos < s.Length)
        {
            char op = s[pos];
            if (op != '*' && op != '/') break;
            pos++;
            double right = ParseFactor(s, ref pos);
            if (op == '*') left *= right;
            else left = (right == 0.0) ? 0.0 : left / right;
        }
        return left;
    }

    private static double ParseFactor(string s, ref int pos)
    {
        if (pos >= s.Length) return 0.0;

        if (s[pos] == '-')
        {
            pos++;
            return -ParseFactor(s, ref pos);
        }
        if (s[pos] == '+')
        {
            pos++;
            return ParseFactor(s, ref pos);
        }

        if (s[pos] == '(')
        {
            pos++; // consume '('
            double val = ParseExpression(s, ref pos);
            if (pos < s.Length && s[pos] == ')') pos++; // consume ')'
            return val;
        }

        int start = pos;
        while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.'))
        {
            pos++;
        }

        if (start == pos) return 0.0;
        string numStr = s.Substring(start, pos - start);
        return double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0.0;
    }
}
