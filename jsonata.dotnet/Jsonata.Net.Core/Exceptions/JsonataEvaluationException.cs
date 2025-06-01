using System;

namespace Jsonata.Net.Core.Exceptions
{
    public class JsonataEvaluationException : Exception
    {
        public JsonataEvaluationException(string message) : base(message) { }
        public JsonataEvaluationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
