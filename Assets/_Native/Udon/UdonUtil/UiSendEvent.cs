
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class UiSendEvent : UdonSharpBehaviour
{
    public UdonBehaviour target;
    public bool networked;
    public string eventName;

    public override void Interact()
    {
        SendEvent();
    }

    public void SendEvent()
    {
        if(networked)
        {
            target.SendCustomNetworkEvent(NetworkEventTarget.All, eventName);
        }
        else
        {
            target.SendCustomEvent(eventName);
        }
    }
}
