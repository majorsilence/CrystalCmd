using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Majorsilence.CrystalCmd.CoreServer;

public class ConsoleSubProcess
{
    private string _crystalCmdNetFrameworkConsoleExeFolder;

    private string _workingFolder;
    public ConsoleSubProcess(string workingFolder, string crystalCmdNetFrameworkConsoleExeFolder)
    {
        _workingFolder = workingFolder;
        _crystalCmdNetFrameworkConsoleExeFolder = crystalCmdNetFrameworkConsoleExeFolder;
    }
    public async Task Run()
    {

        // This method is intended to run the console application as a subprocess.
        // The implementation details would depend on how the console application is structured.
        // For example, you might use Process.Start to run the console application with the necessary arguments.
        // 
        // Calls Majorsilence.CrystalCmd.NetframeworkConsole.exe with the working folder and any other necessary parameters.
        // Here is a placeholder for the actual implementation.

        // if on windows it will run the execetuable directly,
        // if on linux it will run the executable using wine with .netframework installed

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await RunWindowsSubProcess();
        }
        else
        {
            await RunLinuxSubProcess();
        }


    }

    private async Task RunWindowsSubProcess()
    {
        // Implementation for running the subprocess on Windows
        // This could involve using Process.Start with the appropriate executable and arguments
        // For example:
        // Process.Start("Majorsilence.CrystalCmd.NetframeworkConsole.exe", $"WorkingFolder={WorkingFolder}");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = _crystalCmdNetFrameworkConsoleExeFolder,
                FileName = "Majorsilence.CrystalCmd.NetframeworkConsole.exe",
                Arguments = $"WorkingFolder={_workingFolder}",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();
    }

    private async Task RunLinuxSubProcess()
    {
        // Implementation for running the subprocess on Linux using Wine
        // This could involve using Process.Start with the appropriate executable and arguments
        // For example:
        // Process.Start("wine", "Majorsilence.CrystalCmd.NetframeworkConsole.exe WorkingFolder={WorkingFolder}");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = _crystalCmdNetFrameworkConsoleExeFolder,
                FileName = "wine",
                Arguments = $"Majorsilence.CrystalCmd.NetframeworkConsole.exe WorkingFolder={_workingFolder}",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();
    }

}

