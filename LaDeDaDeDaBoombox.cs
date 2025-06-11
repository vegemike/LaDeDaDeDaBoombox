using BepInEx;
using HarmonyLib;
using System.Reflection;
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
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            var guiObj = new GameObject("BoomboxVolumeGUI");
            GameObject.DontDestroyOnLoad(guiObj);
            guiObj.AddComponent<BoomboxVolumeGUI>();
        };

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
        //Debug.Log($"[LaDeDaDeDaBoomboxPatch] preventing battery drain, prev: {__instance.insertedBattery.charge}");
        __instance.insertedBattery.charge = 1f;
    }
}


public class BoomboxVolumeGUI : MonoBehaviour
{
    private Rect windowRect;
    private const int WindowId = 0xB00B1E; // arbitrary ID for the GUI window
    private const float WindowWidth = 300f;
    private const float WindowHeight = 100f;

    public static bool UIOpen = false;    
    public static float Volume = 100f;       
    public static BoomboxItem TargetBoombox; 

    void Start()
    {
        // Center the GUI window
        windowRect = new Rect(
            (Screen.width  - WindowWidth) / 2f,
            (Screen.height - WindowHeight) / 2f,
            WindowWidth,
            WindowHeight
        );
    }

    void Update()
    {
        // toggle window with F9
        if (Keyboard.current?.f9Key.wasPressedThisFrame == true)
        {
            UIOpen = !UIOpen;
            Cursor.visible   = UIOpen;
            Cursor.lockState = UIOpen ? CursorLockMode.None : CursorLockMode.Locked;
        }

        // if open and a boombox is targeted, apply the volume
        if (UIOpen && TargetBoombox != null)
        {
            TargetBoombox.boomboxAudio.volume = Volume / 100f;
        }
    }

    void OnGUI()
    {
        if (!UIOpen) return;

        windowRect = GUI.ModalWindow(WindowId, windowRect, DrawWindowContents, "Boombox Volume");
    }

    private void DrawWindowContents(int id)
    {
        float w = windowRect.width;

        // close button (top right)
        if (GUI.Button(new Rect(w - 24, 4, 20, 20), "X"))
        {
            UIOpen = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        // label and slider
        GUI.Label(new Rect(10, 30, 110, 20), "Volume (0–100):");
        float newVol = GUI.HorizontalSlider(new Rect(120, 35, w - 140, 20), Volume, 0f, 100f);
        if (!Mathf.Approximately(newVol, Volume))
        {
            Volume = newVol;
        }

        // show numeric value
        GUI.Label(new Rect(w - 50, 30, 40, 20), Volume.ToString("0"));

        GUI.DragWindow(new Rect(0, 0, w, 24)); // drag bar
    }
}
public static class BoomboxVolumeUI
{
    public static float VolumeValue = 100f; // default full volume
    public static bool UIOpen = true;       // to toggle visibility
    public static BoomboxItem CurrentBoombox; // reference set elsewhere
}

[HarmonyPatch(typeof(BoomboxItem), "ItemActivate")]
class BoomboxItem_ItemActivate_Patch
{
    static void Postfix(BoomboxItem __instance, bool used)
    {
        if (used)
        {

        }
    }
}
