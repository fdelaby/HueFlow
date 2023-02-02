using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using HueFlow;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Groups;

namespace HueFlowLauncher
{
    public class Main : IAsyncPlugin, IPluginI18n, ISettingProvider
    {
        internal PluginInitContext Context;
        //private string pluginDirectory;
        private ILocalHueClient client;

        internal static PluginInitContext _context { get; private set; }

        private static Settings _settings;
        private const string GroupIcon = @"\Images\2x\baseline_lightbulb_circle_black_48dp.png";
        private const string BulbIcon = @"\Images\2x\baseline_emoji_objects_black_48dp.png";

        //private readonly Dictionary<string, Func<string, List<Result>>> _terms = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<string, Func<string, Task<List<Result>>>> _expensiveTerms = new(StringComparer.InvariantCultureIgnoreCase);

        #region title and description
        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("plugin_hueflow_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("plugin_hueflow_plugin_description");
        }
        #endregion

        public async System.Threading.Tasks.Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            List<Result> results;

            try
            {
                if (_expensiveTerms.ContainsKey(query.FirstSearch))
                {
                    results = await _expensiveTerms[query.FirstSearch].Invoke(query.SecondToEndSearch);
                    return results;
                }

                return await SearchAllAsync(query.Search);

            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                return SingleResult(
                    "There was an error with your request",
                    e.GetBaseException().Message
                );
            }

        }

        private async Task<List<Result>> SearchAllAsync(string param)
        {

           return SingleResult("hue {command} {light|group}", "command : commands get all usable commands");
        }

        public System.Threading.Tasks.Task InitAsync(PluginInitContext context)
        {
            _context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();

           // pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            Task.Run(() => ConnectBridge());

            _expensiveTerms.Add("lights", GetLights);
            _expensiveTerms.Add("groups", GetGroups);
            //_expensiveTerms.Add("plop", plop);
            _expensiveTerms.Add("on", TOn);
            _expensiveTerms.Add("off", TOff);

            _expensiveTerms.Add("command", GetCommands);


            return Task.CompletedTask;
        }

        #region commands
        

        /// <summary>
        /// turn off light with the id in param
        /// </summary>
        /// <param name="lightId">light id to turn off</param>
        /// <returns></returns>
        private async Task TurnOff(string lightId)
        {
            var command = new LightCommand();
            command.On = true;
            command.TurnOff();
            await client.SendCommandAsync(command, new List<string> { lightId });
        }

        /// <summary>
        /// turn off light or groups by name
        /// </summary>
        /// <param name="name">group or light name</param>
        /// <returns>result to display</returns>
        private async Task<List<Result>> TOff(string name)
        {
            List<Result> returnResult = new List<Result>();

            string id = await getIdLightOrGroup(name);

            if (id.Equals("Z"))
            {
                returnResult.Add(new Result() { Title = "Problem", SubTitle = "no connection to Hue bridge" });
                return returnResult;
            }

            if (id.Equals("N"))
            {
                returnResult.Add(new Result() { Title = "Problem", SubTitle = "no light or group with this name" });
                return returnResult;
            }

            var command = new LightCommand();
            command.On = true;
            command.TurnOff();
            await client.SendCommandAsync(command, new List<string> { id });
            returnResult.Add(new Result() { Title = "Ok", SubTitle = "light off" });
            return returnResult;
        }

        /// <summary>
        /// turn on light or groups by name
        /// </summary>
        /// <param name="name">group or light name</param>
        /// <returns>result to display</returns>
        private async Task<List<Result>> TOn(string name)
        {
            List<Result> returnResult = new List<Result>();

            string id = await getIdLightOrGroup(name);

            if (id.Equals("Z"))
            {
                returnResult.Add(new Result() { Title = "Problem", SubTitle = "no connection to Hue bridge"});
                return returnResult;
            }

            if (id.Equals("N"))
            {
                returnResult.Add(new Result() { Title = "Problem", SubTitle = "no light or group with this name" });
                return returnResult;
            }

            var command = new LightCommand();
            command.On = true;
            command.TurnOn();
            await client.SendCommandAsync(command, new List<string> { id });
            returnResult.Add(new Result() { Title = "Ok", SubTitle = "light on" });
            return returnResult;
        }

        /// <summary>
        /// return the list of lights own by the bridge
        /// </summary>
        /// <param name="param"></param>
        /// <returns>list of lights, with action to turn off</returns>
        private async Task<List<Result>> GetLights(string param)
        {
            var connected = await client.CheckConnection();
            if (!connected) return ConnectionProblemResult;


            //Get first page of results
            var lightList = await client.GetLightsAsync();
            var results = lightList.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = x.Type,
                IcoPath = BulbIcon,
                Action = _ =>
                {
                    TurnOff(x.Id);
                    return true;
                }
            }) ;

            await Task.WhenAll(results);
            return results.Any() ? results.Select(x => x.Result).ToList() : NoLightFoundResult;
        }

        /// <summary>
        /// action display all supported commands and use hints
        /// </summary>
        /// <param name="param"></param>
        /// <returns>list of all supported commands</returns>
        private async Task<List<Result>> GetCommands(string param)
        {
            List<Result> returnResult = new List<Result>();
            returnResult.Add(new Result() { Title = "groups", SubTitle = "return all groups lights", Action = _ =>{ GetGroups("help");return true;}});
            returnResult.Add(new Result() { Title = "lights", SubTitle = "return all lights", Action = _ => { GetLights("help"); return true;}});
            returnResult.Add(new Result() { Title = "on {light|group}", SubTitle = "turn on the indicated light or group", Action = _ => { TOn("help"); return true; }});
            returnResult.Add(new Result() { Title = "off {light|group}", SubTitle = "turn off the indicated light or group", Action = _ => { TOff("help"); return true; }});
            return returnResult;
        }

        /// <summary>
        /// return groups own by the bridge
        /// </summary>
        /// <param name="param"></param>
        /// <returns>list of groups own by the bridge</returns>
        private async Task<List<Result>> GetGroups(string param)
        {
            var connected = await client.CheckConnection();
            if (!connected) return ConnectionProblemResult;


            //Get first page of results
            var groupList = await client.GetGroupsAsync();
            var results = groupList.Select(async x => new Result()
            {
                Title = x.Name,
                IcoPath =  GroupIcon,

                Action = _ =>
                {
                    TurnOff(x.Id);
                    return true;
                }
            }) ;

            await Task.WhenAll(results);
            return results.Any() ? results.Select(x => x.Result).ToList() : NoLightFoundResult;
        }

        #endregion

   //     //TODO à revoir
        private List<Result> NoLightFoundResult =>
           SingleResult("No lights found", "No light assign to the bridge", () => { });

        private List<Result> ConnectionProblemResult =>
           SingleResult("Hue bridge not connected", "Click this to try another connection", ReconnectAction());


        private static List<Result> SingleResult(string title, string subtitle = "", Action action = default, bool hideAfterAction = true) =>
    new()
    {
        new Result()
        {
            Title = title,
            SubTitle = subtitle,
            Action = _ =>
            {
                action?.Invoke();
                return hideAfterAction;
            }
        }
    };

        /// <summary>
        /// try to reconnect to the bridge
        /// </summary>
        /// <returns></returns>
        private Action ReconnectAction()
        {
            return async () =>
            {
                try
                {
                    await ConnectBridge();
                }
                catch
                {
                    Console.WriteLine("Failed to connect");
                }
            };
        }

        /// <summary>
        /// connect to the bridge
        /// </summary>
        /// <returns></returns>
        public async Task ConnectBridge()
        {
            Console.WriteLine("try to connect");

            client = new LocalHueClient(_settings.BridgeIpAdress);

            if (!string.IsNullOrEmpty(_settings.apikey))
            {
                client.Initialize(_settings.apikey);
            }            
        }

        /// <summary>
        /// return id of a light or group by his name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>id || Z if connection fail || N if not found</returns>
        private async Task<string> getIdLightOrGroup(string name)
        {

            var connected = await client.CheckConnection();
            if (!connected) return "Z";


            //search in group
            var groupList = await client.GetGroupsAsync();
            var group = groupList.Where(group => group.Name == name).FirstOrDefault();
            if (group != null) return group.Id;


            //search in lights
            var lightList = await client.GetLightsAsync();
            var light = lightList.Where(light => light.Name == name).FirstOrDefault();
            if (light != null) return light.Id;

            return "N";

        }

        public System.Windows.Controls.Control CreateSettingPanel() => new SettingsView(_context, _settings);
    }
}
