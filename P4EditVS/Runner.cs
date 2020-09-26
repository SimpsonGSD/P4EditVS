using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.VisualStudio.Shell;

namespace P4EditVS
{
    /// <summary>
    /// Run a process asynchronously, feeding data to its stdin and capturing
    /// its stdout/stderr. 
    /// </summary>
    class Runner
    {
        public class RunnerResult
        {
            public readonly UInt64 JobId = 0;
            public readonly string Cmd = null;
            public readonly string Args = null;
            public readonly string Stdout = null;
            public readonly string Stderr = null;
            public readonly int? ExitCode = null;

            public RunnerResult(UInt64 jobId, string cmd, string args, string stdout, string stderr, int? exitCode)
            {
                JobId = jobId;
                Cmd = cmd;
                Args = args;
                Stdout = stdout;
                Stderr = stderr;
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
        private StringBuilder _stdoutBuilder = new StringBuilder();
        private StringBuilder _stderrBuilder = new StringBuilder();
        private UInt64 _jobId = 0;

        //########################################################################
        //########################################################################

        public static UInt64 Run(string cmd, string args, Action<RunnerResult> callback, Dictionary<string, string> env, string stdin)
        {
            var startInfo = new ProcessStartInfo();

            startInfo.FileName = cmd;

            if (args != null) startInfo.Arguments = args;

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            if (env != null)
            {
                foreach (var kv in env) startInfo.EnvironmentVariables.Add(kv.Key, kv.Value);
            }

            UInt64 jobId = _nextJobId++;

            var runner = new Runner(startInfo, callback, stdin, jobId);
            ThreadPool.QueueUserWorkItem(ThreadProcThunk, runner);

            return jobId;
        }

        //########################################################################
        //########################################################################

        private Runner(ProcessStartInfo processStartInfo, Action<RunnerResult> callback, string stdin, UInt64 jobId)
        {
            _processStartInfo = processStartInfo;
            _callback = callback;
            _stdin = stdin;
            _jobId = jobId;
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

            string stdout, stderr;
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

                    if (_stdin != null) process.StandardInput.Write(_stdin);

                    process.StandardInput.Close();//^Z

                    // Should really be able to configure the timeout! MaxValue
                    // is probably safest in the absence of that.
                    if (process.WaitForExit(Int32.MaxValue))
                        good = true;
                    else
                        process.Kill();
                }
                catch (System.Exception ex)
                {
                    _stderrBuilder.Clear();
                    _stderrBuilder.Append(ex.ToString());
                }

                if (good)
                {
                    stdout = _stdoutBuilder.ToString();
                    stderr = _stderrBuilder.ToString();

                    exitCode = process.ExitCode;
                }
                else
                {
                    stdout = null;
                    stderr = null;
                    exitCode = null;
                }
            }

            if (_callback != null)
            {
                var result = new RunnerResult(_jobId, _processStartInfo.FileName, _processStartInfo.Arguments, stdout, stderr, exitCode);
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
        private void OnDataReceived(DataReceivedEventArgs e, StringBuilder b, string name)
        {
            if (e.Data == null)
            {
                //Debug.WriteLine(name + " OnDataReceived: got: null");
            }
            else
            {
                //Debug.WriteLine(name + " OnDataReceived: got: \"" + e.Data + "\"");

                if (b.Length > 0)
                    b.Append(Environment.NewLine);

                b.Append(e.Data);
            }
        }

        //########################################################################
        //########################################################################

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            OnDataReceived(e, _stderrBuilder, "STDERR");
        }

        //########################################################################
        //########################################################################

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            OnDataReceived(e, _stdoutBuilder, "STDOUT");
        }

        //########################################################################
        //########################################################################
    }
}
