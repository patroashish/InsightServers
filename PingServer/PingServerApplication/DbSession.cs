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

namespace PingServer
{
    /// <summary>
    /// Store the UDP and TCP RTT (Round Trip Time) values recorded during a session.
    /// </summary>
    class DbSession
    {
        public LinkedList<DbEntry> udpEntries;
        public LinkedList<DbEntry> tcpEntries;
        //public string locationString;
        //public string ipAddress;
        //public string platformString;

        public DbSession()
        {
            udpEntries = new LinkedList<DbEntry>();
            tcpEntries = new LinkedList<DbEntry>();
        }
    }
}
