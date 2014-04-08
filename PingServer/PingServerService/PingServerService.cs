using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using PingServer;
using System.Threading;
using log4net;

namespace PingServerService
{
    public partial class PingServerService : ServiceBase
    {
        Server _server = new Server();

        public PingServerService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // setup logging
            //log4net.Config.XmlConfigurator.Configure();
            //ILog log = LogManager.GetLogger(typeof(Program));
            //log.Info("Ping Server SERVICE started.  Logging started.  Starting the Ping Server...\n");

            _server.StartThreads();
        }

        protected override void OnStop()
        {
            _server.ShutDown();
        }
    }
}
