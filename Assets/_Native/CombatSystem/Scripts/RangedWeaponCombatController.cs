
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using static VRC.Core.ApiAvatar;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class RangedWeaponCombatController : UdonSharpBehaviour
{
    public string weaponName = "default_weapon";

    [Header("Damage Settings")]
    [Tooltip("If enabled, the weapon will fire as long as the trigger is held down.")]
    public bool isAutomatic;
    [Tooltip("The damage each round deals to a player")]
    public float projectileDamage = 1.0f;
    [Tooltip("The amount of rounds the weapon can fire before being forced to reload")]
    public int magazineSize;
    [Tooltip("The maximum rate of fire for the weapon. Values above 500rpm will make the weapon fire as fast as you can pull the trigger. Time between rounds in seconds = 60 / RPM")]
    public int roundsPerMin;

    [Header("Projectile Settings")]
    [Tooltip("If enabled, this weapon will use a raycast instead of a projectile.")]
    public bool isHitscan;
    [Tooltip("If enabled, the fired projectile will be destroyed when it contacts another collider.")]
    public bool destroyOnEnter;
    [Tooltip("How much the bullet is affected by gravity. 1 = normal, 0 = none, -1 is inverted, etc.")]
    public float projectileGravity = 1;
    [Tooltip("How long the projectile is allowed to exist after being spawned")]
    public float projectileLifespan = 10.0f;
    [Tooltip("The speed of the projectile after being spawned")]
    public float muzzleVelocity;
    [Tooltip("The number of projectiles fired by this weapon. Must be at least one.")]
    public int pelletCount = 1;
    [Tooltip("Location at which the projectile is spawned")]
    public Transform bulletSpawn;

    [Header("Reload Settings")]
    [Tooltip("If enabled, each shot must be rechambered manually. Think pump action shotties, or bolt action snipers. Turning this on will disable automatic fire.")]
    public bool rechamberEachShot;
    [Tooltip("Time it takes to rechamber the weapon. If the mag is fully depleted, this will be added onto the reload time.")]
    public float rechamberTime;
    [Tooltip("Time it takes to reload the weapon.")]
    public float reloadTime;

    [Header("Accuracy Settings")]
    [Tooltip("The max angle of deviation while at rest (Min Cone of Fire)")]
    public float minConeOfFireBloom = 0.0f;
    [Tooltip("The maximum angle of deviation while firing (Max Cone of Fire")]
    public float maxConeOfFireBloom = 1.0f;
    [Tooltip("Angle of deviation increase per round fired (Cone of Fire bloom")]
    public float bloomPerShot = 0.0f;
    [Tooltip("How quickly the bloom decays back to normal (angle per second)")]
    public float bloomDecayRate = 1.0f;
    [Tooltip("How long the user must wait before bloom begins to decay (in seconds)")]
    public float bloomDecayDelay = 0.0f;
    [Tooltip("Multiplier added to the bloom per shot when dual weilding.")]
    public float dualWeildBloomPenalty = 2.0f;
    [Tooltip("Multiplier added to the min CoF when dual weilding.")]
    public float dualWeildMinBloomPenalty = 2.0f;

    [Header("Misc Settings")]
    [Tooltip("If disabled, this weapon will force the player to drop whatever is in their other hand.")]
    public bool allowDualWeilding;

    [Header("External References")]
    public VRC_Pickup pickupComponent;
    public GameObject bulletPrefab;
    public AudioSource audioSource;
    public AudioClip[] muzzleSounds;
    public Collider[] nonTriggerChildColliders;
    public Canvas weaponCanvas;
    public Slider reloadSlider;
    public Text ammoText;

    // Externally set
    [HideInInspector] public CombatController combatController;
    [HideInInspector] public PlayerCombatController localPlayerController;

    // Hidden public

    // Private vars
    [Header("Debug Values")]
    int roundsLeft = 0;
    bool isFiring = false;
    bool canReset = false;
    bool triggerDown = false;
    [HideInInspector] public bool isDualWeilding = false;
    float currentBloom = 0.0f;
    VRCPlayerApi localPlayer;
    VRCPlayerApi holdingPlayer;
    VRC_Pickup.PickupHand holdingHand;
    Vector3 spawnPos;
    Quaternion spawnRot;
    //ProjectileCombatController bulletBehaviour;

    // Timers
    float timerRefire = 0.0f;
    float timerRechamber = 0.0f;
    float timerReload = 0.0f;
    float timerBloom = 0.0f;
    float timerReset = 0.0f;

    // Bullet Object Pool
    int poolIndex = 0;
    ProjectileCombatController[] bulletPool;

    // ========== MONO BEHAVIOUR ==========

    private void Start()
    {
        if (bulletPrefab == null)
        {
            bulletPrefab = Instantiate(bulletPrefab);
            bulletPrefab.SetActive(false);
        }

        localPlayer = Networking.LocalPlayer;
        pickupComponent = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));

        // Pump/Lever/Bolt/etc action weapons should not fire automatically.
        if (rechamberEachShot)
            isAutomatic = false;

        roundsLeft = magazineSize;
        currentBloom = minConeOfFireBloom;

        if (reloadTime <= 0.0f)
        {
            reloadTime = 0.001f;
        }

        reloadSlider.value = 0;
        weaponCanvas.gameObject.SetActive(false);
        ammoText.text = $"{magazineSize} / {magazineSize}";

        spawnPos = transform.position;
        spawnRot = transform.rotation;

        bulletPool = new ProjectileCombatController[magazineSize];
        for(int i =0; i < magazineSize; ++i)
        {
            bulletPool[i] = Instantiate(bulletPrefab).GetComponent<ProjectileCombatController>();

            bulletPool[i].gameObject.SetActive(false);
            bulletPool[i].transform.SetParent(transform);
            bulletPool[i].linkedWeapon = this;
            bulletPool[i].InitProjectile();
        }

        //if (combatController == null)
        //    Debug.LogError($"{gameObject.name} does not have combat controller set! Please add it to the combat controller.");
    }

    private void Update()
    {
#if UNITY_EDITOR
        triggerDown = Input.GetMouseButton(0);
#endif
        UpdateReload();

        if (UpdateReload() && pickupComponent.IsHeld)
        {
            if (CanFireWeapon() && triggerDown)
            {
                FireWeapon();
            }

            if (roundsLeft <= 0 || Input.GetKeyDown(KeyCode.F))
                ReloadWeapon();
        }

        // isFiring is set to false as soon as:
        //   - the trigger is released
        //   - the weapon is reloading
        isFiring &= triggerDown || roundsLeft <= 0;
        UpdateBloom();

        // Update reset
        if (canReset)
        {
            timerReset -= Time.deltaTime;
            if (timerReset <= 0.0f)
                ResetWeapon();
        }
    }

    // ========== U# BEHAVIOUR ==========

    public override void OnPickupUseDown()
    {
        triggerDown = true;
    }
    public override void OnPickupUseUp()
    {
        triggerDown = false;
    }

    public override void OnPickup()
    {
        foreach (var collider in nonTriggerChildColliders)
            collider.isTrigger = true;

        canReset = false;
        weaponCanvas.gameObject.SetActive(true);
        holdingHand = pickupComponent.currentHand;
        holdingPlayer = pickupComponent.currentPlayer;
        Networking.SetOwner(holdingPlayer, this.gameObject);

        var offhandObject = GetOffHandObject();
        if (offhandObject != null)
        {
            if (allowDualWeilding)
            {
                isDualWeilding = true;

                // Tell the offhand it is being dual weilded
                var offhandWeapon = offhandObject.GetComponent<RangedWeaponCombatController>();
                if (offhandWeapon != null)
                {
                    if (!offhandWeapon.allowDualWeilding)
                        offhandWeapon.pickupComponent.Drop();
                    else offhandWeapon.isDualWeilding = true;
                }
            }
            else offhandObject.Drop();
        }
    }
    public override void OnDrop()
    {
        foreach (var collider in nonTriggerChildColliders)
            collider.isTrigger = false;

        ReloadWeapon();
        isDualWeilding = false;
        weaponCanvas.gameObject.SetActive(false);

        if (combatController.weaponResetTime >= 0.0f)
        {
            canReset = true;
            timerReset = combatController.weaponResetTime;
        }

        // Tell the offhand weapon it isnt being dual weilded anymore
        var offhandObj = GetOffHandObject();
        var offhandWeapon = offhandObj == null ? null : offhandObj.GetComponent<RangedWeaponCombatController>();
        if(offhandWeapon != null)
            offhandWeapon.isDualWeilding = false;
        holdingPlayer = null;
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        //int spreadLength = (spreadPattern == null) ? -1 : spreadPattern.Length;
        //Debug.Log($"Weapon {weaponName} for player {player.displayName}[{player.playerId}]:" +
        //    $"\nOwner: {player.isMaster} Spread: {spreadLength} Index: {spreadIndex}");
    }

    // ========== PUBLIC ==========

    [HideInInspector] public int _hitPlayerId;
    [HideInInspector] [UdonSynced] public int _syncedFromPlayerId;
    [UdonSynced][FieldChangeCallback(nameof(SyncedHitPlayerId))] int _syncedHitPlayerId;

    // We use a callback to ensure we run damage as soon as a change occurs.
    int SyncedHitPlayerId
    {
        set
        {
            _syncedHitPlayerId = value;
            Debug.Log($"[LaserFlag] - Damage from callback");
            DamagePlayerGlobal();
        }
        get => _syncedHitPlayerId;
    }

    public void DamagePlayerLocal()
    {
        if (!Networking.IsOwner(localPlayer, gameObject))
        {
            return;
        }

        if (SyncedHitPlayerId != _hitPlayerId)
        {
            Debug.Log($"[LaserFlag] - DamagePlayerLocal New player hit: {_hitPlayerId}");
            SyncedHitPlayerId = _hitPlayerId;
            RequestSerialization();
        }
        else
        {
            Debug.Log($"[LaserFlag] - DamagePlayerLocal Same player hit: {_hitPlayerId}");
            this.SendCustomNetworkEvent(NetworkEventTarget.All, "DamagePlayerGlobal");
        }
    }

    public void DamagePlayerGlobal()
    {
        VRCPlayerApi holdingPlayer = pickupComponent.currentPlayer;
        VRCPlayerApi hitPlayer = VRCPlayerApi.GetPlayerById(SyncedHitPlayerId);
        if (hitPlayer != null && hitPlayer.isLocal)
        {
            bool isFriendlyFire = combatController.GetPlayerTeam(holdingPlayer) == combatController.GetPlayerTeam(hitPlayer);
            float dmg = combatController.allowFriendlyFire && isFriendlyFire 
                ? projectileDamage * combatController.friendlyFireDamageMult
                : projectileDamage;

            Debug.Log($"[LaserFlag] - DamagePlayerGlobal {hitPlayer.displayName}[{hitPlayer.playerId}] dmg={dmg}");
            localPlayerController.DamagePlayer(dmg, pickupComponent.currentPlayer.playerId);
        }
        else
        {
           Debug.Log($"[LaserFlag] - Did not damage player: null={hitPlayer == null} local={SyncedHitPlayerId == localPlayer.playerId}");
        }
    }

    public void SpawnBulletGlobal()
    {
        for (int i = 0; i < pelletCount; ++i)
            SpawnBullet();
    }

    void SpawnBullet()
    {
        Vector3 angles = Random.insideUnitSphere * currentBloom;
        var adjustedDirection = Quaternion.Euler(angles) * bulletSpawn.rotation;
        // var bullet = Instantiate(bulletPrefab);
        var bullet = bulletPool[poolIndex];

        bullet.gameObject.SetActive(true);
        bullet.transform.position = bulletSpawn.position;
        bullet.transform.rotation = adjustedDirection;
        bullet.OnProjectileFired();
        poolIndex = (poolIndex + 1) % magazineSize;

        if (muzzleSounds.Length > 1)
            audioSource.clip = muzzleSounds[Random.Range(0, muzzleSounds.Length)];
        audioSource.Play();

        // This would be better plaed in FireWeapon, since only a local player can initiate it.
        // But if I have to find this chunk of code again I will kill myself, so it STAYS HERE.
        if (holdingPlayer.isLocal && isHitscan && Physics.Raycast(bulletSpawn.position, adjustedDirection.eulerAngles, out RaycastHit info))
        {
            var playerController = info.collider.gameObject.GetComponentInParent<PlayerCombatController>();

            bool hitDamagableTarget =
                playerController != null &&
                playerController.linkedPlayer != null &&
                playerController.linkedPlayer.playerId != holdingPlayer.playerId;

            if(hitDamagableTarget)
            {
                _hitPlayerId = playerController.linkedPlayer.playerId;
                _syncedFromPlayerId = holdingPlayer.playerId;
                DamagePlayerLocal();
            }
        }
    }

    public void ResetWeapon()
    {
        ReloadWeapon();
        transform.position = spawnPos;
        transform.rotation = spawnRot;
        canReset = false;
    }

    // ========== PRIVATE ==========

    void ReloadWeapon()
    {
        if (timerReload > 0.0f || roundsLeft >= magazineSize)
            return;

        if(roundsLeft > 0)
        {
            timerReload += reloadTime;
        }
        else
        {
            timerReload += reloadTime;
            timerRechamber += rechamberTime;
        }

        timerRefire = 0.0f;
    }

    bool CanFireWeapon()
    {
        if (timerRefire > 0.0f)
            timerRefire -= Time.deltaTime;
        if (timerRechamber > 0.0f && roundsLeft > 0)
            timerRechamber -= Time.deltaTime;

        // Trigger is active if:
        //  - The weapon isnt firing
        //  - If it is automatic
        //  - It has any rounds left in the mag
        bool triggerActive = roundsLeft > 0;
        triggerActive &= !isFiring || (isFiring && isAutomatic);

        // Weapon can only fire if it has been rechambered, and the refire timer is at 0.
        return timerRefire <= 0.0f && timerRechamber <= 0.0f && triggerActive;
    }

    void FireWeapon()
    {
        isFiring = true;
        SendCustomNetworkEvent(NetworkEventTarget.All, "SpawnBulletGlobal");

        // Only update these stats on the user's end. 
        // NOTE: Only updating bloom here means other people only see bullets go
        // in the direction the weapon is aiming, and wont see the bloom on your end.
        if (pickupComponent.IsHeld && pickupComponent.currentPlayer.isLocal)
        {
            float addBloom = isDualWeilding ? bloomPerShot * dualWeildBloomPenalty : bloomPerShot;

            --roundsLeft;
            ammoText.text = $"{roundsLeft} / {magazineSize}";
            currentBloom = Mathf.Min(
                currentBloom + (isDualWeilding ? bloomPerShot * dualWeildBloomPenalty : bloomPerShot),
                maxConeOfFireBloom);
            timerBloom = bloomDecayDelay; // Reset the bloom delay when the weapon fires
            timerRefire += 60.0f / roundsPerMin; // Refire is += to ensure that we get as close as possible to the exact fire rate.
            timerRechamber = rechamberEachShot ? rechamberTime : 0.0f;
        }
    }

    void UpdateBloom()
    {
        // Dont update bloom if the weapon is firing
        if (isFiring)
            return;

        // Only update bloom after the delay completes
        if (timerBloom > 0.0f)
            timerBloom -= Time.deltaTime;
        else
        {
            currentBloom = Mathf.Max(
                currentBloom - (bloomDecayRate * Time.deltaTime),
                (isDualWeilding ? minConeOfFireBloom * dualWeildMinBloomPenalty : minConeOfFireBloom));
        }
    }

    bool UpdateReload()
    {
        if (timerReload > 0.0f)
        {
            timerReload -= Time.deltaTime;
            reloadSlider.value = Mathf.Max(0, 1 - (timerReload / reloadTime));
            if (timerReload <= 0.0f)
            {
                timerReload = 0.0f;
                reloadSlider.value = 0;
                roundsLeft = magazineSize;
                ammoText.text = $"{roundsLeft} / {magazineSize}";
                return true;
            }
            return false;
        }
        return true;
    }

    VRC_Pickup GetOffHandObject()
    {
        if (pickupComponent.IsHeld)
        {
            return pickupComponent.currentHand == VRC_Pickup.PickupHand.Right
            ? pickupComponent.currentPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Left)
            : pickupComponent.currentPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);
        }
        else if(holdingPlayer != null) // if dropped, query last holding player
        {
            return holdingPlayer.GetPickupInHand(holdingHand == VRC_Pickup.PickupHand.Right
                ? VRC_Pickup.PickupHand.Left
                : VRC_Pickup.PickupHand.Right);
        }

        return null;
    }
}
