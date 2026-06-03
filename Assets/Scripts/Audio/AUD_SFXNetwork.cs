using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;

// Made by: Jason Lodge
// Summary: SFX script, stores a list of sounds and can play random sounds, sounds by name, sounds by list order, and chained sounds.
// Networked version: owner asks the server to play sounds, then all clients play them.
// Owner hears their own sounds in 2D, other players hear them using playAs3D's current state.

public class AUD_SFXNetwork : NetworkBehaviour
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

    [SerializeField] private bool playFirstSoundOnAwake = false;

    [Tooltip("Prefab that has an AudioSource on it.")]
    [SerializeField] private GameObject audioSourcePrefab;

    [Header("Audio Mixer")]
    [Tooltip("Any spawned audio sources or local audio sources this script uses will set their audio mixer group to this.")]
    [SerializeField] private AudioMixerGroup targetAudioMixer;

    [Header("Audio Mode")]
    [Tooltip("Should sounds play in 3D. If false, sounds play in 2D.")]
    [SerializeField] private bool playAs3D = true;

    [Tooltip("If true, the owning player hears their own sounds in 2D.")]
    [SerializeField] private bool ownerHearsOwnSoundsAs2D = true;

    [Tooltip("If true, this script adds and uses an AudioSource on this object instead of spawning a prefab. This allows previous clips to be cut off.")]
    [SerializeField] private bool useLocalAudioSource = false;

    [Header("3D Audio Settings")]
    [Tooltip("Minimum distance before 3D sounds start getting quieter.")]
    [SerializeField] private float minDistance = 1f;

    [Tooltip("Maximum distance 3D sounds can be heard from.")]
    [SerializeField] private float maxDistance = 25f;

    [Tooltip("How much doppler effect 3D sounds use.")]
    [SerializeField] private float dopplerLevel = 1f;

    [Tooltip("How much 3D spread the sound has.")]
    [SerializeField] private float spread = 0f;

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

        if (!IsSpawned && playFirstSoundOnAwake)
        {
            PlaySoundLocalOnly(0, GetPitch());
        }
    }

    public override void OnNetworkSpawn()
    {
        if (playFirstSoundOnAwake && IsOwner) { PlaySound(0); }
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

    // cache refs before runtime logic starts.
    private void InitialiseComponents()
    {
        if (!useLocalAudioSource) { return; }

        localAudioSource = GetComponent<AudioSource>();

        if (localAudioSource == null)
        {
            localAudioSource = gameObject.AddComponent<AudioSource>();
        }

        localAudioSource.playOnAwake = false;
        ApplyAudioSourceSettings(localAudioSource);
    }

    public void PlayRandomSound()
    {
        if (sounds == null || sounds.Count == 0)
        {
            Debug.LogWarning("AUD_SFX has no sounds assigned.", this);
            return;
        }

        if (!IsSpawned)
        {
            int localSoundIndex = Random.Range(0, sounds.Count);
            PlaySoundLocalOnly(localSoundIndex, GetPitch());
            return;
        }

        PlayRandomSoundRequestRpc();
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

        if (!IsSpawned)
        {
            PlaySoundLocalOnly(soundIndex, GetPitch());
            return;
        }

        PlaySoundRequestRpc(soundIndex);
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
        if (!IsSpawned)
        {
            StopSoundLocalOnly();
            return;
        }

        StopSoundRequestRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlayRandomSoundRequestRpc(RpcParams rpcParams = default)
    {
        if (sounds == null || sounds.Count == 0)
        {
            return;
        }

        if (!CanSenderPlaySound(rpcParams))
        {
            return;
        }

        int soundIndex = Random.Range(0, sounds.Count);
        float pitch = GetPitch();

        PlaySoundRpc(soundIndex, pitch);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaySoundRequestRpc(int soundIndex, RpcParams rpcParams = default)
    {
        if (sounds == null || sounds.Count == 0)
        {
            return;
        }

        if (soundIndex < 0 || soundIndex >= sounds.Count)
        {
            return;
        }

        if (!CanSenderPlaySound(rpcParams))
        {
            return;
        }

        float pitch = GetPitch();

        PlaySoundRpc(soundIndex, pitch);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StopSoundRequestRpc(RpcParams rpcParams = default)
    {
        if (!CanSenderPlaySound(rpcParams))
        {
            return;
        }

        StopSoundRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void PlaySoundRpc(int soundIndex, float pitch)
    {
        PlaySoundLocalOnly(soundIndex, pitch);
    }

    [Rpc(SendTo.Everyone)]
    private void StopSoundRpc()
    {
        StopSoundLocalOnly();
    }

    private bool CanSenderPlaySound(RpcParams rpcParams)
    {
        if (NetworkManager.Singleton == null)
        {
            return false;
        }

        if (IsServer && OwnerClientId == NetworkManager.Singleton.LocalClientId)
        {
            return true;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (senderClientId != OwnerClientId)
        {
            Debug.LogWarning("AUD_SFX blocked sound request from non-owner.", this);
            return false;
        }

        return true;
    }

    private void StopSoundLocalOnly()
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

    private void PlaySoundLocalOnly(int soundIndex, float pitch)
    {
        if (sounds == null || sounds.Count == 0)
        {
            return;
        }

        if (soundIndex < 0 || soundIndex >= sounds.Count)
        {
            return;
        }

        currentSoundIndex = soundIndex;
        PlaySound(sounds[soundIndex], soundIndex, pitch);
    }

    private void PlaySound(Sound sound, int soundIndex, float pitch)
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
            PlayLocalSound(sound, soundIndex, pitch);
            return;
        }

        PlaySpawnedSound(sound, soundIndex, pitch);
    }

    private void PlayLocalSound(Sound sound, int soundIndex, float pitch)
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
        localAudioSource.pitch = pitch;
        localAudioSource.spatialBlend = GetSpatialBlend();

        ApplyAudioSourceSettings(localAudioSource);

        localAudioSource.Play();

        localSoundIsPaused = false;
        localSoundWasPlayingLastFrame = localAudioSource.isPlaying;
    }

    private void PlaySpawnedSound(Sound sound, int soundIndex, float pitch)
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
        audioSource.pitch = pitch;
        audioSource.spatialBlend = GetSpatialBlend();

        ApplyAudioSourceSettings(audioSource);

        audioSource.Play();

        if (!sound.loop)
        {
            AUD_SFX.AUD_SFX_SpawnedSound spawnedSoundScript = spawnedSound.GetComponent<AUD_SFX.AUD_SFX_SpawnedSound>();

            if (spawnedSoundScript == null)
            {
                spawnedSoundScript = spawnedSound.AddComponent<AUD_SFX.AUD_SFX_SpawnedSound>();
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

        // chained sounds are played locally because the first networked sound already reached every client
        PlaySoundLocalOnly(nextSoundIndex, GetPitch());
    }

    private void ApplyAudioSourceSettings(AudioSource audioSource)
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.outputAudioMixerGroup = targetAudioMixer;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.dopplerLevel = dopplerLevel;
        audioSource.spread = spread;
    }

    private float GetSpatialBlend()
    {
        if (ownerHearsOwnSoundsAs2D && IsOwner)
        {
            return 0f;
        }

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
}