using System.Collections;
using HarmonyLib;
using System.IO;
using BepInEx;
using UnityEngine;
using System.Reflection;

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
        
        __instance.StartCoroutine(LoadEmbeddedWavAndSetClip(__instance));
    }

    static IEnumerator LoadEmbeddedWavAndSetClip(BoomboxItem boombox)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "LaDeDaDeDaBoombox.Assets.song.wav"; 

        byte[] wavBytes;
        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                Debug.LogError($"Embedded WAV resource '{resourceName}' not found!");
                yield break;
            }
            wavBytes = new byte[stream.Length];
            stream.Read(wavBytes, 0, wavBytes.Length);
        }

        // Use WavUtility to convert bytes into an AudioClip
        AudioClip clip = WavUtility.ToAudioClip(wavBytes, 0, "EmbeddedSong");

        if (clip == null)
        {
            Debug.LogError("Failed to convert embedded WAV bytes to AudioClip!");
            yield break;
        }

        boombox.boomboxAudio.clip = clip;
        boombox.boomboxAudio.Play();

        Debug.Log("Embedded audio loaded and playing!");

        yield return null;
    }
}
