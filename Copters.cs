using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Copters", "mnlnk", "0.2.0"), Description("Manages the spawn of copters.")]

    public class Copters: RustPlugin
    {
        #region Config

        private PluginConfig _config;

        private class PluginConfig
        {
            public  bool InstantStartupEngine;        // Разрешить быстрый запуск двигателя коптера
            public  bool CanSpawnWhenBuildingBlocked; // Разрешить спавн коптрера в зоне действия чужого шкафа
            public  bool HideFireBall;                // Cкрывать эффект горения огненных шаров после того как коптер уничтожен
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig()
            {
                InstantStartupEngine = true,
                CanSpawnWhenBuildingBlocked = false,
                HideFireBall = true
            };
        }

        private PluginConfig LoadConfiguration() => Config.ReadObject<PluginConfig>();

        private void SaveConfiguration(PluginConfig config) => Config.WriteObject(config, true);

        protected override void LoadDefaultConfig() => SaveConfiguration(GetDefaultConfig());

        #endregion

        #region Permission

        private const string SpawnMini = "copters.spawnmini";      // Разрешение на спавн миникоптеров
        private const string SpawnScrapHeli = "copters.spawnheli"; // Разрешение на спавн транспортных коптеров

        private bool HasPermission(string userId, string perm) => permission.UserHasPermission(userId, perm);

        #endregion

        #region Messages

        private const string CommandColor = "#9ACD32";
        private const string ImportantColor = "#FCCD32";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoCommandPermission", "You <color=" + ImportantColor + ">do not have permission</color> to execute the command <color=" + CommandColor + ">{0}</color>" },
                { "BuildingBlockedMini", "You can't spawn a <color=" + ImportantColor + ">minicopter</color> while in range of someone else's cupboard" },
                { "BuildingBlockedScrapHeli", "You can't spawn a <color=" + ImportantColor + ">transport helicopter</color> while in range of someone else's cupboard" },
                { "AlreadyExistsMini", "You already have a <color=" + ImportantColor + ">minicopter</color>, use the <color=" + CommandColor + ">/nomini</color> command to destroy it" },
                { "AlreadyExistsScrapHeli", "You already have a <color=" + ImportantColor + ">transport helicopter</color>, use the <color=" + CommandColor + ">/noheli</color> command to destroy it" },
                { "DoNotHaveMini", "You do not have a <color=" + ImportantColor + ">minicopter</color>, use the <color=" + CommandColor + ">/mini</color> command to create one" },
                { "DoNotHaveScrapHeli", "You don't have a <color=" + ImportantColor + ">transport helicopter</color>, use the <color=" + CommandColor + ">/heli</color> command to create one" },
                { "CanNotRemoveMiniSelf", "You can't destroy <color=" + ImportantColor + ">minicopter</color> while you are in mounted of it" },
                { "CanNotRemoveMini", "You can't destroy <color=" + ImportantColor + ">minicopter</color> while it's being occupied by another player" },
                { "CanNotRemoveScrapHeliSelf", "You can't destroy <color=" + ImportantColor + ">transport helicopter</color> while you are in mounted of it" },
                { "CanNotRemoveScrapHeli", "You can't destroy <color=" + ImportantColor + ">transport helicopter</color> while it is occupied by another player" },
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoCommandPermission", "У вас <color=" + ImportantColor + ">нет разрешения</color> на выполнение команды <color=" + CommandColor + ">{0}</color>" },
                { "BuildingBlockedMini", "Вы не можете создать <color=" + ImportantColor + ">миникоптер</color>, находясь в зоне действия чужого шкафа" },
                { "BuildingBlockedScrapHeli", "Вы не можете создать <color=" + ImportantColor + ">транспортный вертолет</color>, находясь в зоне действия чужого шкафа" },
                { "AlreadyExistsMini", "У вас уже есть <color=" + ImportantColor + ">миникоптер</color>, используйте команду <color=" + CommandColor + ">/nomini</color> для его уничтожения" },
                { "AlreadyExistsScrapHeli", "У вас уже есть <color=" + ImportantColor + ">транспортный вертолет</color>, используйте команду <color=" + CommandColor + ">/noheli</color> для его уничтожения" },
                { "DoNotHaveMini", "У вас нет <color=" + ImportantColor + ">миникоптера</color>, используйте команду <color=" + CommandColor + ">/mini</color> для его создания" },
                { "DoNotHaveScrapHeli", "У вас нет <color=" + ImportantColor + ">транспортного вертолета</color>, используйте команду <color=" + CommandColor + ">/heli</color> для его создания" },
                { "CanNotRemoveMiniSelf", "Вы не можете уничтожить <color=" + ImportantColor + ">миникоптер</color>, пока вы на нем" },
                { "CanNotRemoveMini", "Вы не можете уничтожить <color=" + ImportantColor + ">миникоптер</color>, пока он занят другим игроком" },
                { "CanNotRemoveScrapHeliSelf", "Вы не можете уничтожить <color=" + ImportantColor + ">транспортный вертолет</color>, пока вы на нем" },
                { "CanNotRemoveScrapHeli", "Вы не можете уничтожить <color=" + ImportantColor + ">транспортный вертолет</color>, пока он занят другим игроком" },
            }, this, "ru");
        }

        private string Lang(string key, string id) => lang.GetMessage(key, this, id);

        private void Message(BasePlayer player, string text, params object[] args) => player.ChatMessage(string.Format(text, args));

        #endregion

        #region Data

        private StoredData _data;

        private class StoredData
        {
            public readonly Dictionary<ulong, CopterData> Players = new Dictionary<ulong, CopterData>();
        }

        private class CopterData
        {
            public bool IsHeli;
            public NetworkableId Id;

            public CopterData(NetworkableId id, bool isHeli)
            {
                Id = id;
                IsHeli = isHeli;
            }
        }

        private StoredData LoadData() => Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

        private void SaveData(StoredData data) => Interface.Oxide.DataFileSystem.WriteObject(Name, data);

        #endregion

        #region Load

        private bool _inited = false;

        private void Init()
        {
            AddCovalenceCommand("mini", "SpawnMiniCopterCommand");
            AddCovalenceCommand("nomini", "KillMiniCopterCommand");
            AddCovalenceCommand("heli", "SpawnScrapHeliCopterCommand");
            AddCovalenceCommand("noheli", "KillScrapHeliCopterCommand");

            permission.RegisterPermission(SpawnMini, this);
            permission.RegisterPermission(SpawnScrapHeli, this);
        }

        private void OnServerInitialized(bool initial)
        {
            _data = LoadData();
            _config = LoadConfiguration();

            foreach (MiniCopter mini in UnityEngine.Object.FindObjectsOfType<MiniCopter>()) {
                if (mini.IsValid()) ModifyCopter(mini);
            }

            _inited = true;
        }

        #endregion

        #region Unload

        private void Unload()
        {
            SaveData(_data);
            SaveConfiguration(_config);

            foreach (MiniCopter mini in UnityEngine.Object.FindObjectsOfType<MiniCopter>()) {
                if (mini.IsValid()) ResetCopter(mini);
            }
        }

        #endregion

        #region Commands

        [Command("mini")]
        private void SpawnMiniCopterCommand(IPlayer iplayer, string command, string[] args)
        {
            SpawnCopter((BasePlayer) iplayer.Object);
        }

        [Command("nomini")]
        private void KillMiniCopterCommand(IPlayer iplayer, string command, string[] args)
        {
            KillCopter((BasePlayer) iplayer.Object);
        }

        [Command("heli")]
        private void SpawnScrapHeliCopterCommand(IPlayer iplayer, string command, string[] args)
        {
            SpawnCopter((BasePlayer) iplayer.Object, true);
        }

        [Command("noheli")]
        private void KillScrapHeliCopterCommand(IPlayer iplayer, string command, string[] args)
        {
            KillCopter((BasePlayer) iplayer.Object, true);
        }

        #endregion

        #region Core

        private float _defaultFuelPerSec = 0.25f;

        private void SpawnCopter(BasePlayer player, bool isHeli = false)
        {
            if (!HasPermission(player.UserIDString, isHeli ? SpawnScrapHeli : SpawnMini)) {
                Message(player, Lang("NoCommandPermission", player.UserIDString), (isHeli ? "/heli" : "/mini"));
                return;
            }

            if (player.IsBuildingBlocked() && !_config.CanSpawnWhenBuildingBlocked) {
                Message(player, Lang("BuildingBlocked", player.UserIDString));
                return;
            }

            if (_data.Players.ContainsKey(player.userID)) {
                CopterData copterData;
                _data.Players.TryGetValue(player.userID, out copterData);

                if (copterData != null) {
                    if (BaseNetworkable.serverEntities.Contains(copterData.Id)) {
                        Message(player, Lang(copterData.IsHeli ? "AlreadyExistsScrapHeli" : "AlreadyExistsMini", player.UserIDString));
                        return;
                    }
                    _data.Players.Remove(player.userID);
                }
            }

            System.Random rand = new System.Random();

            Vector3 forward = player.eyes.HeadForward();
            Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;
            Vector3 playerPosition = player.transform.position;
            Vector3 position = playerPosition + (straight * ((isHeli ? 4.5f : 3.0f) + (float) rand.NextDouble()));
            Quaternion look = Quaternion.LookRotation(forward) * Quaternion.AngleAxis(-130f, Vector3.up);
            Quaternion rotation = new Quaternion(0f, look.y, 0f, look.w);

            position.y = playerPosition.y + (1.0f + (float) rand.NextDouble());

            string prefab = isHeli
                ? "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab"
                : "assets/content/vehicles/minicopter/minicopter.entity.prefab";

            MiniCopter copter = (MiniCopter) GameManager.server.CreateEntity(prefab, position, rotation);

            copter.OwnerID = player.userID;
            copter.Spawn();

            if (isHeli) {
                copter.serverGibs.guid = null;
            }

            _data.Players.Add(player.userID, new CopterData(copter.net.ID, isHeli));
            SaveData(_data);
        }

        private void KillCopter(BasePlayer player, bool isHeli = false)
        {
            if (!HasPermission(player.UserIDString, isHeli ? SpawnScrapHeli : SpawnMini)) {
                Message(player, Lang("NoCommandPermission", player.UserIDString), (isHeli ? "/noheli" : "/nomini"));
                return;
            }

            CopterData copterData;
            _data.Players.TryGetValue(player.userID, out copterData);

            if (copterData == null) {
                Message(player, Lang(isHeli ? "DoNotHaveScrapHeli" : "DoNotHaveMini", player.UserIDString));
                return;
            }

            if (copterData.IsHeli) {
                if (!isHeli) {
                    Message(player, Lang("DoNotHaveMini", player.UserIDString));
                    return;
                }
            } else {
                if (isHeli) {
                    Message(player, Lang("DoNotHaveScrapHeli", player.UserIDString));
                    return;
                }
            }

            MiniCopter copter = ((MiniCopter) BaseNetworkable.serverEntities.Find(copterData.Id));
            if (copter == null) return;

            BasePlayer driver = copter.GetDriver();

            if (driver != null) {
                Message(player, Lang(
                    driver.userID == player.userID
                        ? isHeli ? "CanNotRemoveScrapHeliSelf" : "CanNotRemoveMiniSelf"
                        : isHeli ? "CanNotRemoveScrapHeli" : "CanNotRemoveMini"
                    , player.UserIDString));
                return;
            }

            copter.Kill();

            _data.Players.Remove(player.userID);
            SaveData(_data);
        }

        private void ModifyCopter(MiniCopter mini)
        {
            _defaultFuelPerSec = mini.fuelPerSec;
            mini.fuelPerSec = 0f;

            if (_config.HideFireBall) {
                mini.fireBall.guid = null;
            }

            StorageContainer fuelTank = mini.GetFuelSystem()?.fuelStorageInstance.Get(true);
            if (fuelTank == null) return;

            Item fuel = fuelTank.inventory.GetSlot(0);
            if (fuel == null) {
                fuel = ItemManager.Create(fuelTank.inventory.onlyAllowedItems.FirstOrDefault());
                if (fuel == null) return;

                fuel.MoveToContainer(fuelTank.inventory);
            }

            fuel.amount = 500;
            fuel.SetFlag(global::Item.Flag.IsLocked, true);

            fuelTank.dropsLoot = false;
            fuelTank.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
            fuelTank.inventory.MarkDirty();
            //fuelTank.SetFlag(BaseEntity.Flags.Locked, true);
        }

        private void ResetCopter(MiniCopter mini)
        {
            mini.fuelPerSec = _defaultFuelPerSec;

            StorageContainer fuelTank = mini.GetFuelSystem()?.fuelStorageInstance.Get(true);
            if (fuelTank == null) return;

            Item fuel = fuelTank.inventory.GetSlot(0);
            if (fuel == null) return;

            fuel.DoRemove();

            fuelTank.dropsLoot = true;
            fuelTank.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            //fuelTank.SetFlag(BaseEntity.Flags.Locked, false);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!(entity as MiniCopter)) return;

            NextTick(() => {
                if (_inited && entity.IsValid()) ModifyCopter((MiniCopter) entity);
            });
        }

        private object OnEntityKill(BaseNetworkable entity)
        {
            /*if (entity is ScrapTransportHelicopter) {
                ScrapTransportHelicopter heli = (ScrapTransportHelicopter) entity;
                if (heli.OwnerID.ToString().Equals("0")) return null;

                timer.Once(5f, () => {
                    //foreach (ServerGib gib in UnityEngine.Object.FindObjectsOfType<ServerGib>()) {
                    //    //gib.RemoveMe();
                    //}
                });

                _data.Players.Remove(heli.OwnerID);
                SaveData(_data);
            }
            else*/
            if (entity is MiniCopter) {
                MiniCopter mini = (MiniCopter) entity;
                if (mini.OwnerID.ToString().Equals("0")) return null;

                _data.Players.Remove(mini.OwnerID);
                SaveData(_data);
            }

            return null;
        }

        private void OnEngineStarted(BaseVehicle vehicle, BasePlayer driver)
        {
            if (!_config.InstantStartupEngine) return;
            if (vehicle is MiniCopter) ((MiniCopter) vehicle).engineController.FinishStartingEngine();
        }

        #endregion
    }
}

