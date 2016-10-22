using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MinicapAdapter
{
    public class AdbAdapter
    {
        private int DEFAULT_TIMEOUT = -1;
        private object _Lock = new object();
        private static string _AdbExecutable;
        public static string AdbExecutable
        {
            get { return _AdbExecutable ?? (_AdbExecutable = new FileInfo(@"..\android_sdk\platform-tools\adb.exe").FullName); }
            set { _AdbExecutable = value; }
        }

        public virtual string RunAdbCommand(string command)
        {
            var result = string.Empty;

            lock(_Lock)
            {
                result = ProcessUtils.RunAndReturnOutput(AdbExecutable, command, DEFAULT_TIMEOUT);
            }

            return result;
        }

        public virtual string RunShellCommand(string command)
        {
            return RunAdbCommand(string.Format(" shell \"{0}\"", command));
        }

        public virtual void RunAdbCommandNoReturn(string command)
        {
            lock(_Lock)
            {
                ProcessUtils.RunAndNoReturn(AdbExecutable, command, DEFAULT_TIMEOUT);
            }
        }

        public virtual void RunShellCommandNoReturn(string command)
        {
            RunAdbCommandNoReturn(string.Format(" shell \"{0}\"", command));
        }

        public void StartServer()
        {
            RunAdbCommand("start-server");
        }

        public void StopServer()
        {
            RunAdbCommand("kill-server");
        }

        public List<DeviceAdapter> ConnectedDevices()
        {
            var output = RunAdbCommand("devices");

            var list = new List<DeviceAdapter>();
            using (var st = new StringReader(output))
            {
                while(st.Peek() != -1)
                {
                    var line = st.ReadLine();

                    if (line == string.Empty) continue;

                    var match = Regex.Match(line.Trim(), @"^(?<device>[\S]+)\tdevice$");

                    if (match.Success)
                        list.Add(new DeviceAdapter(match.Groups["device"].Value));
                }
            }

            return list;
        }
    }

    public class DeviceAdapter : AdbAdapter
    {
        public string SerialNumber { get; private set; }
        public int DisplayHeight { get; private set; }
        public int DisplayWidth { get; private set; }
        public DeviceProp Prop { get; private set; }

        public DeviceAdapter(string serialNumber)
        {
            SerialNumber = serialNumber;

            GetDisplaySize();

            Prop = new DeviceProp(this);
        }

        public override string RunAdbCommand(string command)
        {
            return base.RunAdbCommand(string.Format(" -s {0} {1}", SerialNumber, command));
        }

        public override void RunAdbCommandNoReturn(string command)
        {
            base.RunAdbCommandNoReturn(command);
        }

        private void GetDisplaySize()
        {
            var output = RunShellCommand("wm size");
            var match = Regex.Match(output, @"^Physical size: (?<width>[\d]+)x(?<height>[\d]+)$");
            if (match.Success)
            {
                DisplayHeight = int.Parse(match.Groups["height"].Value);
                DisplayWidth = int.Parse(match.Groups["width"].Value);
            }
        }
    }

    public class DeviceProp
    {
        private string _Abi;
        private string _SDK;
        private Dictionary<string, string> _KV;
        public DeviceAdapter Device { get; private set; }
        public DeviceProp(DeviceAdapter device)
        {
            Device = device;
            _KV = new Dictionary<string, string>();
        }

        public string Abi
        {
            get { return _Abi ?? (_Abi = Device.RunShellCommand("getprop ro.product.cpu.abi")); }
        }

        public string SDK
        {
            get { return _SDK ?? (_SDK = Device.RunShellCommand("getprop ro.build.version.sdk")); }
        }

        public Dictionary<string, string> KV
        {
            get
            {
                if (_KV == null)
                {
                    _KV = new Dictionary<string, string>();
                    var output = Device.RunShellCommand(" getprop ");
                    using (var s = new StringReader(output))
                    {
                        while(s.Peek() != -1)
                        {
                            var line = s.ReadLine();

                            if (line == string.Empty) continue;

                            var match = Regex.Match(line.Trim(), @"^[[](?<key>[.,-\S]+)[]]: [[](?<value>[.,-\S]+)$");

                            if (match.Success)
                                _KV.Add(match.Groups["key"].Value, match.Groups["value"].Value);
                        }
                    }

                }
                return _KV;
            }
        }
    }

    public static class ProcessUtils
    {
        public static void RunAndNoReturn(string executable, string arguments, int timeout)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = executable;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = true;

                p.Start();

                //p.WaitForExit(timeout);
            }
        }

        public static string RunAndReturnOutput(string executable, string arguments, int timeout, bool forceRegular = false)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = executable;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                    return HandleOutput(p, outputWaitHandle, errorWaitHandle, timeout, forceRegular);
            }
        }

        private static string HandleOutput(Process p, AutoResetEvent outputWaitHandle, AutoResetEvent errorWaitHandle, int timeout, bool forceRegular)
        {
            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            p.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                    outputWaitHandle.Set();
                else
                    output.AppendLine(e.Data);
            };
            p.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                    errorWaitHandle.Set();
                else
                    error.AppendLine(e.Data);
            };

            p.Start();

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            if (p.WaitForExit(timeout) && outputWaitHandle.WaitOne(timeout) && errorWaitHandle.WaitOne(timeout))
            {
                string strReturn = "";

                if (error.ToString().Trim().Length.Equals(0) || forceRegular)
                    strReturn = output.ToString().Trim();
                else
                    strReturn = error.ToString().Trim();

                return strReturn;
            }
            else
            {
                // Timed out.
                return "PROCESS TIMEOUT";
            }
        }
    }
}
