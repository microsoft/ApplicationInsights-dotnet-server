using System;
using System.Diagnostics;
using System.IO;

namespace FuncTest.Helpers
{
    internal class DotNetCoreTestWebApplication : TestWebApplication
    {
        private Process process;

        internal override void Deploy()
        {
            const string dotnet = "dotnet.exe";
            string aspxCoreDll = Path.Combine(this.AppFolder, this.AppName, ".dll");
            process = Process.Start(dotnet, $"{aspxCoreDll} {this.Port}");
        }

        internal override void DoTest(Action<TestWebApplication> action)
        {
        }

        internal override void Remove()
        {
            if (process != null)
            {
                process.Kill();
                process = null;
            }
        }
    }
}
