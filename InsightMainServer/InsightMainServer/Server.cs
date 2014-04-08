using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

using log4net;
using log4net.Config;
using MySql.Data;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SystemInfoServer
{
    /// <summary>
    /// The main server instance from collecting Insight related statitics. All statistics except the network latency measurements are collected by this server.
    /// The Insight clients connect to this server and server periodically collects the following client statistics over a TCP network connection.
    /// - CPU/Memory consumption statistics.
    /// - Battery drain statistics.
    /// - Device related metadata (e.g., device type, screen size).
    /// - Application specific events reported by the developer (e.g., fighting a monster in a game).
    /// - Location related statistics.
    /// - Data download events reported by the developer.
    /// </summary>
    public class Server
    {
        /*
        class ConnectionInfo
        {
            public NetworkStream clientStream;
            public Timer timer;

            public ConnectionInfo(NetworkStream clientStream, Timer timer)
            {
                this.clientStream = clientStream;
                this.timer = timer;
            }
        }
        */

        // Logger setup.
        //private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Server));
        static ILog log = LogManager.GetLogger(typeof(Server));

        // Debug related constants.
        private const bool IS_DEBUG = true;

        // Network related constants.
        private const int SERVER_PORT = 11210;

        // Message related constants.
        private const int BUFFER_SIZE = 1024 * 10;
        private const int DEVICE_ID_INDEX = 2;
        private const string MESSAGE_PREAMBLE = "ew9y3g2e3ga"; // The first bytes received from the client should have this value
        private const string MESSAGE_PREAMBLE2 = "re08h4089y"; // The first bytes received from the client should have this value
        private const string MESSAGE_POSTAMBLE = "dsf9u0932j"; // The last bytes received from the client should have this value

        private const long MIN_RECEIVED_BYTES = 5000;

        private const string JOIN_DELIMITER = "\n";

        // Time related constants.
        private const int SEC = 1000; // The number of milliseconds in a sec.
        private const int MIN = 60; // The number of seconds in a minute.

        private const int MYSQL_THREAD_SLEEP_DURATION = 11 * MIN; // In Seconds.
        private const int CLEANUP_SLEEP_DURATION = 15 * MIN; // In Seconds.

        // Having a big timeout value because a client can stop sending messages if the app is still active but the screen is turned 
        // off due to inactivity. So, the cause of receiving no messages can be:-
        // 1. Connection closed on client but not detected by the server. So, client is unable to send messages.
        // 2. Connection is still active but due to inactivity on the device, the threads are not active. Thus no messages are being received from the client.
        private const double SESSION_TIMEOUT_INTERVAL = 60 * MIN; //Constants.RESOURCE_UPDATE_INTERVAL / SEC; // In Seconds. Keep it atleast greater than 2 times the minimum message interval.


        //private const int KEEP_ALIVE_PING_INTERVAL = 30; // In seconds
        //private const int KEEP_ALIVE_INIT_TIME = 5; // In seconds

        // Minimum number of entries before updating MySQL database.
        private const int MYSQL_ENTRYCOUNT_THRESHOLD = 3;

        //System.Timers.Timer _statusTimer = new System.Timers.Timer(SEC * MIN * 7); // 7 MIN
        //System.Timers.Timer _tcpCleanerTimer = new System.Timers.Timer(SEC * CLEANUP_SLEEP_DURATION);

        private static ReaderWriterLockSlim _mysqlListLock = new ReaderWriterLockSlim();
        private LinkedList<DbEntry> _dbEntryList;

        private TcpListener _tcpListener;

        private Dictionary<TcpClient, Thread> _tcpThreads = new Dictionary<TcpClient, Thread>();
        private Dictionary<TcpClient, DbEntrySessions> _sessionMap = new Dictionary<TcpClient, DbEntrySessions>();

        private Thread _mysqlThread;
        private Thread _listenThread;
        private Thread _cleanupThread;

        private string connString; // Connection string for the database.
        private byte[] clientConfigBytes; // Configuration string to be sent to the client.
        private int clientConfigLength;

        // Debug counter object;
        private Counter counter;

        public Server()
        {
            try
            {
                // setup logging
                XmlConfigurator.Configure();
                log.Info("Ping Server Console started.  Logging started.  Starting the Ping Server...\n");

                _dbEntryList = new LinkedList<DbEntry>();

                // Initialialize the debug counter;
                counter = new Counter();

                // Create the client configuration string.
                JObject o = new JObject();
                o["resourceInterval"] = Constants.RESOURCE_UPDATE_INTERVAL;
                o["measurmentProbability"] = Constants.MEASUREMENT_PROBABILITY;

                clientConfigBytes = Encoding.ASCII.GetBytes(o.ToString());
                clientConfigLength = clientConfigBytes.Length;

                // Start MySQL thread
                connString = String.Format("server={0};user={1};database={2};port=3306;password={3};",
                    Constants.MYSQL_SERVER, Constants.MYSQL_USERNAME, Constants.MYSQL_DBNAME, Constants.MYSQL_PASSWORD);
                _mysqlThread = new Thread(new ThreadStart(PushDbEntries));
                _cleanupThread = new Thread(new ThreadStart(Cleanup_Dead_Connections));

                // Start TCP thread
                _tcpListener = new TcpListener(IPAddress.Any, SERVER_PORT);
                _listenThread = new Thread(new ThreadStart(ListenForClients));

                // if (IS_DEBUG)
                {
                    log.Info("server is now running");
                }
                //else
                {
                    //  Console.WriteLine("server is now running");
                }
            }
            catch (Exception ex)
            {
                log.Error("Exception in constructor while starting the server", ex);
            }
        }

        /// <summary>
        /// Start the measurement thread to listen for client TCP connections. Also starts a thread that periodically inserts the collected data into a MySQL database.
        /// </summary>
        public void StartThreads()
        {
            XmlConfigurator.Configure();
            /*
            _tcpCleanerTimer.Elapsed += new System.Timers.ElapsedEventHandler(Cleanup_Dead_Connections);
            _tcpCleanerTimer.AutoReset = true;
            _tcpCleanerTimer.Enabled = true;
            _tcpCleanerTimer.Start();
            */

            _mysqlThread.Start();
            _listenThread.Start();
            _cleanupThread.Start();
        }

        /// <summary>
        /// Stop all measurement activity.
        /// </summary>
        public void ShutDown()
        {
            XmlConfigurator.Configure();
            _mysqlThread.Abort();
            _listenThread.Abort();
            _cleanupThread.Abort();

            foreach (var el in _tcpThreads)
            {
                el.Value.Abort();
            }
        }

        /// <summary>
        /// Periodically check if any of the tcp connections have gone bad... then remove them...
        /// </summary>
        private void Cleanup_Dead_Connections()
        {
            while (true)
            {
                DateTime now = DateTime.Now;

                try
                {
                    log.Info(DateTime.Now + ": Before cleanup: " + _tcpThreads.Count + " TCP connections");
                    List<TcpClient> removals = new List<TcpClient>();
                    byte[] tempByte = new byte[1];

                    lock (_tcpThreads)
                    {
                        lock (_sessionMap)
                        {
                            foreach (TcpClient tcpClient in _tcpThreads.Keys)
                            //foreach (TcpClient tcpClient in _sessionMap.Keys)
                            {
                                if (!tcpClient.Connected)
                                {
                                    removals.Add(tcpClient);
                                    counter.removalNotConnected++;
                                }
                                else
                                {
                                    try
                                    {
                                        DbEntrySessions dbEntrySessionInfoTemp = _sessionMap[tcpClient];

                                        if ((now - dbEntrySessionInfoTemp.lastMsgReceivedTime).TotalSeconds > SESSION_TIMEOUT_INTERVAL)
                                        {
                                            //log.Info("CleanupDeadConnections: Connection removed due to session timout " +
                                            //(now - dbEntrySessionInfoTemp.lastMsgReceivedTime).TotalSeconds + ", interval = " +
                                            //    SESSION_TIMEOUT_INTERVAL);

                                            removals.Add(tcpClient);
                                            counter.removalTimeout++;
                                        }
                                        else
                                        {
                                            //log.Info("Timediff for " + dbEntrySessionInfoTemp.deviceID + " is " + 
                                            //    (now - dbEntrySessionInfoTemp.lastMsgReceivedTime).TotalSeconds + " seconds.");
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        //log.Error("CleanupDeadConnections Error. Removing connection removed due to exception ", ex);
                                        if (tcpClient != null)
                                        {
                                            removals.Add(tcpClient);
                                        }

                                        counter.removalException++;
                                    }
                                }
                            }

                            // Debug: Print counter stats here and reset it.
                            counter.closeCleanupCount = counter.removalNotConnected + counter.removalTimeout + counter.removalException;

                            counter.tcpThreadCount = _tcpThreads.Keys.Count;
                            counter.sessionMapCount = _sessionMap.Keys.Count;
                            log.Info(counter.ToString());
                            counter.resetStats();
                        }

                        foreach (TcpClient tcpClient in removals)
                        {
                            try
                            {
                                // In this case, we also won'timer have any eventCount, eventValue or Download information.
                                //log.Info("Calling removeSessionInfoEntry in CleanupDeadConnections.");

                                removeSessionInfoEntry(tcpClient, true, Constants.CLOSE_CLEANUP);

                                _tcpThreads[tcpClient] = null;
                                _tcpThreads.Remove(tcpClient);

                                tcpClient.Close();
                            }
                            catch (Exception ex)
                            {
                                log.Error("CleanupDeadConnections: Error while removing connection", ex);
                            }
                        }

                        log.Info(DateTime.Now + ": After cleanup: " + _tcpThreads.Count + " connections");
                    }
                }
                catch (Exception ex)
                {
                    log.Error("CleanupDeadConnections Error ", ex);
                }

                log.Info("CleanupDeadConnections: Time taken for cleanup: " + (DateTime.Now - now).Milliseconds + " milliseconds.");


                Thread.Sleep(TimeSpan.FromSeconds(CLEANUP_SLEEP_DURATION));
            }
        }

        /// <summary>
        /// Periodically push the collected statistics into the database.
        /// </summary>
        private void PushDbEntries()
        {
            while (true)
            {
                try
                {
                    log.Info("PushDbEntries _dbEntryList.Count: " + _dbEntryList.Count + " " + MYSQL_ENTRYCOUNT_THRESHOLD);

                    if (_dbEntryList.Count > MYSQL_ENTRYCOUNT_THRESHOLD)
                    {
                        _mysqlListLock.EnterReadLock();
                        LinkedList<DbEntry> listCopy = new LinkedList<DbEntry>();
                        try
                        {
                            listCopy = new LinkedList<DbEntry>(_dbEntryList);
                            _dbEntryList.Clear();
                        }
                        catch (Exception ex)
                        {
                            log.Error("Error while copying the sql entries: ", ex);
                        }
                        _mysqlListLock.ExitReadLock();

                        string query_session = "INSERT INTO " + Constants.SESSIONS_DB + " (appID, deviceID, sessionID, applicationUID, serverTime, start, end, length, totalTxBytes, totalRxBytes, mobileTxBytes," +
                            " mobileRxBytes, appTxBytes, appRxBytes, platform, batteryTechnology, isForceClosed) VALUES ";
                        string query_battery = "INSERT INTO " + Constants.BATTERY_DB + " (appID, deviceID, sessionID, serverTime, sequenceNumber, level, temp, voltage, plugged) VALUES ";
                        string query_resource = "INSERT INTO " + Constants.RESOURCE_DB + " (appID, deviceID, sessionID, serverTime, sequenceNumber, cpuUsage, rss, bogomips, " +
                            "avgLoadOneMin, runningProcs, totalProcs, memTotalAvail, totalCpuUsage, screenBrightness, isScreenOn, isAudioSpeakerOn, isAudioWiredHeadsetOn, " +
                            "audioLevel) VALUES ";
                        string query_systeminfo = "INSERT INTO " + Constants.SYSTEMINFO_DB + " (appID, deviceID, sessionID, serverTime, cellularCarrier, ip, osVersion, osBuild, osAPI, " +
                             "deviceModel, deviceProduct, processor, memTotal, screenWidth, screenHeight, densityDpi, screenXDpi, screenYDpi, isGpsLocationOn, isNetworkLocationOn, activeNetwork, activeSubType) VALUES ";
                        string query_event_count = "INSERT INTO " + Constants.EVENT_COUNT_DB + " (appID, deviceID, sessionID, serverTime, eventID, count) VALUES ";
                        string query_event_value = "INSERT INTO " + Constants.EVENT_VALUE_DB + " (appID, deviceID, sessionID, serverTime, timeSinceStart, eventID, value) VALUES ";
                        string query_event_string = "INSERT INTO " + Constants.EVENT_STRING_DB + " (appID, deviceID, sessionID, serverTime, timeSinceStart, eventID, value) VALUES ";
                        string query_location = "INSERT INTO " + Constants.LOCATION_DB + " (appID, deviceID, sessionID, serverTime, locationlat, locationlng, countryCode, adminArea) VALUES ";
                        string query_download = "INSERT INTO " + Constants.DOWNLOAD_DB + " (appID, deviceID, sessionID, serverTime, timeSinceStart, txBytes, rxBytes, duration) VALUES ";

                        int query_battery_count = 0, query_resource_count = 0, query_systeminfo_count = 0, query_session_count = 0;
                        int query_event_count_count = 0, query_event_value_count = 0, query_event_string_count = 0, query_location_count = 0, query_download_count = 0;

                        foreach (DbEntry entry in listCopy)
                        {
                            try
                            {
                                if (entry.entryType == Constants.BATTERY)
                                {
                                    DbEntryBattery batteryInfo = (DbEntryBattery)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\")",

                                        batteryInfo.appID, batteryInfo.deviceID, batteryInfo.sessionID, batteryInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        batteryInfo.sequenceNumber, batteryInfo.level, batteryInfo.temp, batteryInfo.voltage,
                                        // batteryInfo.health, batteryInfo.technology
                                        batteryInfo.plugged);

                                    query_battery += entryString + ",";
                                    query_battery_count++;

                                }
                                else if (entry.entryType == Constants.RESOURCE)
                                {
                                    DbEntryResource resourceInfo = (DbEntryResource)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\", \"{9}\"," +
                                        "\"{10}\", \"{11}\", \"{12}\", \"{13}\", \"{14}\", \"{15}\", \"{16}\", \"{17}\")",

                                        resourceInfo.appID, resourceInfo.deviceID, resourceInfo.sessionID, resourceInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        resourceInfo.sequenceNumber, resourceInfo.cpuUsage, resourceInfo.rss, //resourceInfo.vss,
                                        resourceInfo.bogomips, //resourceInfo.totalCpuIdleRatio,
                                        resourceInfo.avgLoadOneMin, //resourceInfo.avgLoadFiveMin, resourceInfo.avgLoadFifteenMin, 
                                        resourceInfo.runningProcs, resourceInfo.totalProcs, resourceInfo.memTotalAvail,
                                        //, resourceInfo.availMem, resourceInfo.threshold
                                        resourceInfo.totalCpuUsage, resourceInfo.screenBrightness, resourceInfo.isScreenOn, resourceInfo.isAudioSpeakerOn,
                                        resourceInfo.isAudioWiredHeadsetOn, resourceInfo.audioLevel
                                        //, resourceInfo.audioMaxLevel
                                        );

                                    query_resource += entryString + ",";
                                    query_resource_count++;

                                    //log.Info(entryString);
                                }
                                else if (entry.entryType == Constants.SYSTEM_INFO)
                                {
                                    DbEntrySysteminfo systemInfo = (DbEntrySysteminfo)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\"," +
                                        "\"{9}\", \"{10}\", \"{11}\", \"{12}\", \"{13}\", \"{14}\", \"{15}\", \"{16}\", \"{17}\", \"{18}\", \"{19}\", \"{20}\", \"{21}\")",

                                        systemInfo.appID, systemInfo.deviceID, systemInfo.sessionID, systemInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        systemInfo.cellularCarrier, systemInfo.ipAddress,
                                        systemInfo.osVersion, systemInfo.osBuild, systemInfo.osAPI,
                                        //systemInfo.deviceName,
                                        systemInfo.deviceModel, systemInfo.deviceProduct,
                                        //systemInfo.deviceManufacturer, systemInfo.deviceBoard, systemInfo.deviceBrand,
                                        systemInfo.processor,
                                        //systemInfo.bogomips, systemInfo.hardware, 
                                        systemInfo.memtotal, systemInfo.screenWidth, systemInfo.screenHeight, systemInfo.densityDpi, systemInfo.screenXDpi,
                                        systemInfo.screenYDpi, systemInfo.isGpsLocationOn, systemInfo.isNetworkLocationOn,
                                        systemInfo.activeNetwork, systemInfo.activeSubType);


                                    query_systeminfo += entryString + ",";
                                    query_systeminfo_count++;

                                    //log.Info(entryString);
                                }
                                else if (entry.entryType == Constants.SESSIONS)
                                {
                                    DbEntrySessions sessionInfo = (DbEntrySessions)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\"," +
                                        " \"{8}\", \"{9}\", \"{10}\", \"{11}\", \"{12}\", \"{13}\", \"{14}\", \"{15}\", \"{16}\")",

                                        sessionInfo.appID, sessionInfo.deviceID, sessionInfo.sessionID, sessionInfo.applicationUID,
                                        sessionInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        sessionInfo.startTime.ToString("yyyy:MM:dd HH:mm:ss"), sessionInfo.endTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        (int)((sessionInfo.endTime - sessionInfo.startTime).TotalSeconds), sessionInfo.totalTxBytes, sessionInfo.totalRxBytes,
                                        sessionInfo.mobileTxBytes, sessionInfo.mobileRxBytes, sessionInfo.appTxBytes, sessionInfo.appRxBytes,
                                        sessionInfo.platform, sessionInfo.batteryTechnology, sessionInfo.isForceClosed);

                                    query_session += entryString + ",";
                                    query_session_count++;

                                    //log.Info(entryString);
                                }
                                else if (entry.entryType == Constants.EVENT_COUNT)
                                {
                                    DbEntryEventCount eventCountInfo = (DbEntryEventCount)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\")",

                                        eventCountInfo.appID, eventCountInfo.deviceID, eventCountInfo.sessionID, eventCountInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        eventCountInfo.eventID, eventCountInfo.count);

                                    query_event_count += entryString + ",";
                                    query_event_count_count++;

                                    //log.Info(entryString);
                                }
                                else if (entry.entryType == Constants.EVENT_VALUE)
                                {
                                    DbEntryEventValue eventValueInfo = (DbEntryEventValue)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\")",

                                        eventValueInfo.appID, eventValueInfo.deviceID, eventValueInfo.sessionID, eventValueInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        //eventValueInfo.sequenceNumber,
                                        eventValueInfo.timeSinceStart, eventValueInfo.eventID, eventValueInfo.value);

                                    query_event_value += entryString + ",";
                                    query_event_value_count++;

                                    //log.Info(entryString);
                                }
                                else if (entry.entryType == Constants.EVENT_STRING)
                                {
                                    DbEntryEventString eventStringInfo = (DbEntryEventString)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\")",

                                        eventStringInfo.appID, eventStringInfo.deviceID, eventStringInfo.sessionID, eventStringInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        //eventValueInfo.sequenceNumber,
                                        eventStringInfo.timeSinceStart, eventStringInfo.eventID, eventStringInfo.value);

                                    query_event_string += entryString + ",";
                                    query_event_string_count++;

                                    //log.Info(entryString);
                                }
                                else if (entry.entryType == Constants.LOCATION_INFO)
                                {
                                    DbEntryLocation locationInfo = (DbEntryLocation)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\")",

                                        locationInfo.appID, locationInfo.deviceID, locationInfo.sessionID, locationInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        locationInfo.locationlat, locationInfo.locationlng, locationInfo.countryCode, locationInfo.adminArea);

                                    query_location += entryString + ",";
                                    query_location_count++;

                                    //log.Info(entryString);
                                }
                                else if (entry.entryType == Constants.DOWNLOAD_INFO)
                                {
                                    DbEntryDownloads downloadInfo = (DbEntryDownloads)entry;
                                    string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\")",

                                        downloadInfo.appID, downloadInfo.deviceID, downloadInfo.sessionID, downloadInfo.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                        //downloadInfo.sequenceNumber, 
                                        downloadInfo.timeSinceStart, downloadInfo.txBytes, downloadInfo.rxBytes, downloadInfo.duration);

                                    query_download += entryString + ",";
                                    query_download_count++;

                                    //log.Info(entryString);
                                }
                                else
                                {
                                    log.Warn("Warning!! The  message type being inserted into the database " + entry.entryType + " is not valid...\n");
                                }
                            }
                            catch (Exception ex)
                            {
                                counter.mysqlInsertionException++;
                                log.Error("Exception in processing entry for insertion", ex);
                            }
                        }

                        //log.Info(DateTime.Now + ": Inserting " + query_battery_count + ", " + query_resource_count + " and " + 
                        //query_systeminfo_count + " battery, resource and systeminfo entries respectively...\n");

                        // Inserting battery entries.
                        executeSqlQuerystring(query_battery, query_battery_count, Constants.BATTERY_DB);

                        // Inserting resource entries.
                        executeSqlQuerystring(query_resource, query_resource_count, Constants.RESOURCE_DB);

                        // Inserting systeminfo entries.
                        executeSqlQuerystring(query_systeminfo, query_systeminfo_count, Constants.SYSTEMINFO_DB);

                        // Inserting session entries.
                        executeSqlQuerystring(query_session, query_session_count, Constants.SESSIONS_DB);

                        // Inserting event count entries.
                        executeSqlQuerystring(query_event_count, query_event_count_count, Constants.EVENT_COUNT_DB);

                        // Inserting event_value entries.
                        executeSqlQuerystring(query_event_value, query_event_value_count, Constants.EVENT_VALUE_DB);

                        // Inserting event_value entries.
                        executeSqlQuerystring(query_event_string, query_event_string_count, Constants.EVENT_STRING_DB);

                        // Inserting location update entries.
                        executeSqlQuerystring(query_location, query_location_count, Constants.LOCATION_DB);

                        // Inserting download entries.
                        executeSqlQuerystring(query_download, query_download_count, Constants.DOWNLOAD_DB);
                    }
                }
                catch (Exception ex)
                {
                    counter.mysqlPushException++;
                    log.Error("Exception in push db entries", ex);
                }

                Thread.Sleep(TimeSpan.FromSeconds(MYSQL_THREAD_SLEEP_DURATION));
            }
        }

        /// <summary>
        /// Execute the input the SQL query string and insert the data into the MySQL table.
        /// </summary>
        /// <param name="query_prep_string"></param>
        /// <param name="query_total_count"></param>
        /// <param name="db_name"></param>
        private void executeSqlQuerystring(string query_prep_string, int query_total_count, string db_name)
        {
            try
            {
                if (query_total_count > 0)
                {
                    query_prep_string = query_prep_string.Remove(query_prep_string.Length - 1, 1);
                    query_prep_string += ";";
                    //log.Info("System SQL stmt: " + query_prep_string);

                    MySqlConnection conn = new MySqlConnection(connString);
                    MySqlCommand command = new MySqlCommand(query_prep_string, conn);
                    conn.Open();
                    command.ExecuteNonQuery();
                    conn.Close();
                    log.Info(query_total_count + " records written to " + db_name + " database");
                }
            }
            catch (Exception ex)
            {
                counter.mysqlExecuteQueryException++;
                log.Error("Exception in insertion of " + db_name + " entries", ex);
            }
        }

        /// <summary>
        /// Listen for new client connections and create a handler thread for them.
        /// </summary>
        private void ListenForClients()
        {
            this._tcpListener.Start();

            while (true)
            {
                // Prefer to crash stop listening for client instead of leaving any inconsistencies.
                //try
                //{
                //blocks until a client has connected to the server
                TcpClient client = this._tcpListener.AcceptTcpClient();

                //create a thread to handle communication with connected client
                Thread tcpThread = new Thread(new ParameterizedThreadStart(TCPConnHandler));
                tcpThread.Start(client);
                lock (_tcpThreads)
                {
                    if (_tcpThreads.ContainsKey(client))
                    {
                        log.Info("Already present... " + client.ToString());
                    }

                    _tcpThreads[client] = tcpThread;

                    //log.Info("Adding connection... " + _tcpThreads.Count + " connections");
                }
                //}
                //catch (Exception ex)
                //{
                //    log.Error("Exception while listening for new client connections", ex);
                //}
            }
        }

        /// <summary>
        /// End and remove the session for an Insight client. Also, record the cause of this action.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="isForceClosed"></param>
        /// <param name="closeType"></param>
        public void removeSessionInfoEntry(TcpClient client, Boolean isForceClosed, byte closeType)
        {
            try
            {
                lock (_sessionMap)
                {
                    DbEntrySessions dbEntrySessionInfo = _sessionMap[client];

                    if (dbEntrySessionInfo != null)
                    {
                        dbEntrySessionInfo.endTime = DateTime.Now;

                        if (isForceClosed)
                        {
                            dbEntrySessionInfo.isForceClosed = closeType;
                        }
                        else
                        {
                            dbEntrySessionInfo.isForceClosed = Constants.CLOSE_NORMAL;
                        }

                        _mysqlListLock.EnterWriteLock();

                        //log.Info("Inserting " + dbEntrySessionInfo.ToString() + " as session information....");
                        _dbEntryList.AddLast(dbEntrySessionInfo);
                        _mysqlListLock.ExitWriteLock();
                    }

                    _sessionMap[client] = null;
                    _sessionMap.Remove(client);
                    //log.Info("Removed session entry from sessionMap.. " + _sessionMap.Count + " entries");
                }
            }
            catch (Exception)
            {
                log.Error("removeSessionInfoEntry Error ");
            }
        }

        /// <summary>
        /// Handler for managing the TCP connection with a single Insight client.
        /// </summary>
        /// <param name="client"></param>
        private void TCPConnHandler(object client)
        {
            try
            {
                TcpClient tcpClient = (TcpClient)client;
                NetworkStream clientStream = tcpClient.GetStream();
                string ipAddress = IPAddress.Parse(((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString()).ToString();
                DbEntrySessions dbEntrySessionLocal = null;

                //TimerCallback timerCB = new TimerCallback(SendKeepAliveTCPPing);
                //Timer timer = null;
                //ConnectionInfo connInfo = new ConnectionInfo(clientStream, timer);

                bool isForceEnded = true;
                bool listenForMessages = true;

                byte closeType = Constants.CLOSE_NORMAL;
                byte[] message = new byte[BUFFER_SIZE];

                while (listenForMessages)
                {
                    int bytesRead = 0;
                    string str = "";

                    for (int i = 0; i < BUFFER_SIZE; i++)
                    {
                        message[i] = 0;
                    }

                    try
                    {
                        //blocks until a client sends a message
                        // TODO: commented out.
                        //bytesRead = clientStream.Read(message, 0, BUFFER_SIZE);
                        do
                        {
                            bytesRead = clientStream.Read(message, 0, BUFFER_SIZE);
                            str += System.Text.ASCIIEncoding.ASCII.GetString(message, 0, bytesRead).Trim('\0');
                            //log.Info("Reading Data... ");
                        }
                        while (!(str.StartsWith(MESSAGE_PREAMBLE) || str.EndsWith(MESSAGE_POSTAMBLE)) && bytesRead > 0);
                    }
                    catch (Exception)
                    {
                        //a socket error has occured
                        log.Error("TCPConnHandler: A socket error occured on read: ");
                        isForceEnded = true;
                        closeType = Constants.CLOSE_READ_ERROR;
                        counter.closeReadErrorCount++;

                        listenForMessages = false;
                        break;
                    }

                    if (bytesRead >= BUFFER_SIZE - 10)
                    {
                        counter.highMessageBufferUsageCount++;
                        log.Error("The number of bytes read " + bytesRead + " is near the buffer size of " + BUFFER_SIZE);
                    }

                    if (bytesRead == 0)
                    {
                        //the client has disconnected from the server
                        log.Error("TCPConnHandler: bytes read == 0");
                        isForceEnded = true;
                        closeType = Constants.CLOSE_READ_ZERO_BYTES;
                        counter.closeReadZeroCount++;

                        listenForMessages = false;
                        break;
                    }
                    else
                    {
                        //log.Info("TCPConnHandler: bytes read == " + bytesRead);
                    }

                    try
                    {
                        //string str = "";
                        //StringBuilder str = new StringBuilder();
                        //for (int i = 0; i < bytesRead; i++)
                        //{
                        //str += (char) message[i];
                        //str.Append((char) message[i]);
                        //}
                        //string str = System.Text.ASCIIEncoding.ASCII.GetString(message, 0, bytesRead).Trim('\0');

                        //log.Info("Currently " + _tcpThreads.Count + " connections");
                        //log.Info("Received " + bytesRead + " bytes.\nString: " + str + "\n");
                        //log.Info("Received from " + ipAddress + " bytesRead: " + bytesRead + " bytes.");

                        string[] message_strings;

                        if (str.StartsWith(MESSAGE_PREAMBLE))
                        {
                            message_strings = str.Split(new string[] { MESSAGE_PREAMBLE }, StringSplitOptions.None);
                            for (int a = 1; a < message_strings.Length; a++)
                            {
                                message_strings[a] = MESSAGE_PREAMBLE + message_strings[a];
                            }
                        }
                        else
                        {
                            message_strings = str.Split(new string[] { MESSAGE_PREAMBLE2 }, StringSplitOptions.None);
                            for (int b = 1; b < message_strings.Length; b++)
                            {
                                message_strings[b] = MESSAGE_PREAMBLE2 + message_strings[b];
                            }

                        }

                        int r = 1;

                        for (; r < 2; r++) //message_strings.Length; a++)
                        {
                            //log.Info("Processing string " + a + ": " + message_strings[a] + "\n");

                            // TODO: Commented out.
                            string[] message_contents = message_strings[r].Split(new string[] { JOIN_DELIMITER }, StringSplitOptions.None);

                            // TODO: Commented out.
                            //string[] message_contents = str.Split(new string[] { JOIN_DELIMITER }, StringSplitOptions.None);

                            if (message_contents[0].Equals(MESSAGE_PREAMBLE) || message_contents[0].Equals(MESSAGE_PREAMBLE2))
                            {
                                int messageType = Byte.Parse(message_contents[1]);

                                //log.Info("Received " + a + " count " + messageType + " messageType.");

                                // TODO: Finalize.
                                if (message_contents[2] == "349cb138dac382b00136541a3e3c837a")
                                {
                                    log.Info("Received " + messageType + " messageType.");
                                    log.Info("Received " + bytesRead + " bytes.\nString: " + str + "\n");
                                }

                                // Client initiating a new session with the Insight server.
                                if (messageType == Constants.SESSIONS)
                                {
                                    //log.Info("Processing " + message_contents[1] + " as session information....");
                                    dbEntrySessionLocal = initNewEntrySessionInfo(message_contents, tcpClient);

                                    try
                                    {
                                        //log.Info("Sending: " + System.Text.ASCIIEncoding.ASCII.GetString(clientConfigBytes, 0, clientConfigLength).Trim('\0'));
                                        clientStream.Write(clientConfigBytes, 0, clientConfigLength);
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error("Exception while sending config information.", ex);
                                        isForceEnded = true;
                                        closeType = Constants.CLOSE_SEND_CONFIG_ERROR;
                                        counter.closeSendConfigErrorCount++;

                                        listenForMessages = false;
                                        //break;
                                    }

                                    // While loop here sending data every x seconds.
                                    //timer = new Timer(timerCB, connInfo, KEEP_ALIVE_INIT_TIME * SEC, KEEP_ALIVE_PING_INTERVAL * SEC);
                                    //connInfo.timer = timer;
                                }
                                else if (messageType == Constants.RESOURCE) // Receive statistics about CPU and memory usage.
                                {
                                    DbEntryResource dbEntryResource = processResourceString(message_contents);

                                    _mysqlListLock.EnterWriteLock();
                                    //log.Info("Inserting " + message_contents[1] + " as resource information....");
                                    if (dbEntryResource != null)
                                    {
                                        //log.Info("Inserting " + dbEntryResource.ToString() + " as resource information....");
                                        _dbEntryList.AddLast(dbEntryResource);
                                    }
                                    _mysqlListLock.ExitWriteLock();
                                }
                                else if (messageType == Constants.BATTERY) // Receive statistics about battery usage.
                                {
                                    DbEntryBattery dbEntryBattery = processBatteryInfoString(message_contents, tcpClient, dbEntrySessionLocal);

                                    _mysqlListLock.EnterWriteLock();
                                    //log.Info("Processing " + message_contents[1] + " as battery information....");
                                    if (dbEntryBattery != null)
                                    {
                                        //log.Info("Inserting " + dbEntryBattery.ToString() + " as battery information....");
                                        _dbEntryList.AddLast(dbEntryBattery);
                                    }
                                    _mysqlListLock.ExitWriteLock();
                                }
                                else if (messageType == Constants.LOCATION_INFO) // Receive statistics about location.
                                {
                                    DbEntryLocation dbEntryLocationinfo = processLocationinfoString(message_contents);

                                    _mysqlListLock.EnterWriteLock();
                                    //log.Info("Processing " + message_contents[1] + " as location information....");

                                    if (dbEntryLocationinfo != null)
                                    {
                                        //log.Info("Inserting " + dbEntrySysteminfo.ToString() + " as system information....");
                                        _dbEntryList.AddLast(dbEntryLocationinfo);
                                    }
                                    _mysqlListLock.ExitWriteLock();
                                }
                                else if (messageType == Constants.EVENT_UPDATE_INFO) // Receive statistics about application specific events logged by the developer through the Insight API.
                                {
                                    //log.Info("Received remove currentStats message.");
                                    processStatsUpdateString(message_contents, dbEntrySessionLocal);
                                }
                                else if (messageType == Constants.SYSTEM_INFO) // Receive statistics about the client device (e.g., device name, screen size etc.)
                                {
                                    DbEntrySysteminfo dbEntrySysteminfo = processSysteminfoString(message_contents, ipAddress);

                                    _mysqlListLock.EnterWriteLock();
                                    //log.Info("Processing " + message_contents[1] + " as system information....");

                                    if (dbEntrySysteminfo != null)
                                    {
                                        //log.Info("Inserting " + dbEntrySysteminfo.ToString() + " as system information....");
                                        _dbEntryList.AddLast(dbEntrySysteminfo);
                                    }
                                    _mysqlListLock.ExitWriteLock();
                                }
                                else if (messageType == Constants.APPUID_INFO) // Receive the client application specifc UID reported by the developer through the Insight API.
                                {
                                    updateApplicationUid(message_contents, dbEntrySessionLocal);
                                }
                                else if (messageType == Constants.REMOVE_SESSION) // Client notification to close the current session. The client sends this message when application containing the Insight library is closed. 
                                {
                                    //log.Info("Received remove session message.");
                                    processEndSessionString(message_contents, tcpClient, dbEntrySessionLocal);
                                    isForceEnded = false;
                                    counter.closeNormalCount++;

                                    listenForMessages = false;
                                    //break;
                                }
                                else
                                {
                                    log.Warn("Warning!! The received message type " + messageType + "is not valid...");
                                }
                            }
                            else
                            {
                                log.Warn("Warning!! The first received is " + message_contents[0] +
                                "... Should be " + MESSAGE_PREAMBLE + " or " + MESSAGE_PREAMBLE2 + "....");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("Exception while processing the received message ", ex);
                    }

                    // Update the keep alive timer.
                    if (dbEntrySessionLocal != null)
                    {
                        dbEntrySessionLocal.lastMsgReceivedTime = DateTime.Now;
                    }
                    else
                    {
                        log.Warn("dbEntrySessionLocal is null in TCP connection handler.");
                    }
                }


                // Close the TCP connection to the Insight client, if the previous loop is exited.
                try
                {
                    tcpClient.Close();
                }
                catch (Exception ex)
                {
                    log.Error("Exception while closing tcpClient: ", ex);
                }

                try
                {
                    clientStream.Close();
                }
                catch (Exception ex)
                {
                    log.Error("Exception while closing clientStream: ", ex);
                }

                /*
                try
                {
                    if (timer != null)
                    {
                        log.Info("Disposing the timer object");
                        timer.Dispose();
                        timer = null;
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Exception while disposing timer: ", ex);

                }
                */

                lock (_tcpThreads)
                {
                    if (isForceEnded)
                    {
                        //log.Info("Calling removeSessionInfoEntry in TCPConnhandler, isForceClosed = True.");
                        removeSessionInfoEntry(tcpClient, true, closeType);
                    }
                    else
                    {
                        //log.Info("Calling removeSessionInfoEntry in TCPConnhandler, isForceClosed = False.");
                        removeSessionInfoEntry(tcpClient, false, Constants.CLOSE_NORMAL);
                    }

                    _tcpThreads[tcpClient] = null;
                    _tcpThreads.Remove(tcpClient);

                    //log.Info("Removed connection. Now " + _tcpThreads.Count + " connections.");
                }
            }
            catch (Exception ex)
            {
                log.Error("Error in TCP connection", ex);
            }

            //log.Info("TCPConnHandler thread ended.");
        }

        /// <summary>
        /// Update the application specific user ID (e.g., a user's player name within a game). It enables the developer to
        /// associate the statistics collected by Insight with the application specific user ID.
        /// </summary>
        /// <param name="message_contents"></param>
        /// <param name="dbEntrySessionInfo"></param>
        private void updateApplicationUid(string[] message_contents, DbEntrySessions dbEntrySessionInfo)
        {
            try
            {
                //log.Info("Processing Battery Info String of length {0} items.", message_contents.Length);
                int indexNum = DEVICE_ID_INDEX;

                string deviceID = message_contents[indexNum++];
                int appID = Convert.ToInt32(message_contents[indexNum++]);
                string sessionID = message_contents[indexNum++];

                string applicationUID = message_contents[indexNum++];

                if (dbEntrySessionInfo != null)
                {
                    dbEntrySessionInfo.applicationUID = applicationUID;
                }
            }
            catch (Exception ex)
            {
                String msgStr = "";
                for (int i = 0; i < message_contents.Length; i++)
                {
                    msgStr += message_contents[i] + " ";
                }

                counter.msgAppuidException++;
                log.Error("Exception while processing application UID information " + msgStr, ex);
            }
        }

        /// <summary>
        /// Initialize a new Insight session with the input client for stats collection.
        /// </summary>
        /// <param name="message_contents"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public DbEntrySessions initNewEntrySessionInfo(string[] message_contents, TcpClient client)
        {
            try
            {
                //log.Info("Processing Session Info String of length {0} items.", message_contents.Length);
                int indexNum = DEVICE_ID_INDEX;

                string deviceID = message_contents[indexNum++];
                int appID = Convert.ToInt32(message_contents[indexNum++]);
                string sessionID = message_contents[indexNum++];
                int platform = Convert.ToInt32(message_contents[indexNum++]);

                DbEntrySessions tempDbEntrySessions;

                lock (_sessionMap)
                {
                    if (!_sessionMap.ContainsKey(client))
                    {
                        tempDbEntrySessions = new DbEntrySessions(appID, deviceID, sessionID, platform);
                        _sessionMap[client] = tempDbEntrySessions;
                    }
                    else
                    {
                        tempDbEntrySessions = _sessionMap[client];
                    }

                    //log.Info("Added Session Item : " + _sessionMap.Count + " entries...");
                }

                return tempDbEntrySessions;
            }
            catch (Exception ex)
            {
                log.Error("Exception while processing a new initiate session entry ", ex);
                counter.msgInitSessionException++;
                return null;
            }
        }

        /// <summary>
        /// Process the location update message reported by the Insight client.
        /// </summary>
        /// <param name="message_contents"></param>
        /// <returns></returns>
        private DbEntryLocation processLocationinfoString(string[] message_contents)
        {
            try
            {
                //log.Info("Processing Battery Info String of length {0} items.", message_contents.Length);
                int indexNum = DEVICE_ID_INDEX;

                string deviceID = message_contents[indexNum++];
                int appID = Convert.ToInt32(message_contents[indexNum++]);
                string sessionID = message_contents[indexNum++];

                string[] locationContents = message_contents[indexNum].Split(new string[] { "@" }, StringSplitOptions.None);

                DbEntryLocation locationInfo = new DbEntryLocation(appID, deviceID, sessionID,
                    Convert.ToDouble(locationContents[0]), Convert.ToDouble(locationContents[1]),
                    locationContents[2], locationContents[3]);

                return locationInfo;
            }
            catch (Exception ex)
            {
                String msgStr = "";
                for (int i = 0; i < message_contents.Length; i++)
                {
                    msgStr += message_contents[i] + " ";
                }

                counter.msgLocationException++;
                log.Error("Exception while processing location information " + msgStr, ex);
                return null;
            }
        }

        /// <summary>
        /// Process the event count messages received from the Insight client.
        /// </summary>
        /// <param name="eventCounts"></param>
        /// <param name="appID"></param>
        /// <param name="deviceID"></param>
        /// <param name="sessionID"></param>
        private void addEventsCountsInformation(string[] eventCounts, int appID, string deviceID, string sessionID)
        {
            _mysqlListLock.EnterWriteLock();

            for (int i = 0; i < eventCounts.Length - 1; i++)
            {
                try
                {
                    //log.Info("Processing " + eventCounts[i] + " as event count");
                    string[] tupleItem = eventCounts[i].Split(new string[] { "#" }, StringSplitOptions.None);

                    _dbEntryList.AddLast(new DbEntryEventCount(appID, deviceID, sessionID,
                        Convert.ToInt32(tupleItem[0]), Convert.ToInt32(tupleItem[1])));
                }
                catch (Exception ex)
                {
                    counter.msgEventCountException++;
                    log.Error("Exception Processing Event Count Item.. ", ex);
                }
            }

            _mysqlListLock.ExitWriteLock();
        }

        /// <summary>
        /// Process the event-value tuple messages received from the Insight client.
        /// </summary>
        /// <param name="eventVals"></param>
        /// <param name="appID"></param>
        /// <param name="deviceID"></param>
        /// <param name="sessionID"></param>
        private void addEventsValuesInformation(string[] eventVals, int appID, string deviceID, string sessionID)
        {
            _mysqlListLock.EnterWriteLock();

            for (int i = 0; i < eventVals.Length - 1; i++)
            {
                try
                {
                    //log.Info("Processing " + eventVals[i] + " as event value");
                    string[] tupleItem = eventVals[i].Split(new string[] { "#" }, StringSplitOptions.None);
                    _dbEntryList.AddLast(new DbEntryEventValue(appID, deviceID, sessionID, i,
                        Convert.ToInt64(tupleItem[1]), Convert.ToInt32(tupleItem[0]), Convert.ToDouble(tupleItem[2])));
                }
                catch (Exception ex)
                {
                    counter.msgEventValException++;
                    log.Error("Exception Processing Event Value Item.. ", ex);
                }
            }

            _mysqlListLock.ExitWriteLock();
        }

        /// <summary>
        /// Process the event-value string tuple messages received from the Insight client.
        /// </summary>
        /// <param name="eventStrings"></param>
        /// <param name="appID"></param>
        /// <param name="deviceID"></param>
        /// <param name="sessionID"></param>
        private void addEventStringInformation(string[] eventStrings, int appID, string deviceID, string sessionID)
        {
            _mysqlListLock.EnterWriteLock();

            for (int i = 0; i < eventStrings.Length - 1; i++)
            {
                try
                {
                    //log.Info("Processing " + eventStrings[i] + " as event string");
                    string[] tupleItem = eventStrings[i].Split(new string[] { "#" }, StringSplitOptions.None);
                    _dbEntryList.AddLast(new DbEntryEventString(appID, deviceID, sessionID, i,
                        Convert.ToInt64(tupleItem[1]), Convert.ToInt32(tupleItem[0]), tupleItem[2]));
                }
                catch (Exception ex)
                {
                    counter.msgEventValException++;
                    log.Error("Exception Processing Event String Item.. ", ex);
                }
            }

            _mysqlListLock.ExitWriteLock();
        }

        /// <summary>
        /// Process the download activity events reported by the application developer through the Insight API.
        /// </summary>
        /// <param name="downloadInfo"></param>
        /// <param name="appID"></param>
        /// <param name="deviceID"></param>
        /// <param name="sessionID"></param>
        private void addDownloadsInformation(string[] downloadInfo, int appID, string deviceID, string sessionID)
        {
            _mysqlListLock.EnterWriteLock();

            DbEntryDownloads tempEntry = null;

            for (int i = 0; i < downloadInfo.Length - 1; i++)
            {
                try
                {
                    //log.Info("Processing " + downloadInfo[i] + " as download Info");
                    string[] tupleItem = downloadInfo[i].Split(new string[] { "#" }, StringSplitOptions.None);

                    tempEntry = new DbEntryDownloads(appID, deviceID, sessionID, i,
                        Convert.ToInt64(tupleItem[2]), Convert.ToInt64(tupleItem[0]), Convert.ToInt64(tupleItem[1]), Convert.ToInt64(tupleItem[3]));

                    // TODO: Finalize. Filtering out the small downloads.
                    if (tempEntry.rxBytes > MIN_RECEIVED_BYTES)
                    {
                        _dbEntryList.AddLast(tempEntry);
                    }
                }
                catch (Exception ex)
                {
                    counter.msgDownloadInfoException++;
                    log.Error("Exception Processing Event Value Item.. ", ex);
                }
            }

            _mysqlListLock.ExitWriteLock();
        }

        /// <summary>
        /// End the existing Insight session with the input client.
        /// </summary>
        /// <param name="message_contents"></param>
        /// <param name="client"></param>
        /// <param name="dbEntrySessionInfo"></param>
        public void processEndSessionString(string[] message_contents, TcpClient client, DbEntrySessions dbEntrySessionInfo)
        {
            int indexNum = DEVICE_ID_INDEX;

            string deviceID = message_contents[indexNum++];
            int appID = Convert.ToInt32(message_contents[indexNum++]);
            string sessionID = message_contents[indexNum++];

            //log.Info("Processing " + message_contents[indexNum - 1] + "'s end session string : " + message_contents[indexNum]);

            string[] stringContents = message_contents[indexNum].Split(new string[] { "$" }, StringSplitOptions.None);

            string[] dataTransferVals = stringContents[0].Split(new string[] { "@" }, StringSplitOptions.None);
            string[] eventCounts = stringContents[1].Split(new string[] { "@" }, StringSplitOptions.None);
            string[] eventVals = stringContents[2].Split(new string[] { "@" }, StringSplitOptions.None);
            string[] eventStrings = stringContents[3].Split(new string[] { "@" }, StringSplitOptions.None);
            string[] downloadInfo = stringContents[4].Split(new string[] { "@" }, StringSplitOptions.None);

            //log.Info("Processing " + stringContents[0] + " as dataTransferVals");
            //log.Info("Processing " + stringContents[1] + " as eventCounts");
            //log.Info("Processing " + stringContents[2] + " as eventVals");
            //log.Info("Processing " + stringContents[3] + " as eventStrings");
            //log.Info("Processing " + stringContents[4] + " as downloadInfo");

            // Adding the data transfer information.
            //lock (_sessionMap)
            {
                try
                {
                    //DbEntrySessions dbEntrySessionInfo = _sessionMap[client];
                    dbEntrySessionInfo.totalTxBytes = Convert.ToInt64(dataTransferVals[0]);
                    dbEntrySessionInfo.totalRxBytes = Convert.ToInt64(dataTransferVals[1]);
                    dbEntrySessionInfo.mobileTxBytes = Convert.ToInt64(dataTransferVals[2]);
                    dbEntrySessionInfo.mobileRxBytes = Convert.ToInt64(dataTransferVals[3]);
                    dbEntrySessionInfo.appTxBytes = Convert.ToInt64(dataTransferVals[4]);
                    dbEntrySessionInfo.appRxBytes = Convert.ToInt64(dataTransferVals[5]);

                    //log.Info("Session: " + dbEntrySessionInfo);
                }
                catch (Exception ex)
                {
                    counter.msgDataTransException++;
                    log.Error("Exception Processing Data Transfer information. ", ex);
                }
            }

            // Adding the event count information.
            addEventsCountsInformation(eventCounts, appID, deviceID, sessionID);

            // Adding the event-value information.
            addEventsValuesInformation(eventVals, appID, deviceID, sessionID);

            // Adding the event-value information.
            addEventStringInformation(eventStrings, appID, deviceID, sessionID);

            // Adding the download times information.
            addDownloadsInformation(downloadInfo, appID, deviceID, sessionID);
        }

        /// <summary>
        /// Process the application specific event statistics reported by the application developer through the Insight API.
        /// </summary>
        /// <param name="message_contents"></param>
        /// <param name="dbEntrySessionLocal"></param>
        public void processStatsUpdateString(string[] message_contents, DbEntrySessions dbEntrySessionLocal)
        {
            int indexNum = DEVICE_ID_INDEX;

            string deviceID = message_contents[indexNum++];
            int appID = Convert.ToInt32(message_contents[indexNum++]);
            string sessionID = message_contents[indexNum++];

            string[] stringContents = message_contents[indexNum].Split(new string[] { "$" }, StringSplitOptions.None);

            string[] eventVals = stringContents[0].Split(new string[] { "@" }, StringSplitOptions.None);
            string[] eventStrings = stringContents[1].Split(new string[] { "@" }, StringSplitOptions.None);
            string[] downloadInfo = stringContents[2].Split(new string[] { "@" }, StringSplitOptions.None);

            //log.Info("Processing " + stringContents[0] + " as eventVals");
            //log.Info("Processing " + stringContents[1] + " as eventStrings");
            //log.Info("Processing " + stringContents[2] + " as downloadInfo");

            // Adding the event-value information.
            addEventsValuesInformation(eventVals, appID, deviceID, sessionID);

            // Adding the event-value information.
            addEventStringInformation(eventStrings, appID, deviceID, sessionID);

            // Adding the download times information.
            addDownloadsInformation(downloadInfo, appID, deviceID, sessionID);
        }

        /// <summary>
        /// Process the battery consumption statistics reported by the Insight client.
        /// </summary>
        /// <param name="message_contents"></param>
        /// <param name="client"></param>
        /// <param name="dbEntrySessionInfo"></param>
        /// <returns></returns>
        private DbEntryBattery processBatteryInfoString(string[] message_contents, TcpClient client, DbEntrySessions dbEntrySessionInfo)
        {
            try
            {
                //log.Info("Processing Battery Info String of length {0} items.", message_contents.Length);
                int indexNum = DEVICE_ID_INDEX;

                string deviceID = message_contents[indexNum++];
                int appID = Convert.ToInt32(message_contents[indexNum++]);
                string sessionID = message_contents[indexNum++];

                DbEntryBattery batteryInfo = new DbEntryBattery(appID, deviceID, sessionID);
                batteryInfo.sequenceNumber = Convert.ToInt32(message_contents[indexNum++]);
                batteryInfo.level = Convert.ToInt32(message_contents[indexNum++]);
                batteryInfo.scale = Convert.ToInt32(message_contents[indexNum++]);
                batteryInfo.temp = Convert.ToInt32(message_contents[indexNum++]);
                batteryInfo.voltage = Convert.ToInt32(message_contents[indexNum++]);
                batteryInfo.health = Convert.ToInt32(message_contents[indexNum++]);
                batteryInfo.technology = message_contents[indexNum++];
                batteryInfo.plugged = Convert.ToInt32(message_contents[indexNum++]);

                // Adding the battery technology information.
                //lock (_sessionMap)
                {
                    try
                    {
                        //DbEntrySessions dbEntrySessionInfo = _sessionMap[client];
                        dbEntrySessionInfo.batteryTechnology = batteryInfo.technology;
                    }
                    catch (Exception ex)
                    {
                        log.Error("Exception while processing battery technology information: " + batteryInfo.technology, ex);
                        return null;
                    }
                }

                return batteryInfo;
            }
            catch (Exception ex)
            {
                String msgStr = "";
                for (int i = 0; i < message_contents.Length; i++)
                {
                    msgStr += message_contents[i] + " ";
                }

                counter.msgBatteryException++;
                log.Error("Exception while processing battery information " + msgStr, ex);
                return null;
            }
        }

        /// <summary>
        /// Process the device information string (e.g., device type, screen size, OS version) obtained from the Insight client.
        /// </summary>
        /// <param name="message_contents"></param>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private DbEntrySysteminfo processSysteminfoString(string[] message_contents, string ipAddress)
        {
            try
            {
                //log.Info("Processing System Info String of length " + message_contents.Length + " items.");
                int indexNum = DEVICE_ID_INDEX;

                string deviceID = message_contents[indexNum++];
                int appID = Convert.ToInt32(message_contents[indexNum++]);
                string sessionID = message_contents[indexNum++];

                DbEntrySysteminfo systemInfo = new DbEntrySysteminfo(appID, deviceID, sessionID);
                systemInfo.ipAddress = ipAddress;
                //systemInfo.ipAddress = message_contents[indexNum++];
                systemInfo.cellularCarrier = message_contents[indexNum++];

                systemInfo.osVersion = message_contents[indexNum++];
                systemInfo.osBuild = message_contents[indexNum++];
                systemInfo.osAPI = message_contents[indexNum++];

                systemInfo.deviceName = message_contents[indexNum++];
                systemInfo.deviceModel = message_contents[indexNum++];
                systemInfo.deviceProduct = message_contents[indexNum++];
                systemInfo.deviceManufacturer = message_contents[indexNum++];
                systemInfo.deviceBoard = message_contents[indexNum++];
                systemInfo.deviceBrand = message_contents[indexNum++];

                systemInfo.processor = message_contents[indexNum++];
                systemInfo.bogomips = message_contents[indexNum++];
                systemInfo.hardware = message_contents[indexNum++];
                systemInfo.memtotal = Convert.ToInt64(message_contents[indexNum++]) / 1024;

                systemInfo.screenWidth = Convert.ToInt32(message_contents[indexNum++]);
                systemInfo.screenHeight = Convert.ToInt32(message_contents[indexNum++]);
                systemInfo.densityDpi = Convert.ToInt32(message_contents[indexNum++]);
                systemInfo.screenXDpi = Convert.ToInt32(message_contents[indexNum++]);
                systemInfo.screenYDpi = Convert.ToInt32(message_contents[indexNum++]);

                systemInfo.isGpsLocationOn = Convert.ToByte(message_contents[indexNum++]);
                systemInfo.isNetworkLocationOn = Convert.ToByte(message_contents[indexNum++]);

                systemInfo.activeNetwork = 0;
                if (message_contents.Length > indexNum)
                {
                    systemInfo.activeNetwork = Convert.ToByte(message_contents[indexNum++]);
                }

                systemInfo.activeSubType = 0;
                if (message_contents.Length > indexNum)
                {
                    systemInfo.activeSubType = Convert.ToByte(message_contents[indexNum++]);
                }

                return systemInfo;
            }
            catch (Exception ex)
            {
                String msgStr = "";
                for (int i = 0; i < message_contents.Length; i++)
                {
                    msgStr += message_contents[i] + " ";
                }

                counter.msgSystemInfoException++;
                log.Error("Exception while processing system information " + msgStr, ex);
                return null;
            }
        }

        /// <summary>
        /// Process the CPU and memory consumption statistics collected from the Insight clients.
        /// </summary>
        /// <param name="message_contents"></param>
        /// <returns></returns>
        private DbEntryResource processResourceString(string[] message_contents)
        {
            try
            {
                //log.Info("Processing Resource Info String of length {0} " + message_contents.Length + " items..");
                int indexNum = DEVICE_ID_INDEX;

                string deviceID = message_contents[indexNum++];
                int appID = Convert.ToInt32(message_contents[indexNum++]);
                string sessionID = message_contents[indexNum++];

                DbEntryResource resourceUsageStats = new DbEntryResource(appID, deviceID, sessionID);
                resourceUsageStats.sequenceNumber = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.totalEntries = Convert.ToInt32(message_contents[indexNum++]);

                resourceUsageStats.pidVal = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.cpuUsage = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.rss = Convert.ToDouble(message_contents[indexNum++]);
                resourceUsageStats.vss = Convert.ToDouble(message_contents[indexNum++]);
                resourceUsageStats.thrVal = Convert.ToInt32(message_contents[indexNum++]);

                resourceUsageStats.bogomips = Convert.ToDouble(message_contents[indexNum++]);

                resourceUsageStats.totalCpuIdleRatio = Convert.ToDouble(message_contents[indexNum++]);

                resourceUsageStats.avgLoadOneMin = Convert.ToDouble(message_contents[indexNum++]);
                resourceUsageStats.avgLoadFiveMin = Convert.ToDouble(message_contents[indexNum++]);
                resourceUsageStats.avgLoadFifteenMin = Convert.ToDouble(message_contents[indexNum++]);
                resourceUsageStats.runningProcs = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.totalProcs = Convert.ToInt32(message_contents[indexNum++]);

                resourceUsageStats.memTotalAvail = (Convert.ToInt64(message_contents[indexNum++]) / 1024);
                resourceUsageStats.availMem = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.threshold = Convert.ToInt32(message_contents[indexNum++]);

                resourceUsageStats.totalCpuUsage = Convert.ToInt32(message_contents[indexNum++]);

                resourceUsageStats.screenBrightness = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.isScreenOn = Convert.ToInt32(message_contents[indexNum++]);

                resourceUsageStats.isAudioSpeakerOn = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.isAudioWiredHeadsetOn = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.audioLevel = Convert.ToInt32(message_contents[indexNum++]);
                resourceUsageStats.audioMaxLevel = Convert.ToInt32(message_contents[indexNum++]);

                return resourceUsageStats;
            }
            catch (Exception ex)
            {
                String msgStr = "";
                for (int i = 0; i < message_contents.Length; i++)
                {
                    msgStr += message_contents[i] + " ";
                }

                counter.msgResourceException++;
                log.Error("Exception while processing resource information " + msgStr, ex);
                return null;
            }
        }
    }
}
