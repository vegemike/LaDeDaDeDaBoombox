using System.Collections;
using HarmonyLib;
using System.IO;
using BepInEx;
using UnityEngine;
using System.Reflection;
using UnityEngine.Networking;

public class ModMain
{
    public static void Init()
    {
        var harmony = new Harmony("com.vegemike.LaDeDaDeDaBoombox");
        Harmony.CreateAndPatchAll(typeof(LaDeDaDeDaBoomboxPatch));


        UnityEngine.Debug.Log("Harmony patch applied to BoomboxItem.StartMusic");
    }
}

[BepInEx.BepInPlugin("com.vegemike.LaDeDaDeDaBoombox", "Boombox Patch", "1.0.0")]
public class BoomboxMod : BaseUnityPlugin
{
    private void Awake()
    {
        ModMain.Init();
    }
}


[HarmonyPatch(typeof(BoomboxItem))]
[HarmonyPatch("StartMusic", MethodType.Normal)]
class LaDeDaDeDaBoomboxPatch
{
    static void Postfix(BoomboxItem __instance, bool startMusic)
    {
        if (!startMusic) return;

        string path = Path.Combine(Paths.PluginPath, "song.wav");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"Custom song not found at: {path}");
            return;
        }
        
        __instance.StartCoroutine(LoadAndSetClip(__instance, path));
    }

    static IEnumerator LoadAndSetClip(BoomboxItem boombox, string filePath)
    {
        using var request = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.WAV);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to load custom audio: " + request.error);
            yield break;
        }

        AudioClip customClip = DownloadHandlerAudioClip.GetContent(request);
        customClip.name = "CustomLoadedClip";

        boombox.boomboxAudio.clip = customClip;
        boombox.boomboxAudio.Play();

        Debug.Log("Custom audio loaded and playing!");
    }
}
