using Flow.Launcher.Plugin;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace HueFlow
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        private readonly PluginInitContext _context;
        private readonly Settings _settings;
        private bool apikeyOk;

        public SettingsView(PluginInitContext context, Settings settings)
        {
            _context = context;
            DataContext = _settings = settings;

            if (settings != null)
            {
                if (settings.BridgeIpAdress != null)
                {
                    //ipbridgeadress.IpAddress = settings.BridgeIpAdress;
                }
                apikeyOk = !string.IsNullOrEmpty(settings.apikey);
            }

            InitializeComponent();
        }

        public void Save(object sender = null, RoutedEventArgs e = null)
        {
            if (!apikeyOk)
            {
                _settings.apikey = "not set";
            }
            _context.API.SaveSettingJsonStorage<Settings>();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            apikeyOk = false;
            tokenField.Text = "10 sec to press the button";
            
            try
            {
                ConnectBridge(_settings.BridgeIpAdress);
                Thread.Sleep(10000);
                apikeyOk = !string.IsNullOrEmpty(_settings.apikey);
                tokenField.Text = _settings.apikey;
            }
            catch (LinkButtonNotPressedException)
            {
                tokenField.Text = "you didn't press the button or the bridge is unreachable";
            }
            
        }


        private async Task ConnectBridge(string ipadress)
        {
            ILocalHueClient client = new LocalHueClient(ipadress);
           _settings.apikey = await client.RegisterAsync("HueFlow", "HueFlow");

         }  
    }
}