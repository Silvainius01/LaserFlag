
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class LobbyPlayerTriggerController : UdonSharpBehaviour
{
    [Header("External References")]
    public Collider triggerArea;
    public LobbyController lobbyController;

    // Private
    int neutralTeam;
    VRCPlayerApi localPlayer;
    LobbyPlayerJoinButton neutralTeamJoinButton;
    Text debugText;

    // Synced
    // [UdonSynced] int leavePlayerId;

    // MONO BEHAVIOUR

    private void Start()
    {
        triggerArea.isTrigger = true;
        localPlayer = Networking.LocalPlayer;
        neutralTeam = lobbyController.neutralTeam;
        debugText = lobbyController.debugText;

        // Get the lobby join button for the neutral team, if one exists.
        int numTeams = lobbyController.numTeams;
        if (neutralTeam < numTeams && neutralTeam >= 0)
            neutralTeamJoinButton =
                lobbyController.teamUiObjects[neutralTeam].transform.
                Find("JoinTeam").GetComponent<LobbyPlayerJoinButton>();

        if(lobbyController.joinByTeam)
        {
            Debug.Log("[LaserFlag] - Disabling area trigger");
            this.gameObject.SetActive(false);
        }
        else
        {
            Debug.Log("[LaserFlag] - Enabling area trigger");
            this.gameObject.SetActive(true);
        }
    }

    // U# BEHVAIOUR

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        Debug.Log($"[LaserFlag] - {player.displayName}[{player.playerId}] entered trigger. Local={player.isLocal}");
        if (player.isLocal && !lobbyController.joinByTeam)
        {
            neutralTeamJoinButton.Interact();
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        Debug.Log($"[LaserFlag] - {player.displayName}[{player.playerId}] left trigger. Local={player.isLocal}");
        if (player.isLocal && !lobbyController.joinByTeam)
        {
            neutralTeamJoinButton.Interact();
        }
    }

    public override void OnDeserialization()
    {
        // When the master recieves new data, update the lobby.
        debugText.text += $"\nArea got data: Master={localPlayer.isMaster}";
        RemovePlayerFromLobby();
    }

    // PRIVATE

    private void RemovePlayerFromLobby()
    {
        if (localPlayer.isMaster)
        {
            Debug.Log("[LaserFlag] - Removing player from lobby");
            lobbyController._player = localPlayer;
            lobbyController.RemovePlayerFromLobby();
        }
    }

    private void SyncBehaviour()
    {
        if (!localPlayer.IsOwner(this.gameObject))
        {
            Networking.SetOwner(localPlayer, this.gameObject);
            debugText.text += $"{localPlayer.displayName}[{localPlayer.playerId}]->owner.";
        }
        else debugText.text += $" local owner.";
        RequestSerialization();
    }
}
