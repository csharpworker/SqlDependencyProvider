using SqlDependencyProvider.Helpers;

using System;
using System.Data;
using System.Linq;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Permissions;

namespace SqlDependencyProvider.Service
{
    public class SqlDependencyService
    {

        #region Log

        /// <summary>
        /// Log everything
        /// </summary>
        public event EventHandler<string> Onlog;

        protected void WriteLog(string log, params object[] args)
        {
            this.Onlog?.Invoke(this, string.Format(log, args));
        }

        private void Dp_Onlog(object sender, string e)
        {
            this.WriteLog(e);
        }

        #endregion

        #region Property

        /// <summary>
        /// Service Status
        /// </summary>
        public bool IsServiceStarted { get; set; }
        private string ConnectionString { get; set; }
        private Dictionary<string, SqlDependecyTask> DependecyTasks { get; set; }

        #endregion

        #region Services

        public SqlDependencyService()
        {
            this.IsServiceStarted = false;
            this.DependecyTasks = new Dictionary<string, SqlDependecyTask>();
        }

        /// <summary>
        /// Use in service start after init your classes and Tasks!
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public SqlDependencyService(string ConnectionString) : this()
        {
            try
            {
                this.ConnectionString = ConnectionString;
                if (!DoesUserHavePermission())
                {
                    throw new SqlDependencyPermissionException("Error DoesUserHavePermission!");
                }

                //Enabeling Sql Dependency on database
                this.WriteLog("Enabeling Sql Dependency on database ", this.ConnectionString);

                string DataBaseName = new SqlConnection(this.ConnectionString).Database;
                string Query = @"IF(NOT EXISTS(SELECT is_broker_enabled FROM sys.databases WHERE name = @DbName AND is_broker_enabled = 1))
                                        begin
	                                            DECLARE @sqlCommand NVARCHAR(max)
	                                            SET @sqlCommand = N'ALTER DATABASE ' + @DbName + ' SET ENABLE_BROKER with rollback immediate'
	                                            EXECUTE sp_executesql @sqlCommand
                                        end";

                using (SqlConnection con = new SqlConnection(this.ConnectionString))
                using (SqlCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = Query;
                    cmd.Parameters.AddWithValue("@DbName", DataBaseName);
                    con.Open();
                    cmd.ExecuteNonQuery();
                    con.Close();
                }

                //  You must stop the dependency before starting a new one.
                //  You must start the dependency when creating a new one.
                SqlDependency.Stop(this.ConnectionString);
                SqlDependency.Start(this.ConnectionString);

                this.IsServiceStarted = true;
            }
            catch (Exception ex)
            {
                this.WriteLog("DependencyService", ex);
                Debug.WriteLine(new Exception("DependencyService", ex));
                throw new SqlDependencyProviderException("Enabled Service Broker Error! " + ex.Message, ex);
            }
        }

        /// <summary>
        /// You Must Do it! 
        /// </summary>
        /// <returns></returns>
        public bool DisposeSqlDependency()
        {
            try
            {
                this.StopTasks();
                this.IsServiceStarted = false;
                return SqlDependency.Stop(this.ConnectionString);
            }
            catch
            {
                return false;
            }
        }

        private bool DoesUserHavePermission()
        {
            try
            {
                SqlClientPermission clientPermission = new SqlClientPermission(PermissionState.Unrestricted);
                clientPermission.Demand();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ClearOldSubscriptions()
        {
            this.WriteLog("Clearing OldSubscriptions");
            try
            {
                using (var connection = new SqlConnection(this.ConnectionString))
                using (var command = new SqlCommand())
                {
                    //string sql =
                    //    @"DECLARE @SubscriptionId AS int; 
                    //      DECLARE @Sql AS varchar(max); 
                    //      DECLARE SubscriptionCursor CURSOR LOCAL FAST_FORWARD 
                    //      FOR
                    //        SELECT id FROM sys.dm_qn_subscriptions 
                    //        WHERE database_id = DB_ID()  
                    //      OPEN SubscriptionCursor; 
                    //      FETCH NEXT FROM SubscriptionCursor INTO @SubscriptionId; 
                    //      WHILE @@FETCH_STATUS = 0 
                    //      BEGIN 
                    //          SET @Sql = 'KILL QUERY NOTIFICATION SUBSCRIPTION ' + CONVERT(varchar, @SubscriptionId); 
                    //          EXEC(@Sql);
                    //          FETCH NEXT FROM SubscriptionCursor INTO @SubscriptionId; 
                    //      END";

                    command.Connection = connection;
                    command.CommandType = CommandType.Text;
                    command.CommandText = "KILL QUERY NOTIFICATION SUBSCRIPTION ALL";
                    connection.Open();
                    command.ExecuteNonQuery();
                    this.WriteLog("Cleared OldSubscriptions");
                }
            }
            catch (Exception ex)
            {
                this.WriteLog("{0} {1}", "Cleared OldSubscriptions exception", ex.ToDetailString());
            }
        }

        #endregion

        #region Task Services

        /// <summary>
        /// Start all tasks asynchronous
        /// </summary>
        public async Task StartTasks()
        {
            if (!IsServiceStarted) return;

            var rt = this.DependecyTasks.Values.Where(r => !r.IsRunning).ToList();
            foreach (SqlDependecyTask task in rt) await task.StartTask();
        }

        /// <summary>
        /// Stop all tasks
        /// </summary>
        public void StopTasks()
        {
            var rt = this.DependecyTasks.Values.Where(r => r.IsRunning).ToList();
            foreach (SqlDependecyTask task in rt) task.StopTask();

            this.ClearOldSubscriptions();
        }

        #endregion

        #region Manange Tasks

        /// <summary>
        /// Add new task on service tracking
        /// </summary>
        /// <param name="CommandText">Sql Query</param>
        /// <param name="param">Sql Parameters</param>
        /// <returns>new SqlDependecy Task</returns>
        public SqlDependecyTask AppendTask(string CommandText, params SqlParameter[] param)
        {
            return AppendTask(CommandText, false, param);
        }

        /// <summary>
        /// Add new task on service tracking
        /// </summary>
        /// <param name="CommandText">Sql Query</param>
        /// <param name="IsStoredProcedure">CommandText is StoredProcedure name</param>
        /// <param name="param">Sql Parameters</param>
        /// <returns>new SqlDependecy Task</returns>
        public SqlDependecyTask AppendTask(string CommandText, bool IsStoredProcedure, params SqlParameter[] param)
        {
            return AppendTask(GenerateIdentifier(), CommandText, IsStoredProcedure, param);
        }

        /// <summary>
        /// Add new task on service tracking
        /// </summary>
        /// <param name="Identifier">Task Unique Name to finding</param>
        /// <param name="CommandText">Sql Query</param>
        /// <param name="param">Sql Parameters</param>
        /// <returns>new SqlDependecy Task</returns>
        public SqlDependecyTask AppendTask(string Identifier, string CommandText, params SqlParameter[] param)
        {
            return AppendTask(Identifier, CommandText, false, param);
        }

        /// <summary>
        /// Add new task on service tracking
        /// </summary>
        /// <param name="Identifier">Task Unique Name to finding</param>
        /// <param name="CommandText">Sql Query</param>
        /// <param name="IsStoredProcedure">CommandText is StoredProcedure name</param>
        /// <param name="param">Sql Parameters</param>
        /// <returns>new SqlDependecy Task</returns>
        public SqlDependecyTask AppendTask(string Identifier, string CommandText, bool IsStoredProcedure, params SqlParameter[] param)
        {
            if (this.DependecyTasks.ContainsKey(Identifier))
            {
                this.DependecyTasks[Identifier].StopTask();
                this.DependecyTasks.Remove(Identifier);
            }

            SqlDependecyTask dp = new SqlDependecyTask(Identifier, this.ConnectionString, CommandText, IsStoredProcedure, param);
            dp.Onlog += Dp_Onlog;
            this.DependecyTasks.Add(Identifier, dp);
            return dp;
        }

        /// <summary>
        /// Stop and remove Task that found by Identifier Unique Name
        /// </summary>
        /// <param name="Identifier">Task Unique Name</param>
        public void RemoveTask(string Identifier)
        {
            if (DependecyTasks.ContainsKey(Identifier))
            {
                DependecyTasks[Identifier].StopTask();
                DependecyTasks.Remove(Identifier);
            }
        }

        /// <summary>
        /// Check for Task Is Exists And Is Running by Identifier Unique Name
        /// </summary>
        /// <param name="Identifier">Task Unique Name</param>
        /// <returns>Task Status</returns>
        public bool IsExistsAndIsRunningTask(string Identifier)
        {
            return DependecyTasks.ContainsKey(Identifier) && DependecyTasks[Identifier].IsRunning;
        }

        private string GenerateIdentifier()
        {
            return Guid.NewGuid().ToString();
        }

        #endregion

    }

}
