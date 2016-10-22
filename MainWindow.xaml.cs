using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MinicapAdapter
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        TW.Minicap.MinicapAdapter adapter;
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AdbAdapter.AdbExecutable = new FileInfo(Properties.Settings.Default.ADB).FullName;

            try {
                var client = new TW.Minicap.MinicapClient();
                client.MinicapFrameEvent += Client_MinicapFrameEvent;

                var adb = new AdbAdapter();
                var devicelist = adb.ConnectedDevices();
                if (devicelist.Count == 0)
                    throw new Exception("Please connect a device.");

                var device = devicelist[0];
                adapter = new TW.Minicap.MinicapAdapter(device, client);
                adapter.Start();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Client_MinicapFrameEvent(byte[] frameBody)
        {
            FrameBody = frameBody;
        }

        private byte[] _FrameBody;
        public byte[] FrameBody
        {
            get
            {
                return _FrameBody;
            }
            set
            {
                if (_FrameBody == value)
                    return;
                _FrameBody = value;
                OnPropertyChanged("FrameBody");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            adapter.Stop();

            base.OnClosed(e);
        }

        private void Portrait_Click(object sender, RoutedEventArgs e)
        {
            adapter.Rotation = TW.Minicap.SurfaceRotation.Rotation0;
        }
        private void Landscape_Click(object sender, RoutedEventArgs e)
        {
            adapter.Rotation = TW.Minicap.SurfaceRotation.Rotation90;
        }
    }
}
