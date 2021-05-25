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

namespace DotNetCoreTestTest
{
    public class CustomActions
    {
        // Definition einer Liste von Strings, in denen die zu suchenden Runtimes gespeichert werden.
        // Tatsächlich wird in diesem Programm aber nur der erste Eintrag "runtimes[0]" benutzt,
        // also "Microsoft.NETCore.App" (siehe unten).

        static readonly List<string> runtimes = new List<string>()
        {
            "Microsoft.NETCore.App",            //.NET Runtime
            "Microsoft.AspNetCore.App",         //ASP.NET Core Runtime
            "Microsoft.WindowsDesktop.App",     //.NET Desktop Runtime
        };

        [CustomAction]
        public static ActionResult DotNetCoreTest(Session session)
        {
            // Definition der Variablen

            // Mindest-Versionsnummer von .NET Core

            var minVersion = new Version(3, 1, 14);

            // String für den Programm-Aufruf an der Kommandozeile, hier also
            //   cmd /c dotnet --list-runtimes
            // Der Parameter /c ist wichtig - dadurch wird die Kommandozeile nach der
            // Ausführung des dotnet-Kommandos automatisch wieder beendet.

            var command = "/c dotnet --list-runtimes";

            // Leerer String für die Ausgabe

            var output = string.Empty;

            // An dieser Stelle wird der Start des Kommandozeilen-Prozesses vorbereitet:
            // Ein Objekt der Klasse Process wird erzeugt, das als Eigenschaft u.a. ein
            // Objekt der Klasse ProcessStartInfo besitzt. Dieses wird mit den gewünschten
            // Werten initialisiert.

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

                p.Start();          // Prozess wird gestartet (hier: cmd.exe, also die Kommandozeile)

                // Die Ausgabe des Kommandozeilen-Programms "dotnet --list-runtimes" besteht
                // (normalerweise) aus mehreren Zeilen, die jetzt innerhalb einer Schleife
                // in die String-Variable "output" eingelesen werden, jeweils gefolgt von einem
                // Zeilenumbruch. Die Eigenschaft "NewLine" der Klasse "Environment" enthält das
                // (bzw. die) Steuerzeichen für den Zeilenumbruch, abhängig vom Betriebssystem:
                // "\r\n" für Windows, "\n" für Linux und MacOSX oder "\r" für ältere MacOS-Versionen.

                while (!p.StandardOutput.EndOfStream)
                {
                    output += $"{p.StandardOutput.ReadLine()}{Environment.NewLine}";
                }

                p.WaitForExit();    // Warten auf Beendigung des Prozesses

                // Wenn der Prozess nicht erfolgreich beendet wurde (dann wäre der ExitCode = 0),
                // dann wird die Session-Variable "DOTNETCORE3114" des WiX-Skripts auf Null gesetzt.
                // Als Rückgabewert wird aber trotzdem "Success" zurückgegeben (an dieser Stelle
                // könnte es sinnvoll sein, eine Exception abzufangen)

                if (p.ExitCode != 0)
                {
                    session["DOTNETCORE3114"] = "0";
                    return ActionResult.Success;
                    //throw new Exception($"{p.ExitCode}:{ p.StandardError.ReadToEnd()}");
                }

                // An dieser Stelle wird das Ergebnis des Vergleichs der aktuellen Versionsnummer
                // von .NET Core mit der Mindest-Versionsnummer in die Session-Variable "DOTNETCORE3114"
                // geschrieben. Dazu wird die unten definierte Methode GetLatestVesionOfRuntime() aufgerufen.

                session["DOTNETCORE3114"] = (GetLatestVersionOfRuntime(runtimes[0], output) < minVersion) ? "0" : "1";

                return ActionResult.Success;
            }
        }

        // Diese Methode parst den (meist mehrzeiligen) String, den das Kommandozeilen-Programm
        // "dotnet" ausgibt (und der jetzt im String "output" steht, der dieser Methode als
        // String-Argument "runtimesList" übergeben wird) und sucht darin die Versionsnummer
        // der neuesten Version von .NET Core. Im ebenfalls übergebenen Argument "runtime"
        // steht nur der erste Listeneintrag "Microsoft.NETCore.App".
        // Rückgabewert ist ein Objekt der Klasse "Version".

        private static Version GetLatestVersionOfRuntime(string runtime, string runtimesList)
        {
            // An dieser Stelle wird der (wahrscheinlich mehrzeilige) String "runtimesList"
            // in mehrere Teilstrings gesplittet. Trennzeichen ist '\n' (nicht "Environment.NewLine",
            // weil das ein String ist und kein char). Die Teilstrings werden in einer Art temporären
            // Liste zwischengespeichert. Dort werden die Teilstrings gesucht, die den String "runtime"
            // enthalten (also "Microsoft.NETCore.App"), und diese werden alphanumerisch sortiert.
            // Im String "latestLine" steht am Ende der alphanumerisch letzte (bzw. der einzige)
            // Teilstring, in dem "Microsoft.NETCore.App" enthalten ist.

            var latestLine = runtimesList.Split('\n').ToList().Where(x => x.Contains(runtime)).OrderBy(x => x).LastOrDefault();

            // Wenn in "runtimesList" keine Zeile enthalten war, die "Microsoft.NETCore.App" enthält,
            // ist "latestLine" leer. Sonst wird die Versionsnummer mit Hilfe eines regulären Ausdrucks
            // aus "latestLine" ermittelt.

            if (latestLine != null)
            {
                // Ein Objekt der Klasse Regex (Regular Expression) wird erzeugt. Gesucht wird nach
                // einem Teilstring, der aus mehreren durch Punkte voneinander getrennten Dezimalzahlen
                // besteht. Das @-Zeichen vor dem String bewirkt, dass Sonderzeichen wie der Backslash
                // nicht mehr maskiert werden müssen.

                Regex pattern = new Regex(@"\d+(\.\d+)+");

                // Ein Objekt der Klasse Match (sinngemäß "Treffer") wird erzeugt. Der in "pattern"
                // gespeicherte reguläre Ausdruck wird auf den String "latestLine" angewandt.
                // Rückgabewert ist die erste Übereinstimmung oder "Match.Empty".

                Match m = pattern.Match(latestLine);

                // Der Wert des Matches steht in seiner Eigenschaft "Value" und wird in der String-
                // Variablen "versionValue" gespeichert.

                string versionValue = m.Value;

                // Jetzt wird der String "version.Value" geparst und sein Wert in einem Objekt
                // der Klasse "Version" gespeichert, das der Rückgabewert dieser Methode ist.

                if (Version.TryParse(versionValue, out var version))
                {
                    return version;
                }
            }

            return null;        // "latestLine" war Null; es wurde keine Zeile gefunden,
                                // die den String "Microsoft.NETCore.App" enthält.
        }
    }
}
