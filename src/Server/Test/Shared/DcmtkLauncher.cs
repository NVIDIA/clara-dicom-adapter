/*
 * Apache License, Version 2.0
 * Copyright 2019-2020 NVIDIA Corporation
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Diagnostics;
using System.Text;

namespace Nvidia.Clara.DicomAdapter.Test.Shared
{
    public class DcmtkLauncher
    {
        private const int PROCESS_TIMEOUT = 60000;

        public static Process LaunchNoWait(string exe, string args, StringBuilder outputStringBuilder, string host = "localhost", string port = "1104", string input = "")
        {
            Process process = null;
            try
            {
                var processStartInfo = new ProcessStartInfo(exe, $"-v {args} {host} {port} {input}");
                processStartInfo.RedirectStandardError = true;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;
                process = new Process();
                process.StartInfo = processStartInfo;
                process.EnableRaisingEvents = false;
                process.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);

                Console.WriteLine($"Launching {processStartInfo.FileName} with {processStartInfo.Arguments}");

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return process;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching {exe}: {ex}");
                return null;
            }
        }

        public static string[] Launch(string exe, string args, out int exitCode, string host = "localhost", string port = "1104", string input = "")
        {
            exitCode = 0;
            Process process = null;
            var outputStringBuilder = new StringBuilder();
            try
            {
                var processStartInfo = new ProcessStartInfo(exe, $"-v {args} {host} {port} {input}");
                processStartInfo.RedirectStandardError = true;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;
                process = new Process();
                process.StartInfo = processStartInfo;
                process.EnableRaisingEvents = false;
                process.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);

                Console.WriteLine($"Launching {processStartInfo.FileName} with {processStartInfo.Arguments}");

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var processExited = process.WaitForExit(PROCESS_TIMEOUT);

                if (processExited == false)
                {
                    process.Kill();
                    return new string[] { };
                }
                else if (process.ExitCode != 0)
                {
                    outputStringBuilder.AppendLine($"Process exited with non-zero exit code of: {process.ExitCode}!!");
                }
                exitCode = process.ExitCode;

                return ConvertToList(outputStringBuilder);
            }
            finally
            {
                process?.Close();
            }
        }

        private static string[] ConvertToList(StringBuilder outputStringBuilder)
        {
            try
            {
                return outputStringBuilder.ToString().Split("\n", StringSplitOptions.RemoveEmptyEntries);
            }
            catch (System.Exception)
            {
                return new string[] { };
            }
        }

        public static string[] EchoScu(string args, out int exitCode)
        {
            exitCode = 0;
            return Launch("echoscu", args, out exitCode);
        }

        public static Process StoreScuNoWait(string sourceDir, string transferSyntax, string args, StringBuilder output)
        {
            return LaunchNoWait("storescu", $"+sd +r -R {transferSyntax} {args}", output, input: sourceDir);
        }

        public static string[] StoreScu(string sourceDir, string transferSyntax, string args, out int exitCode, string port = "1104")
        {
            exitCode = 0;
            return Launch("storescu", $"+sd +r -R {transferSyntax} {args}", out exitCode, input: sourceDir, port: port);
        }
    }
}