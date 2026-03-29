using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Mentora.Network
{
    public class GameClient : MonoBehaviour
    {
        public static GameClient Instance { get; private set; }

        private ClientWebSocket socket;
        private CancellationTokenSource cts;
        private PacketManager packetManager = new PacketManager();

        public event Action<Packet> OnPacketReceived;
        public bool IsConnected => socket != null && socket.State == WebSocketState.Open;

        [SerializeField] private string serverUrl = "wss://neuro.serenityutils.club";
        [SerializeField] private float connectTimeoutSeconds = 8f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task Connect()
        {
            if (IsConnected) return;

            CleanupSocket();

            if (!TryCreateServerUri(out Uri serverUri))
            {
                Debug.LogError("Connection failed: invalid server URL `" + serverUrl + "`");
                return;
            }

            socket = new ClientWebSocket();
            cts = new CancellationTokenSource();
            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource linkedCts = null;

            try
            {
                timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Mathf.Max(1f, connectTimeoutSeconds)));
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);

                await socket.ConnectAsync(serverUri, linkedCts.Token);
                Debug.Log("Connected to server");
                _ = ReceiveLoop();

                await SendPacket(new HandShakePacket("unity_game"));
            }
            catch (OperationCanceledException) when (timeoutCts != null && timeoutCts.IsCancellationRequested)
            {
                Debug.LogError("Connection failed: timeout after " + connectTimeoutSeconds + "s to " + serverUri);
                CleanupSocket();
            }
            catch (Exception e)
            {
                Debug.LogError("Connection failed to " + serverUri + ": " + e.Message);
                CleanupSocket();
            }
            finally
            {
                if (linkedCts != null)
                {
                    linkedCts.Dispose();
                }

                if (timeoutCts != null)
                {
                    timeoutCts.Dispose();
                }
            }
        }

        public async Task SendPacket(Packet packet)
        {
            if (!IsConnected || socket == null) return;

            try {
                byte[] data = packet.Encode();
                await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, cts.Token);
            } catch (Exception e) {
                Debug.LogError("Send error: " + e.Message);
            }
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[16384];
            try
            {
                while (socket != null && socket.State == WebSocketState.Open && !cts.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token);
                        continue;
                    }

                    if (result.Count <= 0)
                    {
                        continue;
                    }

                    using (var ms = new MemoryStream())
                    {
                        ms.Write(buffer, 0, result.Count);
                        while (!result.EndOfMessage)
                        {
                            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token);
                                break;
                            }
                            if (result.Count > 0)
                            {
                                ms.Write(buffer, 0, result.Count);
                            }
                        }

                        if (ms.Length == 0)
                        {
                            continue;
                        }

                        byte[] packetData = ms.ToArray();
                        try
                        {
                            Packet packet = Packet.Decode(packetData, packetManager);
                            OnPacketReceived?.Invoke(packet);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Failed to decode packet: " + e.Message);
                        }
                    }
                }
            }
            catch (OperationCanceledException) {}
            catch (Exception e)
            {
                if (socket != null && socket.State != WebSocketState.Closed)
                    Debug.LogError("Receive error: " + e.Message);
            }
        }

        private void OnDestroy()
        {
            CleanupSocket();
        }

        private bool TryCreateServerUri(out Uri uri)
        {
            uri = null;
            string trimmedUrl = string.IsNullOrWhiteSpace(serverUrl) ? string.Empty : serverUrl.Trim();
            if (string.IsNullOrEmpty(trimmedUrl))
            {
                return false;
            }

            if (!trimmedUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                !trimmedUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                trimmedUrl = "wss://" + trimmedUrl;
            }

            return Uri.TryCreate(trimmedUrl, UriKind.Absolute, out uri);
        }

        private void CleanupSocket()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }

            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }
        }
    }
}
