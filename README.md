InsightServers
=================

This code provides the server backend for the Insight client library. The Insight client code (available at https://github.com/patroashish/InsightClient) connects to the server and relays various statistics (e.g., device information, resource consumption statistics, location, application events etc.). The Insight server also performs lightweight network latency measurements over UDP and TCP to measure network latency between the server and the application (over cellular/WiFi).

The Insight server is split two components (the InsightMainServer and PingServer projects):-

1. InsightMainServer: The main Insight server to collect the aforementioned statistics from the clients.

2. PingServer: The ping measurement server performs lightweight network latency measurements over UDP and TCP to measure network latency between the server and the application (over cellular/WiFi).

The server code is written as C# projects and can be easily run on both Linux or Windows platforms. The server uses MySQL as the backend for storing the collected statistics.

1. Before building and running the server, you'll have to create the MySQL database and tables used by the Insight server. Execute the following command using the 'setup_insightserver_db.sql' file.

        mysql -u <mysql_username> -p<mysql_password> insight_stats < setup_insightserver_db.sql

2. Update the following variables in the 'InsightMainServer/InsightMainServer/Constants.cs' and 'PingServer/PingServerApplication/Constants.cs' files corresponding to your MySQL server configuration.

        public const string MYSQL_SERVER = ""; // Update the server hostname based on your server setup.
        public const string MYSQL_USERNAME = ""; // Update mysql username based on your server setup.
        public const string MYSQL_PASSWORD = ""; // Update mysql password based on your server setup.

3. The Insight server compiled and run on a Windows or Linux machine using the follwoing instructions:-

    a) To run the Insight server on a Windows machine, build both projects using Visual studio. The servers can executed on the command line or as a Windows service.
    
        i) To run the servers using the command line, run the following generated executables on two separate terminals: ./InsightMainServerConsole/bin/Debug/InsightMainServerConsole.exe and ./PingServerApplication/bin/Debug/PingServerApplication.exe.
        
        ii) The servers can also be run as a Windows Service. After building, the genrated executables will be available in 'InsightMainServerService/bin/Debug/' and 'PingServerService/bin/Debug/'.

    b) To run the Insight servers on a Linux machine, you need to install mono. On an Ubuntu machine, this can be done using 'sudo apt-get install mono-complete'. Build the projects using xbuild: 'xbuild InsightMainServer.sln' and 'xbuild PingServerApplication.sln'. Then, run the servers on two separate terminals using 'mono InsightMainServerConsole/bin/Debug/InsightMainServerConsole.exe' and 'mono PingServerApplication/bin/Debug/PingServerApplication.exe'.

