
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ProjectileCombatController : UdonSharpBehaviour
{
    // Externally set
    public int owningTeam;
    public string projectileName;
    public float projectileDamage;
    public float projectileLifetime;
    public float projectileGravity;
    public bool destroyOnEnter;
    public VRCPlayerApi owner;
    public RangedWeaponCombatController linkedWeapon;
    public CombatController combatController;

    public Rigidbody rbody;
    public Vector3 gravityCache;

    bool inited = false;

    // Timers
    float timerLifespan;

    // ========== MONO BEHAVIOUR ==========

    private void Update()
    {
        if (timerLifespan > 0.0f)
            timerLifespan -= Time.deltaTime;
        else DisableProjectile();
    }

    private void FixedUpdate()
    {
        rbody.AddForce(gravityCache);
    }

    private void OnTriggerEnter(Collider collider)
    {
        var playerController = collider.gameObject.GetComponentInParent<PlayerCombatController>();

        bool hitDamagableTarget =
            playerController != null &&
            playerController.linkedPlayer != null &&
            playerController.linkedPlayer.playerId != owner.playerId;

        if (hitDamagableTarget)
        {
            if (projectileDamage > 0.0f)
            {
                linkedWeapon._hitPlayerId = playerController.linkedPlayer.playerId;
                linkedWeapon._syncedFromPlayerId = owner.playerId;
                linkedWeapon.DamagePlayerLocal();
                combatController.PlayHitSound();
            }

            DisableProjectile();
        }
        else if (destroyOnEnter && !collider.isTrigger)
        {
            DisableProjectile();
        }
    }

    public void InitProjectile()
    {
        rbody = gameObject.GetComponent<Rigidbody>();
        projectileName = linkedWeapon.weaponName + "_round";
        projectileDamage = linkedWeapon.projectileDamage;
        projectileLifetime = linkedWeapon.projectileLifespan;
        destroyOnEnter = linkedWeapon.destroyOnEnter;
        projectileGravity = linkedWeapon.projectileGravity;
        gravityCache = -Vector3.up * projectileGravity * rbody.mass;
        inited = true;

        if (linkedWeapon.isHitscan) // Dont deal damage if hit scan
            projectileDamage = 0.0f;
    }

    public void OnProjectileFired()
    {
        if (linkedWeapon == null)
        {
            Debug.LogError($"{projectileName} has no linked weapon!");
        }

        if (!inited)
        {
            InitProjectile();
        }

        rbody.velocity = transform.forward * linkedWeapon.muzzleVelocity;
        owner = linkedWeapon.pickupComponent.currentPlayer;
        combatController = linkedWeapon.combatController;
        owningTeam = combatController.GetPlayerTeam(owner);
        timerLifespan = projectileLifetime;

        if (owner != null && !owner.isLocal)
        {
            projectileDamage = 0.0f;
        }
    }

    private void DisableProjectile()
    {
        gameObject.SetActive(false);
    }
}
