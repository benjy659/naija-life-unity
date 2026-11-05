// Naija Streets (Unity) — draws the 40 nearest real remote players from the
// network layer. Name tags come only from the street card (Supabase), never
// from network data. Spec: docs/specs/netcode-m5.md §4.2 and §3.5.
// UNVERIFIED (2026-11-05): not compiled or run yet. Scene setup: M5.1d.

using System.Collections.Generic;
using System.Threading;
using NaijaLife.Network;
using TMPro;
using UnityEngine;

namespace NaijaLife.NetworkGlue
{
    public sealed class RemotePlayerSpawner : MonoBehaviour
    {
        [Tooltip("Visible remote player (capsule for M5.1). May hold a TextMeshPro name tag child.")]
        [SerializeField] GameObject remotePrefab;
        [SerializeField] Transform localPlayer;
        [Tooltip("Name tags only on the nearest few, fading out past this distance (m).")]
        [SerializeField] int nameTagCount = 12;
        [SerializeField] float nameTagRange = 25f;

        sealed class View { public GameObject Go; public TextMeshPro Tag; public bool CardAsked; public float WaveUntil; }

        readonly Dictionary<NetPlayerId, View> _views = new Dictionary<NetPlayerId, View>();
        readonly List<NetPlayerId> _nearby = new List<NetPlayerId>();
        readonly HashSet<NetPlayerId> _keep = new HashSet<NetPlayerId>();
        readonly List<NetPlayerId> _drop = new List<NetPlayerId>();
        INetSession _hooked;

        void Update()
        {
            INetSession s = NetBootstrap.Session;
            if (s != _hooked) Hook(s);
            if (s == null || localPlayer == null) { Clear(); return; }

            double now = Time.realtimeSinceStartupAsDouble;
            Vector3 me = localPlayer.position;
            s.Players.GetNearby(me.x, me.z, _nearby);

            _keep.Clear();
            for (int i = 0; i < _nearby.Count; i++)
            {
                NetPlayerId id = _nearby[i];
                if (!s.Players.TryGetRemotePose(id, now, out NetPose p)) continue;
                _keep.Add(id);
                View v = GetView(id);
                v.Go.transform.SetPositionAndRotation(new Vector3(p.X, p.Y, p.Z), Quaternion.Euler(0f, p.HeadingDeg, 0f));
                if (v.Tag != null)
                {
                    float d = Vector3.Distance(me, v.Go.transform.position);
                    bool show = i < nameTagCount && d < nameTagRange;
                    v.Tag.gameObject.SetActive(show);
                    if (show)
                    {
                        v.Tag.alpha = Mathf.Clamp01((nameTagRange - d) / 5f);
                        if (v.WaveUntil > Time.time) v.Tag.alpha = 1f;
                    }
                }
            }

            _drop.Clear();
            foreach (KeyValuePair<NetPlayerId, View> kv in _views) if (!_keep.Contains(kv.Key)) _drop.Add(kv.Key);
            foreach (NetPlayerId id in _drop) { Destroy(_views[id].Go); _views.Remove(id); }
        }

        View GetView(NetPlayerId id)
        {
            if (_views.TryGetValue(id, out View v)) return v;
            v = new View { Go = Instantiate(remotePrefab, transform) };
            v.Go.name = "Remote player";
            v.Tag = v.Go.GetComponentInChildren<TextMeshPro>(true);
            if (v.Tag != null) { v.Tag.richText = false; v.Tag.text = "Player"; }
            _views[id] = v;
            AskCard(id, v);
            return v;
        }

        async void AskCard(NetPlayerId id, View v)
        {
            if (v.CardAsked || NetBootstrap.StreetCards == null) return;
            v.CardAsked = true;
            StreetCard card = await NetBootstrap.StreetCards.GetAsync(id, CancellationToken.None);
            if (card != null && v.Tag != null && v.Go != null) v.Tag.text = card.Username; // rich text off
        }

        void Hook(INetSession s)
        {
            if (_hooked != null) _hooked.Events.Received -= OnEvent;
            _hooked = s;
            if (s != null) s.Events.Received += OnEvent;
            Clear();
        }

        void OnEvent(NetPlayerId from, WorldEvent ev)
        {
            // M5.1: a wave lights the name tag for 2 s. Real emote animations come with the character work.
            if (ev.Kind == WorldEventKind.Wave && _views.TryGetValue(from, out View v)) v.WaveUntil = Time.time + 2f;
            if (Debug.isDebugBuild) Debug.Log("[net] event " + ev.Kind + " from a nearby player");
        }

        void Clear()
        {
            foreach (View v in _views.Values) if (v.Go != null) Destroy(v.Go);
            _views.Clear();
        }

        void OnDestroy() => Hook(null);
    }
}
