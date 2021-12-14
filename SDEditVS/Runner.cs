using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using System.Collections.ObjectModel;

namespace SDEditVS
{
    /// <summary>
    /// Run a process asynchronously via the command prompt, feeding data to its
    /// stdin and capturing its stdout/stderr. 
    /// </summary>
    public class Runner
    {
        public class RunnerResult
        {
            public readonly UInt64 JobId = 0;
            public readonly string CommandLine = null;
            public readonly ReadOnlyCollection<string> Stdout = null;
            public readonly ReadOnlyCollection<string> Stderr = null;
            public readonly int? ExitCode = null;

            public RunnerResult(UInt64 jobId, string commandLine, List<string> stdout, List<string> stderr, int? exitCode)
            {
                JobId = jobId;
                CommandLine = commandLine;
                Stdout = stdout.AsReadOnly();
                Stderr = stderr.AsReadOnly();
                ExitCode = exitCode;
            }
        }

        //########################################################################
        //########################################################################

        private static UInt64 _nextJobId = 1;

        //########################################################################
        //########################################################################

        private string _stdin = null;
        private ProcessStartInfo _processStartInfo = null;
        private Action<RunnerResult> _callback = null;
        private List<string> _stdoutLines = new List<string>();
        private List<string> _stderrLines = new List<string>();
        private UInt64 _jobId = 0;
        private float _timeoutSeconds = 0.0f; // equivalent to infinite
        private string _commandLine = null;

        //########################################################################
        //########################################################################

        public UInt64 JobId
        {
            get => _jobId;
        }

        /// <summary>
        /// Create runner for subprocess.
        /// </summary>
        /// <param name="commandLine">command line to run as if at the command prompt</param>
        /// <param name="workingFolder">working folder to use, or null for whatever the .NET default is</param>
        /// <param name="callback">callback, if any, to invoke on the main thread when subprocess finishes</param>
        /// <param name="env">extra environment variables for the subprocess</param>
        /// <param name="stdin">data to supply to subprocess's redirected stdin, or null if no stdin redirection</param>
        /// <returns>job id, an arbitrary value uniquely identifying this subprocess</returns>
        public static Runner Create(string commandLine, string workingFolder, Action<RunnerResult> callback, Dictionary<string, string> env, string stdin)
        {
            var startInfo = new ProcessStartInfo();

            startInfo.FileName = "cmd.exe";

            // Note the notes about /C in the cmd /? output. The behaviour is a
            // bit ugly, but it makes constructing the string a lot easier. No
            // problems with embedded quotes!..
            startInfo.Arguments = string.Format("/c \"{0}\"", commandLine);

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            startInfo.RedirectStandardInput = NeedStdinRedirection(stdin);
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            if (workingFolder != null) startInfo.WorkingDirectory = workingFolder;

            if (env != null)
            {
                foreach (var kv in env) startInfo.EnvironmentVariables.Add(kv.Key, kv.Value);
            }

            UInt64 jobId = _nextJobId++;

            var runner = new Runner(startInfo, callback, stdin, jobId, commandLine);
            return runner;
        }

        /// <summary>
        /// Run subprocess.
        /// </summary>
        /// <param name="runner">runner instance to run</param>
        /// <param name="async">if false, block until subprocess finishes - note that callback may still be executed asynchronously</param>
        /// <param name="timeoutSeconds">if > 0.0f this is the maxiumum time the runner will take before it is killed</param>
        /// <returns>job id, an arbitrary value uniquely identifying this subprocess</returns>
        public static void Run(Runner runner, bool async, float timeoutSeconds)
        {
            runner._timeoutSeconds = timeoutSeconds;
            if (async)
            {
                ThreadPool.QueueUserWorkItem(ThreadProcThunk, runner);
            }
            else
            {
                runner.ThreadProc();
            }
        }

        //########################################################################
        //########################################################################

        private static bool NeedStdinRedirection(string stdin)
        {
            // appropriate policy?
            return stdin != null;
        }

        //########################################################################
        //########################################################################

        private Runner(ProcessStartInfo processStartInfo, Action<RunnerResult> callback, string stdin, UInt64 jobId, string commandLine)
        {
            _processStartInfo = processStartInfo;
            _callback = callback;
            _stdin = stdin;
            _jobId = jobId;
            _commandLine = commandLine;
        }

        //########################################################################
        //########################################################################

        private static void ThreadProcThunk(object arg)
        {
            var this_ = arg as Runner;
            this_.ThreadProc();
        }

        private void ThreadProc()
        {
            bool good = false;

            int? exitCode;

            using (var process = new Process())
            {
                process.StartInfo = _processStartInfo;

                process.EnableRaisingEvents = true;
                process.ErrorDataReceived += OnErrorDataReceived;
                process.OutputDataReceived += OnOutputDataReceived;

                try
                {
                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (NeedStdinRedirection(_stdin))
                    {
                        process.StandardInput.Write(_stdin);
                        process.StandardInput.Close();//^Z
                    }

                    int timeoutMs = _timeoutSeconds > 0.0f ? (int)(_timeoutSeconds * 1000.0f) : Int32.MaxValue;
                    if (process.WaitForExit(timeoutMs))
                        good = true;
                    else
                        process.Kill();
                }
                catch (System.Exception ex)
                {
                    _stderrLines.Clear();
                    _stderrLines.Append(ex.ToString());
                }

                if (good)
                {
                    exitCode = process.ExitCode;
                }
                else
                {
                    _stdoutLines = null;
                    _stderrLines = null;
                    exitCode = null;
                }
            }

            if (_callback != null)
            {
                var result = new RunnerResult(_jobId, _commandLine, _stdoutLines, _stderrLines, exitCode);
                Action<RunnerResult> callback = _callback;

                // https://stackoverflow.com/questions/58237847/how-to-resolve-vs2019-warning-to-use-joinabletaskfactory-switchtomainthreadasyn
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    callback(result);
                });
            }
        }

        //########################################################################
        //########################################################################
        private void OnDataReceived(DataReceivedEventArgs e, List<string> lines, string name)
        {
            if (e.Data == null)
            {
                //Debug.WriteLine(name + " OnDataReceived: got: null");
            }
            else
            {
                //Debug.WriteLine(name + " OnDataReceived: got: \"" + e.Data + "\"");

                lines.Add(e.Data);
            }
        }

        //########################################################################
        //########################################################################

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            OnDataReceived(e, _stderrLines, "STDERR");
        }

        //########################################################################
        //########################################################################

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            OnDataReceived(e, _stdoutLines, "STDOUT");
        }

        //########################################################################
        //########################################################################
    }
}
