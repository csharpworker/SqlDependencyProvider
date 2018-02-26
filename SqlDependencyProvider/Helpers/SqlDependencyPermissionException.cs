using System;

namespace SqlDependencyProvider
{

    [Serializable]
    public class SqlDependencyPermissionException : Exception
    {
        public SqlDependencyPermissionException() { }
        public SqlDependencyPermissionException(string message) : base(message) { }
        public SqlDependencyPermissionException(string message, Exception inner) : base(message, inner) { }
        protected SqlDependencyPermissionException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
