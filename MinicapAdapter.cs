using MinicapAdapter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TW.Minicap
{
    public class MinicapAdapter
    {
        private int _MinicapPID = -1;
        private SurfaceRotation _Rotation;
        private string _ExecutableDirectory;
        public DeviceAdapter Device { get; private set; }
        public MinicapClient Client { get; private set; }
        public bool IsRunning { get { return _MinicapPID > 0; } }
        public SurfaceRotation Rotation
        {
            get { return _Rotation; }
            set
            {
                if (_Rotation == value)
                    return;
                _Rotation = value;

                Task.Run(() => { Stop(); Start(); });
            }
        }
        public MinicapAdapter(DeviceAdapter device, MinicapClient client)
        {
            Device = device;

            Client = client;

            _Rotation = SurfaceRotation.Rotation0;

            _ExecutableDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        public void Start()
        {
            if (!CheckedInstalledMinicap())
                PushMinicap();

            if (!ValidateMinicapEnv())
                throw new Exception(string.Format("Failed to validate the minicap environment on the device: {0}", Device.SerialNumber));

            StartMinicap();

            GetMinicapProcessInfo();

            ForwardMinicapTcpPort();

            Client.Start();
        }

        public void Stop()
        {
            Client.Dispose();

            Device.RunAdbCommandNoReturn("forward --remove-all");

            Device.RunShellCommandNoReturn(string.Format("kill {0}", _MinicapPID));
        }

        private void PushMinicap()
        {
            Device.RunAdbCommandNoReturn(string.Format("push {0}minicap/bin/{1}/minicap /data/local/tmp/", _ExecutableDirectory, Device.Prop.Abi));
            // for SDK < 16, you will have to use the minicap - nopie executable which comes without PIE support
            if (int.Parse(Device.Prop.SDK) < 16)
                Device.RunAdbCommandNoReturn(string.Format("push {0}minicap/bin/{1}/minicap-nopie /data/local/tmp/", _ExecutableDirectory, Device.Prop.Abi));
            Device.RunAdbCommandNoReturn(string.Format("push {0}minicap/shared/android-{1}/{2}/minicap.so /data/local/tmp/", _ExecutableDirectory, Device.Prop.SDK, Device.Prop.Abi));
         
            // wait 1s for pushing minicap on the android device, or the minicap can not be executed.
            Thread.Sleep(1000);

            // set the executable right to minicap*.
            Device.RunShellCommandNoReturn("cd /data/local/tmp && chmod 777 minicap*");

            // wait 1s for completing the right setting
            Thread.Sleep(1000);
        }

        private bool CheckedInstalledMinicap()
        {
            var output = Device.RunShellCommand("cd /data/local/tmp && ls | grep 'mini'");
            var minicapfiles = new List<string> { "minicap", "minicap-nopie", "minicap.so" };

            using (var r = new StringReader(output))
            {
                while (r.Peek() != -1)
                {
                    var line = r.ReadLine();

                    if (line == string.Empty) continue;

                    minicapfiles.Remove(line.Trim());
                }
            }

            return minicapfiles.Count <= 1;
        }

        private bool ValidateMinicapEnv()
        {
            var output = Device.RunShellCommand(string.Format("LD_LIBRARY_PATH=/data/local/tmp /data/local/tmp/minicap -P {0}x{1}@{0}x{1}/0 -t", Device.DisplayWidth, Device.DisplayHeight));
            var list = new List<string> { "ok" };

            using (var r = new StringReader(output))
            {
                while (r.Peek() != -1)
                {
                    var line = r.ReadLine();

                    if (line == string.Empty) continue;

                    list.Remove(line.Trim().ToLower());
                }
            }

            return list.Count == 0;
        }

        private void StartMinicap()
        {
            var virtualWidth = (Rotation == SurfaceRotation.Rotation90 || Rotation == SurfaceRotation.Rotation270) ? Device.DisplayHeight : Device.DisplayWidth;
            var virtualHeight = (Rotation == SurfaceRotation.Rotation90 || Rotation == SurfaceRotation.Rotation270) ? Device.DisplayWidth : Device.DisplayHeight;

            Device.RunShellCommandNoReturn(string.Format("LD_LIBRARY_PATH=/data/local/tmp /data/local/tmp/minicap -P {0}x{1}@{2}x{3}/{4}", Device.DisplayWidth, Device.DisplayHeight, virtualWidth, virtualHeight, (int)Rotation));
        }

        private void ForwardMinicapTcpPort()
        {
            //var cmd = "adb forward tcp:1313 localabstract:minicap";
            Device.RunAdbCommandNoReturn("forward tcp:1313 localabstract:minicap");
        }

        private void GetMinicapProcessInfo()
        {
            var output = Device.RunShellCommand("ps | grep 'minicap'");

            var match = Regex.Match(output, @"^(?<user>[\S]+)[ ]+(?<pid>[\d]+)[ ]+(?<ppid>[\d]+)[ ]+(?<vsize>[\d]+)[ ]+(?<rss>[\d]+)[ ]+(?<wchan>[\S]+)[ ]+(?<pc>[\S]+)[ ]+(?<s>\S)[ ]+(?<exe>[\S]+)$");

            if (match.Success)
            {
                _MinicapPID = int.Parse(match.Groups["pid"].Value);
            }
        }
    }

    public enum SurfaceRotation
    {
        Rotation0 = 0,
        Rotation90 = 90,
        Rotation180 = 180,
        Rotation270 = 270
    }
}
