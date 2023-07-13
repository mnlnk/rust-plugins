namespace Oxide.Plugins
{
    [Info("StopTime", "mnlnk", "0.1.0"), Description("Stops time on the server")]

    public class StopTime: RustPlugin
    {
        #region Config

        private PluginConfig _config;

        private class PluginConfig
        {
            public string Time;
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig()
            {
                Time = "16"
            };
        }

        private PluginConfig LoadConfiguration() => Config.ReadObject<PluginConfig>();

        private void SaveConfiguration(PluginConfig config) => Config.WriteObject(config, true);

        protected override void LoadDefaultConfig() => SaveConfiguration(GetDefaultConfig());

        #endregion

        #region Load

        private TOD_Time _time;

        private void OnServerInitialized(bool initial)
        {
            _config = LoadConfiguration();

            _time = UnityEngine.Object.FindObjectOfType<TOD_Time>();
            _time.ProgressTime = false;

            Server.Command("env.time " + _config.Time);
        }

        #endregion

        #region Unload

        private void Unload()
        {
            _time.ProgressTime = true;
        }

        #endregion
    }
}
