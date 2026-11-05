// Naija Streets — M1.2 Third-Person Camera
// Interface the player controller (M1.1) can implement so the camera gets
// clean, authoritative movement state instead of inferring it from transforms.
//
// The camera does NOT require this interface. If the follow target has no
// component implementing it, PlayerCamera falls back (in order) to a
// CharacterController on the target, then to velocity computed from transform
// deltas. Facing falls back to followTarget.forward; grounded falls back to
// CharacterController.isGrounded, then true.
//
// Contract (per docs/DESIGN-INVENTORY.md §2 and docs/MIGRATION-PLAN.md §6 M1):
//   - Velocity: the player's CURRENT world-space velocity (m/s), incl. Y.
//   - HorizontalSpeed: magnitude of the velocity on the ground plane (m/s).
//   - IsGrounded: true while the player's feet are on the ground.
//   - Facing: the direction the character model faces (horizontal). May be
//     Vector3.zero if the controller has no meaningful facing; the camera
//     then uses followTarget.forward.

using UnityEngine;

namespace NaijaLife.Player
{
    public interface IPlayerCameraTarget
    {
        /// <summary>Current world-space velocity in m/s (includes vertical).</summary>
        Vector3 Velocity { get; }

        /// <summary>Magnitude of the velocity on the ground (XZ) plane, m/s.</summary>
        float HorizontalSpeed { get; }

        /// <summary>True while the player's feet are on the ground.</summary>
        bool IsGrounded { get; }

        /// <summary>
        /// Direction the character model faces (horizontal). Vector3.zero
        /// means "no facing available — use followTarget.forward".
        /// </summary>
        Vector3 Facing { get; }
    }
}
