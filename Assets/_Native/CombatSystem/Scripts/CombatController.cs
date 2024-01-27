
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Components;

/*
 *  The combat controller is meant to ONLY handle:
 *      - Combat specific settings
 *      - Managment of other combat controllers
 */
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CombatController : UdonSharpBehaviour
{
    [Header("Scenario Settings")]
    [Tooltip("Max player speed")]
    public float runSpeed = 4.0f;
    [Tooltip("Player Jump Velocity")]
    public float jumpImpulse = 3.0f;
    [Tooltip("Side to side move speed")]
    public float strafeSpeed = 3.0f;
    [Tooltip("The parent objects for all team spawn locations. If the object has no children, the parent will be used instead.")]
    public GameObject[] teamSpawns;

    [Header("Combat Settings")]
    [Tooltip("If enabled, players on the same team can damage each other.")]
    public bool allowFriendlyFire;
    [Tooltip("Damage modifier against friendly targets")]
    public float friendlyFireDamageMult = 1.0f;
    public float respawnTime = 3.0f;
    public float minAvatarHeight = 1.4f;

    [Header("Weapon Settings")]
    [Tooltip("How long a weapon must be unheld to repspawn. 0 = instant, -1 = never")]
    public float weaponResetTime = 10.0f;

    [Header("External References")]
    public Material playerAllyMaterial;
    public Material playerEnemyMaterial;
    public Material playerNeutralMaterial;
    public LobbyController lobbyController;
    public GameObject playerCombatPrefab;
    public VRCObjectPool playerCombatPool;
    public Text debugText;
    public AudioClip hitSound;
    public AudioClip killSound;
    [Tooltip("After the lobby has been ended, it will call \"OnCombatStart\" on each of these behaviours in order.")]
    [SerializeField] UdonBehaviour[] startGameListeners;
    [Tooltip("After the lobby has been ended, it will call \"OnCombatEnd\" on each of these behaviours in order.")]
    [SerializeField] UdonBehaviour[] endGameListeners;
    public RangedWeaponCombatController[] rangedWeaponObjects;

    // Inherited
    public int numTeams => lobbyController.numTeams;
    public int maxPlayers => lobbyController.maxPlayers;
    public int[] playerSlots => lobbyController.playerSlots;
    public int[] playerTeams => lobbyController.playerTeams;
    public VRCPlayerApi[] allPlayers => lobbyController.allPlayers;

    // Local vars
    [HideInInspector] public int localPlayerIndex;
    [HideInInspector] public bool isCombatStarted;
    [HideInInspector] public VRCPlayerApi localPlayer;
    [HideInInspector] public GameObject[][] allTeamSpawns;
    [HideInInspector] public PlayerCombatController[] allControllers;

    // Timers

    // ========== MONO BEHAVIOUR ==========

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        allTeamSpawns = new GameObject[numTeams][];
        allControllers = new PlayerCombatController[maxPlayers];

        localPlayer.SetRunSpeed(runSpeed);
        localPlayer.SetJumpImpulse(jumpImpulse);
        localPlayer.SetStrafeSpeed(strafeSpeed);
        localPlayer.SetWalkSpeed(runSpeed / 2);

        if (!allowFriendlyFire)
            friendlyFireDamageMult = 0.0f;


        foreach (var controller in playerCombatPool.Pool)
        {
            var playerBehaviour = controller.GetComponent<PlayerCombatController>();
            playerBehaviour.combatController = this;
            playerBehaviour.allowFriendlyFire = allowFriendlyFire;
            playerBehaviour.ffDamageMult = friendlyFireDamageMult;
            playerBehaviour.debugText = debugText;
        }

        for(int i = 0; i < numTeams; ++i)
        {
            int numSpawns = teamSpawns[i].transform.childCount;

            // If there are no child spawns, use the parent.
            if (numSpawns <= 0)
                allTeamSpawns[i] = new GameObject[1] { teamSpawns[i] };
            else // Fill the spawns with the children
            {
                allTeamSpawns[i] = new GameObject[numSpawns];
                for (int j = 0; j < numSpawns; ++j)
                    allTeamSpawns[i][j] = teamSpawns[i].transform.GetChild(j).gameObject;
            }
        }

        foreach(var rangedControllerObject in rangedWeaponObjects)
        {
            var behaviour = rangedControllerObject.GetComponent<RangedWeaponCombatController>();

            if (behaviour == null)
            {
                Debug.LogError($"{rangedControllerObject.name} is not a ranged weapon.");
                continue;
            }

            behaviour.combatController = this;
        }
    }

    private void Update()
    {
    }

    // ========== U# BEHAVIOUR ==========

    // ========== PUBLIC ==========

    public void OnLobbyStart()
    {
        for (int i = 0; i < maxPlayers; ++i)
            if (playerSlots[i] == localPlayer.playerId)
            {
                localPlayerIndex = i;
                break;
            }

        StartCombat();
    }

    public void OnLobbyEnd()
    {
        EndCombat();
    }

    public void StartCombat()
    {
        debugText.text += "\nCombat controller started.";
        Debug.Log("[LaserFlag] - StartCombat");

        if (playerSlots == null)
            debugText.text += "\nplayerSlots[] null!";
        if (playerTeams == null)
            debugText.text += "\nplayerTeams[] null!";
        if (allPlayers == null)
            debugText.text += "\allPlayers[] null!";

        debugText.text += $"\ns: {playerSlots.Length} t: {playerTeams.Length} p: {allPlayers.Length}";

        for (int i = 0; i < maxPlayers; ++i)
        {
            if (playerSlots[i] >= 0)
            {
                debugText.text += $"\nP{i} ";

                int playerTeam = playerTeams[i];
                GameObject contObject = playerCombatPool.Pool[i];
                var playerCombatCont = contObject.GetComponent<PlayerCombatController>();

                contObject.SetActive(true);
                allControllers[i] = playerCombatCont;

                playerCombatCont.enabled = allPlayers[i].isLocal; // Only enable the local controller.
                playerCombatCont.localTeam = playerTeams[localPlayerIndex];
                playerCombatCont.playerTeam = playerTeams[i];
                playerCombatCont.linkedPlayer = allPlayers[i];
                playerCombatCont.InitController();

                if (localPlayer.playerId == playerSlots[i])
                {
                    foreach (var weaponObject in rangedWeaponObjects)
                    {
                        weaponObject.ResetWeapon();
                        weaponObject.pickupComponent.Drop();
                        weaponObject.localPlayerController = playerCombatCont;
                    }

                    playerCombatCont.RespawnPlayerLocal();
                }
            }
        }

        foreach (var behaviour in startGameListeners)
            behaviour.SendCustomEvent("OnCombatStart");

        isCombatStarted = true;
    }

    public void EndCombat()
    {
        debugText.text = "Game is ending:";
        Debug.Log("[LaserFlag] - EndCombat");

        for (int i = 0; i < maxPlayers; ++i)
        {
            if (allControllers[i] != null)
                allControllers[i].gameObject.SetActive(false);
        }

        isCombatStarted = false;

        debugText.text += $"\nRespawning {localPlayer.displayName}[{localPlayer.playerId}]";

        localPlayer.Immobilize(false);
        localPlayer.EnablePickups(true);
        localPlayer.Respawn();

        if(localPlayer.isMaster)
        {
            foreach (var weapon in rangedWeaponObjects)
            {
                weapon.ResetWeapon();
                weapon.pickupComponent.Drop();
            }
        }

        foreach (var behaviour in endGameListeners)
            behaviour.SendCustomEvent("OnCombatEnd");
    }

    public int GetPlayerTeam(VRCPlayerApi player)
    {
        return lobbyController.GetPlayerTeamInternal(player);
    }

    public void PlaySoundForLocalPlayer(AudioClip sound)
    {
        var source = allControllers[localPlayerIndex].playerSoundSource;
        source.clip = sound;
        source.Play();
    }
    public void PlayHitSound()
    {
        var source = allControllers[localPlayerIndex].playerSoundSource;
        source.clip = hitSound;
        source.Play();
    }
    public void PlayKillSound()
    {
        var source = allControllers[localPlayerIndex].playerSoundSource;
        source.clip = killSound;
        source.Play();
    }

    public PlayerCombatController GetPlayerCombatController(VRCPlayerApi player)
    {
        for(int i = 0; i < maxPlayers; ++i)
        {
            if (playerSlots[i] == player.playerId)
                return allControllers[i];
        }
        return null;
    }

    // ========== PRIVATE ==========

}
