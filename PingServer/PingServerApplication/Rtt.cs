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
    /// Each instance of this class stores the values of various network related attributes measured by the smartphone client
    /// during an RTT measurment.
    /// </summary>
    public class Rtt
    {
        public enum PacketType { tcp, udp };
        public PacketType packetType { get; private set; }
        public DateTime sentTime { get; private set; }
        public DateTime receivedTime { get; set; }
        public TimeSpan rtt { get; set; }

        public string deviceID { get; set; }
        public int appID { get; set; }
        public long sessionID { get; set; }

        //public byte isConnectedViaCellular { get; set; }

        public int platform { get; set; }
        public int sequenceNumber { get; private set; }

        public int wifiSignalStrength { get; set; }
        public int wifiSpeed { get; set; }
        public int wifiAvailability { get; set; }
        public int isConnectedtoWifi { get; set; }
        public int wifiIsFailover { get; set; }
        public int wifiState { get; set; }
        public int wifiSubType { get; set; }

        public int signalStrength { get; set; }
        public int cellularAvailable { get; set; }
        public int isConnectedToCellular { get; set; }
        public int cellularIsFailover { get; set; }
        public int cellularState { get; set; }
        public int cellularSubtype { get; set; }

        public int activeSubtype { get; set; }
        public int activeNetwork { get; set; }

        //public string cellularSubtypeString { get; set; }
        //public string cellularExtraInfo { get; set; }
        //public string cellularReason { get; set; }

        public Rtt(PacketType packetType, int sequenceNumber)
        {
            this.packetType = packetType;
            this.sentTime = DateTime.Now;
            this.sequenceNumber = sequenceNumber;
        }

        /// <summary>
        /// Returns a formatted of the measurement values obtained from the smartphone client during this instance of the RTT measurement.
        /// </summary>
        /// <returns>Formatted string containing the measurement values.</returns>
        public override string ToString()
        {
            return "Rtt Entry:-" +
            "\nAppID: " + appID +
            "\nDeviceID: " + deviceID +
            "\nSessionID: " + sessionID +
            "\nPlatform: " + platform +
            "\nWiFi_Signal_Strength: " + wifiSignalStrength +
            "\nWiFi_Speed: " + wifiSpeed +
            "\nIsWiFiAvailable: " + wifiAvailability +
            "\nisConnectedToWiFi: " + isConnectedtoWifi +
            "\nisWiFiFailover: " + wifiIsFailover +
            "\nwifiState: " + wifiState +
            "\nwifiSubType: " + wifiSubType +
            "\nSignalStrength: " + signalStrength +
            "\nIsCellularAvailable: " + cellularAvailable +
            "\nIsConnectedToCellula: " + isConnectedToCellular +
            "\nIsCellularFailover: " + cellularIsFailover +
            "\ncellularState: " + cellularState +
            "\ncellularSubtype: " + cellularSubtype +


            "\nactiveNetwork: " + activeNetwork +
            "\nactiveSubtype: " + activeSubtype + "\n";
            //temp.put("cellInfo", cellInformation);
            //temp.put("wifiInfo", wifiInformation);
            //temp.put("activeInfo", activeInformation);
        }
    }

    /// <summary>
    /// A UDP measurement instance.
    /// </summary>
    public class UDPRtt : Rtt
    {
        public UDPRtt(int sequenceNumber)
            : base(PacketType.udp, sequenceNumber)
        {
        }
    }

    /// <summary>
    /// A TCP measurement instance.
    /// </summary>
    public class TCPRtt : Rtt
    {
        public TCPRtt(int sequenceNumber)
            : base(PacketType.tcp, sequenceNumber)
        {
        }
    }
}
