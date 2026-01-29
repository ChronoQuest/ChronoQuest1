using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HeartIcon : MonoBehaviour
{
    [Header("References")]
    public Image heartImage;

    [Header("Animation Settings")]
    [Tooltip("Time between frames. Lower is faster.")]
    public float animationSpeed = 0.05f; 

    // Assumes: 0 = Full, 1 = 3/4, 2 = Half, 3 = 1/4, 4 = Empty
    private Sprite[] animationFrames; 
    private Coroutine currentRoutine;

    // We initialize as 'false' (Empty) so that when the game starts, 
    // the first status update forces them to animate to 'true' (Full).
    private bool isFull = false; 

    public void Setup(Sprite[] frames)
    {
        animationFrames = frames;
        // Start invisible/empty so we can "grow" into existence
        heartImage.sprite = frames[frames.Length - 1]; 
        isFull = false;
    }

    public void SetHeartState(bool active)
    {
        // If the state is already what we want, do nothing
        if (active == isFull) return;

        isFull = active;

        // Stop any currently running animation so we don't glitch out
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        // Start the new animation (Growing or Shrinking)
        currentRoutine = StartCoroutine(AnimateHeart(active));
    }

    private IEnumerator AnimateHeart(bool fillingUp)
    {
        // HEAL / START: Iterate backwards from Empty (4) to Full (0)
        // DAMAGE: Iterate forwards from Full (0) to Empty (4)
        
        int startFrame = fillingUp ? animationFrames.Length - 1 : 0;
        int endFrame = fillingUp ? 0 : animationFrames.Length - 1;
        int step = fillingUp ? -1 : 1;

        // We use a simple loop logic that handles both directions
        if (fillingUp)
        {
            for (int i = startFrame; i >= endFrame; i += step)
            {
                heartImage.sprite = animationFrames[i];
                yield return new WaitForSeconds(animationSpeed);
            }
        }
        else
        {
            for (int i = startFrame; i <= endFrame; i += step)
            {
                heartImage.sprite = animationFrames[i];
                yield return new WaitForSeconds(animationSpeed);
            }
        }
    }
}