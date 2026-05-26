using System.Collections.Generic;
using UnityEngine;

// Made by: Jason Lodge
// Summary: SFX script, stores a list of sounds and can play random sounds, sounds by name, sounds by list order, and chained sounds.

public class AUD_SFX : MonoBehaviour
{
    [System.Serializable]
    public class Sound
    {
        [Tooltip("Name used when playing this sound by name.")]
        public string soundName;

        [Tooltip("Audio clip this sound will play.")]
        public AudioClip audioClip;

        [Tooltip("Should this sound loop.")]
        public bool loop;

        [Tooltip("Volume this sound will play at.")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("If true, plays next sound in list when this sound ends.")]
        public bool playNextSound = false;
    }

    [Header("SFX")]
    [Tooltip("All sounds this script can play.")]
    [SerializeField] private List<Sound> sounds = new List<Sound>();

    [Tooltip("Prefab that has an AudioSource on it.")]
    [SerializeField] private GameObject audioSourcePrefab;

    [Header("Audio Mode")]
    [Tooltip("Should sounds play in 3D. If false, sounds play in 2D.")]
    [SerializeField] private bool playAs3D = true;

    [Tooltip("If true, this script adds and uses an AudioSource on this object instead of spawning a prefab. This allows previous clips to be cut off.")]
    [SerializeField] private bool useLocalAudioSource = false;

    [Header("Random Pitch")]
    [Tooltip("Should sounds play with a random pitch.")]
    [SerializeField] private bool useRandomPitch = true;

    [Tooltip("Lowest random pitch sounds can use.")]
    [SerializeField] private float minPitch = 0.9f;

    [Tooltip("Highest random pitch sounds can use.")]
    [SerializeField] private float maxPitch = 1.1f;

    [Header("Spawn Settings")]
    [Tooltip("Should the sound spawn at this object's position.")]
    [SerializeField] private bool spawnAtThisObject = true;

    private AudioSource localAudioSource;

    private int currentSoundIndex = -1;
    private bool localSoundIsPaused;
    private bool localSoundWasPlayingLastFrame;

    private void Awake()
    {
        InitialiseComponents();
    }

    private void Update()
    {
        if (!useLocalAudioSource) { return; }
        if (localAudioSource == null) { return; }
        if (sounds == null || sounds.Count == 0) { return; }

        // if the current local sound finished naturally, play the next sound if it is allowed to
        if (!localSoundIsPaused && localSoundWasPlayingLastFrame && !localAudioSource.isPlaying && localAudioSource.clip != null)
        {
            TryPlayNextSound(currentSoundIndex);
        }

        localSoundWasPlayingLastFrame = localAudioSource.isPlaying;
    }

    private void InitialiseComponents()
    {
        if (!useLocalAudioSource) { return; }

        localAudioSource = GetComponent<AudioSource>();

        if (localAudioSource == null)
        {
            localAudioSource = gameObject.AddComponent<AudioSource>();
        }

        localAudioSource.playOnAwake = false;
    }

    public void PlayRandomSound()
    {
        if (sounds == null || sounds.Count == 0)
        {
            Debug.LogWarning("AUD_SFX has no sounds assigned.", this);
            return;
        }

        int soundIndex = Random.Range(0, sounds.Count);
        PlaySound(soundIndex);
    }

    public void PlaySound(int soundIndex)
    {
        if (sounds == null || sounds.Count == 0)
        {
            Debug.LogWarning("AUD_SFX has no sounds assigned.", this);
            return;
        }

        if (soundIndex < 0 || soundIndex >= sounds.Count)
        {
            Debug.LogWarning("AUD_SFX tried to play an invalid sound index.", this);
            return;
        }

        currentSoundIndex = soundIndex;
        PlaySound(sounds[soundIndex], soundIndex);
    }

    public void PlaySound(string soundName)
    {
        if (sounds == null || sounds.Count == 0)
        {
            Debug.LogWarning("AUD_SFX has no sounds assigned.", this);
            return;
        }

        for (int i = 0; i < sounds.Count; i++)
        {
            if (sounds[i].soundName == soundName)
            {
                PlaySound(i);
                return;
            }
        }

        Debug.LogWarning("AUD_SFX could not find a sound called: " + soundName, this);
    }

    public void StopSound()
    {
        if (localAudioSource == null)
        {
            InitialiseComponents();
        }

        if (localAudioSource == null) { return; }

        localAudioSource.Stop();
        localAudioSource.clip = null;

        localSoundIsPaused = false;
        localSoundWasPlayingLastFrame = false;
        currentSoundIndex = -1;
    }

    private void PlaySound(Sound sound, int soundIndex)
    {
        if (sound == null)
        {
            Debug.LogWarning("AUD_SFX tried to play a null sound.", this);
            return;
        }

        if (sound.audioClip == null)
        {
            Debug.LogWarning("AUD_SFX tried to play a sound with no audio clip.", this);
            return;
        }

        if (useLocalAudioSource)
        {
            PlayLocalSound(sound, soundIndex);
            return;
        }

        PlaySpawnedSound(sound, soundIndex);
    }

    private void PlayLocalSound(Sound sound, int soundIndex)
    {
        if (localAudioSource == null)
        {
            InitialiseComponents();
        }

        if (localAudioSource == null)
        {
            Debug.LogWarning("AUD_SFX could not find or add a local AudioSource.", this);
            return;
        }

        // stops the previous clip so the new one can cut it off
        localAudioSource.Stop();

        currentSoundIndex = soundIndex;

        localAudioSource.clip = sound.audioClip;
        localAudioSource.loop = sound.loop;
        localAudioSource.volume = sound.volume;
        localAudioSource.pitch = GetPitch();
        localAudioSource.spatialBlend = GetSpatialBlend();
        localAudioSource.Play();

        localSoundIsPaused = false;
        localSoundWasPlayingLastFrame = localAudioSource.isPlaying;
    }

    private void PlaySpawnedSound(Sound sound, int soundIndex)
    {
        if (audioSourcePrefab == null)
        {
            Debug.LogWarning("AUD_SFX has no audio source prefab assigned.", this);
            return;
        }

        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (spawnAtThisObject)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        GameObject spawnedSound = Instantiate(audioSourcePrefab, spawnPosition, spawnRotation);

        AudioSource audioSource = spawnedSound.GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogWarning("AUD_SFX audio source prefab has no AudioSource component.", this);
            Destroy(spawnedSound);
            return;
        }

        audioSource.clip = sound.audioClip;
        audioSource.loop = sound.loop;
        audioSource.volume = sound.volume;
        audioSource.pitch = GetPitch();
        audioSource.spatialBlend = GetSpatialBlend();
        audioSource.Play();

        if (!sound.loop)
        {
            AUD_SFX_SpawnedSound spawnedSoundScript = spawnedSound.GetComponent<AUD_SFX_SpawnedSound>();

            if (spawnedSoundScript == null)
            {
                spawnedSoundScript = spawnedSound.AddComponent<AUD_SFX_SpawnedSound>();
            }

            spawnedSoundScript.Initialise(this, audioSource, soundIndex);
        }
    }

    public void TryPlayNextSound(int soundIndex)
    {
        if (sounds == null || sounds.Count == 0) { return; }

        if (soundIndex < 0 || soundIndex >= sounds.Count)
        {
            return;
        }

        if (!sounds[soundIndex].playNextSound)
        {
            return;
        }

        int nextSoundIndex = soundIndex + 1;

        if (nextSoundIndex >= sounds.Count)
        {
            return;
        }

        PlaySound(nextSoundIndex);
    }

    private float GetSpatialBlend()
    {
        if (playAs3D)
        {
            return 1f;
        }

        return 0f;
    }

    private float GetPitch()
    {
        if (!useRandomPitch)
        {
            return 1f;
        }

        if (minPitch > maxPitch)
        {
            Debug.LogWarning("AUD_SFX min pitch is higher than max pitch.", this);
            return 1f;
        }

        return Random.Range(minPitch, maxPitch);
    }

    private class AUD_SFX_SpawnedSound : MonoBehaviour
    {
        private AUD_SFX sfxScript;
        private AudioSource audioSource;

        private int soundIndex;

        private bool wasPlayingLastFrame;

        public void Initialise(AUD_SFX newSfxScript, AudioSource newAudioSource, int newSoundIndex)
        {
            sfxScript = newSfxScript;
            audioSource = newAudioSource;
            soundIndex = newSoundIndex;

            wasPlayingLastFrame = audioSource.isPlaying;
        }

        private void Update()
        {
            if (sfxScript == null)
            {
                Destroy(gameObject);
                return;
            }

            if (audioSource == null)
            {
                Destroy(gameObject);
                return;
            }

            // if the spawned sound finished naturally, try to play the next sound before destroying this object
            if (wasPlayingLastFrame && !audioSource.isPlaying && audioSource.clip != null)
            {
                sfxScript.TryPlayNextSound(soundIndex);
                Destroy(gameObject);
                return;
            }

            wasPlayingLastFrame = audioSource.isPlaying;
        }
    }
}