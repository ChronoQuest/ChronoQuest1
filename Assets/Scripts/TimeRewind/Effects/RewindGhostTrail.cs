using UnityEngine;
using System.Collections.Generic;

namespace TimeRewind
{
    /// <summary>
    /// Spawns fading afterimage sprites behind the player during rewind
    /// to visually convey the path being retraced.
    /// Attach to the Player GameObject alongside PlayerRewindController.
    /// </summary>
    [RequireComponent(typeof(PlayerRewindController))]
    public class RewindGhostTrail : MonoBehaviour
    {
        [Header("Spawn Timing")]
        [Tooltip("Seconds (unscaled) between ghost spawns during rewind")]
        [SerializeField] private float spawnInterval = 0.045f;

        [Header("Post-Rewind Ghosts")]
        [Tooltip("Seconds (unscaled) between ghost spawns during post-rewind slow-mo")]
        [SerializeField] private float postRewindSpawnInterval = 0.07f;
        [Tooltip("Ghost lifetime during the post-rewind slow-mo phase")]
        [SerializeField] private float postRewindGhostLifetime = 0.5f;
        [Tooltip("Starting color/alpha of ghosts during post-rewind slow-mo")]
        [SerializeField] private Color postRewindGhostColor = new Color(0.6f, 0.85f, 1f, 0.4f);

        [Header("Ghost Appearance")]
        [Tooltip("How long each ghost frame lingers before fully fading")]
        [SerializeField] private float ghostLifetime = 0.35f;

        [Tooltip("Starting color/alpha of each ghost")]
        [SerializeField] private Color ghostStartColor = new Color(0.35f, 0.65f, 1f, 0.7f);

        [Tooltip("Color the ghost fades to before being recycled")]
        [SerializeField] private Color ghostEndColor = new Color(0.35f, 0.65f, 1f, 0f);

        [Tooltip("Sorting-order offset relative to the player sprite (positive = in front)")]
        [SerializeField] private int ghostSortingOffset = 1;

        [Header("Limits")]
        [SerializeField] private int maxActiveGhosts = 60;

        [Header("Post-Rewind Path Ghosts")]
        [Tooltip("How long each path ghost persists after rewind ends (seconds, unscaled)")]
        [SerializeField] private float pathGhostLifetime = 0.8f;
        [Tooltip("Starting color/alpha of path ghosts")]
        [SerializeField] private Color pathGhostStartColor = new Color(0.4f, 0.7f, 1f, 0.65f);
        [Tooltip("Seconds (unscaled) between position samples recorded during rewind")]
        [SerializeField] private float pathSampleInterval = 0.12f;
        [Tooltip("Maximum number of path ghosts spawned when rewind ends")]
        [SerializeField] private int maxPathGhosts = 30;

        private SpriteRenderer _playerSprite;
        private PlayerRewindController _rewindController;
        private float _spawnTimer;
        private float _pathSampleTimer;
        private bool _isRewinding;
        private bool _isInPostRewindSlow;

        private readonly List<GhostFrame> _active = new List<GhostFrame>();
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();

        private struct PathSample
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Sprite sprite;
            public bool flipX;
        }

        private readonly List<PathSample> _pathSamples = new List<PathSample>();

        private class GhostFrame
        {
            public GameObject go;
            public SpriteRenderer sr;
            public float age;
            public float lifetime;
            public Color startColor;
        }

        private void Awake()
        {
            _playerSprite = GetComponent<SpriteRenderer>()
                ?? GetComponentInChildren<SpriteRenderer>();
            _rewindController = GetComponent<PlayerRewindController>();
        }

        private void OnEnable()
        {
            if (_rewindController != null)
            {
                _rewindController.OnRewindStarted += HandleRewindStarted;
                _rewindController.OnRewindStopped += HandleRewindStopped;
            }
        }

        private void OnDisable()
        {
            if (_rewindController != null)
            {
                _rewindController.OnRewindStarted -= HandleRewindStarted;
                _rewindController.OnRewindStopped -= HandleRewindStopped;
            }
        }

        private void HandleRewindStarted()
        {
            _isRewinding = true;
            _isInPostRewindSlow = false;
            _spawnTimer = 0f;
            _pathSampleTimer = 0f;
            _pathSamples.Clear();
        }

        private void HandleRewindStopped()
        {
            _isRewinding = false;
            _isInPostRewindSlow = true;
            _spawnTimer = 0f;
            _pathSamples.Clear();
        }

        private void Update()
        {
            if (_isRewinding)
            {
                _spawnTimer -= Time.unscaledDeltaTime;
                if (_spawnTimer <= 0f)
                {
                    SpawnGhost(ghostLifetime, ghostStartColor);
                    _spawnTimer = spawnInterval;
                }

                _pathSampleTimer -= Time.unscaledDeltaTime;
                if (_pathSampleTimer <= 0f)
                {
                    RecordPathSample();
                    _pathSampleTimer = pathSampleInterval;
                }
            }
            else if (_isInPostRewindSlow)
            {
                if (Time.timeScale >= 0.95f)
                    _isInPostRewindSlow = false;
            }

            UpdateGhosts();
        }

        private void UpdateGhosts()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                GhostFrame ghost = _active[i];
                ghost.age += Time.unscaledDeltaTime;

                float t = ghost.age / ghost.lifetime;
                if (t >= 1f)
                {
                    ReturnToPool(ghost.go);
                    _active.RemoveAt(i);
                }
                else
                {
                    ghost.sr.color = Color.Lerp(ghost.startColor, ghostEndColor, t);
                }
            }
        }

        private void SpawnGhost(float lifetime, Color startColor)
        {
            if (_playerSprite == null || _playerSprite.sprite == null)
                return;

            while (_active.Count >= maxActiveGhosts)
            {
                ReturnToPool(_active[0].go);
                _active.RemoveAt(0);
            }

            GameObject ghostObj = GetFromPool();
            Transform spriteTransform = _playerSprite.transform;
            ghostObj.transform.position = spriteTransform.position;
            ghostObj.transform.rotation = spriteTransform.rotation;
            ghostObj.transform.localScale = spriteTransform.lossyScale;

            SpriteRenderer sr = ghostObj.GetComponent<SpriteRenderer>();
            sr.sprite = _playerSprite.sprite;
            sr.material = _playerSprite.sharedMaterial;
            sr.flipX = _playerSprite.flipX;
            sr.flipY = _playerSprite.flipY;
            sr.color = startColor;
            sr.sortingLayerID = _playerSprite.sortingLayerID;
            sr.sortingOrder = _playerSprite.sortingOrder + ghostSortingOffset;

            ghostObj.SetActive(true);

            _active.Add(new GhostFrame
            {
                go = ghostObj,
                sr = sr,
                age = 0f,
                lifetime = lifetime,
                startColor = startColor
            });
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0)
            {
                GameObject pooled = _pool.Dequeue();
                return pooled;
            }

            var go = new GameObject("RewindGhost");
            go.AddComponent<SpriteRenderer>();
            return go;
        }

        private void ReturnToPool(GameObject go)
        {
            go.SetActive(false);
            _pool.Enqueue(go);
        }

        private void RecordPathSample()
        {
            if (_playerSprite == null || _playerSprite.sprite == null)
                return;

            Transform t = _playerSprite.transform;
            _pathSamples.Add(new PathSample
            {
                position = t.position,
                rotation = t.rotation,
                scale    = t.lossyScale,
                sprite   = _playerSprite.sprite,
                flipX    = _playerSprite.flipX
            });
        }

        private void SpawnPathGhosts()
        {
            if (_pathSamples.Count == 0)
                return;

            int step = Mathf.Max(1, _pathSamples.Count / maxPathGhosts);
            int spawned = 0;

            for (int i = 0; i < _pathSamples.Count && spawned < maxPathGhosts; i += step)
            {
                PathSample sample = _pathSamples[i];
                float pathT = (float)i / Mathf.Max(1, _pathSamples.Count - 1);

                Color color = pathGhostStartColor;
                color.a *= Mathf.Lerp(0.5f, 1f, pathT);

                SpawnGhostAt(sample, pathGhostLifetime, color);
                spawned++;
            }

            _pathSamples.Clear();
        }

        private void SpawnGhostAt(PathSample sample, float lifetime, Color startColor)
        {
            while (_active.Count >= maxActiveGhosts)
            {
                ReturnToPool(_active[0].go);
                _active.RemoveAt(0);
            }

            GameObject ghostObj = GetFromPool();
            ghostObj.transform.position   = sample.position;
            ghostObj.transform.rotation   = sample.rotation;
            ghostObj.transform.localScale = sample.scale;

            SpriteRenderer sr = ghostObj.GetComponent<SpriteRenderer>();
            sr.sprite         = sample.sprite;
            sr.flipX          = sample.flipX;
            sr.flipY          = false;
            sr.color          = startColor;

            if (_playerSprite != null)
            {
                sr.material       = _playerSprite.sharedMaterial;
                sr.sortingLayerID = _playerSprite.sortingLayerID;
                sr.sortingOrder   = _playerSprite.sortingOrder + ghostSortingOffset;
            }

            ghostObj.SetActive(true);

            _active.Add(new GhostFrame
            {
                go        = ghostObj,
                sr        = sr,
                age       = 0f,
                lifetime  = lifetime,
                startColor = startColor
            });
        }

        private void OnDestroy()
        {
            foreach (GhostFrame ghost in _active)
            {
                if (ghost.go != null) Destroy(ghost.go);
            }
            _active.Clear();

            while (_pool.Count > 0)
            {
                GameObject go = _pool.Dequeue();
                if (go != null) Destroy(go);
            }
        }
    }
}
