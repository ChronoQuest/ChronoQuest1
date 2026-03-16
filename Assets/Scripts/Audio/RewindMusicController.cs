using UnityEngine;

namespace TimeRewind
{
    /// <summary>
    /// Handles game music and ties it into the time rewind system.
    /// Music clips and settings are fully configurable from the inspector.
    /// </summary>
    public class RewindMusicController : MonoBehaviour
    {
        [Header("Music Source")]
        [Tooltip("AudioSource that plays the main game music.")]
        [SerializeField] private AudioSource musicSource;

        [Header("Music Tracks")]
        [Tooltip("Title screen music.")]
        [SerializeField] private AudioClip titleTrack;

        [Tooltip("Tutorial / early level music.")]
        [SerializeField] private AudioClip tutorialTrack;

        [Tooltip("Boss fight music.")]
        [SerializeField] private AudioClip bossFightTrack;

        [Header("Rewind Behaviour")]
        [Tooltip("Pitch used while rewinding. Negative values play audio backwards.")]
        [SerializeField] private float rewindPitch = -1f;

        [Tooltip("Whether to loop music while rewinding (recommended so reverse audio keeps playing).")]
        [SerializeField] private bool loopDuringRewind = true;

        private bool _isRewinding;
        private float _originalPitch = 1f;
        private bool _originalLoop;

        #region Unity Lifecycle

        private void Awake()
        {
            if (musicSource != null)
            {
                _originalPitch = musicSource.pitch;
                _originalLoop = musicSource.loop;
            }
        }

        private void OnEnable()
        {
            if (TimeRewindManager.Instance != null)
            {
                TimeRewindManager.Instance.OnRewindStart += HandleRewindStart;
                TimeRewindManager.Instance.OnRewindStop += HandleRewindStop;
            }
        }

        private void OnDisable()
        {
            if (TimeRewindManager.Instance != null)
            {
                TimeRewindManager.Instance.OnRewindStart -= HandleRewindStart;
                TimeRewindManager.Instance.OnRewindStop -= HandleRewindStop;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Plays the configured title music track, if assigned.
        /// </summary>
        public void PlayTitleMusic()
        {
            PlayTrack(titleTrack);
        }

        /// <summary>
        /// Plays the configured tutorial music track, if assigned.
        /// </summary>
        public void PlayTutorialMusic()
        {
            PlayTrack(tutorialTrack);
        }

        /// <summary>
        /// Plays the configured boss fight music track, if assigned.
        /// </summary>
        public void PlayBossFightMusic()
        {
            PlayTrack(bossFightTrack);
        }

        /// <summary>
        /// Directly play any clip provided from other systems.
        /// </summary>
        public void PlayCustomMusic(AudioClip clip, bool loop = true)
        {
            PlayTrack(clip, loop);
        }

        public void SetMusicSource(AudioSource source)
        {
            musicSource = source;

            if (musicSource != null)
            {
                _originalPitch = musicSource.pitch;
                _originalLoop = musicSource.loop;
            }
        }

        #endregion

        #region Rewind Handling

        private void HandleRewindStart()
        {
            if (musicSource == null || musicSource.clip == null)
                return;

            _isRewinding = true;

            // Cache original playback settings
            _originalPitch = musicSource.pitch;
            _originalLoop = musicSource.loop;

            // Ensure we start from the current position and play backwards.
            // Negative pitch is supported by Unity and will play the clip in reverse.
            musicSource.loop = loopDuringRewind;

            // Workaround for Unity quirk: make sure time is > 0 when using negative pitch.
            if (musicSource.time <= 0f)
            {
                musicSource.time = Mathf.Max(0.01f, musicSource.clip.length - 0.01f);
            }

            musicSource.pitch = Mathf.Min(rewindPitch, -0.01f);

            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        private void HandleRewindStop()
        {
            if (musicSource == null || musicSource.clip == null)
            {
                _isRewinding = false;
                return;
            }

            _isRewinding = false;

            // Keep the music at whatever point we rewound to, but resume playing forward.
            float resumeTime = Mathf.Clamp(musicSource.time, 0f, musicSource.clip.length);

            musicSource.pitch = _originalPitch;
            musicSource.loop = _originalLoop;

            // Restart playback from the rewound position going forward.
            musicSource.Stop();
            musicSource.time = resumeTime;
            musicSource.Play();
        }

        #endregion

        #region Internal Helpers

        private void PlayTrack(AudioClip clip, bool loop = true)
        {
            if (musicSource == null || clip == null)
                return;

            // If we were in a rewinding state, reset to normal playback.
            _isRewinding = false;

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.pitch = _originalPitch;
            musicSource.time = 0f;
            musicSource.Play();
        }

        private void PlayTrack(AudioClip clip)
        {
            PlayTrack(clip, true);
        }

        #endregion
    }
}

