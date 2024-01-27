
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class LaserFlagGameController : UdonSharpBehaviour
{
    [Header("Score Settings")]
    public int scoreToWin;
    public int scorePerFlag;

    [Header("Flag Settings")]
    public float resetTime = 5.0f;

    [Header("Misc Settings")]
    public float gameLength = 5 * 60;
    public float endGameDelayTime = 5.0f;

    [Header("External References")]
    public CombatController combatController;
    public FlagStandController[] flagStands;

    // Inherited
    int numTeams => combatController.numTeams;
    int maxPlayers => combatController.maxPlayers;
    int localPlayerIndex => combatController.localPlayerIndex;
    int[] playerSlots => combatController.playerSlots;
    int[] playerTeams => combatController.playerTeams;
    VRCPlayerApi[] allPlayers => combatController.allPlayers;
    PlayerCombatController[] allControllers => combatController.allControllers;
    public Text debugText => combatController.debugText;

    // Private
    int localTeam;
    bool isGameStarted;
    bool isGameEnding;
    LaserFlagPlayerUiController[] playerUiControllers;

    // Synced
    [UdonSynced] int[] teamScores;

    // timers
    float timerEndDelay = 0.0f;
    float timerGameLength = 0.0f;

    private void Start()
    {
        teamScores = new int[combatController.numTeams];
        playerUiControllers = new LaserFlagPlayerUiController[combatController.maxPlayers];

        foreach (var stand in flagStands)
            stand.gameController = this;
    }

    private void Update()
    {
        if (isGameStarted && timerGameLength > 0.0f)
        {
            timerGameLength -= Time.deltaTime;
            playerUiControllers[localPlayerIndex].timeLeft.text = Mathf.Floor(timerGameLength).ToString("F0");

            // End the game if owner
            if (timerGameLength <= 0.0f && Networking.LocalPlayer.IsOwner(gameObject))
            {
                EndGameInternal();
            }
        }

        if (!Networking.LocalPlayer.IsOwner(gameObject))
            return;
        
        if (isGameEnding)
        {
            timerEndDelay -= Time.deltaTime;
            playerUiControllers[localPlayerIndex].timeLeft.text = Mathf.Floor(timerGameLength).ToString("F0");

            if (timerEndDelay <= 0.0f)
            {
                isGameEnding = false;
                combatController.lobbyController.EndGameButton();
            }
        }
    }

    public override void OnDeserialization()
    {
        UpdatePlayerUiLocal();
    }

    public void OnCombatStart() 
    {
        Debug.Log("[LaserFlag] - LaserFlag OnCombatStart");

        localTeam = combatController.GetPlayerTeam(Networking.LocalPlayer);

        foreach (var stand in flagStands)
            stand.flagController.ResetFlag();

        for (int i = 0; i < numTeams; ++i)
            teamScores[i] = 0;

        for(int i = 0; i < maxPlayers; ++i)
        {
            if (allControllers[i] != null && allControllers[i].gameObject.activeInHierarchy)
            {
                var uiController = allControllers[i].GetComponent<LaserFlagPlayerUiController>();

                Debug.Log($"[LaserFlag] - {allControllers[i].name} Reset text");
                playerUiControllers[i] = uiController;
                playerUiControllers[i].ResetText();
            }
            else Debug.Log($"[LaserFlag] - Controller index {i} skipped.");
        }

        isGameStarted = true;
        timerGameLength = gameLength;
        Debug.Log("[LaserFlag] - LaserFlag game started");
    }

    public void OnCombatEnd() { isGameStarted = false; }

    public void AddScore(int team, int score)
    {
        teamScores[team] += score;
        debugText.text += $" Score added.";

        UpdatePlayerUiLocal();
        RequestSerialization();

        if (teamScores[team] >= scoreToWin)
            EndGameInternal();
    }

    void EndGameInternal()
    {
        Debug.Log($"[LaserFlag] - EndGameInternal");
        isGameEnding = true;
        isGameStarted = false;
        timerEndDelay = endGameDelayTime;
        SendCustomNetworkEvent(NetworkEventTarget.All, "EndGameEvent");
    }

    public void EndGameEvent()
    {
        Debug.Log($"[LaserFlag] - EndGameEvent LaserFlag={enabled} Combat={combatController.enabled} Lobby={combatController.lobbyController.enabled}");

        foreach (var stand in flagStands)
        {
            stand.flagController.pickupComponent.Drop();
            stand.flagController.pickupComponent.pickupable = false;
        }

        if (teamScores[0] == teamScores[1])
            playerUiControllers[localPlayerIndex].winMessage.text = "<color=\"white\">DRAW</color>";
        else if (teamScores[0] > teamScores[1])
            playerUiControllers[localPlayerIndex].winMessage.text = "<color=\"blue\">BLUE TEAM WINS</color>";
        else playerUiControllers[localPlayerIndex].winMessage.text = "<color=\"red\">RED TEAM WINS</color>";
    }

    void UpdatePlayerUiLocal()
    {
        playerUiControllers[localPlayerIndex].blueScore.text = teamScores[0].ToString();
        playerUiControllers[localPlayerIndex].redScore.text = teamScores[1].ToString();
    }

    public int GetPlayerTeam(VRCPlayerApi player) => combatController.GetPlayerTeam(player);
    public PlayerCombatController GetPlayerCombatController(VRCPlayerApi player) => combatController.GetPlayerCombatController(player);
}
