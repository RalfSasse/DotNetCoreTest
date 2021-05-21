/******************************************************************************
 * CustomAction.cs
 * Projekt DotNetCoreTest / CustomAction für WiX
 * Datum: 19.05.2021
 * Autor: Ralf Sasse
 * Quelle: https://stackoverflow.com/questions/58930065/check-if-netcore-is-installed-using-customaction-with-wix
 * 
 ******************************************************************************/

using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Collections.Generic;
using System.Diagnostics;       // für Objekte der Klassen Process und ProcessStartInfo
using System.Linq;
using System.Text.RegularExpressions;

namespace DotNetCoreTest
{
    public class CustomActions
    {
        static readonly List<string> runtimes = new List<string>()
        {
            "Microsoft.NETCore.App",//.NET Runtime
            "Microsoft.AspNetCore.App",//ASP.NET Core Runtime
            "Microsoft.WindowsDesktop.App",//.NET Desktop Runtime
        };

        [CustomAction]
        public static ActionResult DotNetCoreTest(Session session)
        {
            var minVersion = new Version(3, 1, 14);
            var command = "/c dotnet --list-runtimes";// /c is important here
            var output = string.Empty;
            using (var p = new Process())
            {
                p.StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                p.Start();
                while (!p.StandardOutput.EndOfStream)
                {
                    output += $"{p.StandardOutput.ReadLine()}{Environment.NewLine}";
                }
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    session["DOTNETCORE3114"] = "0";
                    return ActionResult.Success;
                    //throw new Exception($"{p.ExitCode}:{ p.StandardError.ReadToEnd()}");
                }
                session["DOTNETCORE3114"] = (GetLatestVersionOfRuntime(runtimes[0], output) < minVersion) ? "0" : "1";
                return ActionResult.Success;
            }
        }

        private static Version GetLatestVersionOfRuntime(string runtime, string runtimesList)
        {
            var latestLine = runtimesList.Split('\n').ToList().Where(x => x.Contains(runtime)).OrderBy(x => x).LastOrDefault();
            if (latestLine != null)
            {
                Regex pattern = new Regex(@"\d+(\.\d+)+");
                Match m = pattern.Match(latestLine);
                string versionValue = m.Value;
                if (Version.TryParse(versionValue, out var version))
                {
                    return version;
                }
            }
            return null;
        }
    }
}
