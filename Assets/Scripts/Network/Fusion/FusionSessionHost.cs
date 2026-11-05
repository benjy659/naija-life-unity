// Naija Streets (Unity) — holds the Fusion session so NetAvatar can find it.
// NetBootstrap adds this (when NL_FUSION is on) and calls Create().
// SKELETON, UNVERIFIED (2026-11-05): compiled only with NL_FUSION.

#if NL_FUSION
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace NaijaLife.Network.Fusion
{
    public sealed class FusionSessionHost : MonoBehaviour
    {
        [Tooltip("Prefab with NetworkObject (Area Of Interest) + NetAvatar.")]
        public NetworkPrefabRef AvatarPrefab;

        public FusionNetSession Session { get; private set; }

        public INetSession Create(ICollection<string> chatBlocklist)
        {
            Session = new FusionNetSession(gameObject, AvatarPrefab,
                () => Time.realtimeSinceStartupAsDouble, chatBlocklist);
            return Session;
        }

        void OnDestroy() => Session?.Dispose();
    }
}
#endif
