namespace FuncTest.Helpers
{
    using System.Diagnostics;

    /// <summary>
    /// A static class that helps interact with .NET Core on this machine.
    /// </summary>
    internal static class DotNetCore
    {
        /// <summary>
        /// Get the version of .NET Core that is installed on this machine, or null if .NET Core is not installed.
        /// </summary>
        internal static string Version
        {
            get
            {
                Process dotnet = DotNetCore.RunProcess("--version");
                string output = dotnet.StandardOutput.ReadToEnd();
                return !string.IsNullOrEmpty(output) && !output.Contains("not recognized") ? output : null;
            }
        }

        /// <summary>
        /// Get whether or not .NET Core is installed on this machine.
        /// </summary>
        /// <returns></returns>
        internal static bool IsInstalled()
        {
            return DotNetCore.Version != null;
        }

        internal static Process RunProcess(string arguments)
        {
            Process process = DotNetCore.StartProcess(arguments);
            process.WaitForExit();
            return process;
        }

        private static Process StartProcess(string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = "dotnet.exe",
                Arguments = arguments
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
            }

            return Process.Start(startInfo);
        }
    }
}
