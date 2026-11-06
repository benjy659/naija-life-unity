// M3 seat and doors (docs/specs/vehicles-m3.md section 7). Child of the vehicle.
// Nigeria drives on the right, so the driver door is on the LEFT.

using UnityEngine;

namespace NaijaLife.Vehicles
{
    [DisallowMultipleComponent]
    public sealed class VehicleSeat : MonoBehaviour
    {
        [Tooltip("Where the driver sits. The player is parented here while driving.")]
        public Transform seatPoint;

        [Tooltip("Outside the driver door at standing height. Entry is offered within 2.5 m of it.")]
        public Transform doorPoint;

        [Tooltip("Where the player may step out, in try order: driver side, passenger side, behind.")]
        public Transform[] exitPoints;

        public const float OfferDistance = 2.5f;
        public const float WithdrawDistance = 3.0f;

        void OnDrawGizmosSelected()
        {
            if (seatPoint != null) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(seatPoint.position, 0.25f); }
            if (doorPoint != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(doorPoint.position, OfferDistance); }
            if (exitPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (var e in exitPoints)
                if (e != null) Gizmos.DrawWireCube(e.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
        }
    }
}
