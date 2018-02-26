using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data.SqlClient;
using System.Data;

namespace SqlDependencyProvider.Test
{
    [TestClass]
    public class UnitTestProvider
    {
        /// <summary>
        /// it isn't a test. just for show simple example
        /// </summary>
        [TestMethod]
        public void TestMethodStart()
        {
            try
            {
                //Config Provider
                SqlDependencyProvider.Instance.Onlog += Instance_Onlog;
                SqlDependencyProvider.Instance.PublicSqlConnectionString = "Data Source=.;Initial Catalog=db;Persist Security Info=True;User ID=sa;Password=***********";

                //create service on public connection string
                var service = SqlDependencyProvider.Instance.GetDependency();

                //create task for query and paramerter
                var task = service.AppendTask("Select Id,Name from tbl where Id > @ID", new SqlParameter("@ID", 1));

                //raise on any change detected 
                task.OnSelectResult += Task_OnSelectResult;

                //Start All task on instance in application start up
                SqlDependencyProvider.Instance.StartAsync().GetAwaiter();

                //Must call this method at then end of application
                //if you have another application run tracking
                SqlDependencyProvider.Instance.Stop();

                //if you want clear any settings run this method
                //if you single application run tracking
                SqlDependencyProvider.Instance.StopAndDispose();
            }
            catch (SqlDependencyPermissionException ex)
            {
                //You may not have permission to perform
                Console.WriteLine(ex.Message);
                Assert.IsTrue(false);
            }
            catch (SqlDependencyProviderException ex)
            {
                Console.WriteLine(ex.Message);
                Assert.IsTrue(false);
            }
            catch (Exception ex)
            {
                //You may not have permission to perform
                Console.WriteLine(ex.Message);
                Assert.IsTrue(false);
            }
            finally
            {
                Assert.IsTrue(true);
            }
        }

        private void Instance_Onlog(object sender, string e)
        {
            Console.WriteLine(e);
        }

        private bool Task_OnSelectResult(Service.SqlDependecyTask Sender, DataTable dt)
        {
            foreach (DataRow item in dt.Rows)
            {

            }

            //if you want to resume tracking return True otherwise return false
            return true;
        }

    }
}
