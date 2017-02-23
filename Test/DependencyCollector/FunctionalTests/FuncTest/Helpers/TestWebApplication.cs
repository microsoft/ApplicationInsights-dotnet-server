namespace FuncTest.Helpers
{
    using System;
    using System.IO;
    using System.Reflection;

    internal abstract class TestWebApplication
    {
        /// <summary>Gets or sets the app name.</summary>
        internal string AppName { get; set; }

        /// <summary>Gets or sets the port.</summary>
        internal int Port { get; set; }

        /// <summary>Gets the app folder.</summary>
        internal string AppFolder
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path).Replace('/', Path.DirectorySeparatorChar);
                string baseExecutingDir = path.Substring(0, path.LastIndexOf(Path.DirectorySeparatorChar));
                return string.Join(Path.DirectorySeparatorChar.ToString(), new string[2] { baseExecutingDir, this.AppName });
            }
        }

        /// <summary>The execute anonymous request.</summary>
        /// <param name="queryString">The query string.</param>
        internal string ExecuteAnonymousRequest(string queryString)
        {
            string url = string.Format("http://localhost:{0}/ExternalCalls.aspx{1}", this.Port, queryString);

            string response;
            RequestHelper.ExecuteAnonymousRequest(url, out response);
            return response;
        }

        /// <summary>The deploy.</summary>
        internal abstract void Deploy();

        /// <summary>The do test.</summary>
        /// <param name="action">The action.</param>
        /// <param name="instrumentRedApp">Whether red app needs to be instrumented or not.</param>
        internal abstract void DoTest(Action<TestWebApplication> action);

        /// <summary>The remove.</summary>
        internal abstract void Remove();
    }
}
