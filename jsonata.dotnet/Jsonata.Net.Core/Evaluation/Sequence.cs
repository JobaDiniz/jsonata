using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes; // Still needed for IsTruthy checks if item is JsonNode

namespace Jsonata.Net.Core.Evaluation
{
    public sealed class Sequence : IEnumerable<object?> // Changed from IEnumerable<JsonNode?>
    {
        private readonly List<object?> _items; // Changed from List<JsonNode?>
        public static readonly Sequence Undefined = new Sequence(null, true /*isUndefined*/);

        public int Count => _items.Count;

        public object? FirstOrDefault() => _items.Count > 0 ? _items[0] : null; // Return type changed

        // Constructor for single item or undefined
        public Sequence(object? item = null, bool isUndefined = false) // Parameter type changed
        {
            _items = new List<object?>();
            if (isUndefined) return;
            if (item != null || (item == null && !isUndefined)) // item can be null for an explicit null value in sequence
            {
                _items.Add(item);
            }
        }

        // Constructor for multiple items
        public Sequence(IEnumerable<object?> items) // Parameter type changed
        {
            _items = new List<object?>(items ?? Enumerable.Empty<object?>());
        }

        public void Add(object? item) // Parameter type changed
        {
            _items.Add(item);
        }

        public IEnumerator<object?> GetEnumerator() => _items.GetEnumerator(); // Return type changed
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public object? AsSingle(object? valueIfMultiple = null) // Return type and param type changed
        {
            return _items.Count == 1 ? _items[0] : valueIfMultiple;
        }

        public bool IsSingleValue() => _items.Count == 1;

        public bool IsUndefined()
        {
            return this == Undefined; // Undefined is a specific instance
        }

        public static bool IsTruthy(Sequence seq)
        {
            if (seq == null || seq.IsUndefined() || seq.Count == 0) return false;
            if (seq.Count > 1) return true; // Sequence of multiple items is truthy by JSONata spec

            object? item = seq.FirstOrDefault(); // item is now object?

            if (item == null) return false; // An explicit JSON null in a sequence is falsy

            if (item is FunctionObject) // User-defined functions are truthy
            {
                return true;
            }

            if (item is JsonNode node) // Check if it's a JsonNode for further JSON-specific truthiness
            {
                if (node is JsonValue val)
                {
                    if (val.TryGetValue<bool>(out bool b)) return b;
                    if (val.TryGetValue<string>(out string? s)) return !string.IsNullOrEmpty(s);
                    // Numbers: 0 is falsy, non-zero is truthy.
                    // TryGetValue<decimal> and other numeric types...
                    if (val.TryGetValue<decimal>(out decimal d)) return d != 0;
                    if (val.TryGetValue<double>(out double dbl)) return dbl != 0;
                    if (val.TryGetValue<float>(out float flt)) return flt != 0;
                    if (val.TryGetValue<int>(out int i)) return i != 0;
                    if (val.TryGetValue<long>(out long l)) return l != 0;
                    // If it's a JsonValue but doesn't match known primitive types for truthiness,
                    // it might be some other less common JsonValue type.
                    // For safety, consider it falsy if not explicitly truthy.
                    return false;
                }
                // JsonObject and JsonArray instances are truthy if they exist (as a single item in sequence)
                return true;
            }

            // If the item is not null, not a FunctionObject, and not a JsonNode, what is it?
            // This case should ideally not be reached if all sequence items are either JsonNode or FunctionObject.
            // For safety, consider unknown non-null objects truthy, or throw an exception.
            // JSONata spec implies any non-empty, non-explicitly-falsy thing is truthy.
            return true;
        }
    }
}
