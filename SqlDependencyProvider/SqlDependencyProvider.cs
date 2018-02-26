using SqlDependencyProvider.Helpers;
using SqlDependencyProvider.Service;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SqlDependencyProvider
{
    public class SqlDependencyProvider 
    {
        #region Log

        /// <summary>
        /// Log everything 
        /// </summary>
        public event EventHandler<string> Onlog;

        private void Dep_Onlog(object sender, string e)
        {
            this.WriteLog(e);
        }

        protected void WriteLog(string log, params object[] args)
        {
            Debug.WriteLine(string.Format(log, args));
            this.Onlog?.Invoke(this, string.Format(log, args));
        }

        #endregion

        #region Property

        public string PublicSqlConnectionString { get; set; }

        private static SqlDependencyProvider _instance;
        /// <summary>
        /// SqlDependency Provider singleton instance
        /// </summary>
        public static SqlDependencyProvider Instance
        {
            get { return _instance ?? (_instance = new SqlDependencyProvider()); }
        }

        private Dictionary<string, SqlDependencyService> DependencyServices { get; set; }

        #endregion

        #region Ctor

        public SqlDependencyProvider()
        {
            this.DependencyServices = new Dictionary<string, SqlDependencyService>();
        }

        public SqlDependencyProvider(string PublicSqlConnectionString) : this()
        {
            this.PublicSqlConnectionString = PublicSqlConnectionString;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Start Services
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task StartAsync()
        {
            try
            {
                this.WriteLog("Starting All SqlDependency");

                foreach (SqlDependencyService service in DependencyServices.Values)
                    await service.StartTasks();

                this.WriteLog("Started All SqlDependency");
            }
            catch (Exception ex)
            {
                this.WriteLog("Websocket Hosted error {0}", ex.ToDetailString());
            }
        }


        /// <summary>
        /// Stop services without Disposing
        /// </summary>
        public void Stop()
        {
            this.WriteLog("Stoping All SqlDependency");

            foreach (SqlDependencyService service in DependencyServices.Values)
                service.StopTasks();

            this.WriteLog("Stoped All SqlDependency");
        }

        /// <summary>
        /// Stop services with Disposing
        /// Dispose on disposing object
        /// </summary>
        public void StopAndDispose()
        {
            this.WriteLog("Dispose All SqlDependency");

            foreach (SqlDependencyService service in DependencyServices.Values)
                service.DisposeSqlDependency();

            this.WriteLog("Disposed All SqlDependency");
        }

        /// <summary>
        /// Get service on Public ConnectionString
        /// </summary>
        /// <returns>SqlDependency Service</returns>
        public SqlDependencyService GetDependency()
        {
            return this.GetDependency(PublicSqlConnectionString);
        }

        /// <summary>
        /// Get service on Custom ConnectionString
        /// </summary>
        /// <returns>SqlDependency Service</returns>
        public SqlDependencyService GetDependency(string ConnectionString)
        {
            if (this.DependencyServices.ContainsKey(ConnectionString))
                return this.DependencyServices[ConnectionString];
            else
            {
                var dep = new SqlDependencyService(ConnectionString);
                dep.Onlog -= Dep_Onlog; dep.Onlog += Dep_Onlog;
                this.DependencyServices.Add(ConnectionString, dep);
                return dep;
            }
        }

        #endregion
    }
}
