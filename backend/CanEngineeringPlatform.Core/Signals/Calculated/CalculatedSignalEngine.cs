using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;

namespace CanEngineeringPlatform.Core.Signals.Calculated;

public class CalculatedSignalDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string Category { get; set; } = "Calculated";
    public HashSet<string> Dependencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public CalculatedSignalDefinition() { }

    public CalculatedSignalDefinition(string id, string name, string expression, string units = "", string category = "Calculated")
    {
        Id = id;
        Name = name;
        Expression = expression;
        Units = units;
        Category = category;
        Dependencies = CalculatedSignalEngine.ExtractDependencies(expression);
    }
}

public interface ICalculatedSignalEngine
{
    void RegisterSignal(CalculatedSignalDefinition signal);
    void UnregisterSignal(string id);
    IReadOnlyList<CalculatedSignalDefinition> GetAllSignals();
    IReadOnlyList<string> GetEvaluationOrder();
    double Evaluate(string expression, IReadOnlyDictionary<string, double> context);
    Dictionary<string, double> EvaluateAll(IReadOnlyDictionary<string, double> inputs);
    void EvaluateDataSet(CanonicalLogDataSet dataset);
}

public class CalculatedSignalEngine : ICalculatedSignalEngine
{
    private readonly ConcurrentDictionary<string, CalculatedSignalDefinition> _signals = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private static readonly HashSet<string> KnownMathFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sqrt", "abs", "min", "max", "clamp", "log", "exp", "pow", "floor", "ceil", "round"
    };

    public CalculatedSignalEngine(IEnumerable<CalculatedSignalDefinition>? initialSignals = null)
    {
        if (initialSignals != null)
        {
            foreach (var sig in initialSignals)
            {
                RegisterSignal(sig);
            }
        }
    }

    public static HashSet<string> ExtractDependencies(string expression)
    {
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(expression)) return dependencies;

        // Match identifier words ([a-zA-Z_][a-zA-Z0-9_]*)
        var matches = Regex.Matches(expression, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");
        foreach (Match m in matches)
        {
            string token = m.Value;
            if (!KnownMathFunctions.Contains(token))
            {
                dependencies.Add(token);
            }
        }
        return dependencies;
    }

    public void RegisterSignal(CalculatedSignalDefinition signal)
    {
        if (signal == null) throw new ArgumentNullException(nameof(signal));
        if (string.IsNullOrWhiteSpace(signal.Id)) signal.Id = Guid.NewGuid().ToString("N");

        signal.Dependencies = ExtractDependencies(signal.Expression);

        lock (_lock)
        {
            _signals[signal.Id] = signal;
            ValidateNoCycles();
        }
    }

    public void UnregisterSignal(string id)
    {
        lock (_lock)
        {
            _signals.TryRemove(id, out _);
        }
    }

    public IReadOnlyList<CalculatedSignalDefinition> GetAllSignals() => _signals.Values.ToList();

    public IReadOnlyList<string> GetEvaluationOrder()
    {
        lock (_lock)
        {
            return TopologicallySortSignals();
        }
    }

    public double Evaluate(string expression, IReadOnlyDictionary<string, double> context)
    {
        if (string.IsNullOrWhiteSpace(expression)) return 0.0;
        var parser = new ExpressionParser(expression, context);
        return parser.Parse();
    }

    public Dictionary<string, double> EvaluateAll(IReadOnlyDictionary<string, double> inputs)
    {
        var context = new Dictionary<string, double>(inputs, StringComparer.OrdinalIgnoreCase);
        var results = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var order = GetEvaluationOrder();
        foreach (var signalId in order)
        {
            if (_signals.TryGetValue(signalId, out var signal))
            {
                double val = Evaluate(signal.Expression, context);
                results[signalId] = val;
                context[signalId] = val; // Allow downstream calculated signals to consume this value
            }
        }

        return results;
    }

    public void EvaluateDataSet(CanonicalLogDataSet dataset)
    {
        if (dataset == null || dataset.Channels.Count == 0 || _signals.IsEmpty) return;

        var primaryChannel = dataset.Channels.Values.OrderByDescending(c => c.SampleCount).FirstOrDefault();
        if (primaryChannel == null || primaryChannel.SampleCount == 0) return;

        var order = GetEvaluationOrder();
        var calculatedChannels = new Dictionary<string, TimeSeriesChannel>(StringComparer.OrdinalIgnoreCase);

        foreach (var sigId in order)
        {
            if (_signals.TryGetValue(sigId, out var def))
            {
                calculatedChannels[def.Name] = new TimeSeriesChannel(def.Name, def.Units, "Calculated");
            }
        }

        for (int i = 0; i < primaryChannel.SampleCount; i++)
        {
            double t = primaryChannel.TimestampsMs[i];
            var context = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var ch in dataset.Channels.Values)
            {
                if (i < ch.Values.Count)
                {
                    context[ch.Name] = ch.Values[i];
                }
            }

            foreach (var sigId in order)
            {
                if (_signals.TryGetValue(sigId, out var def))
                {
                    double val = Evaluate(def.Expression, context);
                    context[def.Name] = val;
                    context[def.Id] = val;
                    calculatedChannels[def.Name].AddPoint(t, val);
                }
            }
        }

        foreach (var kvp in calculatedChannels)
        {
            dataset.Channels[kvp.Key] = kvp.Value;
        }
    }

    private void ValidateNoCycles()
    {
        TopologicallySortSignals(); // Throws InvalidOperationException on cycle
    }

    private List<string> TopologicallySortSignals()
    {
        var result = new List<string>();
        var visited = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 0 = unvisited, 1 = visiting, 2 = visited

        foreach (var key in _signals.Keys)
        {
            if (!visited.ContainsKey(key))
            {
                DfsSort(key, visited, result);
            }
        }

        return result;
    }

    private void DfsSort(string node, Dictionary<string, int> visited, List<string> order)
    {
        visited[node] = 1; // Mark as visiting (gray)

        if (_signals.TryGetValue(node, out var def))
        {
            foreach (var dep in def.Dependencies)
            {
                if (_signals.ContainsKey(dep))
                {
                    if (visited.TryGetValue(dep, out int state))
                    {
                        if (state == 1)
                        {
                            throw new InvalidOperationException($"Circular dependency detected in calculated signals involving '{node}' and '{dep}'.");
                        }
                    }
                    else
                    {
                        DfsSort(dep, visited, order);
                    }
                }
            }
        }

        visited[node] = 2; // Mark as visited (black)
        order.Add(node);
    }
}

/// <summary>
/// Recursive descent expression parser supporting variables, math functions, and standard operator precedence.
/// </summary>
internal class ExpressionParser
{
    private readonly string _text;
    private readonly IReadOnlyDictionary<string, double> _context;
    private int _pos;

    public ExpressionParser(string text, IReadOnlyDictionary<string, double> context)
    {
        _text = text ?? string.Empty;
        _context = context ?? new Dictionary<string, double>();
        _pos = 0;
    }

    public double Parse()
    {
        double val = ParseExpression();
        SkipWhitespace();
        return val;
    }

    private double ParseExpression()
    {
        double left = ParseTerm();
        SkipWhitespace();

        while (_pos < _text.Length)
        {
            char op = _text[_pos];
            if (op != '+' && op != '-') break;
            _pos++;
            double right = ParseTerm();
            if (op == '+') left += right;
            else left -= right;
            SkipWhitespace();
        }
        return left;
    }

    private double ParseTerm()
    {
        double left = ParsePower();
        SkipWhitespace();

        while (_pos < _text.Length)
        {
            char op = _text[_pos];
            if (op != '*' && op != '/' && op != '%') break;
            _pos++;
            double right = ParsePower();
            if (op == '*') left *= right;
            else if (op == '%') left = (right == 0.0) ? 0.0 : left % right;
            else left = (right == 0.0) ? 0.0 : left / right;
            SkipWhitespace();
        }
        return left;
    }

    private double ParsePower()
    {
        double left = ParseFactor();
        SkipWhitespace();

        if (_pos < _text.Length && _text[_pos] == '^')
        {
            _pos++;
            double right = ParsePower(); // Right-associative power
            return Math.Pow(left, right);
        }
        return left;
    }

    private double ParseFactor()
    {
        SkipWhitespace();
        if (_pos >= _text.Length) return 0.0;

        // Unary signs
        if (_text[_pos] == '+')
        {
            _pos++;
            return ParseFactor();
        }
        if (_text[_pos] == '-')
        {
            _pos++;
            return -ParseFactor();
        }

        // Parentheses
        if (_text[_pos] == '(')
        {
            _pos++;
            double val = ParseExpression();
            SkipWhitespace();
            if (_pos < _text.Length && _text[_pos] == ')') _pos++;
            return val;
        }

        // Numeric literals
        if (char.IsDigit(_text[_pos]) || _text[_pos] == '.')
        {
            int start = _pos;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
            {
                _pos++;
            }
            string numStr = _text.Substring(start, _pos - start);
            return double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double num) ? num : 0.0;
        }

        // Identifiers (Variables or Math Functions)
        if (char.IsLetter(_text[_pos]) || _text[_pos] == '_')
        {
            int start = _pos;
            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
            {
                _pos++;
            }
            string ident = _text.Substring(start, _pos - start);
            SkipWhitespace();

            // Function call
            if (_pos < _text.Length && _text[_pos] == '(')
            {
                _pos++; // consume '('
                var args = new List<double>();
                SkipWhitespace();
                if (_pos < _text.Length && _text[_pos] != ')')
                {
                    while (true)
                    {
                        args.Add(ParseExpression());
                        SkipWhitespace();
                        if (_pos < _text.Length && _text[_pos] == ',')
                        {
                            _pos++;
                            SkipWhitespace();
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                if (_pos < _text.Length && _text[_pos] == ')') _pos++;
                return EvaluateFunction(ident, args);
            }

            // Variable lookup
            if (_context.TryGetValue(ident, out double varVal))
            {
                return varVal;
            }
            return 0.0;
        }

        return 0.0;
    }

    private static double EvaluateFunction(string name, List<double> args)
    {
        return name.ToLowerInvariant() switch
        {
            "sqrt" => args.Count > 0 && args[0] >= 0 ? Math.Sqrt(args[0]) : 0.0,
            "abs" => args.Count > 0 ? Math.Abs(args[0]) : 0.0,
            "min" => args.Count >= 2 ? Math.Min(args[0], args[1]) : (args.Count > 0 ? args[0] : 0.0),
            "max" => args.Count >= 2 ? Math.Max(args[0], args[1]) : (args.Count > 0 ? args[0] : 0.0),
            "clamp" => args.Count >= 3 ? Math.Clamp(args[0], args[1], args[2]) : 0.0,
            "log" => args.Count > 0 && args[0] > 0 ? Math.Log(args[0]) : 0.0,
            "exp" => args.Count > 0 ? Math.Exp(args[0]) : 0.0,
            "pow" => args.Count >= 2 ? Math.Pow(args[0], args[1]) : 0.0,
            "floor" => args.Count > 0 ? Math.Floor(args[0]) : 0.0,
            "ceil" => args.Count > 0 ? Math.Ceiling(args[0]) : 0.0,
            "round" => args.Count > 0 ? Math.Round(args[0], args.Count > 1 ? (int)args[1] : 0) : 0.0,
            _ => 0.0
        };
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
        {
            _pos++;
        }
    }
}
