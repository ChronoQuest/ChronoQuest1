using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SlimeAudio : MonoBehaviour
{
    [Header("Idle Sounds")]
    [SerializeField] private AudioClip[] idleClips;
    
    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float idleVolume = 0.4f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpVolume = 0.8f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayIdleSquish()
    {
        if (idleClips != null && idleClips.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, idleClips.Length);
            audioSource.PlayOneShot(idleClips[randomIndex], idleVolume);
        }
    }
    public void PlayJumpSquish()
    {
        if (idleClips != null && idleClips.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, idleClips.Length);
            audioSource.PlayOneShot(idleClips[randomIndex], jumpVolume);
        }
    }
}