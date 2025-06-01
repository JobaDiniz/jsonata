using System.Collections.Generic;
using Jsonata.Net.Core.AstNodes; // Assuming Sequence is in this namespace

namespace Jsonata.Net.Core.Evaluation
{
    public interface IBuiltinFunction
    {
        Sequence Execute(List<Sequence> arguments, EvaluationContext context);
    }
}
