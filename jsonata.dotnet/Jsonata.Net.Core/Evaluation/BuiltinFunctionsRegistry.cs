using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis; // For TryGetValue extension

namespace Jsonata.Net.Core.Evaluation
{
    public sealed class BuiltinFunctionsRegistry
    {
        private readonly Dictionary<string, IBuiltinFunction> _functions = new();

        public void RegisterFunction(string name, IBuiltinFunction function)
        {
            if (_functions.ContainsKey(name))
            {
                throw new ArgumentException($"Function with name '{name}' is already registered.", nameof(name));
            }
            _functions.Add(name, function);
        }

        public bool TryGetFunction(string name, [MaybeNullWhen(false)] out IBuiltinFunction function)
        {
            return _functions.TryGetValue(name, out function);
        }
    }
}
