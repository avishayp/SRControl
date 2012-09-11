using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using Interfaces;
using System.Diagnostics;

namespace Client
{
    /// <summary>
    /// udp client
    /// </summary>
    public class ByteSender : ISender
    {
        public ByteSender()
        {
            Init();
        }

        public void Connect(Object conn)
        {
            try
            {
                _sock.Connect(conn as IPEndPoint);
                _isConnected = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public int Send(Object data)
        {
            return _sock.Send(data as byte[]);
        }

        public void Close()
        {
            _sock.Close();
        }

        public bool IsConnected { get { return _isConnected; } }
        private void Init()
        {
            _isConnected = false;
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        private Socket _sock;
        private bool _isConnected;
    }
}
