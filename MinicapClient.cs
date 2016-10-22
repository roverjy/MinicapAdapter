using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TW.Minicap
{
    public delegate void MinicapFrameEventHandle(byte[] frameBody);
    public class MinicapClient : IDisposable
    {
        private object _lock = new object();
        private int readBannerBytes = 0;
        private int bannerLength = 2;
        private int readFrameBytes = 0;
        private int frameBodyLength = 0;
        private byte[] frameBody;

        private Socket client;
        private byte[] clientBuffer = new byte[1024];

        public MinicapBanner Banner { get; private set; }
        public event MinicapFrameEventHandle MinicapFrameEvent;

        public MinicapClient ()
        {
            Banner = new MinicapBanner();
        }

        public void Start()
        {
            readBannerBytes = 0;
            bannerLength = 2;
            readFrameBytes = 0;
            frameBodyLength = 0;
            Banner = new MinicapBanner();

            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.Connect("localhost", 1313);

            client.BeginReceive(clientBuffer, 0, clientBuffer.Length, SocketFlags.None, OnReceive, this);
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                var minicapClient = (MinicapClient)ar.AsyncState;

                SocketError errorCode;
                var bufferSize = client.EndReceive(ar, out errorCode);
                if (errorCode != SocketError.Success)
                {
                    Dispose();
                    return;
                }

                for (var cursor = 0; cursor < bufferSize;)
                {
                    #region read banner
                    if (readBannerBytes < bannerLength)
                    {
                        switch (readBannerBytes)
                        {
                            case 0:
                                // version
                                Banner.Version = clientBuffer[cursor];
                                break;
                            case 1:
                                // length
                                Banner.Length = bannerLength = clientBuffer[cursor];
                                break;
                            case 2:
                            case 3:
                            case 4:
                            case 5:
                                // pid
                                Banner.PID += (clientBuffer[cursor] << ((readBannerBytes - 2) * 8));
                                break;
                            case 6:
                            case 7:
                            case 8:
                            case 9:
                                // read width
                                Banner.ReadWidth += (clientBuffer[cursor] << ((readBannerBytes - 6) * 8));
                                break;
                            case 10:
                            case 11:
                            case 12:
                            case 13:
                                // real height
                                Banner.ReadHeight += (clientBuffer[cursor] << ((readBannerBytes - 10) * 8));
                                break;
                            case 14:
                            case 15:
                            case 16:
                            case 17:
                                // virtual width
                                Banner.VirtualWidth += (clientBuffer[cursor] << ((readBannerBytes - 14) * 8));
                                break;
                            case 18:
                            case 19:
                            case 20:
                            case 21:
                                // virtual height
                                Banner.VirtualHeight += (clientBuffer[cursor] << ((readBannerBytes - 18) * 8));
                                break;
                            case 22:
                                // orientation
                                Banner.Orientation = clientBuffer[cursor] * 90;
                                break;
                            case 23:
                                // quirks
                                Banner.Quirks = clientBuffer[cursor];
                                break;
                        }

                        cursor += 1;
                        readBannerBytes += 1;

                        if (readBannerBytes == bannerLength)
                        {
                            // log.info(banner.toString());
                        }
                    }
                    #endregion

                    #region read frame length
                    else if (readFrameBytes < 4)
                    {
                        frameBodyLength += (clientBuffer[cursor] << (readFrameBytes * 8));
                        cursor += 1;
                        readFrameBytes += 1;
                        // log.info("headerbyte%d(val=%d)", readFrameBytes, frameBodyLength);
                    }
                    #endregion

                    #region read frame
                    else
                    {
                        if (bufferSize - cursor >= frameBodyLength)
                        {
                            //log.info("bodyfin(len=%d,cursor=%d)", frameBodyLength, cursor)
                            frameBody = BufferConcat(frameBody, clientBuffer, cursor, frameBodyLength);

                            // Sanity check for JPG header, only here for debugging purposes.
                            if (frameBody[0] != 0xFF || frameBody[1] != 0xD8)
                            {
                                throw new Exception("Frame Body JPG header error.");
                            }

                            // send jpeg frame to ui
                            if (MinicapFrameEvent != null)
                                MinicapFrameEvent(frameBody);

                            cursor += frameBodyLength;
                            frameBodyLength = readFrameBytes = 0;
                            frameBody = null;
                        }
                        else
                        {
                            frameBody = BufferConcat(frameBody, clientBuffer, cursor, bufferSize - cursor);
                            frameBodyLength -= bufferSize - cursor;
                            readFrameBytes += bufferSize - cursor;
                            cursor = bufferSize;
                        }
                    }
                    #endregion
                }

                if (bufferSize == 0)
                    Thread.Sleep(500);
            }
            catch { }

            lock (_lock)
            {
                if (client != null && client.Connected)
                    client.BeginReceive(clientBuffer, 0, clientBuffer.Length, SocketFlags.None, OnReceive, this);
            }
        }

        public bool IsRunning
        {
            get 
            {
                if (client != null)
                    return client.Connected;
                return false;
            }
        }

        public void Dispose()
        {
            lock(_lock)
            {
                if (client != null)
                {
                    client.Close();
                    client = null;
                }
            }
        }

        private static byte[] BufferConcat(byte[] buffer1, byte[] buffer2, int offset2, int size2)
        {
            if (buffer1 != null)
            {
                var concatedBuffer = new byte[buffer1.Length + size2];
                Buffer.BlockCopy(buffer1, 0, concatedBuffer, 0, buffer1.Length);
                Buffer.BlockCopy(buffer2, offset2, concatedBuffer, buffer1.Length, size2);
                return concatedBuffer;
            }
            else
            {
                var concatedBuffer = new byte[size2];
                Buffer.BlockCopy(buffer2, offset2, concatedBuffer, 0, size2);
                return concatedBuffer;
            }
        }
    }
}
