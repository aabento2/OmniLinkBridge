﻿using HAI_Shared;
using OmniLinkBridge.Modules;
using OmniLinkBridge.OmniLink;
using OmniLinkBridge.WebAPI;
using log4net;
using Newtonsoft.Json;
using System;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading;

namespace OmniLinkBridge
{
    public class WebServiceModule : IModule
    {
        private static ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static OmniLinkII OmniLink { get; private set; }

        private WebServiceHost host;

        private readonly AutoResetEvent trigger = new AutoResetEvent(false);

        public WebServiceModule(OmniLinkII omni)
        {
            OmniLink = omni;
            OmniLink.OnAreaStatus += Omnilink_OnAreaStatus;
            OmniLink.OnZoneStatus += Omnilink_OnZoneStatus;
            OmniLink.OnUnitStatus += Omnilink_OnUnitStatus;
            OmniLink.OnThermostatStatus += Omnilink_OnThermostatStatus;
        }

        public void Startup()
        {
            WebNotification.RestoreSubscriptions();

            Uri uri = new Uri("http://0.0.0.0:" + Global.webapi_port + "/");
            host = new WebServiceHost(typeof(OmniLinkService), uri);

            try
            {
                ServiceEndpoint ep = host.AddServiceEndpoint(typeof(IOmniLinkService), new WebHttpBinding(), "");
                host.Open();

                log.Info("Listening on " + uri.ToString());
            }
            catch (CommunicationException ex)
            {
                log.Error("An exception occurred starting web service", ex);
                host.Abort();
            }

            // Wait until shutdown
            trigger.WaitOne();

            if (host != null)
                host.Close();

            WebNotification.SaveSubscriptions();
        }

        public void Shutdown()
        {
            trigger.Set();
        }

        private void Omnilink_OnAreaStatus(object sender, AreaStatusEventArgs e)
        {
            WebNotification.Send("area", JsonConvert.SerializeObject(e.Area.ToContract()));
        }

        private void Omnilink_OnZoneStatus(object sender, ZoneStatusEventArgs e)
        {
            if (e.Zone.IsTemperatureZone())
            {
                WebNotification.Send("temp", JsonConvert.SerializeObject(e.Zone.ToContract()));
                return;
            }

            WebNotification.Send(Enum.GetName(typeof(DeviceType), e.Zone.ToDeviceType()), JsonConvert.SerializeObject(e.Zone.ToContract()));
        }

        private void Omnilink_OnUnitStatus(object sender, UnitStatusEventArgs e)
        {
            WebNotification.Send("unit", JsonConvert.SerializeObject(e.Unit.ToContract()));
        }

        private void Omnilink_OnThermostatStatus(object sender, ThermostatStatusEventArgs e)
        {
            if(!e.EventTimer)
                WebNotification.Send("thermostat", JsonConvert.SerializeObject(e.Thermostat.ToContract()));
        }
    }
}
