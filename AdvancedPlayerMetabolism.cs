using Oxide.Plugins;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdvancedPlayerMetabolism", "molokatan", "1.0.0"), Description("Stamina bar for players with movement speed boost.")]
    public class AdvancedPlayerMetabolism : RustPlugin
    {
        [PluginReference]
        private Plugin SimpleStatus;

        public static AdvancedPlayerMetabolism PLUGIN;

        #region plugin loading
        private void OnServerInitialized()
        {
            PLUGIN = this;

            if (SimpleStatus == null || !SimpleStatus.IsLoaded)
            {
                Puts($"You must have SimpleStatus installed to run {Name}.");
                return;
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
        }

        void Loaded()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CreatePlayerStamina(player);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyPlayerStamina(player);
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

        public class PlayerStamina : MonoBehaviour
        {
            private BasePlayer player;

            private bool statusActive = false;
            private float nextUpdate;
            private float delay = 0.2f;
            private float staminaGoneDelay = 3f;

            // who ever logs in has only half stamina
            public float currentStamina = 10f;
            // should never be 0, otherwise it gets stuck
            public float currentMaxStamina = 10f;

            public float maxStamina = 20f;

            public float currentBoost = 0f;
            // should never be 0, otherwise it gets stuck
            public float currentMaxBoost = 1.0f;
            public float maxBoost = 2.5f;

            public float nextUpdateBoost;

            public float staminaCoreLossRatio = 1.0f;
            public float staminaSwimLossMulti = 0.5f;

            public float staminaCoreReplenishRatio = 1.5f;
            public float staminaMaxReplenishRatio = 0.5f;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                nextUpdate = Time.realtimeSinceStartup + delay;
                nextUpdateBoost = Time.realtimeSinceStartup + staminaGoneDelay;
            }

            public void FixedUpdate()
            {
                if (player == null || !player.IsConnected)
                {
                    Destroy(this);
                    return;
                }

                var delta = nextUpdate - Time.realtimeSinceStartup;

                if (delta > 0)
                {
                    // this should be done to get better status pane updates
                    UpdateStatus();
                    return;
                }

                UpdateStamina(delay);
                
                UpdatePermissions();
                UpdateSprint();
                
                UpdateStatus();

                PLUGIN.Puts($"{delta} | {nextUpdateBoost - Time.realtimeSinceStartup}");

                if (currentStamina == 0f)
                    nextUpdate = Time.realtimeSinceStartup + staminaGoneDelay;
                else
                    nextUpdate = Time.realtimeSinceStartup + delay;
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
                    // swimming consumes less stamina
                    var swimmingMulti = player.IsSwimming() ? 0.5f : 1f;
                    if (currentBoost > 0)
                    {
                        UseBoost(delta * staminaCoreLossRatio * swimmingMulti);
                    }
                    else
                    {
                        UseStamina(delta * staminaCoreLossRatio * swimmingMulti);
                    }
                }
                else if (currentStamina != currentMaxStamina)
                {
                    ReplenishStamina(staminaCoreReplenishRatio * delta * (player.IsSwimming() || !player.IsDucked() ? 1f : 1.5f));
                }
                else if (currentMaxStamina != maxStamina)
                {
                    ReplenishStaminaMax(staminaMaxReplenishRatio * delta);
                    nextUpdateBoost = Time.realtimeSinceStartup + staminaGoneDelay;
                }
                else if (nextUpdateBoost - Time.realtimeSinceStartup > 0)
                {
                    // this delays the boost when if was ever used before
                    return;
                }
                else if (currentBoost != currentMaxBoost)
                {
                    ReplenishBoost(staminaCoreReplenishRatio * delta);
                }
                else if (currentMaxBoost != maxBoost)
                {
                    ReplenishBoostMax(staminaMaxReplenishRatio * delta);
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
            }

            private void ReplenishStamina(float amount)
            {
                float num = 1f + Mathf.InverseLerp(maxStamina * 0.5f, maxStamina, currentMaxStamina);
                amount *= num;
                amount = Mathf.Min(currentMaxStamina - currentStamina, amount);
                float num2 = Mathf.Min(currentMaxStamina - staminaCoreReplenishRatio * amount, amount * staminaCoreReplenishRatio);
                float num3 = Mathf.Min(currentMaxStamina - staminaCoreLossRatio * amount * 0.25f, amount * staminaCoreLossRatio * 0.25f);

                currentMaxStamina = Mathf.Clamp(currentMaxStamina - num3, 0f, maxStamina);
                currentStamina = Mathf.Clamp(currentStamina + num2 / staminaCoreReplenishRatio, 0f, currentMaxStamina);
            }

            private void ReplenishStaminaMax(float amount)
            {
                float num = 1f + Mathf.InverseLerp(maxStamina * 0.5f, maxStamina, currentMaxStamina);
                amount *= num;
                amount = Mathf.Min(maxStamina - currentMaxStamina, amount);
                float num2 = Mathf.Min(currentMaxStamina - staminaMaxReplenishRatio * amount, amount * staminaMaxReplenishRatio);

                currentMaxStamina = Mathf.Clamp(currentMaxStamina + num2, 1f, maxStamina);
                currentStamina = Mathf.Clamp(currentStamina + num2, 0f, currentMaxStamina);
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
                nextUpdateBoost = Time.realtimeSinceStartup + staminaGoneDelay;
            }
            private void ReplenishBoost(float amount)
            {
                float num = 1f + Mathf.InverseLerp(maxBoost * 0.5f, maxBoost, currentMaxBoost);
                amount *= num;
                amount = Mathf.Min(currentMaxBoost - currentBoost, amount);
                float num2 = Mathf.Min(currentMaxBoost - staminaCoreReplenishRatio * amount, amount * staminaCoreReplenishRatio);
                float num3 = Mathf.Min(currentMaxBoost - staminaCoreLossRatio * amount * 0.25f, amount * staminaCoreLossRatio * 0.25f);

                currentMaxBoost = Mathf.Clamp(currentMaxBoost - num3, 1f, maxBoost);
                currentBoost = Mathf.Clamp(currentBoost + num2 / staminaCoreReplenishRatio, 0f, currentMaxBoost);
            }

            private void ReplenishBoostMax(float amount)
            {
                float num = 1f + Mathf.InverseLerp(maxBoost * 0.5f, maxBoost, currentMaxBoost);
                amount *= num;
                amount = Mathf.Min(maxBoost - currentMaxBoost, amount);
                float num2 = Mathf.Min(currentMaxBoost - staminaMaxReplenishRatio * amount, amount * staminaMaxReplenishRatio);

                currentMaxBoost = Mathf.Clamp(currentMaxBoost + num2, 0f, maxBoost);
                currentBoost = Mathf.Clamp(currentBoost + num2, 0f, currentMaxBoost);
            }

            #endregion Boost
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // this is used to get rid of errors for missing lang defs
                ["title"] = "0%"
            }, this);
        }
    }
}
