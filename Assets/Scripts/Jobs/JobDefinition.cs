// Naija Streets (Unity) - per-job settings (docs/specs/jobs-m4.md section 3).
// Asset: Assets/Settings/Jobs/Conductor.asset (Create > Naija > Job Definition).

using NaijaLife.Jobs;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    [CreateAssetMenu(menuName = "Naija/Job Definition", fileName = "Conductor")]
    public sealed class JobDefinition : ScriptableObject
    {
        public JobKind kind = JobKind.Conductor;
        public string displayName = "Danfo conductor";
        [Tooltip("Prompt verb + subject (HUD spec 6.2).")]
        public string promptVerb = "Take over";
        public string promptSubject = "Danfo conductor";
        public string promptLine = "₦600 and up a trip";
        [Tooltip("Offer shows inside this distance (HUD spec 6.3 uses 2.5 m show / 3.0 m hide).")]
        public float takeRadius = 2.6f;
        public float leaveRadius = ReleaseWatch.LeaveRadius;
        public float idleSeconds = ReleaseWatch.IdleLimitSeconds;
        public float pawaDrainPerMinute = ShiftRules.PawaDrainPerMinute;
    }
}
