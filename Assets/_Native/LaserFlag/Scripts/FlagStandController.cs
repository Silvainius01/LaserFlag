
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class FlagStandController : UdonSharpBehaviour
{
    public int team;
    public Collider captureCollider;
    public FlagController flagController;
    public LaserFlagGameController gameController;

    [HideInInspector] public Vector3 resetPos;
    [HideInInspector] public Quaternion resetRot;

    private void Start()
    {
        resetPos = flagController.transform.position;
        resetRot = flagController.transform.rotation;

        flagController.team = this.team;
        flagController.homeStandCont = this;
    }

    public void OnTriggerEnter(Collider other)
    {

        var flag = other.GetComponent<FlagController>();

        // Do nothing if:
        //  - Not a flag
        //  - Interacting flag is on the same team
        if (flag == null || flag.team == this.team)
            return;

        // We only care about collision events when for the holder.
        if (!Networking.LocalPlayer.IsOwner(flag.gameObject))
            return;

        flag.ResetFlag();

        // We have to shotgun an event to find the master of the world...
        gameController.debugText.text += $"\nEmitting cap for T{team}.";
        SendCustomNetworkEvent(NetworkEventTarget.All, "AddScoreFromFlag");
    }

    public void AddScoreFromFlag()
    {
        bool isOwner = Networking.LocalPlayer.IsOwner(gameController.gameObject);
        gameController.debugText.text += $"\nGame Owner = {isOwner}.";
        if (isOwner)
        {
            gameController.debugText.text += $"\nCapture for T{team}.";
            gameController.AddScore(team, gameController.scorePerFlag);
        }
    }
}
