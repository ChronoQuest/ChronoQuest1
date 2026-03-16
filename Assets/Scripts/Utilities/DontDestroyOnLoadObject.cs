using UnityEngine;

/// <summary>
/// Keeps this GameObject alive across scene loads.
/// Attach this to your MusicManager so the music system persists.
/// </summary>
public class DontDestroyOnLoadObject : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}

