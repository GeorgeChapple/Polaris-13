using System.Collections.Generic;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Simple radio script, plays a random song from a list and picks a new random song when it ends.

[RequireComponent(typeof(AudioSource))]
public class AUD_Radio : MonoBehaviour
{
    [Header("Radio")]
    [Tooltip("All songs this radio can play.")]
    [SerializeField] private List<AudioClip> songs = new List<AudioClip>();

    [Tooltip("Avoids repeating the same song twice in a row when possible.")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    private AudioSource audioSource;
    private int currentSongIndex = -1;

    bool isPaused;
    bool wasPlayingLastFrame;

    private void Awake()
    {
        InitialiseComponents();
    }

    private void Start()
    {
        PlayRandomSong();
    }

    private void Update()
    {
        if (audioSource == null) { return; }
        if (songs == null || songs.Count == 0) { return; }

        // if the current song finished naturally, play another random one
        if (!isPaused && wasPlayingLastFrame && !audioSource.isPlaying && audioSource.clip != null)
        {
            Debug.Log("Playing Random", this);
            PlayRandomSong();
        }

        wasPlayingLastFrame = audioSource.isPlaying;
    }

    private void InitialiseComponents()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayRandomSong()
    {
        if (songs == null || songs.Count == 0)
        {
            Debug.LogWarning("AUD_Radio has no songs assigned.", this);
            return;
        }

        int nextSongIndex = 0;

        if (songs.Count == 1)
        {
            nextSongIndex = 0;
        }
        else
        {
            nextSongIndex = Random.Range(0, songs.Count);

            if (avoidImmediateRepeat)
            {
                while (nextSongIndex == currentSongIndex)
                {
                    nextSongIndex = Random.Range(0, songs.Count);
                }
            }
        }

        PlaySong(nextSongIndex);
    }

    public void PlaySong(int songIndex)
    {
        if (audioSource == null)
        {
            InitialiseComponents();
        }

        if (songs == null || songs.Count == 0)
        {
            Debug.LogWarning("AUD_Radio has no songs assigned.", this);
            return;
        }

        if (songIndex < 0 || songIndex >= songs.Count)
        {
            Debug.LogWarning("AUD_Radio tried to play an invalid song index.", this);
            return;
        }

        currentSongIndex = songIndex;
        audioSource.clip = songs[currentSongIndex];
        audioSource.Play();

        isPaused = false;
        wasPlayingLastFrame = audioSource.isPlaying;
    }

    public void PauseRadio()
    {
        if (audioSource == null) { InitialiseComponents(); }
        if (audioSource == null) { return; }
        if (audioSource.clip == null) { return; }

        audioSource.Pause();
        isPaused = true;
        wasPlayingLastFrame = false;
    }

    public void UnpauseRadio()
    {
        if (audioSource == null) { InitialiseComponents(); }
        if (audioSource == null) { return; }
        if (audioSource.clip == null) { return; }

        audioSource.UnPause();
        isPaused = false;
        wasPlayingLastFrame = audioSource.isPlaying;
    }
}