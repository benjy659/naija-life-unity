// M3 to HUD bridge (docs/specs/vehicles-m3.md 7.1, docs/specs/hud-and-phone.md 6).
// Hands DriveSession's offer to THE one action prompt (NLPrompts):
//   on foot next to a free car -> "Drive <name>" (Action); tap, E or gamepad South
//   seated and slow enough     -> "Get out" (Exit); withdrawn above 2 m/s
// Uses only NLPrompts' public Offer/Withdraw. When the HUD is not in the scene
// it does nothing and DriveSession's debug label stays on.
//
// UNVERIFIED: no Unity Editor in Claude's container (2026-11-06).

using NaijaLife.UI;
using UnityEngine;

namespace NaijaLife.Vehicles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DriveSession))]
    public sealed class VehiclePromptBridge : MonoBehaviour
    {
        const string DriveId = "m3.drive";
        const string ExitId = "m3.exit";

        DriveSession _session;
        bool _driveOffered, _exitOffered;

        void Awake() { _session = GetComponent<DriveSession>(); }

        void OnEnable()
        {
            _session.OfferChanged += OnOfferChanged;
            _session.Entered += OnEntered;
            _session.Exited += OnExited;
        }

        void OnDisable()
        {
            _session.OfferChanged -= OnOfferChanged;
            _session.Entered -= OnEntered;
            _session.Exited -= OnExited;
            WithdrawAll();
        }

        void Update()
        {
            bool hud = NLPrompts.Instance != null;
            _session.showDebugPrompt = !hud;
            if (!hud) return;

            // Get out: only while seated and slow enough (VehicleMath.CanExit).
            bool wantExit = _session.State == DriveState.Driving && !_session.ExitBlocked;
            if (wantExit && !_exitOffered)
            {
                var v = _session.CurrentVehicle;
                NLPrompts.Offer(new NLPromptOffer
                {
                    Id = ExitId,
                    Kind = NLPromptKind.Exit,
                    Verb = "Get out",
                    Subject = NameOf(v),
                    Icon = "door",
                    Anchor = v != null ? v.transform : null,
                    OnAccept = _session.RequestInteract
                });
                _exitOffered = true;
            }
            else if (!wantExit && _exitOffered)
            {
                NLPrompts.Withdraw(ExitId);
                _exitOffered = false;
            }
        }

        void OnOfferChanged(VehicleController v)
        {
            if (_driveOffered) { NLPrompts.Withdraw(DriveId); _driveOffered = false; }
            if (v == null || NLPrompts.Instance == null) return;
            bool bus = v.definition != null && v.definition.displayName == "Danfo";
            NLPrompts.Offer(new NLPromptOffer
            {
                Id = DriveId,
                Kind = NLPromptKind.Action,
                Verb = "Drive",
                Subject = NameOf(v),
                Icon = bus ? "bus" : "car",
                Anchor = v.Seat != null && v.Seat.doorPoint != null ? v.Seat.doorPoint : v.transform,
                OnAccept = _session.RequestInteract
            });
            _driveOffered = true;
        }

        void OnEntered(VehicleController v)
        {
            if (_driveOffered) { NLPrompts.Withdraw(DriveId); _driveOffered = false; }
        }

        void OnExited(VehicleController v)
        {
            if (_exitOffered) { NLPrompts.Withdraw(ExitId); _exitOffered = false; }
        }

        void WithdrawAll()
        {
            NLPrompts.Withdraw(DriveId);
            NLPrompts.Withdraw(ExitId);
            _driveOffered = _exitOffered = false;
        }

        static string NameOf(VehicleController v)
            => v != null && v.definition != null ? v.definition.displayName : "Motor";
    }
}
