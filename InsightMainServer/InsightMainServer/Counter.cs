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

namespace SystemInfoServer
{
    class Counter
    {
        // Stats about connections Connection
        public long tcpThreadCount { get; set; }
        public long sessionMapCount { get; set; }

        // Stats about connection close.
        public long closeNormalCount { get; set; }
        public long closeReadZeroCount { get; set; }
        public long closeReadErrorCount { get; set; }
        public long closeSendConfigErrorCount { get; set; }
        public long closeCleanupCount { get; set; }

        // Stats about removal.
        public long removalNotConnected { get; set; }
        public long removalTimeout { get; set; }
        public long removalException { get; set; }

        // Stats about mysql.
        public long mysqlInsertionException { get; set; }
        public long mysqlPushException { get; set; }
        public long mysqlExecuteQueryException { get; set; }

        // Stats about errors while processing message strings.
        public long msgInitSessionException { get; set; }
        public long msgSystemInfoException { get; set; }
        public long msgLocationException { get; set; }
        public long msgResourceException { get; set; }
        public long msgBatteryException { get; set; }
        public long msgAppuidException { get; set; }

        public long msgDataTransException { get; set; }
        public long msgEventCountException { get; set; }
        public long msgEventValException { get; set; }
        public long msgDownloadInfoException { get; set; }

        // Warnings.
        public long highMessageBufferUsageCount { get; set; }

        public Counter()
        {
            resetStats();
        }

        public void resetStats() 
        {
            // Stats about connections Connection.
            tcpThreadCount = 0;
            sessionMapCount = 0;

            // Stats about connection close.
            closeNormalCount = 0;
            closeReadZeroCount = 0;
            closeReadErrorCount = 0;
            closeSendConfigErrorCount = 0;
            closeCleanupCount = 0;

            // Stats about removal.
            removalNotConnected = 0;
            removalTimeout = 0;
            removalException = 0;

            // Stats about mysql.
            mysqlInsertionException = 0;
            mysqlPushException = 0;
            mysqlExecuteQueryException = 0;

            // Stats about errors while processing message strings.
            msgInitSessionException = 0;
            msgSystemInfoException = 0;
            msgLocationException = 0;
            msgResourceException = 0;
            msgBatteryException = 0;
            msgAppuidException = 0;

            msgDataTransException = 0;
            msgEventCountException = 0;
            msgEventValException = 0;
            msgDownloadInfoException = 0;

            // Warnings.
            highMessageBufferUsageCount = 0;
        }

        public override string ToString()
        {
            string statPrefix = "Stat: ";
            return statPrefix + "Counter values at " + DateTime.Now.ToString() + ":\n" +

            statPrefix + "\n" +
            statPrefix + "Connection count statistics:\n" +
            statPrefix + "tcpThreadCount: " + tcpThreadCount + "\n" +
            statPrefix + "sessionMapCount: " + sessionMapCount + "\n" +

            statPrefix + "\n" +
            statPrefix + "Connection close statistics:\n" +
            statPrefix + "closeNormalCount: " + closeNormalCount + "\n" +
            statPrefix + "closeReadZeroCount: " + closeReadZeroCount + "\n" +
            statPrefix + "closeReadErrorCount: " + closeReadErrorCount + "\n" +
            statPrefix + "closeSendConfigErrorCount: " + closeSendConfigErrorCount + "\n" +
            statPrefix + "closeCleanupCount: " + closeCleanupCount + "\n" +

            statPrefix + "\n" +
            statPrefix + "Cleanup removal statistics:\n" +
            statPrefix + "removalNotConnected: " + removalNotConnected + "\n" +
            statPrefix + "removalTimeout: " + removalTimeout + "\n" +
            statPrefix + "removalException: " + removalException + "\n" +

            statPrefix + "\n" +
            statPrefix + "Cleanup about mysql:\n" +
            statPrefix + "mysqlInsertionException: " + mysqlInsertionException + "\n" +
            statPrefix + "mysqlExecuteQueryException: " + mysqlExecuteQueryException + "\n" +
            statPrefix + "mysqlPushException: " + mysqlPushException + "\n" +

            statPrefix + "\n" +
            statPrefix + "Stats about errors while processing message strings:\n" +
            statPrefix + "msgInitSessionException: " + msgInitSessionException + "\n" +
            statPrefix + "msgSystemInfoException: " + msgSystemInfoException + "\n" +
            statPrefix + "msgLocationException: " + msgLocationException + "\n" +
            statPrefix + "msgResourceException: " + msgResourceException + "\n" +
            statPrefix + "msgBatteryException: " + msgBatteryException + "\n" +
            statPrefix + "msgAppuidException: " + msgAppuidException + "\n" +

            statPrefix + "\n" + 
            statPrefix + "msgDataTransException: " + msgDataTransException + "\n" +
            statPrefix + "msgEventCountException: " + msgEventCountException + "\n" +
            statPrefix + "msgEventValException: " + msgEventValException + "\n" +
            statPrefix + "msgDownloadInfoException: " + msgDownloadInfoException + "\n" +

            statPrefix + "\n" +
            statPrefix + "Warning statistics:\n" +
            statPrefix + "highMessageBufferUsageCount: " + highMessageBufferUsageCount + "\n";
        }
    }
}
