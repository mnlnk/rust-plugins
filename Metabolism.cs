namespace Oxide.Plugins
{
    [Info("Metabolism", "mnlnk", "0.1.0"), Description("Sets the metabolic and health indicators to the maximum")]

    public class Metabolism: RustPlugin
    {
        #region Core

        private void OnPlayerRespawned(BasePlayer player)
        {
            player.health = 100f;

            player.metabolism.hydration.value = 250f;
            player.metabolism.calories.value = 500f;

            player.SendNetworkUpdate();
        }

        #endregion
    }
}
