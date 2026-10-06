using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Dbc;

public class DbcParser
{
    private static readonly Regex MessageRegex = new(
        @"^BO_\s+(\d+)\s+([a-zA-Z0-9_]+)\s*:\s*(\d+)\s+([a-zA-Z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex SignalRegex = new(
        @"^SG_\s+([a-zA-Z0-9_]+)\s*(?:(M)|m(\d+))?\s*:\s*(\d+)\|(\d+)@([01])([+-])\s*\(([^,]+),([^)]+)\)\s*\[([^|]+)\|([^\]]+)\]\s*""([^""]*)""\s*(.*)",
        RegexOptions.Compiled);

    private static readonly Regex ValueDescriptionRegex = new(
        @"^VAL_\s+(\d+)\s+([a-zA-Z0-9_]+)\s+(.+);",
        RegexOptions.Compiled);

    private static readonly Regex CommentRegex = new(
        @"^CM_\s+(?:(SG_)\s+(\d+)\s+([a-zA-Z0-9_]+)|(BO_)\s+(\d+)|(BU_)\s+([a-zA-Z0-9_]+))\s*""([^""]*)""\s*;",
        RegexOptions.Compiled);

    public static DbcDatabase Parse(string dbcContent)
    {
        using var reader = new StringReader(dbcContent);
        return Parse(reader);
    }

    public static DbcDatabase Parse(TextReader reader)
    {
        var db = new DbcDatabase();
        DbcMessage? currentMessage = null;
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
            {
                continue;
            }

            if (line.StartsWith("VERSION"))
            {
                var match = Regex.Match(line, @"VERSION\s+""([^""]*)""");
                if (match.Success)
                {
                    db.Version = match.Groups[1].Value;
                }
                continue;
            }

            if (line.StartsWith("BU_:"))
            {
                var nodesStr = line.Substring(4).Trim();
                var nodeNames = nodesStr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in nodeNames)
                {
                    db.Nodes.Add(new DbcNode { Name = name });
                }
                continue;
            }

            var msgMatch = MessageRegex.Match(line);
            if (msgMatch.Success)
            {
                uint id = uint.Parse(msgMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                string name = msgMatch.Groups[2].Value;
                byte dlc = byte.Parse(msgMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                string transmitter = msgMatch.Groups[4].Value;

                currentMessage = new DbcMessage
                {
                    Id = id,
                    Name = name,
                    Dlc = dlc,
                    Transmitter = transmitter
                };
                db.Messages.Add(currentMessage);
                continue;
            }

            if (currentMessage != null && line.StartsWith("SG_"))
            {
                var sigMatch = SignalRegex.Match(line);
                if (sigMatch.Success)
                {
                    string name = sigMatch.Groups[1].Value;
                    string muxSwitch = sigMatch.Groups[2].Value;
                    string muxValStr = sigMatch.Groups[3].Value;

                    int startBit = int.Parse(sigMatch.Groups[4].Value, CultureInfo.InvariantCulture);
                    int length = int.Parse(sigMatch.Groups[5].Value, CultureInfo.InvariantCulture);
                    bool isLittleEndian = sigMatch.Groups[6].Value == "1";
                    bool isSigned = sigMatch.Groups[7].Value == "-";

                    double factor = double.Parse(sigMatch.Groups[8].Value, CultureInfo.InvariantCulture);
                    double offset = double.Parse(sigMatch.Groups[9].Value, CultureInfo.InvariantCulture);
                    double min = double.Parse(sigMatch.Groups[10].Value, CultureInfo.InvariantCulture);
                    double max = double.Parse(sigMatch.Groups[11].Value, CultureInfo.InvariantCulture);
                    string unit = sigMatch.Groups[12].Value;
                    string receiversStr = sigMatch.Groups[13].Value.Trim();

                    var receivers = new List<string>();
                    if (!string.IsNullOrEmpty(receiversStr))
                    {
                        var parts = receiversStr.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var r in parts)
                        {
                            if (r != "Vector__XXX" && !string.IsNullOrWhiteSpace(r))
                            {
                                receivers.Add(r);
                            }
                        }
                    }

                    var signal = new DbcSignal
                    {
                        Name = name,
                        StartBit = startBit,
                        Length = length,
                        IsLittleEndian = isLittleEndian,
                        IsSigned = isSigned,
                        Factor = factor,
                        Offset = offset,
                        Minimum = min,
                        Maximum = max,
                        Unit = unit,
                        Receivers = receivers
                    };

                    if (!string.IsNullOrEmpty(muxSwitch))
                    {
                        signal.MultiplexerType = MultiplexerType.MultiplexerSwitch;
                    }
                    else if (!string.IsNullOrEmpty(muxValStr))
                    {
                        signal.MultiplexerType = MultiplexerType.Multiplexed;
                        signal.MultiplexerValue = int.Parse(muxValStr, CultureInfo.InvariantCulture);
                    }

                    currentMessage.Signals.Add(signal);
                }
                continue;
            }

            var valMatch = ValueDescriptionRegex.Match(line);
            if (valMatch.Success)
            {
                uint id = uint.Parse(valMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                string signalName = valMatch.Groups[2].Value;
                string pairs = valMatch.Groups[3].Value;

                var pairMatches = Regex.Matches(pairs, @"(-?\d+)\s+""([^""]*)""");
                var message = db.Messages.Find(m => (m.Id & 0x1FFFFFFFU) == (id & 0x1FFFFFFFU));
                var signal = message?.Signals.Find(s => s.Name == signalName);

                if (signal != null)
                {
                    foreach (Match p in pairMatches)
                    {
                        int val = int.Parse(p.Groups[1].Value, CultureInfo.InvariantCulture);
                        string desc = p.Groups[2].Value;
                        signal.ValueTable[val] = desc;
                    }
                }
                continue;
            }

            var commentMatch = CommentRegex.Match(line);
            if (commentMatch.Success)
            {
                if (commentMatch.Groups[1].Value == "SG_")
                {
                    uint id = uint.Parse(commentMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                    string sigName = commentMatch.Groups[3].Value;
                    string comment = commentMatch.Groups[8].Value;

                    var message = db.Messages.Find(m => (m.Id & 0x1FFFFFFFU) == (id & 0x1FFFFFFFU));
                    var signal = message?.Signals.Find(s => s.Name == sigName);
                    if (signal != null)
                    {
                        signal.Comment = comment;
                    }
                }
                else if (commentMatch.Groups[4].Value == "BO_")
                {
                    uint id = uint.Parse(commentMatch.Groups[5].Value, CultureInfo.InvariantCulture);
                    string comment = commentMatch.Groups[8].Value;
                    var message = db.Messages.Find(m => (m.Id & 0x1FFFFFFFU) == (id & 0x1FFFFFFFU));
                    if (message != null)
                    {
                        message.Comment = comment;
                    }
                }
                else if (commentMatch.Groups[6].Value == "BU_")
                {
                    string nodeName = commentMatch.Groups[7].Value;
                    string comment = commentMatch.Groups[8].Value;
                    var node = db.Nodes.Find(n => n.Name == nodeName);
                    if (node != null)
                    {
                        node.Comment = comment;
                    }
                }
            }
        }

        db.RebuildLookup();
        return db;
    }
}
