using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace FuncTest.Helpers
{
    internal class DotNetCoreTestWebApplication : TestWebApplication
    {
        private DotNetCoreProcess process;

        internal override void Deploy()
        {
            string arguments = $"\"{Path.Combine(this.AppFolder, this.AppName + ".dll")}\" {this.Port}";
            string output = "";
            string error = "";

            process = new DotNetCoreProcess(arguments)
                .RedirectStandardOutputTo((string outputMessage) => output += outputMessage)
                .RedirectStandardErrorTo((string errorMessage) => error += errorMessage)
                .Start();

            bool serverStarted = false;
            while (!serverStarted)
            {
                if (!string.IsNullOrEmpty(error))
                {
                    process.WaitForExit();
                    Assert.Inconclusive($"Failed to start .NET Core server using command 'dotnet.exe {arguments}': {error}");
                }
                else if (output.Contains("Now listening on"))
                {
                    serverStarted = true;
                }
                else
                {
                    // Let someone else run with the hope that the dotnet.exe process will run.
                    Thread.Yield();
                }
            }
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
