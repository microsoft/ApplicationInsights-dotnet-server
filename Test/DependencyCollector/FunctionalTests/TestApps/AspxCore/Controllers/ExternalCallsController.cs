using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Text;

namespace AspxCore.Controllers
{
    [Route("external/calls")]
    public class ExternalCallsController : Controller
    {/// <summary>
     /// Invalid Hostname to trigger exception being thrown
     /// </summary>
        private const string InvalidHostName = "http://www.zzkaodkoakdahdjghejajdnad.com";

        private string GetQueryValue(string valueKey)
        {
            return Request.Query[valueKey].ToString();
        }

        // GET external/calls
        [HttpGet]
        public string Get()
        {
            string title = "(No title set)";
            string response = "(No response set)";

            string type = GetQueryValue("type");
            string countStr = GetQueryValue("count");

            //bool success = true;
            //bool.TryParse(GetQueryValue("success"), out success);
            //string sqlQueryToUse = success ? ValidSqlQueryToApmDatabase : InvalidSqlQueryToApmDatabase;

            int count;
            if (!int.TryParse(countStr, out count))
            {
                count = 1;
            }

            switch (type)
            {
                case "http":
                    title = "Made Sync GET HTTP call to bing";
                    response = MakeHttpGetCallSync(count, "bing");
                    break;
                //case "httpClient":
                //    HttpHelper40.MakeHttpCallUsingHttpClient("http://www.google.com/404");
                //    break;
                case "httppost":
                    title = "Made Sync POST HTTP call to bing";
                    response = MakeHttpPostCallSync(count, "bing");
                    break;
                case "failedhttp":
                    title = "Made failing Sync GET HTTP call to bing";
                    response = MakeHttpCallSyncFailed(count);
                    break;
                //case "httpasync1":
                //    HttpHelper40.MakeHttpCallAsync1(count, "bing");
                //    break;
                //case "failedhttpasync1":
                //    HttpHelper40.MakeHttpCallAsync1Failed(count);
                //    break;
                //case "httpasync2":
                //    HttpHelper40.MakeHttpCallAsync2(count, "bing");
                //    break;
                //case "failedhttpasync2":
                //    HttpHelper40.MakeHttpCallAsync2Failed(count);
                //    break;
                //case "httpasync3":
                //    HttpHelper40.MakeHttpCallAsync3(count, "bing");
                //    break;
                //case "failedhttpasync3":
                //    HttpHelper40.MakeHttpCallAsync3Failed(count);
                //    break;
                //case "httpasync4":
                //    HttpHelper40.MakeHttpCallAsync4(count, "bing");
                //    break;
                //case "failedhttpasync4":
                //    HttpHelper40.MakeHttpCallAsync4Failed(count);
                //    break;
                //case "httpasyncawait1":
                //    HttpHelper45.MakeHttpCallAsyncAwait1(count, "bing");
                //    break;
                //case "failedhttpasyncawait1":
                //    HttpHelper45.MakeHttpCallAsyncAwait1Failed(count);
                //    break;
                //case "sql":
                //    this.MakeSQLCallSync(count);
                //    break;
                //case "azuresdkblob":
                //    HttpHelper40.MakeAzureCallToReadBlobWithSdk(count);
                //    break;
                //case "azuresdkqueue":
                //    HttpHelper40.MakeAzureCallToWriteQueueWithSdk(count);
                //    break;
                //case "azuresdktable":
                //    HttpHelper40.MakeAzureCallToWriteTableWithSdk(count);
                //    HttpHelper40.MakeAzureCallToReadTableWithSdk(count);
                //    break;
                //case "ExecuteReaderAsync":
                //    SqlCommandHelper.ExecuteReaderAsync(ConnectionString, sqlQueryTouse);
                //    break;
                //case "ExecuteScalarAsync":
                //    SqlCommandHelper.ExecuteScalarAsync(ConnectionString, sqlQueryTouse);
                //    break;
                //case "ExecuteReaderStoredProcedureAsync":
                //    this.ExecuteReaderStoredProcedureAsync();
                //    break;
                //case "TestExecuteReaderTwice":
                //    SqlCommandHelper.TestExecuteReaderTwice(ConnectionString, sqlQueryTouse);
                //    break;
                //case "BeginExecuteReader0":
                //    SqlCommandHelper.BeginExecuteReader(ConnectionString, sqlQueryTouse, 0);
                //    break;
                //case "BeginExecuteReader1":
                //    SqlCommandHelper.BeginExecuteReader(ConnectionString, sqlQueryTouse, 1);
                //    break;
                //case "BeginExecuteReader2":
                //    SqlCommandHelper.BeginExecuteReader(ConnectionString, sqlQueryTouse, 2);
                //    break;
                //case "BeginExecuteReader3":
                //    SqlCommandHelper.BeginExecuteReader(ConnectionString, sqlQueryTouse, 3);
                //    break;
                //case "TestExecuteReaderTwiceInSequence":
                //    SqlCommandHelper.TestExecuteReaderTwiceInSequence(ConnectionString, sqlQueryTouse);
                //    break;
                //case "TestExecuteReaderTwiceWithTasks":
                //    SqlCommandHelper.AsyncExecuteReaderInTasks(ConnectionString, sqlQueryTouse);
                //    break;
                //case "ExecuteNonQueryAsync":
                //    SqlCommandHelper.ExecuteNonQueryAsync(ConnectionString, sqlQueryTouse);
                //    break;
                //case "BeginExecuteNonQuery0":
                //    SqlCommandHelper.BeginExecuteNonQuery(ConnectionString, sqlQueryTouse, 0);
                //    break;
                //case "BeginExecuteNonQuery2":
                //    SqlCommandHelper.BeginExecuteNonQuery(ConnectionString, sqlQueryTouse, 2);
                //    break;
                //case "ExecuteXmlReaderAsync":
                //    sqlQueryTouse += " FOR XML AUTO";
                //    SqlCommandHelper.ExecuteXmlReaderAsync(ConnectionString, sqlQueryTouse);
                //    break;
                //case "BeginExecuteXmlReader":
                //    SqlCommandHelper.BeginExecuteXmlReader(ConnectionString, sqlQueryTouse);
                //    break;
                //case "SqlCommandExecuteScalar":
                //    sqlQueryTouse = (success == true)
                //                    ? ValidSqlQueryCountToApmDatabase
                //                    : InvalidSqlQueryToApmDatabase;
                //    SqlCommandHelper.ExecuteScalar(ConnectionString, sqlQueryTouse);
                //    break;
                //case "SqlCommandExecuteNonQuery":
                //    sqlQueryTouse = (success == true)
                //           ? ValidSqlQueryCountToApmDatabase
                //           : InvalidSqlQueryToApmDatabase;
                //    SqlCommandHelper.ExecuteNonQuery(ConnectionString, sqlQueryTouse);
                //    break;
                //case "SqlCommandExecuteReader0":
                //    SqlCommandHelper.ExecuteReader(ConnectionString, sqlQueryTouse, 0);
                //    break;
                //case "SqlCommandExecuteReader1":
                //    SqlCommandHelper.ExecuteReader(ConnectionString, sqlQueryTouse, 1);
                //    break;
                //case "SqlCommandExecuteXmlReader":
                //    sqlQueryTouse += " FOR XML AUTO";
                //    SqlCommandHelper.ExecuteXmlReader(ConnectionString, sqlQueryTouse);
                //    break;
                //case "SqlConnectionOpen":
                //    sqlQueryTouse = "Open";
                //    SqlCommandHelper.OpenConnection(this.GetConnectionString(success, Request.QueryString["exceptionType"]));
                //    break;
                //case "SqlConnectionOpenAsync":
                //    sqlQueryTouse = "Open";
                //    SqlCommandHelper.OpenConnectionAsync(this.GetConnectionString(success, Request.QueryString["exceptionType"]));
                //    break;
                //case "SqlConnectionOpenAsyncAwait":
                //    sqlQueryTouse = "Open";
                //    SqlCommandHelper.OpenConnectionAsyncAwait(this.GetConnectionString(success, Request.QueryString["exceptionType"]));
                //    break;
                default:
                    title = $"Unrecognized request type '{type}'";
                    response = "";
                    break;
            }

            return $"<HTML><BODY>{title}<BR/>{response}</BODY></HTML>";
        }

        ///// <summary>
        ///// Connection string to APM Development database.
        ///// </summary> 
        //private const string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=RDDTestDatabase;Integrated Security=True";

        ///// <summary>
        ///// Invalid connection string to database.
        ///// </summary> 
        //private const string InvalidConnectionString = @"Data Source=invalid\SQLEXPRESS;Initial Catalog=RDDTestDatabase;Integrated Security=True";

        ///// <summary>
        ///// Connection string to database with invalid account.
        ///// </summary> 
        //private const string InvalidAccountConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=RDDTestDatabase;User ID = AiUser;Password=Some";

        ///// <summary>
        ///// Valid SQL Query. The wait for delay of 6msec is used to prevent access time of less than 1msec. SQL is not accurate below 3, so used 6 msec delay.
        ///// </summary> 
        //private const string ValidSqlQueryToApmDatabase = "WAITFOR DELAY '00:00:00:006'; select * from dbo.Messages";

        ///// <summary>
        ///// Valid SQL Query to get count.
        ///// </summary> 
        //private const string ValidSqlQueryCountToApmDatabase = "WAITFOR DELAY '00:00:00:006'; SELECT count(*) FROM dbo.Messages";

        ///// <summary>
        ///// Invalid SQL Query.
        ///// </summary> 
        //private const string InvalidSqlQueryToApmDatabase = "SELECT TOP 2 * FROM apm.[Database1212121]";

        ///// <summary>
        ///// Label used to identify the query being executed.
        ///// </summary> 
        //private const string QueryToExecuteLabel = "Query Executed:";

        /// <summary>
        /// Make sync http GET calls
        /// </summary>        
        /// <param name="count">no of GET calls to be made</param>        
        /// <param name="hostname">the GET call will be made to http://www.hostname.com</param>        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Reviewed manually")]
        public static string MakeHttpGetCallSync(int count, string hostname)
        {
            string result = "";

            Uri ourUri = new Uri(string.Format("http://www.{0}.com", hostname));
            HttpClient client = new HttpClient();
            for (int i = 0; i < count; i++)
            {
                result += $"Request {i + 1}:<BR/>{client.GetStringAsync(ourUri).Result}<BR/>";
            }

            return result;
        }

        /// <summary>
        /// Make sync http POST calls
        /// </summary>        
        /// <param name="count">no of POST calls to be made</param>        
        /// <param name="hostname">the POST call will be made to http://www.hostname.com</param>        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Reviewed manually")]
        public static string MakeHttpPostCallSync(int count, string hostname)
        {
            string result = "";

            Uri ourUri = new Uri(string.Format("http://www.{0}.com", hostname));
            HttpClient client = new HttpClient();
            HttpContent content = new StringContent("thing1=hello&thing2=world", Encoding.ASCII);
            for (int i = 0; i < count; i++)
            {
                result += $"Request {i + 1}:<BR/>{client.PostAsync(ourUri, content).Result}<BR/>";
            }

            return result;
        }

        /// <summary>
        /// Make sync http calls which fails
        /// </summary>        
        /// <param name="count">no of calls to be made</param>                
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Reviewed manually")]
        public static string MakeHttpCallSyncFailed(int count)
        {
            string result = "";

            Uri ourUri = new Uri(InvalidHostName);
            HttpClient client = new HttpClient();
            for (int i = 0; i < count; ++i)
            {
                result += $"Request {i + 1}:<BR/>";
                try
                {
                    result += client.GetStringAsync(ourUri).Result;
                }
                catch (Exception e)
                {
                    result += "Exception occured (as expected):" + e;
                }
            }

            return result;
        }

        //private string GetConnectionString(bool success, string exceptionType)
        //{
        //    string result = ConnectionString;
        //    if (!success)
        //    {
        //        result = exceptionType.Equals("account", StringComparison.OrdinalIgnoreCase) ? InvalidAccountConnectionString : InvalidConnectionString;
        //    }

        //    return result;
        //}

        ///// <summary>
        ///// Make sync SQL calls.
        ///// </summary>        
        ///// <param name="count">No of calls to be made.</param>        
        //private void MakeSQLCallSync(int count)
        //{
        //    SqlConnection conn = null;
        //    SqlCommand cmd = null;
        //    SqlDataReader rdr = null;
        //    for (int i = 0; i < count; i++)
        //    {
        //        conn = new SqlConnection(ConnectionString);
        //        conn.Open();
        //        cmd = new SqlCommand("GetTopTenMessages", conn);
        //        cmd.CommandType = CommandType.StoredProcedure;
        //        rdr = cmd.ExecuteReader();
        //        rdr.Close();
        //    }
        //}

        //private void ExecuteReaderStoredProcedureAsync()
        //{
        //    var storedProcedureName = this.Request.QueryString["storedProcedureName"];
        //    if (string.IsNullOrEmpty(storedProcedureName))
        //    {
        //        throw new ArgumentException("storedProcedureName query string parameter is not defined.");
        //    }

        //    SqlCommandHelper.ExecuteReaderAsync(ConnectionString, storedProcedureName, CommandType.StoredProcedure);
        //}
    }
}
