InsightMainServer
=================

This code provides the backend for the Insight client library. The Insight client code (available at https://github.com/patroashish/InsightClient) connects to this server and relays various statistics (e.g., device information, resource consumption statistics, location, application events etc.).

The server code is written as a C# project and can be easily run on both Linux or Windows platforms. The server uses MySQL as the backend for storing the collected statistics.

1. Before building and running the server, you'll have to create the MySQL database and tables used by the server. Execute the following command using the "setup_mainserver_db.sql" file.

        mysql -u <mysql_username> -p<mysql_password> insight_stats < setup_mainserver_db.sql

2. Update the following variables in the SystemInfoServer/Constants.cs file corresponding to your MySQL server configuration.

        public const string MYSQL_SERVER = ""; // Update the server hostname based on your server setup.
        public const string MYSQL_USERNAME = ""; // Update mysql username based on your server setup.
        public const string MYSQL_PASSWORD = ""; // Update mysql password based on your server setup.

3. The Insight server compiled and run on a Windows or Linux machine using the follwoing instructions:-

    a) To run the Insight server on a Windows machine, build the project using Visual studio. The server can be run on the command line, using the following generated executable: ./SystemInfoConsole/bin/Debug/SystemInfoConsole.exe. The server can also be run as a Windows Server. The genrated executables will be available in SystemInfoService/bin/Debug/

    b) To run the Insight server on a Linux machine, you need to install mono. On an Ubuntu machine, this can be done using 'sudo apt-get install mono-complete'. Build the project using xbuild: xbuild SystemInfoServer.sln. Then, run the server using 'mono SystemInfoConsole/bin/Debug/SystemInfoConsole.exe'

