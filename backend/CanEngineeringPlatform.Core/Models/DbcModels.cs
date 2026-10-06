using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Models;

public enum MultiplexerType
{
    None,
    MultiplexerSwitch,
    Multiplexed
}

public class DbcSignal
{
    public string Name { get; set; } = string.Empty;
    public int StartBit { get; set; }
    public int Length { get; set; }
    public bool IsLittleEndian { get; set; } = true; // Intel = true (1), Motorola = false (0)
    public bool IsSigned { get; set; }
    public double Factor { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public string Unit { get; set; } = string.Empty;
    public List<string> Receivers { get; set; } = new();
    public MultiplexerType MultiplexerType { get; set; } = MultiplexerType.None;
    public int? MultiplexerValue { get; set; }
    public string Comment { get; set; } = string.Empty;
    public Dictionary<int, string> ValueTable { get; set; } = new();
}

public class DbcMessage
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte Dlc { get; set; }
    public string Transmitter { get; set; } = string.Empty;
    public List<DbcSignal> Signals { get; set; } = new();
    public string Comment { get; set; } = string.Empty;
    public bool IsExtended => (Id & 0x80000000U) != 0 || Id > 0x7FFU;
    public uint CleanId => Id & 0x1FFFFFFFU;
}

public class DbcNode
{
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public class DbcDatabase
{
    public string Version { get; set; } = string.Empty;
    public List<DbcNode> Nodes { get; set; } = new();
    public List<DbcMessage> Messages { get; set; } = new();
    public Dictionary<uint, DbcMessage> MessageLookup { get; set; } = new();

    public void RebuildLookup()
    {
        MessageLookup.Clear();
        foreach (var msg in Messages)
        {
            MessageLookup[msg.CleanId] = msg;
        }
    }
}
