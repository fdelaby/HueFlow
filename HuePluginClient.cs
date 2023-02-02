using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Flow.Launcher.Plugin;

namespace HueFlow
{
    class HuePluginClient
    {
        private string pluginDirectory;
        private ILocalHueClient client;

        public HuePluginClient(string pluginDir = null)
        {
            pluginDirectory = pluginDir ?? Directory.GetCurrentDirectory();
        }

    }



}
