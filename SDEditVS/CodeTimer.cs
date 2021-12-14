
using System.IO;

namespace SDEditVS
{
    public class CodeTimer
    {
        private System.Diagnostics.Stopwatch _stopwatch;
        private string _name;

        public CodeTimer(string name)
        {
            _name = name;
            _stopwatch = new System.Diagnostics.Stopwatch();
            _stopwatch.Start();
        }
        public void Stop(StreamWriter outputWindow)
        {
            _stopwatch.Stop();
            outputWindow.WriteLine("{0} took {1}ms", _name, _stopwatch.ElapsedMilliseconds);
        }
    }
}