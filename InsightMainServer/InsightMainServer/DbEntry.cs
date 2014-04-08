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

namespace InsightMainServer
{
    public class DbEntry
    {
        public int appID { get; private set; }
        public string deviceID { get; private set; }
        public string sessionID { get; private set; }
        public int entryType { get; set; }
        public DateTime receivedTime { get; set; }

        public DbEntry(int appID, string deviceID, string sessionID, int entryType)
        {
            this.appID = appID;
            this.deviceID = deviceID;
            this.sessionID = sessionID;
            this.entryType = entryType;
            this.receivedTime = DateTime.Now;
        }
    }

    public class DbEntryLocation : DbEntry
    {
        // Stores the battery related information.
        public double locationlat { get; set; }
        public double locationlng { get; set; }
        public string countryCode { get; set; }
        public string adminArea { get; set; }

        public DbEntryLocation(int appID, string deviceID, string sessionID, double locationlat,
            double locationlng, string countryCode, string adminArea)
            : base(appID, deviceID, sessionID, Constants.LOCATION_INFO)
        {
            this.locationlat = locationlat;
            this.locationlng = locationlng;
            this.countryCode = countryCode;
            this.adminArea = adminArea;
        }

        public override string ToString()
        {
            return "DbEntryLocation:-" + "\n" +
            "locationlat: " + locationlat + "\n" +
            "locationlng: " + locationlng + "\n" +
            "countryCode: " + countryCode + "\n" +
            "adminArea: " + adminArea;
        }
    }

    public class DbEntryEventCount : DbEntry
    {
        // Stores the battery related information.
        public int eventID { get; set; }
        public int count { get; set; }

        public DbEntryEventCount(int appID, string deviceID, string sessionID, int eventID, int count)
            : base(appID, deviceID, sessionID, Constants.EVENT_COUNT)
        {
            this.eventID = eventID;
            this.count = count;
        }

        public override string ToString()
        {
            return "DbEntryEventValue:-" + "\n" +
            "eventID: " + eventID + "\n" +
            "value: " + count;
        }
    }

    public class DbEntryEventValue : DbEntry
    {
        // Stores the battery related information.
        public long timeSinceStart { get; set; }
        public int eventID { get; set; }
        public double value { get; set; }
        public int sequenceNumber { get; set; }

        public DbEntryEventValue(int appID, string deviceID, string sessionID,
            int sequenceNumber, long timeSinceStart, int eventID, double value)
            : base(appID, deviceID, sessionID, Constants.EVENT_VALUE)
        {
            this.sequenceNumber = sequenceNumber;
            this.timeSinceStart = timeSinceStart;
            this.eventID = eventID;
            this.value = value;
        }

        public override string ToString()
        {
            return "DbEntryEventValue:-" + "\n" +
            "timeSinceStart: " + timeSinceStart + "\n" +
            "eventID: " + eventID + "\n" +
            "value: " + value;
        }
    }

    public class DbEntryEventString : DbEntry
    {
        // Stores the battery related information.
        public long timeSinceStart { get; set; }
        public int eventID { get; set; }
        public string value { get; set; }
        public int sequenceNumber { get; set; }

        public DbEntryEventString(int appID, string deviceID, string sessionID,
            int sequenceNumber, long timeSinceStart, int eventID, string value)
            : base(appID, deviceID, sessionID, Constants.EVENT_STRING)
        {
            this.sequenceNumber = sequenceNumber;
            this.timeSinceStart = timeSinceStart;
            this.eventID = eventID;
            this.value = value;
        }

        public override string ToString()
        {
            return "DbEntryEventString:-" + "\n" +
            "timeSinceStart: " + timeSinceStart + "\n" +
            "eventID: " + eventID + "\n" +
            "value: " + value;
        }
    }

    public class DbEntryDownloads : DbEntry
    {
        // Message related information.
        public int sequenceNumber { get; set; }

        // Stores the battery related information.
        public long timeSinceStart { get; set; }
        public long txBytes { get; set; }
        public long rxBytes { get; set; }
        public long duration { get; set; }

        public DbEntryDownloads(int appID, string deviceID, string sessionID,
            int sequenceNumber, long timeSinceStart, long txBytes, long rxBytes, long duration)
            : base(appID, deviceID, sessionID, Constants.DOWNLOAD_INFO)
        {
            this.sequenceNumber = sequenceNumber;

            this.timeSinceStart = timeSinceStart;
            this.txBytes = txBytes;
            this.rxBytes = rxBytes;
            this.duration = duration;
        }

        public override string ToString()
        {
            return "DbEntryBattery:-" + "\n" +
            "timeSinceStart: " + timeSinceStart + "\n" +
            "TxBytes: " + txBytes + "\n" +
            "RxBytes: " + rxBytes + "\n" +
            "Duration: " + duration + "\n";
        }
    }

    public class DbEntrySessions : DbEntry
    {
        // Stores the battery related information.
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
        public int length { get; set; }
        public int platform { get; set; }
        public string applicationUID { get; set; }
        public string batteryTechnology { get; set; }

        public long totalTxBytes { get; set; }
        public long totalRxBytes { get; set; }
        public long mobileTxBytes { get; set; }
        public long mobileRxBytes { get; set; }
        public long appTxBytes { get; set; }
        public long appRxBytes { get; set; }

        public byte isForceClosed { get; set; } // 0 denotes normal session. 1 denotes removed session.
                                                  // (sessions with value of 1 for this variable may have a
                                                  // larger duration than the correct one because it wasn'timer closed properly).

        // Stores the timestamp when the previous message was received for this session.
        // All messages act as keep alive messages for a session. A timeout period can be set
        // as x times the message with the minimum time interval.
        public DateTime lastMsgReceivedTime { get; set; }

        public DbEntrySessions(int appID, string deviceID, string sessionID, int platform)
            : base(appID, deviceID, sessionID, Constants.SESSIONS)
        {
            this.platform = platform;

            startTime = DateTime.Now;
            endTime = DateTime.Now;
            lastMsgReceivedTime = DateTime.Now;
            batteryTechnology = "";
            applicationUID = "Nav";

            totalTxBytes = 0;
            totalRxBytes = 0;
            mobileTxBytes = 0;
            mobileRxBytes = 0;
            appTxBytes = 0;
            appRxBytes = 0;

            isForceClosed = Constants.CLOSE_NORMAL;
        }

        public override string ToString()
        {
            return "DbEntrySessions:-" + "\n" +
            "Start Time: " + startTime.ToString() + "\n" +
            "End Time: " + endTime.ToString() + "\n" +
            "Length: " + (DateTime.Now - startTime) + "\n" +
            "Platform: " + platform + "\n" +
            "BatteryTechnology: " + batteryTechnology + "\n" +
            "totalTxBytes: " + totalTxBytes + "\n" +
            "totalRxBytes: " + totalRxBytes + "\n" +
            "mobileTxBytes: " + mobileTxBytes + "\n" +
            "mobileRxBytes: " + mobileRxBytes + "\n" +
            "appTxBytes: " + appTxBytes + "\n" +
            "appRxBytes: " + appRxBytes + "\n" +
            "isForceClosed: " + isForceClosed + "\n" +
            "lastMsgReceivedTime: " + lastMsgReceivedTime.ToString() + "\n";
        }
    }

    public class DbEntryResource : DbEntry
    {
        // Message related information.
        public int sequenceNumber { get; set; }

        // CPU + Memory utilization related information.
        public int totalEntries { get; set; }

        public int pidVal { get; set; }
        public int cpuUsage { get; set; }
        public double rss { get; set; }
        public double vss { get; set; }
        public int thrVal { get; set; }

        public double bogomips { get; set; }

        public double totalCpuIdleRatio { get; set; }
        public double avgLoadOneMin { get; set; }
        public double avgLoadFiveMin { get; set; }
        public double avgLoadFifteenMin { get; set; }

        public int runningProcs { get; set; }
        public int totalProcs { get; set; }
        
        public long memTotalAvail { get; set; }
        public int availMem { get; set; }
        public int threshold { get; set; }

        public int totalCpuUsage { get; set; }

        public int screenBrightness { get; set; }
        public int isScreenOn { get; set; }

        public int isAudioSpeakerOn { get; set; }
        public int isAudioWiredHeadsetOn { get; set; }
        public int audioLevel { get; set; }
        public int audioMaxLevel { get; set; }
        
        public DbEntryResource(int appID, string deviceID, string sessionID)
            : base(appID, deviceID, sessionID, Constants.RESOURCE)
        {
        }

        public override string ToString()
        {
            return "DbEntryResource:-\n" +
                "Sequence Number: " + sequenceNumber + "\n" +
                "Total entries: " + totalEntries + "\n" +
                "PID: " + pidVal + "\n" +
                "CPU: " + cpuUsage + "\n" +
                "RSS: " + rss + "\n" +
                "VSS: " + vss + "\n" +
                "THR: " + thrVal + "\n" +
                "bogomips: " + bogomips + "\n" +
                "TotalCpuIdleRatio: " + totalCpuIdleRatio + "\n" +
                "avgLoadOneMin: " + avgLoadOneMin + "\n" +
                "avgLoadFiveMin: " + avgLoadFiveMin + "\n" +
                "avgLoadFifteenMin: " + avgLoadFifteenMin + "\n" +
                "runningProcs: " + runningProcs + "\n" +
                "totalProcs: " + totalProcs + "\n" +
                "memTotalAvail: " + memTotalAvail + "\n" +
                "availMem: " + availMem + "\n" +
                "threshold: " + threshold + "\n" +
                "totalCpuUsage: " + totalCpuUsage + "\n" +
                "screenBrightness: " + screenBrightness + "\n" +
                "isScreenOn: " + isScreenOn + "\n" +
                "isAudioSpeakerOn: " + isAudioSpeakerOn + "\n" +
                "isAudioWiredHeadsetOn:" + isAudioWiredHeadsetOn + "\n" +
                "audioLevel:" + audioLevel + "\n" + 
                "audioMaxLevel:" + audioMaxLevel + "\n";
        }
    }

    public class DbEntryBattery : DbEntry
    {
        // Message related information.
        public int sequenceNumber { get; set; }

        // Stores the battery related information.
        public int scale { get; set; }
        public int level { get; set; }
        public int temp { get; set; }
        public int voltage { get; set; }

        public int health { get; set; }
        public string technology { get; set; }
        public int plugged { get; set; }

        public DbEntryBattery(int appID, string deviceID, string sessionID)
            : base(appID, deviceID, sessionID, Constants.BATTERY)
        {
        }

        public override string ToString()
        {
            return "DbEntryBattery:-" + "\n" +
            "Sequence Number: " + sequenceNumber + "\n" +
            "scale: " + scale + "\n" +
            "level: " + level + "\n" +
            "temp: " + temp + "\n" +
            "voltage: " + voltage + "\n" + 
            "health: " + health + "\n" +
            "technology: " + technology + "\n" +
            "plugged: " + plugged + "\n";
        }
    }

    public class DbEntrySysteminfo : DbEntry
    {
        // Stores the network related information.
        public string ipAddress;
        public string cellularCarrier;

        // Stores the android OS related information.
        public string osVersion { get; set; }
        public string osBuild { get; set; }
        public string osAPI { get; set; }

        // Stores the device related information.
        public string deviceName { get; set; }
        public string deviceModel { get; set; }
        public string deviceProduct { get; set; }
        public string deviceManufacturer { get; set; }
        public string deviceBoard { get; set; }
        public string deviceBrand { get; set; }

        // Other component information.
        public string processor { get; set; }
        public string bogomips { get; set; }
        public string hardware { get; set; }
        public long memtotal { get; set; }

        // Screen related information.
        public int screenWidth { get; set; }
        public int screenHeight { get; set; }
        public int densityDpi { get; set; }
        public int screenXDpi { get; set; }
        public int screenYDpi { get; set; }

        // Location related information.
        public byte isGpsLocationOn { get; set; }
        public byte isNetworkLocationOn { get; set; }

        // Network related information.
        public byte activeNetwork { get; set; }
        public byte activeSubType { get; set; }

        public DbEntrySysteminfo(int appID, string deviceID, string sessionID)
            : base(appID, deviceID, sessionID, Constants.SYSTEM_INFO)
        {
        }

        public override string ToString()
        {
            return "DbEntrySysteminfo:-" + "\n" +
            "osVersion: " + osVersion + "\n" +
            "osBuild: " + osBuild + "\n" +
            "osAPI: " + osAPI + "\n" +
            "deviceName: " + deviceName + "\n" +
            "deviceModel: " + deviceModel + "\n" +
            "deviceProduct: " + deviceProduct + "\n" +
            "deviceManufacturer: " + deviceManufacturer + "\n" +
            "deviceBoard: " + deviceBoard + "\n" +
            "deviceBrand: " + deviceBrand + "\n" +
            "processor: " + processor + "\n" +
            "bogomips: " + bogomips + "\n" +
            "hardware: " + hardware + "\n" +
            "memtotal: " + memtotal + "\n" +
            "screenWidth: " + screenWidth + "\n" +
            "screenHeight: " + screenHeight + "\n" +
            "densityDpi: " + densityDpi + "\n" +
            "screenXDpi: " + screenXDpi + "\n" +
            "screenYDpi: " + screenYDpi + "\n" +
            "isGpsLocationOn: " + isGpsLocationOn + "\n" +
            "isNetworkLocationOn: " + isNetworkLocationOn + "\n" +
            "activeNetwork: " + activeNetwork + "\n" +
            "activeSubType: " + activeSubType + "\n";
        }
    }
}
