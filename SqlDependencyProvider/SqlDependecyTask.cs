using SqlDependencyProvider.Helpers;

using System;
using System.Data;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SqlDependencyProvider.Service
{
    public class SqlDependecyTask
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

        #endregion

        #region Property

        private const int CommandTimeout = 1 * 60 * 1000; // 1 Min

        private string ConnectionString { get; set; }
        private string CommandText { get; set; }
        private SqlParameter[] prms { get; set; }
        private bool IsLooped { get; set; }
        private int LoopedCount { get; set; }
        private bool IsStoredProcedure { get; set; }
        private bool IsChangeTracker
        {
            get { return this.OnSelectResult == null && this.OnChangeResult != null; }
        }

        /// <summary>
        /// Task status
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Identifier Unique Name
        /// </summary>
        public string Identifier { get; set; }

        #endregion

        #region Ctor

        public SqlDependecyTask()
        {
            this.IsStoredProcedure = false;
            this.IsLooped = false;
            this.LoopedCount = 0;
        }

        public SqlDependecyTask(string Identifier, string connectionString, string CommandText, bool IsStoredProcedure, params SqlParameter[] prms)
            : this()
        {
            this.Identifier = Identifier;
            this.CommandText = CommandText;
            this.ConnectionString = connectionString;
            this.IsStoredProcedure = IsStoredProcedure;
            this.prms = prms;
        }

        public SqlDependecyTask(string Identifier, string connectionString, string CommandText, params SqlParameter[] prms)
            : this(Identifier, connectionString, CommandText, false, prms) { }

        #endregion

        #region public Methods

        /// <summary>
        /// Start Task
        /// </summary>
        public async Task StartTask()
        {
            try
            {
                this.IsRunning = true;
                this.WriteLog("StartTask {0}", this.Identifier);

                if (this.IsChangeTracker)
                {
                    await this.AddChangeDependency(Result_OnChange);
                    if (this.OnChangeResult != null)
                    {
                        bool eventresult = this.OnChangeResult(this);
                        if (!(eventresult && this.IsRunning)) this.StopTask();
                    }
                    else throw new Exception("StartTask OnChangeResult not set");
                }
                else
                {
                    DataTable result = await this.AddQueryDependency(Result_OnChange);
                    if (this.OnSelectResult != null)
                    {
                        bool eventresult = this.OnSelectResult(this, result);
                        if (!(eventresult && this.IsRunning)) this.StopTask();
                    }
                    else throw new Exception("StartTask OnSelectResult not set");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(new Exception("StartTask", ex));
                this.WriteLog("StartTask Exception {0}", ex.ToDetailString());
                throw new Exception("StartTask", ex);
            }
        }

        /// <summary>
        /// Stop Task
        /// </summary>
        public void StopTask()
        {
            this.WriteLog("Stoped Task {0}", this.Identifier);
            this.IsRunning = false;
        }

        #endregion

        #region private Methods

        private async Task<DataTable> AddQueryDependency(OnChangeEventHandler Handler)
        {
            try
            {
                //remove some SQLDependency limitation
                string correctCommandText = Regex.Replace(CommandText, @"getdate\(\)", "@getdate", RegexOptions.IgnoreCase);

                this.WriteLog("AddQueryDependency");

                using (SqlConnection cn = new SqlConnection(ConnectionString))
                using (SqlCommand cmd = new SqlCommand(correctCommandText, cn))
                {
                    cmd.CommandTimeout = CommandTimeout;
                    cmd.CommandType = IsStoredProcedure ? CommandType.StoredProcedure : CommandType.Text;
                    cmd.Notification = null;

                    foreach (var param in prms)
                        cmd.Parameters.Add(new SqlParameter(param.ParameterName, param.SqlDbType) { Value = param.Value });

                    if (correctCommandText.Contains("@getdate"))
                        cmd.Parameters.Add(new SqlParameter("@getdate", SqlDbType.DateTime) { Value = DateTime.Now });

                    SqlDependency dep = new SqlDependency(cmd);
                    dep.OnChange += Handler;

                    if (cn.State == ConnectionState.Closed) cn.Open();
                    var dr = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
                    cmd.Parameters.Clear();

                    DataTable dt = new DataTable();
                    dt.Load(dr);
                    return dt;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(new Exception("AddSqlDependency", ex));
                this.WriteLog("AddSqlDependency Exception {0}", ex.ToDetailString());
                throw new Exception("AddSqlDependency", ex);
            }
        }

        private async Task AddChangeDependency(OnChangeEventHandler Handler)
        {
            try
            {
                //remove some SQLDependency limitation
                string correctCommandText = Regex.Replace(CommandText, @"getdate\(\)", "@getdate", RegexOptions.IgnoreCase);

                this.WriteLog("AddChangeDependency");

                using (SqlConnection cn = new SqlConnection(ConnectionString))
                using (SqlCommand cmd = new SqlCommand(correctCommandText, cn))
                {
                    cmd.CommandTimeout = CommandTimeout;
                    cmd.Notification = null;
                    cmd.CommandType = IsStoredProcedure ? CommandType.StoredProcedure : CommandType.Text;

                    foreach (var param in prms)
                        cmd.Parameters.Add(new SqlParameter(param.ParameterName, param.SqlDbType) { Value = param.Value });

                    if (correctCommandText.Contains("@getdate"))
                        cmd.Parameters.Add(new SqlParameter("@getdate", SqlDbType.DateTime) { Value = DateTime.Now });

                    SqlDependency dep = new SqlDependency(cmd);
                    dep.OnChange += Handler;

                    if (cn.State == ConnectionState.Closed) await cn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                    cmd.Parameters.Clear();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(new Exception("AddChangeDependency", ex));
                this.WriteLog("AddChangeDependency Exception {0}", ex.ToDetailString());
                throw new Exception("AddChangeDependency", ex);
            }
        }

        private void Result_OnChange(object sender, SqlNotificationEventArgs e)
        {

            this.WriteLog("Result_OnChange");

            //Prevent looping
            if (this.IsLooped && this.LoopedCount > 5)
            {
                this.WriteLog("Task is looped!");
                Debug.WriteLine("Task is looped!");
                this.IsRunning = false;
                return;
            }

            this.IsLooped = true; this.LoopedCount++;

            Task.Run(async () =>
            {
                //start again
                if (IsRunning)
                {
                    this.WriteLog("Result_OnChange start again");
                    await StartTask();
                }
                this.RefreshSqlDependency(sender, e, Result_OnChange);

                //Prevent looping
                if (this.LoopedCount > 0) this.LoopedCount--;
                this.IsLooped = false;
            });
        }

        private void RefreshSqlDependency(object sender, SqlNotificationEventArgs e, OnChangeEventHandler Handler)
        {
            SqlDependency dep = sender as SqlDependency;
            dep.OnChange -= Handler;
        }

        #endregion

        #region Events

        public delegate bool OnSelectResultDlg(SqlDependecyTask Sender, DataTable dt);

        /// <summary>
        /// Raise on any change tracked rows in DataTable
        /// if you want to resume tracking return True otherwise return false
        /// </summary>
        public event OnSelectResultDlg OnSelectResult = null;

        public delegate bool OnChangeResultDlg(SqlDependecyTask Sender);

        /// /// <summary>
        /// Raise on any change tracked rows alarm
        /// if you want to resume tracking return True otherwise return false
        /// </summary>
        public event OnChangeResultDlg OnChangeResult = null;

        #endregion

    }

}
