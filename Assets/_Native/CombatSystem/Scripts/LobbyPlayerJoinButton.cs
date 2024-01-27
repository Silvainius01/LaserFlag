
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class LobbyPlayerJoinButton : UdonSharpBehaviour
{
    // Externally Set
    [HideInInspector] public int team;
    [HideInInspector] public LobbyController lobby;
    //[HideInInspector] public Text debugText;

    // Hidden
    [HideInInspector] public string scriptType = "LobbyPlayerJoinButton";

    // Private vars
    //[UdonSynced] int interactPlayerId;

    #region ========== MONO BEHAVIOUR ==========
    private void Start()
    {
    }
    #endregion

    #region ========== U# BEHAVIOUR ==========

    public override void Interact()
    {
        // Set local player, then sync if not the owner.
        var localPlayer = Networking.LocalPlayer;

        string msg = $"{localPlayer.displayName}[{localPlayer.playerId}] press T{team}: ";

        if (localPlayer.IsOwner(gameObject))
        {
            Debug.Log($"[Laser Flag] - {msg} sending event.");
            SendCustomNetworkEvent(NetworkEventTarget.All, "SendLobbyInteractionEvent");
        }
        else
        {
            Debug.Log($"[Laser Flag] - {msg} setting new owner.");
            Networking.SetOwner(localPlayer, gameObject);
        }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        //if (player.playerId == localPlayer.playerId)
        //{
        //    RequestSerialization();
        //}
        Debug.Log($"[LaserFlag] - T{team} join button recieved new owner: {player.displayName}[{player.playerId}]");
        SendLobbyInteractionEvent();
    }

    public override void OnDeserialization()
    {
        // When the master recieves new data, update the lobby.
        //SendLobbyInteractionEvent();
    }

    #endregion

    #region ========== PUBLIC ==========

    // Send the joining player data to the lobby. Only usable by the master of the world.
    public void SendLobbyInteractionEvent()
    {
        if (Networking.LocalPlayer.isMaster)
        {
            var player = Networking.GetOwner(gameObject);
            // var player = VRCPlayerApi.GetPlayerById(interactPlayerId);
            Debug.Log($"[LaserFlag] - Interact T{team} from {player.displayName}[{player.playerId}]");

            lobby._team = team;
            lobby._player = player;
            lobby.OnPlayerLobbyInteract();
        }
    }

    #endregion

    #region ========== PRIVATE ==========

    #endregion
}
