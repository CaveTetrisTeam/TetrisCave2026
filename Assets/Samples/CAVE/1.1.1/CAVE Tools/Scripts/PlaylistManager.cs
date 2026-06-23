using UnityEngine;

public class PlaylistManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip[] songs; // Hier packen wir deine Lieder rein
    private int currentSongIndex = 0;

    void Start()
    {
        if (songs.Length > 0 && audioSource != null)
        {
            PlayCurrentSong();
        }
    }

    void Update()
    {
        // Falls das aktuelle Lied zu Ende ist, spiele das nächste
        if (!audioSource.isPlaying)
        {
            NextSong();
        }
    }

    void PlayCurrentSong()
    {
        audioSource.clip = songs[currentSongIndex];
        audioSource.Play();
    }

    void NextSong()
    {
        // Erhöhe den Index und springe zurück auf 0, wenn das letzte Lied vorbei ist
        currentSongIndex = (currentSongIndex + 1) % songs.Length;
        PlayCurrentSong();
    }
}