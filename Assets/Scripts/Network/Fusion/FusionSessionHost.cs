// Naija Streets (Unity) — holds the Fusion session so NetAvatar can find it.
// NetBootstrap adds this (when NL_FUSION is on) and calls Create().
// SKELETON, UNVERIFIED (2026-11-05): compiled only with NL_FUSION.

#if NL_FUSION
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace NaijaLife.Network.Fusion
{
    public sealed class FusionSessionHost : MonoBehaviour
    {
        [Tooltip("Prefab with NetworkObject (Area Of Interest) + NetAvatar.")]
        public NetworkPrefabRef AvatarPrefab;

        public FusionNetSession Session { get; private set; }

        public INetSession Create(ICollection<string> chatBlocklist,
                                  Func<NetPlayerId, string, CancellationToken, Task<bool>> sendDirect = null)
        {
            Session = new FusionNetSession(gameObject, AvatarPrefab,
                () => Time.realtimeSinceStartupAsDouble, chatBlocklist, sendDirect);
            return Session;
        }

        void OnDestroy() => Session?.Dispose();
    }
}
#endif
