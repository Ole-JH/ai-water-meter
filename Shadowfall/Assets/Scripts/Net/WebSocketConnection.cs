using System;
using System.Collections.Concurrent;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Shadowfall
{
    /// <summary>
    /// Minimal polling WebSocket. In WebGL builds it uses the browser's WebSocket through
    /// Plugins/WebGL/ShadowfallWebSocket.jslib; in the editor / desktop it uses ClientWebSocket.
    /// Control events are delivered in-band as "__open", "__close:reason" and "__error:reason".
    /// </summary>
    public class WebSocketConnection
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SFWS_Connect(string url);
        [DllImport("__Internal")] static extern void SFWS_Send(string msg);
        [DllImport("__Internal")] static extern void SFWS_Close();
        [DllImport("__Internal")] static extern string SFWS_Poll();

        /// <param name="url">Empty = same host that served the page, path /ws.</param>
        public void Connect(string url) => SFWS_Connect(url ?? "");
        public void Send(string msg) => SFWS_Send(msg);
        public void Close() => SFWS_Close();

        public bool TryReceive(out string msg)
        {
            msg = SFWS_Poll();
            return msg != null;
        }
#else
        ClientWebSocket ws;
        CancellationTokenSource cts;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();

        public void Connect(string url)
        {
            Close();
            if (string.IsNullOrEmpty(url)) url = "ws://localhost:8080/ws";
            ws = new ClientWebSocket();
            cts = new CancellationTokenSource();
            _ = Run(ws, url, cts.Token);
        }

        async Task Run(ClientWebSocket socket, string url, CancellationToken token)
        {
            try
            {
                await socket.ConnectAsync(new Uri(url), token);
                inbox.Enqueue("__open");
                _ = SendLoop(socket, token);
                var buffer = new byte[64 * 1024];
                using (var ms = new MemoryStream())
                {
                    while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                    {
                        var r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (r.MessageType == WebSocketMessageType.Close)
                        {
                            inbox.Enqueue("__close:" + (r.CloseStatusDescription ?? "closed"));
                            return;
                        }
                        ms.Write(buffer, 0, r.Count);
                        if (!r.EndOfMessage) continue;
                        inbox.Enqueue(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
                        ms.SetLength(0);
                    }
                }
                inbox.Enqueue("__close:connection closed");
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested) inbox.Enqueue("__error:" + e.Message);
            }
        }

        async Task SendLoop(ClientWebSocket socket, CancellationToken token)
        {
            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    while (outbox.TryDequeue(out var msg))
                    {
                        var bytes = Encoding.UTF8.GetBytes(msg);
                        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
                    }
                    await Task.Delay(5, token);
                }
            }
            catch (Exception) { /* receive loop reports the error */ }
        }

        public void Send(string msg) => outbox.Enqueue(msg);

        public void Close()
        {
            if (ws == null) return;
            try
            {
                cts.Cancel();
                if (ws.State == WebSocketState.Open)
                    ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch (Exception) { }
            ws = null;
            while (outbox.TryDequeue(out _)) { }
        }

        public bool TryReceive(out string msg) => inbox.TryDequeue(out msg);
#endif
    }
}
