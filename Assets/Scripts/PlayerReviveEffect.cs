using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections;
using TimeRewind;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerReviveEffect : MonoBehaviour
{
    [Header("Hitstop")]
    [SerializeField] private float hitstopDuration = 0.12f;

    [Header("Slow Motion")]
    // This effect is primarily used for "rewind out of death" and should feel heavy/intentional.
    [SerializeField] private float slowMotionScale = 0.015f;
    [SerializeField] private float slowMotionDuration = 3.0f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeMagnitude = 0.2f;
    [SerializeField] private float launchShakeDuration = 0.2f;
    [SerializeField] private float launchShakeMagnitude = 0.3f;

    [Header("Revive Shockwave")]
    [SerializeField] private float shockwaveRadius = 6f;
    [SerializeField] private float shockwaveForce = 20f;

    [Header("Sprite Flash")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashFadeDuration = 0.3f;

    [Header("Scale Pulse")]
    [SerializeField] private float scalePeak = 1.2f;
    [SerializeField] private float scalePulseDuration = 0.35f;

    [Header("Ghost Afterimages")]
    [SerializeField] private int ghostCount = 8;
    [SerializeField] private float ghostSpacing = 0.06f;
    [SerializeField] private float ghostFadeDuration = 0.4f;
    [SerializeField] private Color ghostTint = new Color(0.6f, 0.85f, 1f, 0.6f);

    [Header("Gamepad Rumble")]
    [SerializeField] private float rumbleLowFreq = 0.6f;
    [SerializeField] private float rumbleHighFreq = 0.9f;
    [SerializeField] private float rumbleDuration = 0.3f;

    [Header("Post-Revive Invincibility")]
    [SerializeField] private float invincibilityDuration = 1.5f;
    [SerializeField] private float invincibilityFlashSpeed = 0.08f;

    [Header("Safety Heal")]
    [SerializeField] private int healOnRevive = 1;

    [Header("Post-Revive Launch")]
    [SerializeField] private float launchImpulse = 4f;

    [Header("Movement Lock")]
    [SerializeField] private float movementLockDuration = 0.25f;

    [Header("Sound Effect")]
    [SerializeField] private AudioClip reviveSFX;
    [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private AudioSource reviveAudioSource;

    private SpriteRenderer _sprite;
    private Animator _animator;
    private Rigidbody2D _rb;
    private Color _originalColor;
    private Vector3 _originalScale;
    private bool _isReviving;
    private const float NormalTimeScale = 1f;

    public bool IsReviving => _isReviving;
    public event Action OnReviveComplete;

    private void Awake()
    {
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _animator = GetComponentInChildren<Animator>();
        _rb = GetComponent<Rigidbody2D>();

        if (reviveAudioSource == null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                reviveAudioSource = cam.GetComponent<AudioSource>();
                if (reviveAudioSource == null)
                {
                    reviveAudioSource = cam.gameObject.AddComponent<AudioSource>();
                }
            }
            else
            {
                reviveAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (reviveAudioSource != null)
        {
            reviveAudioSource.spatialBlend = 0f; // 2D sound
            reviveAudioSource.playOnAwake = false;
        }
    }

    public void Play()
    {
        if (_isReviving) return;
        StartCoroutine(ReviveSequence());
    }

    public void Cancel()
    {
        if (!_isReviving) return;
        StopAllCoroutines();
        Cleanup();
    }

    private IEnumerator ReviveSequence()
    {
        _isReviving = true;
        _originalColor = _sprite != null ? _sprite.color : Color.white;
        _originalScale = transform.localScale;

        var health = GetComponent<PlayerHealth>();
        health.SetInvincible(true);
        if (healOnRevive > 0)
        {
            health.ModifyHealth(healOnRevive);
        }

        var movement = GetComponent<PlayerPlatformer>();
        if (movement != null)
            movement.TriggerKnockbackLock(movementLockDuration);

        // Hitstop: do not read Time.timeScale as a "restore" target (after rewind exit it is the
        // post-rewind slow value from TimeRewindManager, e.g. 0.08). The revive slow-mo ramps to normal 1f.
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(hitstopDuration);

        CameraShake shaker = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        if (shaker != null)
        {
            shaker.FlashWhite();
            shaker.Shake(shakeDuration, shakeMagnitude);
        }

        if (shockwaveRadius > 0f && shockwaveForce > 0f)
        {
            int enemyMask = LayerMask.GetMask("Enemy");
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, shockwaveRadius, enemyMask);
            foreach (var hit in hits)
            {
                var rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    Vector2 dir = (rb.worldCenterOfMass - (Vector2)transform.position).normalized;
                    rb.AddForce(dir * shockwaveForce, ForceMode2D.Impulse);
                }
            }
        }

        if (reviveSFX != null && reviveAudioSource != null && !reviveSFX.ambisonic)
        {
            reviveAudioSource.PlayOneShot(reviveSFX, sfxVolume);
        }

        StartCoroutine(GamepadRumble());

        if (_sprite != null)
            StartCoroutine(SpawnGhosts());

        float baselineFixed = GetBaselineFixedDelta();
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = baselineFixed * slowMotionScale;
        float slowElapsed = 0f;
        while (slowElapsed < slowMotionDuration)
        {
            slowElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(slowElapsed / slowMotionDuration);
            float scale = Mathf.Lerp(slowMotionScale, NormalTimeScale, EaseOutQuad(t));
            Time.timeScale = scale;
            Time.fixedDeltaTime = baselineFixed * scale;

            if (_sprite != null && slowElapsed <= flashFadeDuration)
            {
                float ft = Mathf.Clamp01(slowElapsed / flashFadeDuration);
                _sprite.color = Color.Lerp(flashColor, _originalColor, ft);
            }
            if (slowElapsed <= scalePulseDuration)
            {
                float st = Mathf.Clamp01(slowElapsed / scalePulseDuration);
                float s = Mathf.LerpUnclamped(scalePeak, 1f, EaseOutBack(st));
                transform.localScale = _originalScale * s;
            }

            yield return null;
        }

        Time.timeScale = NormalTimeScale;
        Time.fixedDeltaTime = baselineFixed;
        if (_sprite != null) _sprite.color = _originalColor;
        transform.localScale = _originalScale;

        if (_rb != null)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
            _rb.AddForce(Vector2.up * launchImpulse, ForceMode2D.Impulse);
        }

        if (_animator != null)
        {
            // Prefer a dedicated revive animation if present, otherwise fire Jump to reuse jump animation
            if (!TrySetTrigger("Revive"))
            {
                TrySetTrigger("Jump");
            }
        }

        // Extra impact shake on launch
        CameraShake launchShaker = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        if (launchShaker != null)
        {
            launchShaker.Shake(launchShakeDuration, launchShakeMagnitude);
        }

        // -- INVINCIBILITY FLASH --
        if (_sprite != null)
        {
            float invElapsed = 0f;
            while (invElapsed < invincibilityDuration)
            {
                _sprite.enabled = false;
                yield return new WaitForSeconds(invincibilityFlashSpeed);
                _sprite.enabled = true;
                yield return new WaitForSeconds(invincibilityFlashSpeed);
                invElapsed += invincibilityFlashSpeed * 2f;
            }
            _sprite.enabled = true;
        }

        health.SetInvincible(false);
        _isReviving = false;
        OnReviveComplete?.Invoke();
    }

    private IEnumerator SpawnGhosts()
    {
        for (int i = 0; i < ghostCount; i++)
        {
            var ghostGO = new GameObject("ReviveGhost");
            ghostGO.transform.position = transform.position;
            ghostGO.transform.rotation = transform.rotation;
            ghostGO.transform.localScale = transform.lossyScale;

            var ghostSR = ghostGO.AddComponent<SpriteRenderer>();
            ghostSR.sprite = _sprite.sprite;
            ghostSR.flipX = _sprite.flipX;
            ghostSR.flipY = _sprite.flipY;
            ghostSR.sortingLayerID = _sprite.sortingLayerID;
            ghostSR.sortingOrder = _sprite.sortingOrder - 1;
            ghostSR.color = ghostTint;

            StartCoroutine(FadeAndDestroy(ghostSR, ghostFadeDuration));

            yield return new WaitForSecondsRealtime(ghostSpacing);
        }
    }

    private static IEnumerator FadeAndDestroy(SpriteRenderer sr, float duration)
    {
        Color startColor = sr.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            sr.color = c;
            sr.transform.localScale *= 1f + (0.3f * Time.unscaledDeltaTime);
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    private IEnumerator GamepadRumble()
    {
        if (Gamepad.current == null) yield break;
        Gamepad.current.SetMotorSpeeds(rumbleLowFreq, rumbleHighFreq);
        yield return new WaitForSecondsRealtime(rumbleDuration);
        if (Gamepad.current != null)
            Gamepad.current.SetMotorSpeeds(0f, 0f);
    }

    private void Cleanup()
    {
        Time.timeScale = NormalTimeScale;
        Time.fixedDeltaTime = GetBaselineFixedDelta();

        if (_sprite != null)
        {
            _sprite.enabled = true;
            _sprite.color = _originalColor;
        }
        transform.localScale = _originalScale;

        if (Gamepad.current != null)
            Gamepad.current.SetMotorSpeeds(0f, 0f);

        var health = GetComponent<PlayerHealth>();
        if (health != null) health.SetInvincible(false);

        _isReviving = false;
    }

    private bool TrySetTrigger(string triggerName)
    {
        foreach (var param in _animator.parameters)
        {
            if (param.name == triggerName && param.type == AnimatorControllerParameterType.Trigger)
            {
                _animator.SetTrigger(triggerName);
                return true;
            }
        }

        return false;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }

    private static float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private static float GetBaselineFixedDelta()
    {
        if (TimeRewindManager.Instance != null)
            return TimeRewindManager.Instance.BaselineFixedDeltaTime;
        return 0.02f;
    }
}
