using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TimeRewind; // Needed to check if player started rewinding

public class PlayerSafetyNet : MonoBehaviour
{
    [Header("Safety Settings")]
    [Tooltip("Select your Ground Layer here.")]
    [SerializeField] private LayerMask groundLayer;
    
    [Tooltip("The tag you put on your Trap objects.")]
    [SerializeField] private string unsafeTag = "Trap"; 
    private string bossTag = "Boss"; 

    [Tooltip("How long you must be on safe ground before it saves")]
    [SerializeField] private float recordInterval = 0.05f; 

    [Header("Respawn Settings")]
    [Tooltip("Time to wait before teleporting (gives player chance to rewind)")]
    [SerializeField] public float respawnDelay = 1.0f; // NEW SETTING

    [Header("Detection Box")]
    [SerializeField] private float boxWidth = 0.5f;
    [SerializeField] private float boxHeight = 0.2f;
    [SerializeField] private Vector2 offset = new Vector2(0f, -0.6f);

    private Vector3 _lastSafePosition;
    private List<Vector3> safePositions = new List<Vector3>();
    private float minDistanceBetweenPoints = 1.0f;
    private int maxLen = 5;
    private float _safeTimer;
    
    // References
    private Rigidbody2D _rb;
    private PlayerHealth _health;
    private bool _isRespawning;
    private Coroutine _respawnRoutine; // Store reference to cancel it

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _health = GetComponent<PlayerHealth>();
        _lastSafePosition = transform.position;
        safePositions.Add(_lastSafePosition);
    }

    private void FixedUpdate()
    {
        if (_health.IsDead || _isRespawning) return;
        
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding) return;

        if (IsCurrentlySafe())
        {
            _safeTimer += Time.fixedDeltaTime;
            
            if (_safeTimer >= recordInterval)
            {
                Vector3 currentSafeSpot = transform.position + Vector3.up * 0.1f;
                _lastSafePosition = currentSafeSpot;
                
                if (safePositions.Count == 0 || Vector2.Distance(currentSafeSpot, safePositions[safePositions.Count - 1]) >= minDistanceBetweenPoints)
                {
                    safePositions.Add(currentSafeSpot);
                    
                    if (safePositions.Count > maxLen)
                    {
                        safePositions.RemoveAt(0); 
                    }
                }
                
                _safeTimer = 0f; // Reset timer
            }
        }
        else
        {
            _safeTimer = 0f;
        }
    }

    private bool IsCurrentlySafe()
    {
        Vector2 center = (Vector2)transform.position + offset;
        Vector2 size = new Vector2(boxWidth, boxHeight);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, groundLayer);
        bool foundSolidGround = false;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.CompareTag(unsafeTag)) return false;
            foundSolidGround = true;
        }

        if (!foundSolidGround) return false;

        // Belt-and-braces: spikes (SpikeDamage) and traps (TrapDamage) live on a hazard
        // layer the feet-mask can't see and aren't tagged Trap, so the feet check above
        // misses them. Make sure the player's body isn't currently overlapping any of
        // those before we record this spot — otherwise we'd memorise a position that
        // teleports the player back into the spike they just fell on.
        if (IsHazardOverlapping((Vector2)transform.position)) return false;

        return true;
    }

    private static readonly Collider2D[] _hazardOverlapBuffer = new Collider2D[16];

    /// <summary>True if any SpikeDamage / TrapDamage collider overlaps a player-sized
    /// box at <paramref name="worldPos"/>. Layer-agnostic — uses the component, not the
    /// tag, since hazards in this project are untagged.</summary>
    private bool IsHazardOverlapping(Vector2 worldPos)
    {
        // Slightly larger than the player's collider so a candidate that's flush
        // against a spike still counts as unsafe.
        Vector2 bodySize = new Vector2(0.6f, 1.2f);
        int hitCount = Physics2D.OverlapBoxNonAlloc(worldPos, bodySize, 0f, _hazardOverlapBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var c = _hazardOverlapBuffer[i];
            if (c == null) continue;
            if (c.gameObject == gameObject) continue;
            if (c.CompareTag(unsafeTag)) return true;
            if (c.GetComponent<SpikeDamage>() != null) return true;
            if (c.GetComponent<TrapDamage>() != null) return true;
        }
        return false;
    }

    public void RespawnAtSafety()
    {
        if (_isRespawning || _health.IsDead) return;
        
        // Start the delayed respawn
        _respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    // Call this if the player presses Rewind manually to cancel the pending respawn
    public void CancelRespawn()
    {
        if (_isRespawning && _respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
            
            // Re-enable physics if we disabled them
            if (_rb != null) 
            {
                _rb.simulated = true;
                _rb.linearVelocity = Vector2.zero;
            }
            
            _isRespawning = false;
        }
    }

private IEnumerator RespawnRoutine()
    {
        _isRespawning = true;

        if (_rb != null) 
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false; 
        }

        float timer = 0f;
        while (timer < respawnDelay)
        {
            timer += Time.deltaTime;
            if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            {
                if (_rb != null) _rb.simulated = true;
                _isRespawning = false;
                yield break; 
            }
            yield return null;
        }

        transform.position = GetClearRespawnPosition();
        // Force the physics engine to refresh trigger overlap state from the new
        // transform. Without this, if the new position happens to overlap a hazard,
        // OnTriggerEnter2D may not fire on re-enabling simulation, leaving the player
        // visually stuck inside the spike with no second teleport attempt.
        Physics2D.SyncTransforms();

        yield return new WaitForSeconds(0.1f);
        if (_rb != null) _rb.simulated = true;
        _isRespawning = false;
    }
    private Vector3 GetClearRespawnPosition()
    {
        if (IsCandidatePositionValid(_lastSafePosition)) return _lastSafePosition;

        for (int i = safePositions.Count - 1; i >= 0; i--)
        {
            if (IsCandidatePositionValid(safePositions[i]))
            {
                return safePositions[i];
            }
        }

        // No recorded position is currently safe (e.g. all the platforms the player
        // crossed have since fallen, or every saved spot is now flush with a spike).
        // Lift the player straight up from their current position to break out of any
        // hazard they're sitting in. Step upwards until we find clear air.
        Vector3 here = transform.position;
        for (float lift = 2f; lift <= 8f; lift += 1f)
        {
            Vector3 candidate = here + Vector3.up * lift;
            if (IsCandidatePositionValid(candidate)) return candidate;
        }

        GameObject boss = GameObject.FindGameObjectWithTag(bossTag);
        if (boss != null)
        {
            float ejectionDistance = 6f; // How far to shove them sideways
            float pushDirection = transform.position.x < boss.transform.position.x ? ejectionDistance : -ejectionDistance;
            return _lastSafePosition + new Vector3(pushDirection, 1f, 0f);
        }
        return _lastSafePosition;
    }

    /// <summary>A candidate respawn position is usable only if it's clear of the boss
    /// AND not overlapping any hazard collider (spikes / traps).</summary>
    private bool IsCandidatePositionValid(Vector3 pos)
    {
        if (!IsPositionClearOfBoss(pos)) return false;
        if (IsHazardOverlapping(pos)) return false;
        return true;
    }
    private bool IsPositionClearOfBoss(Vector3 pos)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, 2f);
        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag(bossTag)) return false;
        }
        return true;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector2 center = (Vector2)transform.position + offset;
        Vector3 size = new Vector3(boxWidth, boxHeight, 1f);
        Gizmos.DrawWireCube(center, size);
    }
}