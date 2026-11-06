// Naija Streets (Unity) — the WebSocket under RealtimeDmClient (M5.3 part 2).
// WebGL: NLRealtime.jslib (browser WebSocket). Editor and desktop:
// System.Net.WebSockets.ClientWebSocket. Both raise Opened / Message / Closed
// on the main thread, and Closed also follows our own Close().
// UNVERIFIED (2026-11-06): not compiled in Unity or run against the server yet.

using System;
using NaijaLife.Network;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace NaijaLife.NetworkGlue
{
    public sealed class RealtimeSocket : IRealtimeSocket
    {
        public event Action Opened;
        public event Action<string> Message;
        public event Action Closed;

        readonly Func<string> _url;

        /// <param name="url">Called on each connect, so the URL can carry fresh values.</param>
        public RealtimeSocket(Func<string> url) { _url = url ?? throw new ArgumentNullException(nameof(url)); }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void NLRT_Open(string url, string gameObject);
        [DllImport("__Internal")] static extern void NLRT_Send(string text);
        [DllImport("__Internal")] static extern void NLRT_Close();
        [DllImport("__Internal")] static extern int NLRT_IsOpen();

        /// <summary>Name of the GameObject whose OnSocket* methods receive the browser events.</summary>
        public string ReceiverObject;

        public bool IsOpen => NLRT_IsOpen() == 1;
        public void Connect() => NLRT_Open(_url(), ReceiverObject);
        public void Send(string text) => NLRT_Send(text);
        public void Close() { NLRT_Close(); Closed?.Invoke(); }

        // Called by the receiver MonoBehaviour from SendMessage.
        public void RaiseOpened() => Opened?.Invoke();
        public void RaiseMessage(string text) => Message?.Invoke(text);
        public void RaiseClosed() => Closed?.Invoke();
#else
        ClientWebSocket _ws;
        CancellationTokenSource _cts;

        public bool IsOpen => _ws != null && _ws.State == WebSocketState.Open;

        // Unity's main-thread SynchronizationContext brings every await back to the main thread.
        public async void Connect()
        {
            Drop();
            var ws = new ClientWebSocket();
            var cts = new CancellationTokenSource();
            _ws = ws; _cts = cts;
            try
            {
                await ws.ConnectAsync(new Uri(_url()), cts.Token);
                if (_ws != ws) return;
                Opened?.Invoke();
                await ReceiveLoop(ws, cts.Token);
            }
            catch (Exception) { /* closed or failed; Closed below */ }
            if (_ws == ws) { Drop(); Closed?.Invoke(); }
        }

        async Task ReceiveLoop(ClientWebSocket ws, CancellationToken cancel)
        {
            var buffer = new byte[16 * 1024];
            var text = new StringBuilder();
            while (ws.State == WebSocketState.Open && !cancel.IsCancellationRequested)
            {
                WebSocketReceiveResult r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancel);
                if (r.MessageType == WebSocketMessageType.Close) return;
                text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                if (text.Length > 64 * 1024) return; // no frame of ours is this big
                if (!r.EndOfMessage) continue;
                string msg = text.ToString();
                text.Length = 0;
                if (r.MessageType == WebSocketMessageType.Text && _ws == ws) Message?.Invoke(msg);
            }
        }

        // Sends are tiny and rare; queueing through one Task keeps them in order.
        Task _sendChain = Task.CompletedTask;
        public void Send(string text)
        {
            ClientWebSocket ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            CancellationToken cancel = _cts.Token;
            _sendChain = _sendChain.ContinueWith(_ =>
                ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancel), TaskScheduler.Default).Unwrap();
        }

        public void Close()
        {
            bool had = _ws != null;
            Drop();
            if (had) Closed?.Invoke();
        }

        void Drop()
        {
            ClientWebSocket ws = _ws;
            _ws = null;
            _cts?.Cancel();
            _cts = null;
            if (ws != null) { try { ws.Abort(); ws.Dispose(); } catch (Exception) { } }
            _sendChain = Task.CompletedTask;
        }
#endif
    }
}
