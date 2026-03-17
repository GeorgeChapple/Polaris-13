using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

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

    private void Awake()
    {
        InitialiseComponents();
    }

    private void Start()
    {
        PlayRadio();
    }

    private void Update()
    {
        if (audioSource == null) { return; }
        if (songs == null || songs.Count == 0) { return; }

        // if the current song has finished, play another random one
        if (!audioSource.isPlaying && audioSource.clip != null)
        {
            PlayRandomSong();
        }
    }

    private void InitialiseComponents()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayRadio()
    {
        if (songs == null || songs.Count == 0)
        {
            Debug.LogWarning("AUD_Radio has no songs assigned.", this);
            return;
        }
        PlayRandomSong();
    }

    public void StopRadio()
    {
        if (audioSource == null) { return; }

        audioSource.Stop();
    }

    public void PauseRadio()
    {
        if (audioSource == null) { return; }

        audioSource.Pause();
    }

    public void ResumeRadio()
    {
        if (audioSource == null) { return; }

        audioSource.UnPause();
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
    }
}