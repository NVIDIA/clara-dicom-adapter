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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Nvidia.Clara.DicomAdapter.Test.Integration
{
    public class StoreScpWrapper : IDisposable
    {
        private Process _process;
        private List<string> _outputStringBuilder;

        /// <summary>
        /// During development, you may set this to true to see output for the storescp wrapper.
        /// </summary>
        private bool outputToConsole = true;

        public StoreScpWrapper(string args, int port)
        {
            _outputStringBuilder = new List<string>();
            KillAllStoreScpsThatAreRunning();

            var processStartInfo = new ProcessStartInfo("storescp", $"-v --ignore {args} {port}");
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            _process = new Process();
            _process.StartInfo = processStartInfo;
            _process.EnableRaisingEvents = true;
            _process.ErrorDataReceived += (sender, eventArgs) =>
            {
                _outputStringBuilder.Add(eventArgs.Data);
                if (outputToConsole) Console.WriteLine("===CLIENT=== {0}", eventArgs.Data);
            };
            _process.OutputDataReceived += (sender, eventArgs) =>
           {
               _outputStringBuilder.Add(eventArgs.Data);
               if (outputToConsole) Console.WriteLine("===CLIENT=== {0}", eventArgs.Data);
           };

            Console.WriteLine($"Launching {processStartInfo.FileName} with {processStartInfo.Arguments}");
            _process.Start();
            if (_process.HasExited)
            {
                throw new ApplicationException($"Failed to start 'storescp' with exit code: {_process.ExitCode}");
            }
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();

            Thread.Sleep(2000); //wait for storescp to be ready
            Console.WriteLine("storescp #{0} listening on {1}", _process.Id, port);
        }

        /// <summary>
        /// This method shall be call right before Dispose as it closes the StandardInput stream
        /// </summary>
        /// <returns></returns>
        public string[] GetLogs()
        {
            Thread.Sleep(2500);

            _process.StandardInput.Flush();
            _process.StandardInput.Close();
            Thread.Sleep(1000);

            return _outputStringBuilder.ToArray();
        }

        public void Dispose()
        {
            try
            {
                Thread.Sleep(1000);
                Console.WriteLine("Stopping storescp #{0}", _process.Id);
                _process.Kill();
                KillAllStoreScpsThatAreRunning();
            }
            catch
            {
            }
        }

        private void KillAllStoreScpsThatAreRunning()
        {
            foreach (var proc in Process.GetProcessesByName("storescp"))
            {
                try
                {
                    proc.Kill();
                }
                catch
                {
                }
            }
        }
    }
}
