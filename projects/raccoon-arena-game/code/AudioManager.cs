using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TaggedClip
{
    public string tag;
    public AudioClip clip;
}

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public List<AudioClip> musicClips;
    public List<TaggedClip> sfxClips;

    [HideInInspector] public int currentMusicClipIdx;
    [HideInInspector] public float trackProgress;

    private AudioSource musicTrack;
    private AudioSource sfxTrack;

    private Dictionary<string, AudioClip> sfxClipsMap;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicTrack = GetComponent<AudioSource>();
        sfxTrack = gameObject.AddComponent<AudioSource>();

        BuildClipsMap();
    }

    private void Start()
    {
        if (musicClips.Count > 0)
            PlayMusic(0);
    }

    private void Update()
    {
        if (musicTrack.clip != null && musicTrack.clip.length > 0f)
            trackProgress = musicTrack.time / musicTrack.clip.length;
    }

    private void BuildClipsMap()
    {
        sfxClipsMap = new Dictionary<string, AudioClip>();

        foreach (TaggedClip taggedClip in sfxClips)
        {
            if (!string.IsNullOrEmpty(taggedClip.tag) &&
                taggedClip.clip != null)
            {
                sfxClipsMap[taggedClip.tag] = taggedClip.clip;
            }
        }
    }

    public void PlayMusic(int musicIndex)
    {
        if (musicIndex < 0 || musicIndex >= musicClips.Count)
            return;

        currentMusicClipIdx = musicIndex;

        musicTrack.clip = musicClips[musicIndex];
        musicTrack.loop = true;
        musicTrack.Play();
    }

    public void PlaySFX(string clipTag)
    {
        if (!sfxClipsMap.ContainsKey(clipTag))
        {
            Debug.LogWarning("Audio clip not found: " + clipTag);
            return;
        }

        sfxTrack.PlayOneShot(sfxClipsMap[clipTag]);
    }

    public void StopMusic()
    {
        musicTrack.Stop();
    }
}

