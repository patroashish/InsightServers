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
using System.Threading;

/**
 * Stress test for the server..
 */ 
namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            int numberOfClients = 500;

            Thread.Sleep(1000);

            for (int i = 0; i < numberOfClients; i++)
            {
                TestClient testClient = new TestClient(i, "localhost");

                Thread clientThread = new Thread(new ThreadStart(testClient.RunClient));
                clientThread.Start();

                Thread.Sleep(10);
            }
            Console.ReadKey();
        }
    }
}
