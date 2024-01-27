
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class MirrorControl : UdonSharpBehaviour
{
    public GameObject mirrorLow;
    public GameObject mirrorHigh;

    public Text mirrorToggleText;
    public Text mirrorQualityText;
    public GameObject mirrorQualityButton;

    private bool isMirrorOn = false;
    private bool isMirrorHigh = false;

    public void Start()
    {
        isMirrorOn = true;
        MirrorToggle();
    }

    // Because events.....
    public void MirrorToggle()
    {
        if(!isMirrorOn)
        {
            isMirrorHigh = false;
            mirrorLow.SetActive(true);
            mirrorQualityButton.SetActive(true);

            mirrorToggleText.text = "Mirror OFF";
            mirrorQualityText.text = "Mirror HIGH";
        }
        else
        {
            mirrorLow.SetActive(false);
            mirrorHigh.SetActive(false);
            mirrorQualityButton.SetActive(false);
            mirrorToggleText.text = "Mirror ON";
        }

        isMirrorOn = mirrorLow.activeSelf;
    }

    public void MirrorQualityToggle()
    {
        if(!isMirrorHigh)
        {
            mirrorLow.SetActive(false);
            mirrorHigh.SetActive(true);
            mirrorQualityText.text = "Mirror LOW";
        }
        else
        {
            mirrorLow.SetActive(true);
            mirrorHigh.SetActive(false);
            mirrorQualityText.text = "Mirror HIGH";
        }

        isMirrorHigh = mirrorHigh.activeSelf;
    }
}
