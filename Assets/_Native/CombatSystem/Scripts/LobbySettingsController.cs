
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class LobbySettingsController : UdonSharpBehaviour
{
    [Header("Synced Player Modifiables")]
    [Tooltip("If enabled, only the master of the world can start the game.")]
    [UdonSynced] public bool masterStartOnly = true;
    [Tooltip("[unused] If enabled, will attempt to make teams as even as possible. Otherwise, teams are random. Ignored if teams are formed manually.")]
    [UdonSynced] public bool autoBalanceTeams = true;
    [Tooltip("[unused] If enabled, colliders will scale with avatar size. Not reccomended.")]
    [UdonSynced] public bool dynamicColliders;

    [Header("Local Player Modifiables")]
    [Tooltip("If enabled, player colliders will be rendered in game.")]
    public bool showColliders = true;

    [Header("Option Refs")]
    public Toggle masterStartToggle;
    public Toggle autoBalanceToggle;
    public Toggle showCollidersToggle;
    public Toggle dynamicCollidersToggle;

    //[Header("External Refs")]
    //public LobbyController lobbyController;

    // ========== MONO BEHAVIOUR ==========

    private void Start()
    {
        if (!Networking.LocalPlayer.isMaster)
            SetOptionsInteractable(false);
        ApplySettings();
    }

    // ========== U# BEHAVIOUR ==========

    public override void OnDeserialization()
    {
        ApplySettings();
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!Networking.LocalPlayer.isMaster)
            SetOptionsInteractable(true);
    }

    // ========== PUBLIC ==========

    public void OnMasterStartToggle()
    {
        masterStartOnly = masterStartToggle.isOn;
        RequestSerialization();
    }
    public void OnAutoBalanceTeamsToggle()
    {
        autoBalanceTeams = autoBalanceToggle.isOn;
        RequestSerialization();
    }
    public void OnDynamicCollidersToggle()
    {
        dynamicColliders = dynamicCollidersToggle.isOn;
        RequestSerialization();
    }

    public void OnCollidersVisibleToggle()
    {
        showColliders = showCollidersToggle.isOn;
    }

    // ========== PRIVATE ==========

    void SetOptionsInteractable(bool value)
    {
        masterStartToggle.interactable = value;
        autoBalanceToggle.interactable = value;
        dynamicCollidersToggle.interactable = value;
    }

    void ApplySettings()
    {
        masterStartToggle.isOn = masterStartOnly;
        autoBalanceToggle.isOn = autoBalanceTeams;
        dynamicCollidersToggle.isOn = dynamicColliders;
        showCollidersToggle.isOn = showColliders;

        if (!Networking.LocalPlayer.isMaster)
            SetOptionsInteractable(!masterStartOnly);
    }
}
