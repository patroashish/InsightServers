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

/**
 * Stress test for the server..
 */ 

namespace TestClient
{
    class TestClient
    {
        public string clientString { get; private set; }
        private string hostName;
        private TcpClient TCPClient;
        private Random r1;

        private const int SERVER_PORT = 5210;
        private const int MAX_MESSAGES = 1;
        private int message_count = 0;

        public TestClient(int clientNumber, string hostName)
        {
            this.clientString = "testClient" + clientNumber;
            this.TCPClient = new TcpClient();
            this.hostName = hostName;
            r1 = new Random(clientNumber);
        }

        public void RunClient()
        {

            while (true)
            {
                if (message_count > MAX_MESSAGES)
                {
                    break;
                }

                try
                {
                    while (TCPClient.Connected == false)
                    {
                        TCPClient.Connect(hostName, SERVER_PORT);
                        Console.WriteLine("{0} established.", clientString);
                    }

                    Byte[] receiveBytes = new Byte[512];
                    int cellularStrength = -160;

                    receiveBytes[42] = (Byte)(cellularStrength & 0xff);
                    receiveBytes[43] = (Byte)((cellularStrength & 0xff00) >> 8);
                    receiveBytes[44] = (Byte)((cellularStrength & 0xff0000) >> 16);
                    receiveBytes[45] = (Byte)((cellularStrength & 0xff000000) >> 24);

                    Byte[] cellular = new Byte[1];
                    cellular[0] = 1;

                    Buffer.BlockCopy(cellular, 0, receiveBytes, 46, 1);
                    //Thread.Sleep(r1.Next(1500));
                    TCPClient.GetStream().Write(receiveBytes, 0, receiveBytes.Length);

                    message_count++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("{0} closed.", clientString);
                    TCPClient.Close();
                    TCPClient = new TcpClient();

                    Thread.Sleep(TimeSpan.FromSeconds(30));
                }
            }
        }
    }
}
