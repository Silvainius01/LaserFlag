
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class LaserFlagPlayerUiController : UdonSharpBehaviour
{
    public Text redScore;
    public Text blueScore;
    public Text timeLeft;
    public Text winMessage;

    public void ResetText()
    {
        redScore.text = "0";
        blueScore.text = "0";
        timeLeft.text = "000";
        winMessage.text = "";
    }
}
