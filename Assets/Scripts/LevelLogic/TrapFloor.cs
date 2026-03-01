using UnityEngine;
using System.Collections;
using TimeRewind; // uses your namespace

public class TrapFloor : MonoBehaviour, IRewindable
{
    [Header("Configuration")]
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeIntensity = 0.05f;
    [SerializeField] private float restoreDelay = 5f; // Optional auto-fix if not rewinding

    [Header("References")]
    [Tooltip("The Tilemap Collider that holds the player up")]
    [SerializeField] private Collider2D physicsCollider; 
    [Tooltip("The Tilemap Renderer (to hide visual)")]
    [SerializeField] private Renderer tilemapRenderer;
    [Tooltip("Optional particles when breaking")]
    [SerializeField] private ParticleSystem breakParticles;

    private bool _isBroken = false;
    private bool _isShaking = false;
    private Vector3 _originalPos;
    private Coroutine _breakRoutine;

    private void Start()
    {
        // Register this object to the rewind system
        TimeRewindManager.Instance.Register(this);
        _originalPos = transform.localPosition;
    }

    private void OnDestroy()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Unregister(this);
    }

    // Call this from the Child Trigger's script
    public void TriggerBreak()
    {
        if (_isBroken || _isShaking) return;
        
        // If we are currently rewinding, don't trigger new breaks
        if (TimeRewindManager.Instance.IsRewinding) return;

        _breakRoutine = StartCoroutine(BreakSequence());
    }

    private IEnumerator BreakSequence()
    {
        _isShaking = true;
        float timer = 0f;

        // 1. Shake
        while (timer < shakeDuration)
        {
            // If rewind starts mid-shake, stop coroutine
            if (TimeRewindManager.Instance.IsRewinding) yield break;

            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;
            transform.localPosition = _originalPos + new Vector3(x, y, 0);
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 2. Break
        transform.localPosition = _originalPos; // Reset position
        SetBrokenState(true);
        _isShaking = false;
        
        // 3. Optional Auto-Restore (if game logic requires it)
        // yield return new WaitForSeconds(restoreDelay);
        // if (!TimeRewindManager.Instance.IsRewinding) SetBrokenState(false);
    }

    private void SetBrokenState(bool broken)
    {
        _isBroken = broken;
        
        // Disable visuals and physics
        physicsCollider.enabled = !broken;
        tilemapRenderer.enabled = !broken;

        if (broken && breakParticles != null)
        {
             breakParticles.Play();
        }
    }

    // ====================================================
    // REWIND IMPLEMENTATION
    // ====================================================

    public void OnStartRewind()
    {
        // Stop the breaking coroutine if it's running so it doesn't conflict
        if (_breakRoutine != null) StopCoroutine(_breakRoutine);
        _isShaking = false;
        transform.localPosition = _originalPos;
    }

    public void OnStopRewind()
    {
        // Nothing special needed here
    }

    public RewindState CaptureState()
    {
        // We only need to know if we are broken or not. 
        // Position is static (unless shaking), but we reset shake on rewind anyway.
        var state = RewindState.Create(transform.position, transform.rotation, Time.time);
        
        // Store our custom flag
        state.SetCustomData("TrapBroken", _isBroken);
        
        return state;
    }

    public void ApplyState(RewindState state)
    {
        // Retrieve the boolean. Default to false if not found.
        bool wasBroken = state.GetCustomData<bool>("TrapBroken", false);

        // Only update if the state has changed to save performance
        if (_isBroken != wasBroken)
        {
            SetBrokenState(wasBroken);
        }
    }
}