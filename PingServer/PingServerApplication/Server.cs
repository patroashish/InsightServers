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
using System.Net;
using System.Net.Sockets;

using log4net;
using MySql.Data;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PingServer
{
    /// <summary>
    /// The main server class that performs application level TCP and UDP ping measuremnts with the Insight clients. 
    /// The measurement works as follows:
    /// 
    /// - For UDP measurements, the client sends an initialization packet that is used to punch holes in the NATs on the path to this measurement server. 
    ///   The server periodically sends a UDP ping to all active clients and the clients echo this packet back to the server with some network measurement data.
    ///   The server records the network measurement data received from the clients. It also computes the interval between the time of transmission of the ping 
    ///   packet and the time of arrival of the client's response. This is used to compute the application layer network latency between the client and the measurement server.
    /// 
    /// - For the TCP measurements, the clients establish a TCP conenction with this measurement server. From this point onwards, the server performs network
    ///   measurements with the clients in the same fashion as the UDP measurements.
    /// </summary>
    public class Server
    {
        /// <summary>
        /// Used to per-client end point information for UDP measurements only.
        /// </summary>
        class UdpState
        {
            public IPEndPoint e;
            public UdpClient u;
        }

        // Magic number used used at the beginning of each Ping RTT measurement packet.
        public const byte SERVER_IDENTIFIER = 8;

        // Network related constants.
        private const int SERVER_PORT = 11200;
        private const int PACKET_BUFFER_SIZE = 512;
        private const int MAX_TIMEOUT_COUNT_VAL = 2;

        // Minimum number of entries before updating MySQL database.
        private const int MYSQL_ENTRYCOUNT_THRESHOLD = 10;

        // Time related variables.
        private const int MIN = 60; // The number of seconds in a minute.

        private const int CLIENT_PING_INTERVAL = 0;
        private const int DEFAULT_UDP_PING_INTERVAL = 30; // Server interval in seconds
        private const int SERVER_PING_INTERVAL = 30; // Server interval in seconds

        private const int CLEANUP_SLEEP_DURATION = 15 * MIN; // In Seconds.

        private const int MYSQL_SLEEP_INTERVAL = 11 * MIN; // In Seconds

        // Logger setup
        //private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Server));
        static ILog log;

        private static ReaderWriterLockSlim _mysqlListLock = new ReaderWriterLockSlim();
        private LinkedList<DbEntry> _dbEntryList;

        // For synchronization
        private static readonly object _udpRecordsLock = new object();
        private static readonly object _removeListLock = new object();
        private static readonly object _addListLock = new object();
        private static readonly object _timeoutCounterLock = new object();

        // TCP and UDP ping measurement related structures.
        private Dictionary<string, Dictionary<int, UDPRtt>> _udpRecords;
        private Dictionary<string, UDPConnection> _udpConnections;
        private LinkedList<UDPConnection> _removeList;
        private LinkedList<UDPConnection> _addList;
        private Dictionary<string, int> _timeoutCounter;

        private TcpListener _tcpListener;
        private UdpClient _udpClient;

        private Dictionary<TcpClient, Thread> _tcpThreads = new Dictionary<TcpClient, Thread>();
        private Thread _listenThread;
        private Thread _udpThread;
        private Thread _mysqlThread;
        private Thread _cleanupThread;

        //System.Timers.Timer _tcpCleanerTimer = new System.Timers.Timer(1000 * CLEANUP_SLEEP_DURATION); // 10 MIN
        private string connString;

        public Server()
        {
            try
            {
                // setup logging
                log4net.Config.XmlConfigurator.Configure();
                log = LogManager.GetLogger(typeof(Server));
                log.Info("Ping Server Console started.  Logging started.  Starting the Ping Server...\n");

                _dbEntryList = new LinkedList<DbEntry>();

                // Start MySQL thread
                connString = String.Format("server={0};user={1};database={2};port=3306;password={3};",
                    Constants.MYSQL_SERVER, Constants.MYSQL_USERNAME, Constants.MYSQL_DBNAME, Constants.MYSQL_PASSWORD);

                _mysqlThread = new Thread(new ThreadStart(PushDbEntries));

                // Start TCP thread
                _tcpListener = new TcpListener(IPAddress.Any, SERVER_PORT);
                _listenThread = new Thread(new ThreadStart(ListenForClients));
                _udpThread = new Thread(new ThreadStart(UdpPinger));

                _udpRecords = new Dictionary<string, Dictionary<int, UDPRtt>>();
                _udpConnections = new Dictionary<string, UDPConnection>();
                _addList = new LinkedList<UDPConnection>();
                _removeList = new LinkedList<UDPConnection>();
                _timeoutCounter = new Dictionary<string, int>();
                _udpClient = new UdpClient(SERVER_PORT);

                // see http://msdn.microsoft.com/en-us/library/ms741621.aspx
                // and http://www.netframeworkdev.com/net-framework-networking-communication/c-udp-socket-exception-on-bad-disconnect-46210.shtml
                // Crashes in mono because flag is not recognized: http://jira.openmetaverse.org/browse/LIBOMV-573
                try 
                {
	                uint IOC_IN = 0x80000000;
        	        uint IOC_VENDOR = 0x18000000;
                	uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
	                _udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
                }
                catch (Exception ex)
                {
	                log.Error("Exception while setting the _udpClient.Client.IOControl flags. Exceptions occurs due to mono,", ex);
                }

                AsyncBeginReceive();

                // Start the cleanup thread;
                _cleanupThread = new Thread(new ThreadStart(Cleanup_Dead_Connections));
            	log.Info("server is now running");
            }
            catch (Exception ex)
            {
                log.Error("Exception in constructor ", ex);
            }
        }

        /// <summary>
        /// Start the TCP and UDP ping measurement activity. Also, start the mysql thread that periodically inserts the ping data into the database.
        /// </summary>
        public void StartThreads()
        {
            log4net.Config.XmlConfigurator.Configure();
            /*
            _tcpCleanerTimer.Elapsed += new System.Timers.ElapsedEventHandler(_cleanup_Dead_Connections);
            _tcpCleanerTimer.AutoReset = true;
            _tcpCleanerTimer.Enabled = true;
            _tcpCleanerTimer.Start();
            */

            _mysqlThread.Start();
            _listenThread.Start();
            _udpThread.Start();
            _cleanupThread.Start();
        }

        /// <summary>
        /// Stop all measurement activity and server threads.
        /// </summary>
        public void ShutDown()
        {
            log4net.Config.XmlConfigurator.Configure();

            _mysqlThread.Abort();
            _listenThread.Abort();
            _udpThread.Abort();
            _cleanupThread.Abort();

            foreach (var el in _tcpThreads)
            {
                el.Value.Abort();
            }
        }

        //private void _cleanup_Dead_Connections(object sender, System.Timers.ElapsedEventArgs e)
        /// <summary>
        /// Periodically check if any of the tcp connections have gone bad... then remove them...
        /// </summary>
        private void Cleanup_Dead_Connections()
        {
            while (true)
            {
                try
                {
                    lock (_tcpThreads)
                    {
                        log.Info("Before cleanup: " + _tcpThreads.Count + " TCP connections");
                        List<TcpClient> removals = new List<TcpClient>();
                        DateTime now = DateTime.Now;

                        /* 
                         * Mark all inactive TCP measurement threads for removal that have not closed yet. 
                         * This is used to handle cases when some failure conditions happen at the client side (e.g., application crash).
                         */ 
                        foreach (TcpClient tcpclient in _tcpThreads.Keys)
                        {
                            if (!tcpclient.Connected)
                            {
                                removals.Add(tcpclient);
                            }
                        }

                        // Remove the inactive TCP connections.
                        foreach (TcpClient tcpclient in removals)
                        {
                            _tcpThreads[tcpclient] = null;
                            _tcpThreads.Remove(tcpclient);
                        }

                        log.Info("After cleanup: " + _tcpThreads.Count + " TCP connections");
                    }
                }
                catch (Exception ex)
                {
                    log.Error("CleanupDeadConnections Error ", ex);
                }

                Thread.Sleep(TimeSpan.FromSeconds(CLEANUP_SLEEP_DURATION));
            }
        }

        /// <summary>
        /// Periodically push the collected ping measurement data into the MySQL database.  
        /// The periodicity is controlled by the MYSQL_SLEEP_INTERVAL parameter.
        /// </summary>
        private void PushDbEntries()
        {
            while (true)
            {
                try
                {
                    log.Info("MYSQL EntryCount Check: " + _dbEntryList.Count + " " + MYSQL_ENTRYCOUNT_THRESHOLD);
                    if (_dbEntryList.Count > MYSQL_ENTRYCOUNT_THRESHOLD)
                    {
                        _mysqlListLock.EnterReadLock();
                        LinkedList<DbEntry> listCopy = new LinkedList<DbEntry>(_dbEntryList);
                        _dbEntryList.Clear();
                        _mysqlListLock.ExitReadLock();

                        string query = "INSERT INTO " + Constants.MYSQL_TABLE + " (rtt, appID, deviceID, sessionID, serverTime, sequenceNumber, platform, transportType, activeNetwork, activeSubType, signalStrength," +
                            " cellularAvailability, isConnectedToCellular, cellularState, cellularSubType, wifiSignalStrength, wifiSpeed, wifiAvailability," + 
                            " isConnectedToWifi, wifiState, wifiSubType) VALUES ";

                        foreach (DbEntry entry in listCopy)
                        {
                            string entryString = String.Format("(\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\", " +
                                "\"{9}\", \"{10}\", \"{11}\", \"{12}\", \"{13}\", \"{14}\", \"{15}\", \"{16}\", \"{17}\", \"{18}\", \"{19}\", \"{20}\")",
                                entry.rtt.rtt.TotalSeconds, entry.rtt.appID, entry.deviceID, entry.rtt.sessionID, entry.rtt.receivedTime.ToString("yyyy:MM:dd HH:mm:ss"),
                                entry.rtt.sequenceNumber, entry.rtt.platform, entry.rtt.packetType, entry.rtt.activeNetwork, entry.rtt.activeSubtype, entry.rtt.signalStrength,
                                entry.rtt.cellularAvailable, entry.rtt.isConnectedToCellular, //entry.rtt.cellularIsFailover,
                                entry.rtt.cellularState, entry.rtt.cellularSubtype,
                                entry.rtt.wifiSignalStrength, entry.rtt.wifiSpeed, entry.rtt.wifiAvailability, entry.rtt.isConnectedtoWifi,// entry.rtt.wifiIsFailover,
                                entry.rtt.wifiState, entry.rtt.wifiSubType);
                            query += entryString;
                            if (entry != listCopy.Last.Value)
                            {
                                query += ",";
                            }
                        }

                        query += ";";

                        MySqlConnection conn = new MySqlConnection(connString);
                        MySqlCommand command = new MySqlCommand(query, conn);

                        // log.Info((query);

                        conn.Open();
                        command.ExecuteNonQuery();
                        conn.Close();
                        log.Info("Records written to database");
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Exception in push db entries: " + ex.StackTrace);
                }

                Thread.Sleep(TimeSpan.FromSeconds(MYSQL_SLEEP_INTERVAL));
            }
        }

        /// <summary>
        /// Receive a UDP ping packet from the client.
        /// </summary>
        private void AsyncBeginReceive()
        {
            UdpState s = new UdpState();
            s.e = new IPEndPoint(IPAddress.Any, SERVER_PORT);
            s.u = _udpClient;

            _udpClient.BeginReceive(new AsyncCallback(AsyncEndReceive), s);
        }

        int udpcounter = 0;

        /// <summary>
        /// Process the UDP ping measurement packet received from the client.
        /// </summary>
        /// <param name="ar"></param>
        private void AsyncEndReceive(IAsyncResult ar)
        {
            try
            {
                Interlocked.Increment(ref udpcounter);
                //log.Info("Inside AsyncEndReceive: " + udpcounter);

                UdpClient u = (UdpClient)((UdpState)(ar.AsyncState)).u;
                IPEndPoint e = (IPEndPoint)((UdpState)(ar.AsyncState)).e;

                Byte[] receiveBytes = u.EndReceive(ar, ref e);

                /*
                 * The packet is a valid ping measurement packet only if it has the magic number at the beginnig of the packet.
                 * Otherwise, the packet is a initialization packet used to establish a new connection with the server.
                 */
                if (receiveBytes[0] == SERVER_IDENTIFIER)
                {
                    // RTT packet received
                    //log.Info("Inside RTT: " + udpcounter);
                    byte sequenceNumber = receiveBytes[1];
                    string messageString = System.Text.ASCIIEncoding.ASCII.GetString(receiveBytes, 2, 500).Trim('\0');
                    DateTime receivedTime = DateTime.Now;

                    //log.Info("UDP Received: " + messageString);
                    Rtt tempRtt = processPacket(messageString);
                    string deviceId = tempRtt.deviceID;

                    // record Rtt and add to list
                    lock (_udpRecordsLock)
                    {

                        //log.Info("Inside _udpRecordsLock: " + udpcounter);
                        if (_udpRecords.ContainsKey(deviceId))
                        {
                            _udpRecords[deviceId][sequenceNumber].receivedTime = receivedTime;

                            _udpRecords[deviceId][sequenceNumber].appID = tempRtt.appID;

                            _udpRecords[deviceId][sequenceNumber].deviceID = tempRtt.deviceID;
                            _udpRecords[deviceId][sequenceNumber].sessionID = tempRtt.sessionID;

                            _udpRecords[deviceId][sequenceNumber].platform = tempRtt.platform;

                            _udpRecords[deviceId][sequenceNumber].wifiSignalStrength = tempRtt.wifiSignalStrength;
                            _udpRecords[deviceId][sequenceNumber].wifiSpeed = tempRtt.wifiSpeed;
                            _udpRecords[deviceId][sequenceNumber].wifiAvailability = tempRtt.wifiAvailability;
                            _udpRecords[deviceId][sequenceNumber].isConnectedtoWifi = tempRtt.isConnectedtoWifi;
                            //_udpRecords[deviceId][sequenceNumber].wifiIsFailover = tempRtt.wifiIsFailover;
                            _udpRecords[deviceId][sequenceNumber].wifiState = tempRtt.wifiState;
                            _udpRecords[deviceId][sequenceNumber].wifiSubType = tempRtt.wifiSubType;

                            _udpRecords[deviceId][sequenceNumber].signalStrength = tempRtt.signalStrength;
                            _udpRecords[deviceId][sequenceNumber].cellularAvailable = tempRtt.cellularAvailable;
                            _udpRecords[deviceId][sequenceNumber].isConnectedToCellular = tempRtt.isConnectedToCellular;
                            //_udpRecords[deviceId][sequenceNumber].cellularIsFailover = tempRtt.cellularIsFailover;
                            _udpRecords[deviceId][sequenceNumber].cellularState = tempRtt.cellularState;
                            _udpRecords[deviceId][sequenceNumber].cellularSubtype = tempRtt.cellularSubtype;

                            _udpRecords[deviceId][sequenceNumber].activeNetwork = tempRtt.activeNetwork;
                            _udpRecords[deviceId][sequenceNumber].activeSubtype = tempRtt.activeSubtype;

                            _udpRecords[deviceId][sequenceNumber].rtt = _udpRecords[deviceId][sequenceNumber].receivedTime - _udpRecords[deviceId][sequenceNumber].sentTime;

                            _mysqlListLock.EnterWriteLock();
                            //log.Info("Inside _mysqlListLock: " + udpcounter);
                            _dbEntryList.AddLast(new DbEntry(_udpRecords[deviceId][sequenceNumber], deviceId));
                            _mysqlListLock.ExitWriteLock();
                            //log.Info("Outside _mysqlListLock: " + udpcounter);

                            lock (_timeoutCounterLock)
                            {
                                //log.Info("Inside _timeoutCounterLock: " + udpcounter);
                                _timeoutCounter[deviceId] = 0;
                            }
                            //log.Info("Outside _timeoutCounterLock: " + udpcounter);
                        }
                    }
                    //log.Info("Outside _udpRecordsLock: " + udpcounter);
                    //log.Info("Outside RTT: " + udpcounter);
                }
                else // This is an initialization packet to setup a new UDP connection.
                {
                    //log.Info("Inside ping: " + udpcounter);
                    // adding a new connection
                    string messageString = System.Text.ASCIIEncoding.ASCII.GetString(receiveBytes, 1, 500).Trim('\0');
                    Rtt tempRtt = processPacket(messageString);
                    string deviceId = tempRtt.deviceID;

                    lock (_addListLock)
                    {
                        _addList.AddLast(new UDPConnection(e.Address, deviceId, e.Port));
                    }
                    //log.Info("Outside ping: " + udpcounter);
                }
            }
            catch (Exception ex)
            {
                log.Error("Exception in UDP Async End Receive ", ex);
            }
            finally
            {
                AsyncBeginReceive(); // Start another receive
            }
        }

        /// <summary>
        /// Periodically ping all the connected clients using a UDP packet.
        /// The periodicity of the per-client ping interval is controlled by the SERVER_PING_INTERVAL parameter (in seconds).
        /// </summary>
        private void UdpPinger()
        {
            while (true)
            {
                // Add new connections
                LinkedList<UDPConnection> addListCopy;
                lock (_addListLock)
                {
                    addListCopy = new LinkedList<UDPConnection>(_addList);
                    _addList.Clear();
                }

                foreach (UDPConnection connection in addListCopy)
                {
                    if (_udpConnections.ContainsKey(connection.deviceID))
                    {
                        _udpConnections[connection.deviceID].ipAddress = connection.ipAddress;
                        _udpConnections[connection.deviceID].portNumber = connection.portNumber;

                        lock (_timeoutCounterLock)
                        {
                            _timeoutCounter[connection.deviceID] = 0;
                        }
                    }
                    else
                    {
                        _udpConnections.Add(connection.deviceID, connection);
                        lock (_timeoutCounterLock)
                        {
                            _timeoutCounter.Add(connection.deviceID, 0);
                        }
                    }
                }


                TimeSpan sleepInterval = TimeSpan.FromSeconds(DEFAULT_UDP_PING_INTERVAL);
                if (_udpConnections.Count != 0)
                {
                    sleepInterval = TimeSpan.FromSeconds((double)SERVER_PING_INTERVAL / (double)_udpConnections.Count);
                }
                else
                {
                    Thread.Sleep(TimeSpan.FromSeconds(DEFAULT_UDP_PING_INTERVAL));
                    continue;
                }

                // Sleep for an interval of time (SERVER_PING_INTERVAL / _udpConnections.Count) and then ping the next UDP client.
                try
                {
                    foreach (UDPConnection connection in _udpConnections.Values)
                    {
                        lock (_timeoutCounterLock)
                        {
                            if (_timeoutCounter[connection.deviceID] > MAX_TIMEOUT_COUNT_VAL)
                            {
                                // Cannot remove while iterating, so we will skip and remove after
                                _removeList.AddLast(connection);
                                continue;
                            }
                        }

                        lock (_udpRecordsLock)
                        {
                            byte[] packet = new byte[PACKET_BUFFER_SIZE];
                            packet[0] = Server.SERVER_IDENTIFIER;
                            packet[1] = connection.counter;

                            _udpClient.Send(packet, PACKET_BUFFER_SIZE, new IPEndPoint(connection.ipAddress, connection.portNumber));
                            if (_udpRecords.ContainsKey(connection.deviceID))
                            {
                                if (_udpRecords[connection.deviceID].ContainsKey(connection.counter))
                                {
                                    _udpRecords[connection.deviceID][connection.counter] = new UDPRtt(connection.counter);
                                }
                                else
                                {
                                    _udpRecords[connection.deviceID].Add(connection.counter, new UDPRtt(connection.counter));
                                }
                            }
                            else
                            {
                                //log.Info("Adding to _udpRecord, key: " + connection.deviceID + "\n");
                                _udpRecords.Add(connection.deviceID, new Dictionary<int, UDPRtt>());
                                _udpRecords[connection.deviceID].Add(connection.counter, new UDPRtt(connection.counter));
                            }
                            connection.counter = (byte)((connection.counter + 1) % 256);
                            lock (_timeoutCounterLock)
                            {
                                _timeoutCounter[connection.deviceID] += 1;
                                //log.Info("Increasing timeout counter, key: " + connection.deviceID + " val: " + _timeoutCounter[connection.deviceID] + "\n" );
                            }
                        }

                        Thread.Sleep(sleepInterval);
                    }

                    // Remove all the inactive UDP clients.
                    foreach (UDPConnection connection in _removeList)
                    {
                        //log.Info("Removing: " + connection.deviceID);
                        _udpConnections.Remove(connection.deviceID);

                        lock (_udpRecordsLock)
                        {
                            //log.Info("Remove from _udpRecords, key: " + connection.deviceID + "\n");
                            _udpRecords.Remove(connection.deviceID);
                            lock (_timeoutCounterLock)
                            {
                                _timeoutCounter.Remove(connection.deviceID);
                            }
                        }
                    }

                    _removeList.Clear();
                }
                catch (Exception ex)
                {
                    log.Error("Error in Udp Pinger ", ex);
                }
            }
        }

        /// <summary>
        /// Start listening for TCP connections from the clients.
        /// </summary>
        private void ListenForClients()
        {
            this._tcpListener.Start();

            while (true)
            {
                //blocks until a client has connected to the server
                TcpClient client = this._tcpListener.AcceptTcpClient();

                //create a thread to handle communication 
                //with connected client
                Thread tcpThread = new Thread(new ParameterizedThreadStart(TCPConnHandler));
                tcpThread.Start(client);
                lock (_tcpThreads)
                {
                    _tcpThreads[client] = tcpThread;
                    //log.Info(_tcpThreads.Count + " TCP connections");
                }
            }
        }

        /// <summary>
        /// Handle the TCP ping measurement work with a single client.
        /// </summary>
        /// <param name="client"></param>
        private void TCPConnHandler(object client)
        {
            try
            {
                TcpClient tcpClient = (TcpClient)client;
                NetworkStream clientStream = tcpClient.GetStream();

                TCPConnection tcpConnection = new TCPConnection(
                    ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address,
                    null,
                    ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port,
                    tcpClient);

                byte[] intervalPacket = new byte[sizeof(int)];
                Buffer.BlockCopy(System.BitConverter.GetBytes(CLIENT_PING_INTERVAL), 0, intervalPacket, 0, sizeof(int));

                try
                {
                    clientStream.Write(intervalPacket, 0, sizeof(int));
                }
                catch
                {
                    tcpClient.Close();
                    return;
                }

                // While loop here sending data every x seconds
                TimerCallback timerCB = new TimerCallback(SendTCPPing);

                Timer t = new Timer(timerCB, tcpConnection, 0, SERVER_PING_INTERVAL * 1000);

                tcpConnection.timer = t;

                while (true)
                {
                    byte[] message = new byte[PACKET_BUFFER_SIZE];
                    int bytesRead = 0;

                    try
                    {
                        //blocks until a client sends a message
                        bytesRead = clientStream.Read(message, 0, PACKET_BUFFER_SIZE);
                    }
                    catch
                    {
                        //a socket error has occured
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        //the client has disconnected from the server
                        break;
                    }

                    // The ping packet received from client is valid, iff it begins the SERVER_IDENTIFIER magic number.
                    if (message[0] == SERVER_IDENTIFIER)
                    {
                        byte sequenceNumber = message[1];
                        string messageString = System.Text.ASCIIEncoding.ASCII.GetString(message, 2, 500).Trim('\0');
                        DateTime receivedTime = DateTime.Now;

                        //log.Info("TCP Received: " + messageString);
                        Rtt tempRtt = processPacket(messageString);

                        lock (tcpConnection)
                        {
                            tcpConnection.tcpRecords[sequenceNumber].receivedTime = receivedTime;

                            tcpConnection.tcpRecords[sequenceNumber].appID = tempRtt.appID;

                            tcpConnection.tcpRecords[sequenceNumber].deviceID = tempRtt.deviceID;
                            tcpConnection.tcpRecords[sequenceNumber].sessionID = tempRtt.sessionID;

                            tcpConnection.tcpRecords[sequenceNumber].platform = tempRtt.platform;

                            tcpConnection.tcpRecords[sequenceNumber].wifiSignalStrength = tempRtt.wifiSignalStrength;
                            tcpConnection.tcpRecords[sequenceNumber].wifiSpeed = tempRtt.wifiSpeed;
                            tcpConnection.tcpRecords[sequenceNumber].wifiAvailability = tempRtt.wifiAvailability;
                            tcpConnection.tcpRecords[sequenceNumber].isConnectedtoWifi = tempRtt.isConnectedtoWifi;
                            //tcpConnection.tcpRecords[sequenceNumber].wifiIsFailover = tempRtt.wifiIsFailover;
                            tcpConnection.tcpRecords[sequenceNumber].wifiState = tempRtt.wifiState;
                            tcpConnection.tcpRecords[sequenceNumber].wifiSubType = tempRtt.wifiSubType;

                            tcpConnection.tcpRecords[sequenceNumber].signalStrength = tempRtt.signalStrength;
                            tcpConnection.tcpRecords[sequenceNumber].cellularAvailable = tempRtt.cellularAvailable;
                            tcpConnection.tcpRecords[sequenceNumber].isConnectedToCellular = tempRtt.isConnectedToCellular;
                            //tcpConnection.tcpRecords[sequenceNumber].cellularIsFailover = tempRtt.cellularIsFailover;
                            tcpConnection.tcpRecords[sequenceNumber].cellularState = tempRtt.cellularState;
                            tcpConnection.tcpRecords[sequenceNumber].cellularSubtype = tempRtt.cellularSubtype;

                            tcpConnection.tcpRecords[sequenceNumber].activeNetwork = tempRtt.activeNetwork;
                            tcpConnection.tcpRecords[sequenceNumber].activeSubtype = tempRtt.activeSubtype;

                            tcpConnection.tcpRecords[sequenceNumber].rtt = tcpConnection.tcpRecords[sequenceNumber].receivedTime - tcpConnection.tcpRecords[sequenceNumber].sentTime;

                            _mysqlListLock.EnterWriteLock();
                            _dbEntryList.AddLast(new DbEntry(tcpConnection.tcpRecords[sequenceNumber], tempRtt.deviceID));
                            _mysqlListLock.ExitWriteLock();
                        }
                    }
                }

                // Close the TCP connection if the measurement loop is existed.
                t.Dispose();
                tcpClient.Close();
                lock (_tcpThreads)
                {
                    _tcpThreads[tcpClient] = null;
                    _tcpThreads.Remove(tcpClient);
                    //log.Info(_tcpThreads.Count + " TCP connections");
                }
            }
            catch (Exception ex)
            {
                log.Error("Error in TCP connection ", ex);
            }
        }

        /// <summary>
        /// Send a TCP ping packet to the client.
        /// </summary>
        /// <param name="state"></param>
        static void SendTCPPing(object state)
        {
            try
            {
                TCPConnection tcpConnection = state as TCPConnection;
                NetworkStream clientStream = tcpConnection.tcpClient.GetStream();

                byte[] packet = new byte[PACKET_BUFFER_SIZE];
                packet[0] = Server.SERVER_IDENTIFIER;
                packet[1] = tcpConnection.counter;

                try
                {
                    lock (tcpConnection)
                    {
                        clientStream.Write(packet, 0, PACKET_BUFFER_SIZE);
                        tcpConnection.tcpRecords[tcpConnection.counter] = new TCPRtt(tcpConnection.counter);
                        tcpConnection.counter = (byte)((tcpConnection.counter + 1) % 256);
                    }
                }
                catch
                {
                    lock (tcpConnection)
                    {
                        tcpConnection.tcpClient.Close();
                        tcpConnection.timer.Dispose();
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Error("Error in Send TCP Ping ", ex);

            }
        }

        /// <summary>
        /// Parse the measurement values within the ping response packets received from the clients. The values are contained in a JSON formatted string.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Rtt processPacket(string message)
        {
            // Using a dummy Rtt. Type doesn't matter.
            Rtt tempRtt = new Rtt(0, -1);

            JObject o = JObject.Parse(message);
            tempRtt.appID = (int)o["appID"];

            tempRtt.deviceID = (string) o["deviceID"];
            tempRtt.sessionID = (long) o["sessionID"];
            tempRtt.signalStrength = (int) o["cellSignal"];

            tempRtt.wifiSignalStrength = (int) o["wifiRssi"];
            tempRtt.wifiSpeed = (int) o["wifiSpeed"];

            tempRtt.platform = (int)o["platform"];

            tempRtt.wifiAvailability = (int) o["wifiAvail"];
            tempRtt.isConnectedtoWifi = (int) o["wifiCon"];
            tempRtt.wifiIsFailover = (int)o["wifiFO"];
            tempRtt.wifiState = (int) o["wifiState"];
            tempRtt.wifiSubType = (int) o["wifiSubtype"];
		    //temp.put("wifiInfo", wifiInformation);

            tempRtt.cellularAvailable = (int) o["cellAvail"];
            tempRtt.isConnectedToCellular = (int) o["cellConn"];
            tempRtt.cellularIsFailover = (int) o["cellFO"];
            tempRtt.cellularState = (int) o["cellState"];
            tempRtt.cellularSubtype = (int) o["cellSubtype"];
		    //temp.put("cellInfo", cellInformation);	

            tempRtt.activeNetwork = (int) o["activeType"];
            tempRtt.activeSubtype = (int)o["activeSub"];
		    //temp.put("activeInfo", activeInformation);

            //log.Info("Received: " + tempRtt);
            return tempRtt;
        }
    }
}
