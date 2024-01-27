
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class PlayerCombatController : UdonSharpBehaviour
{
    [Header("Health Settings")]
    public float maxHealth;
    public Slider healthSlider;

    [Header("Colliders")]
    public GameObject bodyColliderParent;
    public float bodyColliderHeight;

    [Header("External References")]
    public GameObject canvasParent;
    public Text debugText;
    public AudioSource playerSoundSource;

    // Externally Set
    [HideInInspector] public int localTeam = -1;
    [HideInInspector] public int playerTeam = -1;
    [HideInInspector] public bool allowFriendlyFire;
    [HideInInspector] public float ffDamageMult;
    [HideInInspector] public VRCPlayerApi linkedPlayer;
    [HideInInspector] public CombatController combatController;

    // Hidden
    [HideInInspector] public string scriptType = "PlayerCombatController";

    // Synced
    [UdonSynced] public float currentHealth;
    [UdonSynced] public int lastDamagingPlayer;

    // Private
    bool inited = false;
    float respawnTime;
    VRCPlayerApi localPlayer;
    Collider bodyCollider;
    MeshRenderer[] bodyColliderMeshs;
    LobbyController lobby;
    bool isRespawning;
    float respawnTimer = 0.0f;

    private void Start()
    {
        if (!inited)
        {
            lobby = combatController.lobbyController;
            localPlayer = Networking.LocalPlayer;
            respawnTime = combatController.respawnTime;

            bodyCollider = bodyColliderParent.GetComponentInChildren<Collider>();
            bodyColliderMeshs = bodyColliderParent.GetComponentsInChildren<MeshRenderer>();

            // Ensure we dont have any collision. Bad things happen otherwise.
            bodyCollider.isTrigger = true;

            // Disable the controller until we need it.
            debugText.text += $"\ninit {gameObject.name}";
            inited = true;
        }
    }

    private void Update()
    {
        if (linkedPlayer != null)
        {
            if (linkedPlayer.isLocal)
            {
                // Update the HUD to track player head
                var headTrackingData = linkedPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                canvasParent.transform.position = headTrackingData.position;
                canvasParent.transform.rotation = headTrackingData.rotation;

                if (isRespawning && respawnTimer <= 0.0f)
                    RespawnPlayerLocal();
                else respawnTimer -= Time.deltaTime;
            }

            transform.position = linkedPlayer.GetPosition();
            UpdateDynamicBodyColliderScale();
        }
    }

    //========== U# BEHAVIOUR ==========

    public override void OnDeserialization()
    {
    }

    // ========== PUBLIC ==========

    public void InitController()
    {
        if (linkedPlayer == null)
        {
            Debug.LogError("Linked Null player");
            debugText.text += "Tried linking null player!";
            return;
        }
        else if (!inited)
            Start();

        string linkedPlayerString = $"{linkedPlayer.displayName}[{linkedPlayer.playerId}]";
        string localPlayerString = $"{localPlayer.displayName}[{localPlayer.playerId}]";

        debugText.text += $" {linkedPlayerString}: T:{playerTeam} L:{localTeam}";

        Material teamMat = localTeam == playerTeam
            ? combatController.playerAllyMaterial
            : combatController.playerEnemyMaterial;
        foreach (var renderer in bodyColliderMeshs)
            renderer.material = teamMat;

        // Dont want to see our own colliders
        if (linkedPlayer.isLocal)
        {
            currentHealth = maxHealth;
            canvasParent.SetActive(true);
            SetCollidersVisible(false);
            debugText.text += " off";
            UpdateHealthSlider();
        }
        else
        {
            SetCollidersVisible(combatController.lobbyController.uiSettings.showColliders);
            canvasParent.SetActive(false);
            debugText.text += " on";
        }

        this.enabled = true;

        if (!linkedPlayer.isLocal)
        {
            Debug.Log($"[{name}] Giving ownership to {linkedPlayerString} from {localPlayerString}");
            Networking.SetOwner(linkedPlayer, this.gameObject);

            // Set all children to be owned by this player.
            int numChildren = this.gameObject.transform.childCount;
            for (int i = 0; i < numChildren; ++i)
                Networking.SetOwner(linkedPlayer, this.gameObject.transform.GetChild(i).gameObject);

            RequestSerialization();
        }
    }

    //[HideInInspector] public float _damage;

    public void DamagePlayer(float damage, int fromPlayerId)
    {
        if (linkedPlayer.isLocal && currentHealth > 0.0f)
        {
            currentHealth -= damage;
            lastDamagingPlayer = fromPlayerId;

            UpdateHealthSlider();
            if (currentHealth <= 0)
            {
                PlayerDeathLocal();
            }
        }
    }

    public void PlayerDeathLocal()
    {
        var pickupLeft = localPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Left);
        var pickupRight = localPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);

        isRespawning = true;
        respawnTimer = respawnTime;
        localPlayer.Immobilize(true);

        // Drop weapons and prevent pickup
        if (pickupLeft != null)
            pickupLeft.Drop();
        if (pickupRight != null)
            pickupRight.Drop();
        localPlayer.EnablePickups(false);

        this.SendCustomNetworkEvent(NetworkEventTarget.All, "PlayerDeathGlobal");
    }
    public void PlayerDeathGlobal()
    {
        if (lastDamagingPlayer == Networking.LocalPlayer.playerId)
            combatController.PlayKillSound();

        bodyColliderParent.SetActive(false);
    }

    public void RespawnPlayerLocal()
    {
        // Renable collisions, weapons, movement
        localPlayer.Immobilize(false);
        localPlayer.EnablePickups(true);

        // tp to a random spawn
        var t = GetRandomTeamSpawn();
        localPlayer.TeleportTo(t.position, t.rotation);

        // Reset hp
        isRespawning = false;
        currentHealth = maxHealth;
        UpdateHealthSlider();

        this.SendCustomNetworkEvent(NetworkEventTarget.All, "RespawnPlayerGlobal");
    }
    public void RespawnPlayerGlobal()
    {
        bodyColliderParent.SetActive(true);
    }

    // ========== PRIVATE ==========

    void UpdateHealthSlider()
    {
        healthSlider.value = currentHealth / maxHealth;
    }

    Transform GetRandomTeamSpawn()
    {
        var teamSpawns = combatController.allTeamSpawns[playerTeam];
        int rIndex = Random.Range(0, teamSpawns.Length);
        return teamSpawns[rIndex].transform;
    }

    void SetCollidersVisible(bool value)
    {
        foreach (var meshRenderer in bodyColliderMeshs)
            meshRenderer.enabled = value;
    }

    void UpdateDynamicBodyColliderScale()
    {
        float height = (linkedPlayer.GetBonePosition(HumanBodyBones.Head)-linkedPlayer.GetPosition()).magnitude;
        float minHeight = combatController.minAvatarHeight;

        height = height > minHeight
            ? height / 2
            : minHeight / 2;

        bodyCollider.transform.localPosition = new Vector3(0, height, 0);
        bodyCollider.transform.localScale = new Vector3(height * .7f, height, height * .7f);
    }
}
