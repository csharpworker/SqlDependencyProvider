# Sql Dependency Provider
A Lightweigth SQL Dependency Provider C# 4.5 library

this library use [ADO.NET SQL Depedency](https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql/detecting-changes-with-sqldependency) that work on any query in limitations

The most complete list as follows:
- The projected columns in the SELECT statement must be explicitly stated, and table names must be qualified with two-part names. Notice that this means that all tables referenced in the statement must be in the same database.
- The statement may not use the asterisk () or table_name. syntax to specify columns.
- The statement may not use unnamed columns or duplicate column names.
- The statement must reference a base table.
- The statement must not reference tables with computed columns.
- The projected columns in the SELECT statement may not contain aggregate expressions unless the statement uses a GROUP BY expression. When a GROUP BY expression is provided, the select list may contain the aggregate functions COUNT_BIG() or SUM(). However, SUM() may not be specified for a nullable column. The statement may not specify HAVING, CUBE, or ROLLUP.
- A projected column in the SELECT statement that is used as a simple expression must not appear more than once.
- The statement must not include PIVOT or UNPIVOT operators.
- The statement must not include the UNION, INTERSECT, or EXCEPT operators.
- The statement must not reference a view.
- The statement must not contain any of the following: DISTINCT, COMPUTE or COMPUTE BY, or INTO.
- The statement must not reference server global variables (@@variable_name).
- The statement must not reference derived tables, temporary tables, or table variables.
- The statement must not reference tables or views from other databases or servers.
- The statement must not contain subqueries, outer joins, or self-joins.
- The statement must not reference the large object types: text, ntext, and image.
- The statement must not use the CONTAINS or FREETEXT full-text predicates.
- The statement must not use rowset functions, including OPENROWSET and OPENQUERY.
- The statement must not use any of the following aggregate functions: AVG, COUNT(*), MAX, MIN, STDEV, STDEVP, VAR, or VARP.
- The statement must not use any nondeterministic functions, including ranking and windowing functions.
- The statement must not contain user-defined aggregates.
- The statement must not reference system tables or views, including catalog views and dynamic management views.
- The statement must not include FOR BROWSE information.
- The statement must not reference a queue.
- The statement must not contain conditional statements that cannot change and cannot return results (for example, WHERE 1=0).
- The statement can not specify READPAST locking hint.
- The statement must not reference any Service Broker QUEUE.
- The statement must not reference synonyms.
- The statement must not have comparison or expression based on double/real data types.
- The statement must not use the TOP expression.

There is also a documented information:
- [Creating a Query for Notification](https://msdn.microsoft.com/en-us/library/ms181122.aspx)


## Sample Usage

We are prevent limitaion for `GETDATE()` method so you can use it without worry.

```csharp
   public class program
    {
        static void main()
        {
            try
            {
                //Config Provider
                SqlDependencyProvider.Instance.Onlog += Instance_Onlog;
                SqlDependencyProvider.Instance.PublicSqlConnectionString = "Data Source=.;Initial Catalog=db;Persist Security Info=True;User ID=sa;Password=***********";



                //create service on public connection string
                var service = SqlDependencyProvider.Instance.GetDependency();

                //create task for query and paramerter
                var task1 = service.AppendTask("Select Id,Name from tbl where InsertDate > GETDATE()", new SqlParameter("@ID", 1));
                //raise on any change detected 
                task1.OnSelectResult += Task_OnSelectResult;

                //create task for query and paramerter
                var task2 = service.AppendTask("runonce", "Select a.Id,b.code from tbl1 a left join tbl2 b on a.Id=b.Id where a.Id > @ID", new SqlParameter("@ID", 1));
                //raise on any change detected 
                task2.OnSelectResult += Task_OnSelectResult;



                //Start All tasks on instance in application start up
                SqlDependencyProvider.Instance.StartAsync().GetAwaiter();



                Console.WriteLine("Wait for press any key...");
                Console.ReadLine();



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
            }
            catch (SqlDependencyProviderException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                //You may not have permission to perform
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Console.WriteLine("Finished!");
            }
        }

        static void Instance_Onlog(object sender, string e)
        {
            Console.WriteLine(e);
        }

        static bool Task_OnSelectResult(Service.SqlDependecyTask Sender, DataTable dt)
        {
            foreach (DataRow item in dt.Rows)
            {

            }

            //if you want to resume tracking return True otherwise return false
           if (Sender.Identifier == "runonce")
                return false;
            else
                return true;
        }
    }
```
