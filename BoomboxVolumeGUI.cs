using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class BoomboxVolumeGUI : MonoBehaviour
{
    public static BoomboxVolumeGUI Instance { get; private set; }

    // 0–100 volume
    public float Volume => volume;
    private float volume = 100f;

    // F9 key control
    private KeyControl f9Key;

    // GUI state
    private bool isOpen = false;
    private Rect windowRect = new Rect(200, 200, 260, 120);

    // Remember cursor state
    private CursorLockMode prevLock;
    private bool prevVisible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        f9Key = Keyboard.current?.f9Key;
    }

    private void Update()
    {
        if (f9Key == null)
            f9Key = Keyboard.current?.f9Key;
        if (f9Key == null)
            return;

        if (f9Key.wasPressedThisFrame)
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                prevLock    = Cursor.lockState;
                prevVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else
            {
                Cursor.lockState = prevLock;
                Cursor.visible   = prevVisible;
            }
        }
    }

    private void OnGUI()
    {
        if (!isOpen) return;

        // Draw window; returns updated rect after you drag it
        windowRect = GUI.Window(
            6969,               // window ID
            windowRect,         // clientRect
            DrawWindowContents, // function to render inside
            "Boombox Volume"    // window title
        );
    }

    private void DrawWindowContents(int windowID)
    {
        GUILayout.BeginVertical();

        GUILayout.Label($"Volume: {(int)volume}%");

        float newVol = GUILayout.HorizontalSlider(volume, 0f, 100f);
        if (!Mathf.Approximately(newVol, volume))
            volume = newVol;

        GUILayout.Space(10);

        if (GUILayout.Button("Close"))
        {
            isOpen = false;
            Cursor.lockState = prevLock;
            Cursor.visible   = prevVisible;
        }

        GUILayout.EndVertical();

        // Allow dragging the window by its top 20 px
        GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));
    }
}
