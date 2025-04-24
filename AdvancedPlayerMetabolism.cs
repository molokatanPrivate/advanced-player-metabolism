using Oxide.Plugins;
using Oxide.Core.Plugins;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using UnityEngine;

/**
 * Note:
 * It is recommended to use a specific movement speed permission to not collide with other plugins.
 * This plugin assigns and removes the permission as it likes.
 * 
 * TODO:
 * - Add a simple UI, if SimpleStatus should not be used
 * 
 * FIXME:
 * - when realoding the plugin, it can happen that PlayerStamina behavior is not reapplied (has to reload twice)
 * - when assigning permissions to groups, behavior is not set up for players -> permission check in behavior might be better instead
 * 
 * Optional:
 * - check if jump height can be increased with boost
 **/

namespace Oxide.Plugins
{
    [Info("AdvancedPlayerMetabolism", "molokatan", "1.0.0"), Description("Stamina bar for players with movement speed boost.")]
    public class AdvancedPlayerMetabolism : RustPlugin
    {
        [PluginReference]
        private Plugin SimpleStatus, InjuriesAndDiseases;

        public static AdvancedPlayerMetabolism PLUGIN;

        public static string perm_prefix = "advancedplayermetabolism.";
        public static string perm_use = perm_prefix + "use";

        #region plugin loading
        private void OnServerInitialized()
        {
            PLUGIN = this;

            InitSimpleStatus();

            if (InjuriesAndDiseases == null || !InjuriesAndDiseases.IsLoaded)
            {
                Puts($"Support for broken legs disabled.");
            }

            RegisterPermissions();
            
            NextTick(() =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                    CreatePlayerStamina(player);
            });
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyPlayerStamina(player);
        }

        private void InitSimpleStatus()
        {
            if (SimpleStatus == null || !SimpleStatus.IsLoaded)
            {
                Puts($"You must have SimpleStatus installed to run {Name}.");
                return;
            }
            else
            {
                SimpleStatus?.CallHook("CreateStatus", PLUGIN, "Player_Stamina", new Dictionary<string, object>
                {
                    ["color"] = "0.969 0.922 0.882 0.055",
                    ["title"] = "0%",
                    ["titleColor"] = "1 1 1 0.8",
                    ["icon"] = "assets/icons/electric.png",
                    ["iconColor"] = "1 1 1 0.8",
                    ["progress"] = 0f,
                    ["progressColor"] = "0.5 0.3 0.6 0.9",
                    ["rank"] = -1
                });
            }
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(perm_use, this);

            foreach (var permMaxStamina in config.permissions.max_stamina_perms)
                permission.RegisterPermission(perm_prefix + permMaxStamina.Key, this);

            foreach (var permMaxBoost in config.permissions.max_boost_perms)
                permission.RegisterPermission(perm_prefix + permMaxBoost.Key, this);

            foreach (var permStaminaReplenish in config.permissions.stamina_replenish_perms)
                permission.RegisterPermission(perm_prefix + permStaminaReplenish.Key, this);

            foreach (var permBoostReplenish in config.permissions.boost_replenish_perms)
                permission.RegisterPermission(perm_prefix + permBoostReplenish.Key, this);
        }
        #endregion

        #region Player Connections
        
        private void OnPlayerConnected(BasePlayer player) => CreatePlayerStamina(player);

        private void OnPlayerDisconnected(BasePlayer player, string reason) => DestroyPlayerStamina(player);

        #endregion

        private void DestroyPlayerStamina(BasePlayer player)
        {
            var result = player.GetComponent<PlayerStamina>();
            if (result == null) return;

            GameObject.Destroy(result);

            PLUGIN.permission.RevokeUserPermission(player.UserIDString, config.boost_settings.sprint_boost_perm);
            PLUGIN.permission.RevokeUserPermission(player.UserIDString, config.boost_settings.swim_boost_perm);
        }

        private void CreatePlayerStamina(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm_use)) return;

            var result = player.GetComponent<PlayerStamina>();
            if (result == null)
                player.gameObject.AddComponent<PlayerStamina>();
        }

        #region Behavior

        public class PlayerStamina : MonoBehaviour
        {
            private BasePlayer player;

            // if active, the current status is shown
            private bool statusActive = false;
            private float lastCheck = Time.realtimeSinceStartup;
            private float lastPlayerValueUpdate = Time.realtimeSinceStartup;

            // stamina
            private float lastStaminaUsed;

            private float currentStamina;
            private float currentMaxStamina;
            private float maxStamina;
            private float staminaReplenishMulti;

            // boost
            private float lastBoostUsed;

            private float currentBoost = 0f;
            private float currentMaxBoost;
            private float maxBoost;
            private float boostReplenishMulti;

            // broken leg
            private float lastBrokenLegCheck;
            private bool hasBrokenLeg = false;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();

                InitPlayerValues();

                currentStamina = maxStamina;
                currentMaxStamina = maxStamina;

                lastCheck = Time.realtimeSinceStartup;
                lastStaminaUsed = Time.realtimeSinceStartup;
                lastBoostUsed = Time.realtimeSinceStartup;
            }

            public void FixedUpdate()
            {
                if (player == null || !player.IsConnected)
                {
                    Destroy(this);
                    return;
                }

                if (player.IsSleeping() || player.IsDead()) return;

                if (!PLUGIN.permission.UserHasPermission(player.UserIDString, perm_use))
                {
                    NoPermission();
                    lastCheck = Time.realtimeSinceStartup + 1f;
                    return;
                }

                var delta = Time.realtimeSinceStartup - lastCheck;

                if (delta < PLUGIN.config.tick_rate)
                    return;

                var brokenLegDelta = Time.realtimeSinceStartup - lastBrokenLegCheck;

                if (hasBrokenLeg && brokenLegDelta < 1f)
                    return;

                // we dont want to check too often
                if (PLUGIN.InjuriesAndDiseases != null && brokenLegDelta > 1f)
                {
                    // if we are not invoking NoSprint, it has to be from another plugin.
                    hasBrokenLeg = !IsInvoking(nameof(NoSprint)) && player.HasPlayerFlag(BasePlayer.PlayerFlags.NoSprint);
                    lastBrokenLegCheck = Time.realtimeSinceStartup;
                }

                if (hasBrokenLeg)
                {
                    currentBoost = 0f;
                    currentMaxBoost = PLUGIN.config.boost_settings.min;
                    currentStamina = 0f;
                }
                else
                {
                    // in case a permission has changed
                    InitPlayerValues();
                    UpdateStamina(delta);
                    UpdateSprint();
                }
                
                UpdateBoostPermissions();
                UpdateStatus();

                lastCheck = Time.realtimeSinceStartup;
            }

            private void NoPermission()
            {
                currentBoost = 0f;
                
                UpdateBoostPermissions();

                PLUGIN.SimpleStatus?.CallHook("SetStatus", player.UserIDString, "Player_Stamina", 0);
                statusActive = false;
            }

            #region Player Values

            private void InitPlayerValues()
            {
                maxStamina = GetMaxStamina();
                maxBoost = GetMaxBoost();
                staminaReplenishMulti = GetStaminaReplenishMulti();
                boostReplenishMulti = GetBoostReplenishMulti();
            }

            private float GetMaxStamina()
            {
                var max = 1f;
                foreach(var pk in PLUGIN.config.permissions.max_stamina_perms)
                {
                    if (PLUGIN.permission.UserHasPermission(player.UserIDString, perm_prefix + pk.Key) && pk.Value > max) max = pk.Value;
                }
                return max * PLUGIN.config.stamina_settings.max;
            }

            private float GetMaxBoost()
            {
                var max = 1f;
                foreach(var pk in PLUGIN.config.permissions.max_boost_perms)
                {
                    if (PLUGIN.permission.UserHasPermission(player.UserIDString, perm_prefix + pk.Key) && pk.Value > max) max = pk.Value;
                }
                return max * PLUGIN.config.boost_settings.max;
            }

            private float GetStaminaReplenishMulti()
            {
                var max = 1f;
                foreach(var pk in PLUGIN.config.permissions.stamina_replenish_perms)
                {
                    if (PLUGIN.permission.UserHasPermission(player.UserIDString, perm_prefix + pk.Key) && pk.Value > max) max = pk.Value;
                }
                return max;
            }

            private float GetBoostReplenishMulti()
            {
                var max = 1f;
                foreach(var pk in PLUGIN.config.permissions.boost_replenish_perms)
                {
                    if (PLUGIN.permission.UserHasPermission(player.UserIDString, perm_prefix + pk.Key) && pk.Value > max) max = pk.Value;
                }
                return max;
            }

            #endregion

            #region Status

            private void UpdateStatus()
            {
                if (PLUGIN.SimpleStatus == null || !PLUGIN.SimpleStatus.IsLoaded) return;

                if (player.GetMounted() != null)
                {
                    PLUGIN.SimpleStatus?.CallHook("SetStatus", player.UserIDString, "Player_Stamina", 0);
                    statusActive = false;
                    return;
                }
                
                if (!statusActive)
                {
                    PLUGIN.SimpleStatus?.CallHook("SetStatus", player.UserIDString, "Player_Stamina", int.MaxValue);
                    statusActive = true;
                }

                PLUGIN.SimpleStatus?.CallHook("SetStatusProperty", player.UserIDString, "Player_Stamina", new Dictionary<string, object>
                {
                    ["progress"] = currentBoost > 0f ? currentBoost / maxBoost : currentStamina / maxStamina,
                    ["title"] = currentBoost > 0f ? $"{currentBoost / PLUGIN.config.boost_settings.max * 100f:F0}%" : $"{currentStamina / PLUGIN.config.stamina_settings.max * 100f:F0}%",
                    ["iconColor"] = currentBoost > 0f ? "1 0 0 0.8" : "0.969 0.922 0.882 0.5",
                    ["progressColor"] = currentBoost > 0f ? "0.5 0.3 0.6 0.9" : "1 0.82353 0.44706 1",
                });
            }

            #endregion

            #region NoSprint

            private void UpdateSprint()
            {
                if (currentStamina == 0f)
                {
                    if (!IsInvoking(nameof(NoSprint)))
                    {
                        InvokeRepeating(nameof(NoSprint), 0.1f, 0.1f);
                    }
                }
                else
                {
                    CancelInvoke(nameof(NoSprint));
                }
            }

            private void NoSprint() => player.SetPlayerFlag(BasePlayer.PlayerFlags.NoSprint, true);

            #endregion Status

            public void UpdateStamina(float delta)
            {
                if (player.IsRunning())
                {
                    if (currentBoost > 0)
                        UseBoost(delta);
                    else
                        UseStamina(delta);
                }
                else if (currentStamina != currentMaxStamina)
                {
                    ReplenishStamina(delta);
                }
                else if (currentMaxStamina != maxStamina)
                {
                    ReplenishStaminaMax(delta);
                    // after stamina is filled, we delay the boost
                    lastBoostUsed = Time.realtimeSinceStartup + PLUGIN.config.boost_settings.cooldown_depleted;
                }
                else if (currentBoost != currentMaxBoost)
                {
                    ReplenishBoost(delta);
                }
                else if (currentMaxBoost != maxBoost)
                {
                    ReplenishBoostMax(delta);
                }
            }
            
            #region Stamina
            public void UseStamina(float delta)
            {
                var amount = delta * (player.IsSwimming() ? PLUGIN.config.boost_settings.swim_loss_rate : PLUGIN.config.boost_settings.loss_rate);

                currentStamina -= amount;
                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                }

                if (currentStamina == 0f)
                    lastStaminaUsed = Time.realtimeSinceStartup + PLUGIN.config.stamina_settings.cooldown_depleted;
                else
                    lastStaminaUsed = Time.realtimeSinceStartup + PLUGIN.config.stamina_settings.cooldown;

                if (PLUGIN.config.stamina_settings.hydration.enabled)
                    player.metabolism.hydration.MoveTowards(0f, delta * PLUGIN.config.stamina_settings.hydration.loss_rate);

                if (PLUGIN.config.stamina_settings.heartrate.enabled)
                    player.metabolism.heartrate.MoveTowards(1f, delta * PLUGIN.config.stamina_settings.heartrate.rate);

                if (PLUGIN.config.stamina_settings.temperature.enabled)
                    player.metabolism.temperature.MoveTowards(PLUGIN.config.stamina_settings.temperature.max, delta * PLUGIN.config.stamina_settings.temperature.rate);
            }

            private void ReplenishStamina(float delta)
            {
                // this delays when it was used before
                if (lastStaminaUsed - Time.realtimeSinceStartup > 0) return;

                currentMaxStamina = Mathf.Clamp(currentMaxStamina - (delta * PLUGIN.config.stamina_settings.max_loss_rate), PLUGIN.config.stamina_settings.min, maxStamina);
                currentStamina = Mathf.Clamp(currentStamina + (delta * PLUGIN.config.stamina_settings.replenish_rate * staminaReplenishMulti), 0f, currentMaxStamina);
            }

            private void ReplenishStaminaMax(float delta)
            {
                float amount = delta * PLUGIN.config.stamina_settings.max_replenish_rate * staminaReplenishMulti;

                currentMaxStamina = Mathf.Clamp(currentMaxStamina + amount, 0f, maxStamina);
                currentStamina = Mathf.Clamp(currentStamina + amount, 0f, currentMaxStamina);
            }

            #endregion Stamina

            #region Boost

            public void UseBoost(float delta)
            {
                var amount = delta * (player.IsSwimming() ? PLUGIN.config.stamina_settings.swim_loss_rate : PLUGIN.config.stamina_settings.loss_rate);

                currentBoost -= amount;
                if (currentBoost <= 0f)
                {
                    currentBoost = 0f;
                }
                if (currentBoost == 0f)
                    lastBoostUsed = Time.realtimeSinceStartup + PLUGIN.config.boost_settings.cooldown_depleted;
                else
                    lastBoostUsed = Time.realtimeSinceStartup + PLUGIN.config.boost_settings.cooldown;
                
                if (PLUGIN.config.boost_settings.hydration.enabled)
                    player.metabolism.hydration.MoveTowards(0f, delta * PLUGIN.config.boost_settings.hydration.loss_rate);

                if (PLUGIN.config.boost_settings.heartrate.enabled)
                    player.metabolism.heartrate.MoveTowards(1f, delta * PLUGIN.config.boost_settings.heartrate.rate);

                if (PLUGIN.config.boost_settings.temperature.enabled)
                    player.metabolism.temperature.MoveTowards(PLUGIN.config.boost_settings.temperature.max, delta * PLUGIN.config.boost_settings.temperature.rate);
            }
            private void ReplenishBoost(float delta)
            {
                // this delays when it was used before
                if (lastBoostUsed - Time.realtimeSinceStartup > 0) return;

                currentMaxBoost = Mathf.Clamp(currentMaxBoost - (delta * PLUGIN.config.boost_settings.max_loss_rate), PLUGIN.config.boost_settings.min, maxBoost);
                currentBoost = Mathf.Clamp(currentBoost + (delta * PLUGIN.config.boost_settings.replenish_rate * boostReplenishMulti), 0f, currentMaxBoost);
            }

            private void ReplenishBoostMax(float delta)
            {
                float amount = delta * PLUGIN.config.boost_settings.max_replenish_rate * boostReplenishMulti;

                currentMaxBoost = Mathf.Clamp(currentMaxBoost + amount, 0f, maxBoost);
                currentBoost = Mathf.Clamp(currentBoost + amount, 0f, currentMaxBoost);
            }

            private void UpdateBoostPermissions()
            {
                if (currentBoost == 0f && PLUGIN.permission.UserHasPermission(player.UserIDString, PLUGIN.config.boost_settings.sprint_boost_perm))
                    PLUGIN.permission.RevokeUserPermission(player.UserIDString, PLUGIN.config.boost_settings.sprint_boost_perm);
                else if (currentBoost > 0f && !PLUGIN.permission.UserHasPermission(player.UserIDString, PLUGIN.config.boost_settings.sprint_boost_perm))
                    PLUGIN.permission.GrantUserPermission(player.UserIDString, PLUGIN.config.boost_settings.sprint_boost_perm, null);

                if (currentBoost == 0f && PLUGIN.permission.UserHasPermission(player.UserIDString, PLUGIN.config.boost_settings.swim_boost_perm))
                    PLUGIN.permission.RevokeUserPermission(player.UserIDString, PLUGIN.config.boost_settings.swim_boost_perm);
                else if (currentBoost > 0f && !PLUGIN.permission.UserHasPermission(player.UserIDString, PLUGIN.config.boost_settings.swim_boost_perm))
                    PLUGIN.permission.GrantUserPermission(player.UserIDString, PLUGIN.config.boost_settings.swim_boost_perm, null);
            }

            #endregion Boost
        }

        #endregion Behavior

        #region Config
        
        private Configuration config = new Configuration();

        #region Plugin Config

        public class Configuration
        {
            [JsonProperty("tick rate to calculate stamina and boost loss/gain (lower = more performance impact)")]
            public float tick_rate = 0.2f;

            [JsonProperty("stamina settings")]
            public StaminaSettings stamina_settings = new();

            [JsonProperty("boost settings (requires MovementSpeed plugin)")]
            public BoostSettings boost_settings = new();
            
            [JsonProperty("Permissions")]
            public PermissionSettings permissions = new();

            [JsonProperty("User experience")]
            public UXSettings ux_settings = new();
        }

        public class StaminaSettings
        {
            [JsonProperty("Maximum value a player can reach")]
            public float max = 100f;

            [JsonProperty("Maximum value can deplete to this value (recommended: 20% of max value)")]
            public float min = 20f;

            [JsonProperty("loss rate per second when running")]
            public float loss_rate = 2.0f;

            [JsonProperty("loss rate per second when swimming")]
            public float swim_loss_rate = 1.0f;

            [JsonProperty("max value loss rate per second when recovering")]
            public float max_loss_rate = 1.0f;
            
            [JsonProperty("replenish rate per second")]
            public float replenish_rate = 4.0f;
            
            [JsonProperty("replenish rate of max value per second")]
            public float max_replenish_rate = 1.0f;
            
            [JsonProperty("cooldown before replenishing starts")]
            public float cooldown = 1.0f;
            
            [JsonProperty("cooldown before replenishing starts after it was fully depleted")]
            public float cooldown_depleted = 5.0f;

            public HydrationSettings hydration = new();

            public HeartrateSettings heartrate = new();

            public TemperatureSetting temperature = new();
        }

        public class BoostSettings : StaminaSettings
        {
            [JsonProperty("permission to use for sprint boost")]
            public string sprint_boost_perm = "movementspeed.run.3";

            [JsonProperty("permission to use for swim boost")]
            public string swim_boost_perm = "movementspeed.swim.3";
        }

        public class HeartrateSettings
        {
            [JsonProperty("change heartrate for players when running")]
            public bool enabled = true;
            
            [JsonProperty("heartrate increased per second")]
            public float rate = 0.2f;
        }

        public class HydrationSettings
        {
            [JsonProperty("players can loose hydration when running")]
            public bool enabled = true;
            
            [JsonProperty("hydration loss per second")]
            public float loss_rate = 0.1f;
        }

        public class TemperatureSetting
        {
            [JsonProperty("change heartrate for players when running")]
            public bool enabled = true;
            
            [JsonProperty("target temperature value")]
            public float max = 48.0f;
            
            [JsonProperty("increased temperature per second")]
            public float rate = 3.0f;
        }

        public class PermissionSettings
        {
            [JsonProperty("Permission based max stamina multipliers")]
            public Dictionary<string, float> max_stamina_perms = new();

            [JsonProperty("Permission based max boost multipliers")]
            public Dictionary<string, float> max_boost_perms = new();

            [JsonProperty("Permission based stamina replenish multipliers")]
            public Dictionary<string, float> stamina_replenish_perms = new();

            [JsonProperty("Permission based boost replenish multipliers")]
            public Dictionary<string, float> boost_replenish_perms = new();
        }

        public class UXSettings
        {

        }

        #endregion Plugin Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();

                if (config == null)
                {
                    throw new JsonException();
                }
                Puts($"Configuration file {Name}.json loaded");
            }
            catch (Exception ex)
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();

            config.boost_settings.max = 10f;
            config.boost_settings.min = 2f;
            config.boost_settings.loss_rate = 3f;
            config.boost_settings.swim_loss_rate = 1f;
            config.boost_settings.max_loss_rate = 1f;

            config.boost_settings.replenish_rate = 2f;
            config.boost_settings.max_replenish_rate = 0.5f;

            config.boost_settings.cooldown = 2f;
            config.boost_settings.cooldown_depleted = 4f;

            config.boost_settings.sprint_boost_perm = "movementspeed.run.3";
            config.boost_settings.swim_boost_perm = "movementspeed.swim.3";

            config.boost_settings.hydration.loss_rate = 0.2f;
            config.boost_settings.heartrate.rate = 0.4f;
            config.boost_settings.temperature.rate = 5f;

            config.permissions.max_stamina_perms.Add("stamina.max1", 1.1f);
            config.permissions.max_stamina_perms.Add("stamina.max2", 1.2f);
            config.permissions.max_stamina_perms.Add("stamina.max3", 1.3f);
            config.permissions.max_stamina_perms.Add("stamina.max4", 1.4f);
            config.permissions.max_stamina_perms.Add("stamina.max5", 1.5f);

            config.permissions.max_boost_perms.Add("boost.max1", 1.1f);
            config.permissions.max_boost_perms.Add("boost.max2", 1.2f);
            config.permissions.max_boost_perms.Add("boost.max3", 1.3f);
            config.permissions.max_boost_perms.Add("boost.max4", 1.4f);
            config.permissions.max_boost_perms.Add("boost.max5", 1.5f);

            config.permissions.stamina_replenish_perms.Add("stamina.replenish1", 1.2f);
            config.permissions.stamina_replenish_perms.Add("stamina.replenish2", 1.4f);
            config.permissions.stamina_replenish_perms.Add("stamina.replenish3", 1.6f);
            config.permissions.stamina_replenish_perms.Add("stamina.replenish4", 1.8f);
            config.permissions.stamina_replenish_perms.Add("stamina.replenish5", 2.0f);

            config.permissions.boost_replenish_perms.Add("boost.replenish1", 1.2f);
            config.permissions.boost_replenish_perms.Add("boost.replenish2", 1.4f);
            config.permissions.boost_replenish_perms.Add("boost.replenish3", 1.6f);
            config.permissions.boost_replenish_perms.Add("boost.replenish4", 1.8f);
            config.permissions.boost_replenish_perms.Add("boost.replenish5", 2.0f);
        }

        #region Load/Save Config


        #endregion Load/Save Config

        #endregion Config

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // this is used to get rid of errors for missing lang defs
                ["title"] = "0%"
            }, this);
        }

        #endregion Localization
    }
}
