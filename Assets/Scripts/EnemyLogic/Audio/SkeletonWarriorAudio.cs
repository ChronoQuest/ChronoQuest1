using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SkeletonWarriorAudio : MonoBehaviour
{
    [Header("Idle Sounds")]
    [SerializeField] private AudioClip[] idleClips;
    [SerializeField] private AudioClip attack;
    
    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float idleVolume = 0.6f;
    [Range(0f, 1f)]
    [SerializeField] private float attackVolume = 0.6f;


    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayIdle()
    {
        if (idleClips != null && idleClips.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, idleClips.Length);
            audioSource.PlayOneShot(idleClips[randomIndex], idleVolume);
        }
    }

    public void PlayAttack()
    {
        if(attack != null){
            audioSource.PlayOneShot(attack, attackVolume);
        }
    }

}