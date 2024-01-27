
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.Base;

public class VideoPlayerControl : UdonSharpBehaviour
{
    public BaseVRCVideoPlayer videoPlayer;

    public AudioSource[] audioSources;
    public Slider volumeSlider;
    
    public Text playButtonText;
    public Text urlText;
    
    public VRCUrlInputField urlInput;
    public VRCUrl[] videoQueue;

    private int currentVideo = 0;
    private bool manualVideoLoaded = false;
    public bool playOnJoin = false;

    void Start()
    {
        volumeSlider.value = 0.5f;
        SetVolume();

        urlInput.SetUrl(videoQueue[currentVideo]);
        LoadUrl();
    }

    public override void OnVideoEnd()
    {
        if(!manualVideoLoaded)
        {
            currentVideo = (currentVideo + 1) % videoQueue.Length;
            urlInput.SetUrl(videoQueue[currentVideo]);
            LoadUrl();
        }
    }

    public void PlayToggle()
    {
        if(videoPlayer.IsPlaying)
        {
            videoPlayer.Pause();
            playButtonText.text = "PLAY";
        }
        else
        {
            videoPlayer.Play();
            playButtonText.text = "PAUSE";
        }
    }

    public void SetVolume()
    {
        foreach(var source in audioSources)
        {
            source.volume = volumeSlider.value;
        }
    }

    public void LoadUrl()
    {
        videoPlayer.Stop();
        videoPlayer.LoadURL(urlInput.GetUrl());
    }

    public override void OnVideoReady()
    {
        // Prevent playing when the world is loaded.
        if(!playOnJoin)
        {
            playOnJoin = true;
            return;
        }

        videoPlayer.Play();
        playButtonText.text = "PAUSE";
    }
}

