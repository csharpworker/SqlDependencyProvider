using System;

namespace SqlDependencyProvider.Helpers
{
    public static class Extentions
    {
        /// <summary>
        /// Show Exception and InnerException
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>string concat messages</returns>
        public static string ToDetailString(this Exception ex)
        {
            return string.Join(" ", ex.Message, ex.InnerException?.Message);
        }

    }
}
