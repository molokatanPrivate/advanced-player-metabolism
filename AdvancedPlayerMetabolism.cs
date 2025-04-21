using Oxide.Plugins;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("AdvancedPlayerMetabolism", "molokatan", "1.0.0"), Description("Stamina bar for players with movement speed boost.")]
    public class AdvancedPlayerMetabolism : RustPlugin
    {
        [PluginReference]
        private Plugin SimpleStatus, InjuriesAndDiseases;

        public static AdvancedPlayerMetabolism PLUGIN;

        public static string perm_prefix = "advancedplayermetabolism.";
        // public static string perm_use = perm_prefix + "use";

        #region plugin loading
        private void OnServerInitialized()
        {
            PLUGIN = this;

            if (SimpleStatus == null || !SimpleStatus.IsLoaded)
            {
                Puts($"You must have SimpleStatus installed to run {Name}.");
                return;
            }

            if (InjuriesAndDiseases == null || !InjuriesAndDiseases.IsLoaded)
            {
                Puts($"Support for broken legs disabled.");
            }

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

            RegisterPermissions();
            
            foreach (var player in BasePlayer.activePlayerList)
                CreatePlayerStamina(player);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyPlayerStamina(player);
        }

        void RegisterPermissions()
        {
            foreach (var permMaxStamina in config.permissions.max_stamina_perms)
                permission.RegisterPermission(perm_prefix + permMaxStamina, this);

            foreach (var permMaxBoost in config.permissions.max_boost_perms)
                permission.RegisterPermission(perm_prefix + permMaxBoost, this);
        }
        #endregion

        #region Player Connections
        
        void OnPlayerConnected(BasePlayer player) => CreatePlayerStamina(player);

        void OnPlayerDisconnected(BasePlayer player, string reason) => DestroyPlayerStamina(player);

        #endregion

        void DestroyPlayerStamina(BasePlayer player)
        {
            var result = player.GetComponent<PlayerStamina>();
            if (result == null) return;

            GameObject.Destroy(result);
        }

        void CreatePlayerStamina(BasePlayer player)
        {
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
            private float tickRate = 0.2f;
            private float lastCheck = Time.realtimeSinceStartup;

            private StaminaSettings staminaCfg;
            private BoostSettings boostCfg;

            // stamina
            private float lastStaminaUsed;

            private float currentStamina = 50f;
            private float currentMaxStamina = 50f;
            private float minStamina = 30f;
            private float maxStamina = 100f;

            // boost
            private float lastBoostUsed;

            private float currentBoost = 0f;
            private float currentMaxBoost = 50f;
            private float minBoost = 30f;
            private float maxBoost = 100f;

            // broken leg
            private float lastBrokenLegCheck;
            private bool hasBrokenLeg = false;

            private void UpdateMaxStamina()
            {
            }

            private void UpdateMaxBoost()
            {
            }

            public void Setup(Configuration config)
            {
                tickRate = config.tick_rate;
                
                staminaCfg = config.stamina_settings;
                maxStamina = config.stamina_settings.max;
                
                boostCfg = config.boost_settings;
                maxBoost = config.boost_settings.max;
            }

            private void Awake()
            {
                player = GetComponent<BasePlayer>();

                Setup(PLUGIN.config);

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

                var delta = Time.realtimeSinceStartup - lastCheck;
                var brokenLegDelta = Time.realtimeSinceStartup - lastBrokenLegCheck;

                if (delta < tickRate)
                    return;

                if (hasBrokenLeg && brokenLegDelta < 1f)
                {
                    // FIMXE: really needed?
                    // this should be done to get better status pane updates
                    UpdateStatus();
                    return;
                }

                // we dont want to check too often
                if (PLUGIN.InjuriesAndDiseases != null && brokenLegDelta > 1f)
                {
                    // if we are not invoking NoSprint, it has to be from another plugin.
                    // the origin hooks had not been working
                    hasBrokenLeg = !IsInvoking(nameof(NoSprint)) && player.HasPlayerFlag(BasePlayer.PlayerFlags.NoSprint);
                    lastBrokenLegCheck = Time.realtimeSinceStartup;
                }

                if (hasBrokenLeg)
                {
                    currentBoost = 0f;
                    currentMaxBoost = 1f;
                    currentStamina = 0f;
                }
                else
                {
                    UpdateStamina(delta);
                    UpdateSprint();
                }
                
                UpdatePermissions();
                UpdateStatus();

                lastCheck = Time.realtimeSinceStartup;
            }

            #region Status and Permissions

            private void UpdateStatus()
            {
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
                    ["title"] = currentBoost > 0f ? $"{currentBoost / maxBoost * 100f:F0}%" : $"{currentStamina / maxStamina * 100f:F0}%",
                    ["iconColor"] = currentBoost > 0f ? "1 0 0 0.8" : "0.969 0.922 0.882 0.5",
                    ["progressColor"] = currentBoost > 0f ? "0.5 0.3 0.6 0.9" : "1 0.82353 0.44706 1",
                });
            }

            private void UpdatePermissions()
            {
                if (currentBoost == 0f && PLUGIN.permission.UserHasPermission(player.UserIDString, "movementspeed.run.3"))
                    PLUGIN.permission.RevokeUserPermission(player.UserIDString, "movementspeed.run.3");
                else if (currentBoost > 0f && !PLUGIN.permission.UserHasPermission(player.UserIDString, "movementspeed.run.3"))
                    PLUGIN.permission.GrantUserPermission(player.UserIDString, "movementspeed.run.3", null);

                if (currentBoost == 0f && PLUGIN.permission.UserHasPermission(player.UserIDString, "movementspeed.swim.3"))
                    PLUGIN.permission.RevokeUserPermission(player.UserIDString, "movementspeed.swim.3");
                else if (currentBoost > 0f && !PLUGIN.permission.UserHasPermission(player.UserIDString, "movementspeed.swim.3"))
                    PLUGIN.permission.GrantUserPermission(player.UserIDString, "movementspeed.swim.3", null);
            }

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

            private void NoSprint()
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.NoSprint, true);
            }

            #endregion Status and Permissions


            public void UpdateStamina(float delta)
            {
                if (player.IsRunning())
                {
                    if (currentBoost > 0)
                        UseBoost(delta * (player.IsSwimming() ? staminaCfg.swim_loss_rate : staminaCfg.loss_rate));
                    else
                        UseStamina(delta * (player.IsSwimming() ? boostCfg.swim_loss_rate : boostCfg.loss_rate));
                }
                else if (currentStamina != currentMaxStamina)
                {
                    ReplenishStamina(delta);
                }
                else if (currentMaxStamina != maxStamina)
                {
                    ReplenishStaminaMax(delta);
                    // after stamina is filled, we delay the boost
                    lastBoostUsed = Time.realtimeSinceStartup + boostCfg.cooldown_depleted;
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
            public void UseStamina(float amount)
            {
                currentStamina -= amount;
                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                }

                if (currentStamina == 0f)
                    lastStaminaUsed = Time.realtimeSinceStartup + staminaCfg.cooldown_depleted;
                else
                    lastStaminaUsed = Time.realtimeSinceStartup + staminaCfg.cooldown;
            }

            private void ReplenishStamina(float delta)
            {
                // this delays when it was used before
                if (lastStaminaUsed - Time.realtimeSinceStartup > 0) return;

                currentMaxStamina = Mathf.Clamp(currentMaxStamina - (delta * staminaCfg.max_loss_rate), staminaCfg.min, maxStamina);
                currentStamina = Mathf.Clamp(currentStamina + (delta * staminaCfg.replenish_rate), 0f, currentMaxStamina);
            }

            private void ReplenishStaminaMax(float delta)
            {
                float amount = delta * staminaCfg.max_replenish_rate;

                currentMaxStamina = Mathf.Clamp(currentMaxStamina + amount, 0f, maxStamina);
                currentStamina = Mathf.Clamp(currentStamina + amount, 0f, currentMaxStamina);
            }

            #endregion Stamina

            #region Boost

            public void UseBoost(float amount)
            {
                currentBoost -= amount;
                if (currentBoost <= 0f)
                {
                    currentBoost = 0f;
                }
                if (currentBoost == 0f)
                    lastBoostUsed = Time.realtimeSinceStartup + boostCfg.cooldown_depleted;
                else
                    lastBoostUsed = Time.realtimeSinceStartup + boostCfg.cooldown;
            }
            private void ReplenishBoost(float delta)
            {
                // this delays when it was used before
                if (lastBoostUsed - Time.realtimeSinceStartup > 0) return;

                currentMaxBoost = Mathf.Clamp(currentMaxBoost - (delta * boostCfg.max_loss_rate), boostCfg.min, maxBoost);
                currentBoost = Mathf.Clamp(currentBoost + (delta * boostCfg.replenish_rate), 0f, currentMaxBoost);
            }

            private void ReplenishBoostMax(float delta)
            {
                float amount = delta * boostCfg.max_replenish_rate;

                currentMaxBoost = Mathf.Clamp(currentMaxBoost + amount, 0f, maxBoost);
                currentBoost = Mathf.Clamp(currentBoost + amount, 0f, currentMaxBoost);
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
        }

        public class BoostSettings : StaminaSettings
        {
            [JsonProperty("permission to use for sprint boost")]
            public string sprint_boost_perm = "";

            [JsonProperty("permission to use for swim boost")]
            public string swim_boost_perm = "";
        }

        public class PermissionSettings
        {
            [JsonProperty("Permission based max stamina multiplier")]
            public Dictionary<string, float> max_stamina_perms = new();

            [JsonProperty("Permission based max boost multiplier")]
            public Dictionary<string, float> max_boost_perms = new();
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
