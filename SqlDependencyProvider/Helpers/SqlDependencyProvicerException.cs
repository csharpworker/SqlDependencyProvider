using System;

namespace SqlDependencyProvider
{

    [Serializable]
    public class SqlDependencyProviderException : Exception
    {
        public SqlDependencyProviderException() { }
        public SqlDependencyProviderException(string message) : base(message) { }
        public SqlDependencyProviderException(string message, Exception inner) : base(message, inner) { }
        protected SqlDependencyProviderException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
