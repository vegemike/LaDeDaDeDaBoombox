using BepInEx;
using HarmonyLib;
using System.Reflection;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[BepInPlugin("com.vegemike.LaDeDaDeDaBoombox", "Boombox Patch", "1.0.0")]
public class BoomboxMod : BaseUnityPlugin
{
    private void Awake()
    {
        ModMain.Init();
        Debug.Log("[BoomboxMod] Plugin Awake");
    }
}

[HarmonyPatch(typeof(GameNetworkManager), "Start")]
public static class InitBoomboxVolumeGUI
{
    static void Postfix()
    {
        if (BoomboxVolumeGUI.Instance == null)
        {
            GameObject guiObj = new GameObject("BoomboxVolumeGUI");
            guiObj.AddComponent<BoomboxVolumeGUI>();
            Object.DontDestroyOnLoad(guiObj);
        }
    }
}

public static class ModMain
{
    public static Harmony harmony;

    public static void Init()
    {
        harmony = new Harmony("com.vegemike.LaDeDaDeDaBoombox");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Debug.Log("[ModMain] Harmony patches applied for LaDeDaDeDaBoombox.");
    }
}

[HarmonyPatch(typeof(StartOfRound), "Awake")]
public static class InjectBoomboxSyncManager
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (BoomboxSyncManager.Instance == null)
        {
            Debug.Log("[InjectBoomboxSyncManager] Creating BoomboxSyncManager GameObject");
            GameObject go = new GameObject("BoomboxSyncManager");
            
            // add NetworkObject first
            var netObj = go.AddComponent<NetworkObject>();
            var syncMgr = go.AddComponent<BoomboxSyncManager>();
            Object.DontDestroyOnLoad(go);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                netObj.Spawn();
                Debug.Log("[InjectBoomboxSyncManager] BoomboxSyncManager spawned on server");
            }
            else
            {
                Debug.Log("[InjectBoomboxSyncManager] NetworkManager not ready or not server; delaying spawn until server");
            }
        }
    }
}

[HarmonyPatch(typeof(BoomboxItem))]
[HarmonyPatch("StartMusic", MethodType.Normal)]
class LaDeDaDeDaBoomboxPatch
{
    static void Postfix(BoomboxItem __instance, bool startMusic)
    {
        if (!startMusic) return;

        // stop default playback immediately
        __instance.boomboxAudio.Stop();
        Debug.Log("[LaDeDaDeDaBoomboxPatch] Boombox StartMusic triggered; default audio stopped");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[LaDeDaDeDaBoomboxPatch] NetworkManager.Singleton is null; skipping sync");
            return;
        }

        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[LaDeDaDeDaBoomboxPatch] Running on host—calling HandleBoomboxStart");
            BoomboxSyncManager.Instance.HandleBoomboxStart(__instance);
        }
        else
        {
            Debug.Log("[LaDeDaDeDaBoomboxPatch] Not host or server; skipping song selection");
        }
    }
}

//stop battery drain

[HarmonyPatch(typeof(BoomboxItem))]
[HarmonyPatch("Update", MethodType.Normal)]
class PreventBoomboxBatteryDrain
{
    static void Postfix(BoomboxItem __instance)
    {
        switch (__instance.isPlayingMusic)
        {
            case false:
                return;
            case true:
                if (!__instance.boomboxAudio.isPlaying)
                {
                    __instance.StartMusic(true);
                }
                break;
        }

        if (__instance.insertedBattery != null)
        {
            __instance.insertedBattery.charge = 1f;
        }

        if (__instance.boomboxAudio != null)
        {
            float targetVol = BoomboxVolumeGUI.Instance != null ? BoomboxVolumeGUI.Instance.Volume / 50f : 1f;
            if (!Mathf.Approximately(targetVol, __instance.boomboxAudio.volume))
            {
                __instance.boomboxAudio.volume = targetVol;
            }
        }

    }
}


/*
[HarmonyPatch(typeof(BoomboxItem), "ItemActivate")]
class BoomboxItem_ItemActivate_Patch
{
    static void Postfix(BoomboxItem __instance, bool used)
    {
        if (used)
        {

        }
    }
}*/
