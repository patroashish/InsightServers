/* Copyright 2013 Wisconsin Wireless and NetworkinG Systems (WiNGS) Lab, University of Wisconsin Madison.
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
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PingServer
{
    /// <summary>
    /// Base class to manage a TCP/UDP connection with a smartphone iOS/Android client.
    /// </summary>
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

    /// <summary>
    /// Manage a TCP connection with a smartphone iOS/Android client.
    /// </summary>
    class TCPConnection : Connection
    {
        public TcpClient tcpClient { get; set; }
        public TCPRtt[] tcpRecords { get; set; }
        public Timer timer { get; set; }

        public TCPConnection(IPAddress ipAddress, string deviceID, int portNumber, TcpClient tcpClient)
            : base(ipAddress, deviceID, portNumber)
        {
            tcpRecords = new TCPRtt[256];
            this.tcpClient = tcpClient;
        }
    }

    /// <summary>
    /// Manage a UDP connection with a smartphone iOS/Android client.
    /// </summary>
    class UDPConnection : Connection
    {
        public UDPRtt[] udpRecords { get; set; }
        public uint timeoutCounter { get; set; }

        public UDPConnection(IPAddress ipAddress, string deviceID, int portNumber)
            : base(ipAddress, deviceID, portNumber)
        {
            udpRecords = new UDPRtt[256];
            this.timeoutCounter = 0;
        }

    }
}
