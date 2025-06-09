using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Networking;

public class BoomboxSyncManager : MonoBehaviour
{
    public static BoomboxSyncManager Instance { get; private set; }

    // prefix used to identify embedded .ogg resources
    private string resourcePrefix = "LaDeDaDeDaBoombox.Assets.";

    // list of all embedded .ogg resource names
    private List<string> embeddedSongs = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("[BoomboxSyncManager] Duplicate instance—destroying this one.");
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[BoomboxSyncManager] Awake: instance created.");

        // collect all embedded .ogg resource names
        var assembly = Assembly.GetExecutingAssembly();
        embeddedSongs = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix) && name.EndsWith(".ogg"))
            .ToList();

        Debug.Log($"[BoomboxSyncManager] Found {embeddedSongs.Count} embedded songs.");

        // register a handler for the custom message "BoomboxPlay"
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
                "BoomboxPlay",
                OnReceiveBoomboxPlayMessage
            );
            Debug.Log("[BoomboxSyncManager] Registered custom message handler: 'BoomboxPlay'.");
        }
        else
        {
            Debug.LogWarning("[BoomboxSyncManager] NetworkManager or CustomMessagingManager is null in Awake; message handler not registered.");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("BoomboxPlay");
            Debug.Log("[BoomboxSyncManager] Unregistered 'BoomboxPlay' handler.");
        }
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Called by the host when a boombox starts playing.
    /// Packs songName and boombox NetworkObjectId into a writer and sends to all clients.
    /// </summary>
    public void HandleBoomboxStart(BoomboxItem boombox)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[BoomboxSyncManager] NetworkManager.Singleton is null; cannot send message.");
            return;
        }

        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("[BoomboxSyncManager] HandleBoomboxStart called on non-host/server; ignoring.");
            return;
        }

        if (embeddedSongs.Count == 0)
        {
            Debug.LogWarning("[BoomboxSyncManager] No embedded songs to choose from.");
            return;
        }

        // pick a random song
        int chosenIndex = UnityEngine.Random.Range(0, embeddedSongs.Count);
        string chosenSongName = embeddedSongs[chosenIndex];
        ulong boomboxNetworkId = boombox.NetworkObjectId;

        Debug.Log($"[BoomboxSyncManager] Host picked song: '{chosenSongName}' (index {chosenIndex}) for Boombox ID {boomboxNetworkId}");

        // prepare a FastBufferWriter with enough space (string + ulong)
        using var writer = new FastBufferWriter(sizeof(ulong) + 512, Allocator.Temp);
        writer.WriteValueSafe(chosenSongName);
        writer.WriteValueSafe(boomboxNetworkId);

        // Convert connected client IDs to a list
        List<ulong> clientList = NetworkManager.Singleton.ConnectedClientsIds.ToList();
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            "BoomboxPlay",
            clientList,
            writer,
            NetworkDelivery.Reliable
        );

        Debug.Log("[BoomboxSyncManager] Sent BoomboxPlay message to all clients.");
    }

    /// <summary>
    /// Handler that runs on each client when they receive the "BoomboxPlay" message.
    /// Reads the songName and boomboxNetworkId, then begins loading/playing the clip.
    /// </summary>
    private void OnReceiveBoomboxPlayMessage(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string songName);
        reader.ReadValueSafe(out ulong boomboxNetworkId);

        Debug.Log($"[BoomboxSyncManager] Received BoomboxPlay message from {senderClientId}: songName='{songName}', boomboxID={boomboxNetworkId}");

        if (string.IsNullOrEmpty(songName) || !songName.StartsWith(resourcePrefix))
        {
            Debug.LogWarning($"[BoomboxSyncManager] Invalid songName received: '{songName}'");
            return;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[BoomboxSyncManager] NetworkManager not ready when receiving song. Skipping.");
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boomboxNetworkId, out NetworkObject netObj))
        {
            Debug.LogWarning($"[BoomboxSyncManager] Could not find NetworkObject with ID {boomboxNetworkId}");
            return;
        }

        if (!netObj.TryGetComponent(out BoomboxItem boombox))
        {
            Debug.LogWarning($"[BoomboxSyncManager] Spawned object ID {boomboxNetworkId} is not a BoomboxItem");
            return;
        }

        // Start coroutine to load & play
        boombox.StartCoroutine(PlayClipFromResource(boombox, songName));
    }

    /// <summary>
    /// Loads the embedded song into a cached file (if not already cached) and plays it.
    /// </summary>
    private IEnumerator PlayClipFromResource(BoomboxItem boombox, string songName)
    {
        // Create a safe filename: drop prefix, replace dots with underscores, add .ogg
        string safeName = songName.Replace(resourcePrefix, "").Replace('.', '_') + ".ogg";

        // Paths: <pluginFolder>/cached_songs/<safeName>
        string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string cacheFolder = Path.Combine(pluginFolder, "cached_songs");
        string fullPath = Path.Combine(cacheFolder, safeName);

        Debug.Log($"[BoomboxSyncManager] Preparing to load '{songName}' to '{fullPath}'");

        // Ensure cache folder exists
        try
        {
            if (!Directory.Exists(cacheFolder))
            {
                Directory.CreateDirectory(cacheFolder);
                Debug.Log($"[BoomboxSyncManager] Created cache folder at '{cacheFolder}'");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BoomboxSyncManager] Failed to create cache folder '{cacheFolder}': {e}");
            yield break;
        }

        // If file does not exist or is empty, extract from embedded resource
        if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
        {
            Debug.Log($"[BoomboxSyncManager] Cache miss. Extracting '{songName}' to '{fullPath}'");

            using Stream embeddedStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(songName);
            if (embeddedStream == null)
            {
                Debug.LogError($"[BoomboxSyncManager] Embedded resource stream was null for '{songName}'");
                yield break;
            }

            // Write to disk
            try
            {
                lock (this)
                {
                    using FileStream fs = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                    if (fs.Length == 0)
                    {
                        embeddedStream.CopyTo(fs);
                        Debug.Log($"[BoomboxSyncManager] Extracted '{safeName}' to cache.");
                    }
                    else
                    {
                        Debug.Log($"[BoomboxSyncManager] Cache file '{safeName}' already has data; skipping write.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoomboxSyncManager] Exception while writing cache file '{fullPath}': {e}");
                yield break;
            }
        }
        else
        {
            Debug.Log($"[BoomboxSyncManager] Cache hit. Skipping extraction for '{safeName}'");
        }

        // Load the AudioClip via UnityWebRequest
        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + fullPath, AudioType.OGGVORBIS);
        Debug.Log($"[BoomboxSyncManager] Sending UnityWebRequest for '{fullPath}'");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[BoomboxSyncManager] Error loading audio clip from '{fullPath}': {request.error}");
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip == null)
        {
            Debug.LogError($"[BoomboxSyncManager] DownloadHandler returned null AudioClip for '{fullPath}'");
            yield break;
        }

        clip.name = safeName;
        boombox.boomboxAudio.clip = clip;
        boombox.boomboxAudio.volume = 1f;                // max volume
        boombox.boomboxAudio.spatialBlend = 1f;          // fully 3D
        Debug.Log($"[BoomboxSyncManager] Previous audio maxDistance = {boombox.boomboxAudio.maxDistance}");
        boombox.boomboxAudio.maxDistance = 300f;         // players can hear from 300m
        boombox.boomboxAudio.rolloffMode = AudioRolloffMode.Custom;
        var curve = new AnimationCurve(
            new Keyframe(0f, 1f),       // 0m = full volume
            new Keyframe(30f, 0.6f),    // 30m = 50% volume
            new Keyframe(50f, 0.5f),    // 50m = 30% volume
            new Keyframe(300f, 0f)      // 300m = 0% volume
        );
        boombox.boomboxAudio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
        boombox.boomboxAudio.Play();
        Debug.Log($"[BoomboxSyncManager] Now playing cached song: '{safeName}'");
    }
}
