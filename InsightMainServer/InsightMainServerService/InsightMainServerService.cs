using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using SystemInfoServer;
using System.Threading;
using log4net;

namespace SystemInfoService
{
    public partial class InsightMainServerService : ServiceBase
    {
        Server _server = new Server();

        public InsightMainServerService()
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
