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
    /// <summary>
    /// Global constants
    /// </summary>
    class Constants
    {
        // Entry type constants.
        public const byte BATTERY = 0;
        public const byte RESOURCE = 1;
        public const byte SYSTEM_INFO = 2;
        public const byte SESSIONS = 3;
        public const byte REMOVE_SESSION = 4;
        public const byte EVENT_COUNT = 5;
        public const byte EVENT_VALUE = 6;
        public const byte EVENT_STRING = 7;
        public const byte LOCATION_INFO = 8;
        public const byte DATA_TRANSFER_INFO = 9;
        public const byte DOWNLOAD_INFO = 10;
        public const byte APPUID_INFO = 11;
        public const byte EVENT_UPDATE_INFO = 12;

        // Connection close type entry.
        public const byte CLOSE_NORMAL = 0;
        public const byte CLOSE_READ_ZERO_BYTES = 1;
        public const byte CLOSE_READ_ERROR = 2;
        public const byte CLOSE_SEND_CONFIG_ERROR = 3;
        public const byte CLOSE_CLEANUP = 4;

        // Resource update interval constant.
        public const long SEC = 1000; // Milliseconds in a second.
        public const long RESOURCE_UPDATE_INTERVAL = 35 * SEC;
        public const double MEASUREMENT_PROBABILITY = 1.0;

        // Database related constants.
        // Server configuration constants. Only need to modify this section for setting up the server.
        public const string MYSQL_SERVER = "xxxx"; // Update the server hostname based on the server setup.
        public const string MYSQL_USERNAME = "dummy"; // Update mysql username based on the server setup.
        public const string MYSQL_PASSWORD = "dummy"; // Update mysql password based on the server setup.
        public const string MYSQL_DBNAME = "insight_stats";

        // Table names for the different statistics collected from the insight server.
        public const string BATTERY_DB = "battery_info";
        public const string RESOURCE_DB = "resourceusage_info";
        public const string SYSTEMINFO_DB = "system_device_info";
        public const string SESSIONS_DB = "sessions";
        public const string EVENT_COUNT_DB = "event_count";
        public const string EVENT_VALUE_DB = "event_values";
        public const string EVENT_STRING_DB = "event_strings";
        public const string LOCATION_DB = "locations";
        public const string DOWNLOAD_DB = "downloads";
    }
}
