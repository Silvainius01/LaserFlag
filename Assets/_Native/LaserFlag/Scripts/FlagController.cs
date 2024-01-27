
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class FlagController : UdonSharpBehaviour
{
    public VRC_Pickup pickupComponent;

    [HideInInspector] public int team;
    [HideInInspector] public LaserFlagGameController gameController;
    [HideInInspector] public FlagStandController homeStandCont;

    int localTeam;
    bool canReset = false;
    float timerReset = 0.0f;

    int holdingTeam;
    VRCPlayerApi holdingPlayer;
    VRC_Pickup.PickupHand holdingHand;
    PlayerCombatController holdingPlayerCont;

    private void Update()
    {
        if(canReset)
        {
            timerReset -= Time.deltaTime;
            if(timerReset <= 0.0f)
            {
                ResetFlag();
            }
        }
    }

    public void ResetFlag()
    {
        if(pickupComponent.IsHeld)
            pickupComponent.Drop();

        canReset = false;
        transform.position = homeStandCont.resetPos;
        transform.rotation = homeStandCont.resetRot;
        localTeam = gameController.GetPlayerTeam(Networking.LocalPlayer);
        SendCustomNetworkEvent(NetworkEventTarget.All, "DisableSameTeamPickUp"); ;
    }

    public override void OnPickup()
    {
        holdingTeam = gameController.GetPlayerTeam(pickupComponent.currentPlayer);
        holdingPlayerCont = gameController.GetPlayerCombatController(pickupComponent.currentPlayer);

        float dist = (transform.position - pickupComponent.currentPlayer.GetPosition()).sqrMagnitude;
        
        if(dist > pickupComponent.proximity*pickupComponent.proximity)
        {
            pickupComponent.Drop();
            return;
        }

        canReset = false;
        holdingHand = pickupComponent.currentHand;
        holdingPlayer = pickupComponent.currentPlayer;
        Networking.SetOwner(holdingPlayer, this.gameObject);

        // Update any ranged weapon you may or may not be carrying
        var offhandObject = GetOffHandObject();
        if (offhandObject != null)
        {
            // Tell the offhand it is being dual weilded
            var offhandWeapon = offhandObject.GetComponent<RangedWeaponCombatController>();
            if (offhandWeapon != null)
            {
                if (!offhandWeapon.allowDualWeilding)
                    offhandWeapon.pickupComponent.Drop();
                else offhandWeapon.isDualWeilding = true;
            }
        }
        
        if (holdingTeam == team)
            ResetFlag();
        else SendCustomNetworkEvent(NetworkEventTarget.All, "EnableSameTeamPickUp");
    }
    public override void OnDrop()
    {
        canReset = true;
        pickupComponent.pickupable = true;
        timerReset = gameController.resetTime;

        // Tell the offhand weapon it isnt being dual weilded anymore
        var offhandObj = GetOffHandObject();
        var offhandWeapon = offhandObj == null ? null : offhandObj.GetComponent<RangedWeaponCombatController>();
        if (offhandWeapon != null)
            offhandWeapon.isDualWeilding = false;
        holdingPlayer = null;
    }

    public void EnableSameTeamPickUp() => pickupComponent.pickupable = true;
    public void DisableSameTeamPickUp() => pickupComponent.pickupable = team != localTeam;

    VRC_Pickup GetOffHandObject()
    {
        if (pickupComponent.IsHeld)
        {
            return pickupComponent.currentHand == VRC_Pickup.PickupHand.Right
            ? pickupComponent.currentPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Left)
            : pickupComponent.currentPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);
        }
        else if (holdingPlayer != null) // if dropped, query last holding player
        {
            return holdingPlayer.GetPickupInHand(holdingHand == VRC_Pickup.PickupHand.Right
                ? VRC_Pickup.PickupHand.Left
                : VRC_Pickup.PickupHand.Right);
        }

        return null;
    }
}
