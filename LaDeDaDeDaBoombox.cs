using System.Collections;
using HarmonyLib;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;
using System.Reflection;
using UnityEngine.Networking;

public class ModMain
{
    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            Debug.Log("Embedded resource: " + name);
        }

        var harmony = new Harmony("com.vegemike.LaDeDaDeDaBoombox");
        Harmony.CreateAndPatchAll(typeof(LaDeDaDeDaBoomboxPatch));

        Debug.Log("Harmony patch applied to BoomboxItem.StartMusic");
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
        __instance.boomboxAudio.Stop();
        __instance.boomboxAudio.clip = null;
        __instance.StartCoroutine(LoadRandomEmbeddedSong(__instance));
    }
    
    
    static IEnumerator LoadRandomEmbeddedSong(BoomboxItem boombox)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string prefix = "LaDeDaDeDaBoombox.Assets.";

        // Cache directory inside plugin folder
        string cacheDir = Path.Combine(Paths.PluginPath, "cached_songs");
        Directory.CreateDirectory(cacheDir);
        
        var songs = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix) && name.EndsWith(".ogg"))
            .ToArray();

        if (songs.Length == 0)
        {
            Debug.LogWarning("No embedded songs found!");
            yield break;
        }

        // pick ogg file randomly
        string chosenResource = songs[UnityEngine.Random.Range(0, songs.Length)];
        string songFileName = chosenResource.Substring(prefix.Length); // e.g., "mysong.ogg"
        string cachedPath = Path.Combine(cacheDir, songFileName);
        
        if (!File.Exists(cachedPath))
        {
            Debug.Log($"Extracting embedded song to cache: {cachedPath}");
            using var stream = assembly.GetManifestResourceStream(chosenResource);
            if (stream == null)
            {
                Debug.LogError($"Failed to get stream for {chosenResource}");
                yield break;
            }

            using var fileStream = File.Create(cachedPath);
            stream.CopyTo(fileStream);
        }
        using var request = UnityWebRequestMultimedia.GetAudioClip("file://" + cachedPath, AudioType.OGGVORBIS);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to load audio clip: " + request.error);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip == null)
        {
            Debug.LogError("Failed to decode audio clip");
            yield break;
        }

        boombox.boomboxAudio.clip = clip;
        boombox.boomboxAudio.Play();

        Debug.Log($"Playing cached song: {songFileName}");
    }



}
