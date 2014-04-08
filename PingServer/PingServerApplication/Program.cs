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

//using log4net;
using PingServer;

namespace PingServerApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server();

            //log4net.Config.XmlConfigurator.Configure();
            //ILog log = LogManager.GetLogger(typeof(Program));
            //log.Info("Ping Server Console started.  Logging started.  Starting the Ping Server...\n");

            /* Start the main ping measurement thread. The Android/iOS client devices connect to this server 
             * and perform network RTT measurments using both UDP and TCP.
             */
            Thread mainThread = new Thread(new ThreadStart(server.StartThreads));
            mainThread.Start();
        }
    }
}
