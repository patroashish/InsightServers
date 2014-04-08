using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SystemInfoServer
{
    class Connection
    {
        public IPAddress ipAddress { get; set; }
        public string deviceID { get; set; }
        public int portNumber { get; set; }
        public byte counter { get; set; }

        public Connection(IPAddress ipAddress, string deviceID, int portNumber)
        {
            this.ipAddress = ipAddress;
            this.deviceID = deviceID;
            this.portNumber = portNumber;
            this.counter = 0;
        }
    }

    class TCPConnection : Connection
    {
        public TcpClient tcpClient { get; set; }
        public Timer timer { get; set; }
        public TCPConnection(IPAddress ipAddress, string deviceID, int portNumber, TcpClient tcpClient)
            : base(ipAddress, deviceID, portNumber)
        {
            this.tcpClient = tcpClient;
        }
    }
}
