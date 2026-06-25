using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Mentora.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerSessionManager : MonoBehaviour
{
    private const string PlayerNamePrefKey = "MultiplayerPlayerName";
    private const string HostAddressPrefKey = "MultiplayerHostAddress";
    private const string PortPrefKey = "MultiplayerPort";
    private const int DefaultPort = 7777;
    private const float SendIntervalSeconds = 0.015f;

    private static MultiplayerSessionManager instance;

    public static MultiplayerSessionManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<MultiplayerSessionManager>();
            if (instance != null)
            {
                return instance;
            }

            GameObject root = new GameObject("MultiplayerSessionManager");
            instance = root.AddComponent<MultiplayerSessionManager>();
            DontDestroyOnLoad(root);
            return instance;
        }
    }

    public event Action<string> StatusChanged;

    private enum SessionMode
    {
        Idle,
        Hosting,
        Joining
    }

    private sealed class NetworkPeer
    {
        public string ClientId;
        public string PlayerName;
        public Vector3 Position;
        public float Yaw;
        public TcpClient TcpClient;
        public NetworkStream Stream;
        public readonly SemaphoreSlim SendLock = new SemaphoreSlim(1, 1);
        public bool IsConnected => TcpClient != null && TcpClient.Connected;
    }

    private sealed class RemoteAvatar
    {
        public GameObject Root;
        public Transform NameRoot;
        public TextMesh NameText;
        public Vector3 TargetPosition;
        public float TargetYaw;
        public bool HasFirstPacket;
    }

    private sealed class BillboardToCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 toCamera = transform.position - cam.transform.position;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }
    }

    private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
    private readonly ConcurrentDictionary<string, RemoteAvatar> remoteAvatars = new ConcurrentDictionary<string, RemoteAvatar>();
    private readonly ConcurrentDictionary<string, NetworkPeer> serverPeers = new ConcurrentDictionary<string, NetworkPeer>();

    private SessionMode mode = SessionMode.Idle;
    private TcpListener serverListener;
    private CancellationTokenSource serverCts;

    private TcpClient clientSocket;
    private NetworkStream clientStream;
    private CancellationTokenSource clientCts;
    private Task clientReceiveTask;
    private string localClientId = string.Empty;
    private string localPlayerName = "Player";
    private string hostAddress = "127.0.0.1";
    private int hostPort = DefaultPort;
    private float lastSendTime = -999f;
    private readonly SemaphoreSlim clientSendLock = new SemaphoreSlim(1, 1);
    private int stateSendInFlight;
    private bool localLabelAttached;
    private Transform localNameLabelRoot;
    private TextMesh localNameLabelText;
    private string currentStatus = "Offline";

    public string CurrentStatus => currentStatus;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        hostAddress = PlayerPrefs.GetString(HostAddressPrefKey, hostAddress);
        hostPort = PlayerPrefs.GetInt(PortPrefKey, DefaultPort);
        localPlayerName = GetSavedName();
        SetStatus(currentStatus);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopAllNetworking();
            instance = null;
        }
    }

    private void Update()
    {
        while (mainThreadQueue.TryDequeue(out Action action))
        {
            action?.Invoke();
        }

        if (IsClientConnected && Time.unscaledTime - lastSendTime >= SendIntervalSeconds)
        {
            lastSendTime = Time.unscaledTime;
            _ = SendLocalStateAsync();
        }

        InterpolateRemoteAvatars();
        RefreshLocalNameLabel();
    }

    private void InterpolateRemoteAvatars()
    {
        // Smooth = 20 means the avatar covers ~63% of the remaining gap per second,
        // giving fluid motion at 66 Hz send rate with no visible rubber-banding.
        const float posSpeed = 20f;
        const float rotSpeed = 20f;
        float dt = Time.unscaledDeltaTime;

        foreach (RemoteAvatar avatar in remoteAvatars.Values)
        {
            if (avatar == null || avatar.Root == null || !avatar.HasFirstPacket)
            {
                continue;
            }

            avatar.Root.transform.position = Vector3.Lerp(
                avatar.Root.transform.position,
                avatar.TargetPosition,
                1f - Mathf.Exp(-posSpeed * dt));

            avatar.Root.transform.rotation = Quaternion.Slerp(
                avatar.Root.transform.rotation,
                Quaternion.Euler(0f, avatar.TargetYaw, 0f),
                1f - Mathf.Exp(-rotSpeed * dt));
        }
    }

    public void SetPlayerName(string playerName)
    {
        localPlayerName = NormalizePlayerName(playerName);
        PlayerPrefs.SetString(PlayerNamePrefKey, localPlayerName);
        PlayerPrefs.Save();
        RefreshLocalNameLabel();
    }

    public void SetHostAddress(string address)
    {
        hostAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        PlayerPrefs.SetString(HostAddressPrefKey, hostAddress);
        PlayerPrefs.Save();
    }

    public void SetHostPort(int port)
    {
        hostPort = Mathf.Clamp(port, 1024, 65535);
        PlayerPrefs.SetInt(PortPrefKey, hostPort);
        PlayerPrefs.Save();
    }

    public async void HostGame(string playerName, int port)
    {
        SetPlayerName(playerName);
        SetHostPort(port);
        SetHostAddress("127.0.0.1");
        StopAllNetworking();
        mode = SessionMode.Hosting;

        try
        {
            serverCts = new CancellationTokenSource();
            serverListener = new TcpListener(IPAddress.Any, hostPort);
            serverListener.Start();
            SetStatus("Hosting on port " + hostPort + "...");
            _ = RunServerLoopAsync(serverCts.Token);
            await JoinLocalHostAsync();
            SetStatus("Hosting on port " + hostPort + " and connected locally.");
        }
        catch (Exception ex)
        {
            SetStatus("Host failed: " + ex.Message);
            StopAllNetworking();
        }
    }

    public async void JoinGame(string playerName, string address, int port)
    {
        SetPlayerName(playerName);
        SetHostAddress(address);
        SetHostPort(port);
        StopAllNetworking();
        mode = SessionMode.Joining;

        try
        {
            await ConnectClientAsync(hostAddress, hostPort);
            SetStatus("Connected to " + hostAddress + ":" + hostPort);
        }
        catch (Exception ex)
        {
            SetStatus("Join failed: " + ex.Message);
            StopAllNetworking();
        }
    }

    public void StopAllNetworking()
    {
        mode = SessionMode.Idle;
        localClientId = string.Empty;

        if (clientCts != null)
        {
            clientCts.Cancel();
            clientCts.Dispose();
            clientCts = null;
        }

        if (clientStream != null)
        {
            clientStream.Dispose();
            clientStream = null;
        }

        if (clientSocket != null)
        {
            clientSocket.Close();
            clientSocket.Dispose();
            clientSocket = null;
        }

        if (serverCts != null)
        {
            serverCts.Cancel();
            serverCts.Dispose();
            serverCts = null;
        }

        if (serverListener != null)
        {
            try
            {
                serverListener.Stop();
            }
            catch { }
            serverListener = null;
        }

        foreach (var peer in serverPeers.Values)
        {
            try
            {
                peer.Stream?.Dispose();
                peer.TcpClient?.Close();
            }
            catch { }
        }
        serverPeers.Clear();

        foreach (var avatar in remoteAvatars.Values)
        {
            if (avatar?.Root != null)
            {
                Destroy(avatar.Root);
            }
        }
        remoteAvatars.Clear();

        if (localNameLabelRoot != null)
        {
            Destroy(localNameLabelRoot.gameObject);
            localNameLabelRoot = null;
            localNameLabelText = null;
        }

        localLabelAttached = false;
        SetStatus("Offline");
    }

    private bool IsClientConnected => clientSocket != null && clientSocket.Connected && clientStream != null;

    private async Task JoinLocalHostAsync()
    {
        await ConnectClientAsync("127.0.0.1", hostPort);
    }

    private async Task ConnectClientAsync(string address, int port)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Host address is empty.");
        }

        clientSocket = new TcpClient();
        clientSocket.NoDelay = true;
        clientCts = new CancellationTokenSource();
        clientReceiveTask = null;

        await clientSocket.ConnectAsync(address, port);
        clientStream = clientSocket.GetStream();

        clientReceiveTask = Task.Run(() => ClientReceiveLoopAsync(clientCts.Token));

        await SendClientPacketAsync(new MultiplayerJoinPacket(localPlayerName));
        SetStatus("Joined " + address + ":" + port);
    }

    private async Task RunServerLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (serverListener == null)
                {
                    break;
                }

                if (!serverListener.Pending())
                {
                    await Task.Delay(25, token);
                    continue;
                }

                TcpClient tcpClient = await serverListener.AcceptTcpClientAsync();
                tcpClient.NoDelay = true;
                _ = HandleIncomingServerClientAsync(tcpClient, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EnqueueMainThread(() => SetStatus("Server stopped: " + ex.Message));
        }
    }

    private async Task HandleIncomingServerClientAsync(TcpClient tcpClient, CancellationToken serverToken)
    {
        NetworkPeer peer = new NetworkPeer
        {
            TcpClient = tcpClient,
            Stream = tcpClient.GetStream()
        };

        try
        {
            while (!serverToken.IsCancellationRequested && tcpClient.Connected)
            {
                byte[] frame = await ReadFrameAsync(peer.Stream, serverToken);
                if (frame == null)
                {
                    break;
                }

                Packet packet = Packet.DecodeRaw(frame, new PacketManager());
                switch (packet)
                {
                    case MultiplayerJoinPacket joinPacket:
                        peer.ClientId = Guid.NewGuid().ToString("N");
                        peer.PlayerName = NormalizePlayerName(joinPacket.PlayerName);
                        serverPeers[peer.ClientId] = peer;
                        await SendServerPacketAsync(peer, new MultiplayerWelcomePacket(peer.ClientId, peer.PlayerName));
                        await SendExistingPlayersToClientAsync(peer);
                        SetStatus(peer.PlayerName + " joined the session.");
                        break;

                    case MultiplayerPlayerStatePacket statePacket:
                        if (string.IsNullOrEmpty(peer.ClientId))
                        {
                            break;
                        }

                        peer.PlayerName = NormalizePlayerName(statePacket.PlayerName);
                        peer.Position = new Vector3(statePacket.PositionX, statePacket.PositionY, statePacket.PositionZ);
                        peer.Yaw = statePacket.Yaw;
                        BroadcastServerPacket(statePacket, excludeClientId: peer.ClientId);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            EnqueueMainThread(() => SetStatus("Peer dropped: " + ex.Message));
        }
        finally
        {
            if (!string.IsNullOrEmpty(peer.ClientId))
            {
                serverPeers.TryRemove(peer.ClientId, out _);
                BroadcastServerPacket(new MultiplayerPlayerLeftPacket(peer.ClientId), excludeClientId: peer.ClientId);
                EnqueueMainThread(() => RemoveRemoteAvatar(peer.ClientId));
            }

            try
            {
                peer.Stream?.Dispose();
                peer.TcpClient?.Close();
                peer.SendLock.Dispose();
            }
            catch { }
        }
    }

    private async Task SendExistingPlayersToClientAsync(NetworkPeer peer)
    {
        foreach (NetworkPeer existing in serverPeers.Values)
        {
            if (existing == peer || string.IsNullOrEmpty(existing.ClientId))
            {
                continue;
            }

            await SendServerPacketAsync(peer, new MultiplayerPlayerStatePacket(
                existing.ClientId,
                existing.PlayerName,
                existing.Position,
                existing.Yaw));
        }
    }

    private async Task ClientReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && clientSocket != null && clientSocket.Connected)
            {
                byte[] frame = await ReadFrameAsync(clientStream, token);
                if (frame == null)
                {
                    break;
                }

                Packet packet = Packet.DecodeRaw(frame, new PacketManager());
                HandleClientPacket(packet);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EnqueueMainThread(() => SetStatus("Connection lost: " + ex.Message));
        }
    }

    private void HandleClientPacket(Packet packet)
    {
        switch (packet)
        {
            case MultiplayerWelcomePacket welcomePacket:
                localClientId = welcomePacket.ClientId;
                localPlayerName = NormalizePlayerName(welcomePacket.PlayerName);
                EnqueueMainThread(() =>
                {
                    RefreshLocalNameLabel();
                    SetStatus("Connected as " + localPlayerName);
                });
                break;

            case MultiplayerPlayerStatePacket statePacket:
                if (!string.IsNullOrEmpty(localClientId) && statePacket.ClientId == localClientId)
                {
                    break;
                }

                EnqueueMainThread(() => ApplyRemoteState(statePacket));
                break;

            case MultiplayerPlayerLeftPacket leftPacket:
                EnqueueMainThread(() => RemoveRemoteAvatar(leftPacket.ClientId));
                break;
        }
    }

    private void ApplyRemoteState(MultiplayerPlayerStatePacket statePacket)
    {
        if (string.IsNullOrEmpty(statePacket.ClientId))
        {
            return;
        }

        RemoteAvatar avatar = remoteAvatars.GetOrAdd(statePacket.ClientId, _ => CreateRemoteAvatar(statePacket.PlayerName));
        if (avatar == null || avatar.Root == null)
        {
            return;
        }

        Vector3 newPos = new Vector3(statePacket.PositionX, statePacket.PositionY, statePacket.PositionZ);
        if (!avatar.HasFirstPacket)
        {
            avatar.Root.transform.position = newPos;
            avatar.Root.transform.rotation = Quaternion.Euler(0f, statePacket.Yaw, 0f);
            avatar.HasFirstPacket = true;
        }

        avatar.TargetPosition = newPos;
        avatar.TargetYaw = statePacket.Yaw;
        SetAvatarName(avatar, statePacket.PlayerName);
    }

    private RemoteAvatar CreateRemoteAvatar(string playerName)
    {
        GameObject root = new GameObject("RemoteBean_" + playerName);

        GameObject beanPrefab = Resources.Load<GameObject>("Characters/SM_Bean_Female_01");
        if (beanPrefab != null)
        {
            GameObject body = UnityEngine.Object.Instantiate(beanPrefab, root.transform);
            body.name = "BeanBody";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * 2.2f;

            foreach (Collider col in body.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }
        }
        else
        {
            // Fallback: tinted capsule if prefab is missing
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(root.transform, false);
            capsule.transform.localScale = new Vector3(1.3f, 1.7f, 1.3f);
            Collider col = capsule.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer rend = capsule.GetComponent<Renderer>();
            if (rend != null)
            {
                int hash = Mathf.Abs((playerName ?? "Player").GetHashCode());
                float hue = (hash % 360) / 360f;
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = Color.HSVToRGB(hue, 0.45f, 0.95f);
                rend.material = mat;
            }
        }

        GameObject labelRoot = new GameObject("NameLabel");
        labelRoot.transform.SetParent(root.transform, false);
        labelRoot.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        labelRoot.AddComponent<BillboardToCamera>();

        TextMesh textMesh = labelRoot.AddComponent<TextMesh>();
        textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textMesh.fontSize = 48;
        textMesh.characterSize = 0.02f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.black;
        textMesh.text = NormalizePlayerName(playerName);

        return new RemoteAvatar
        {
            Root = root,
            NameRoot = labelRoot.transform,
            NameText = textMesh
        };
    }

    private void SetAvatarName(RemoteAvatar avatar, string playerName)
    {
        if (avatar == null || avatar.NameText == null)
        {
            return;
        }

        avatar.NameText.text = NormalizePlayerName(playerName);
    }

    private void RemoveRemoteAvatar(string clientId)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return;
        }

        if (remoteAvatars.TryRemove(clientId, out RemoteAvatar avatar) && avatar?.Root != null)
        {
            Destroy(avatar.Root);
        }
    }

    private async Task SendLocalStateAsync()
    {
        if (!IsClientConnected)
        {
            return;
        }

        // Drop this tick if the previous state send is still in flight. Player
        // state is latest-wins, so skipping a stale frame is correct — and it
        // stops a backlog of overlapping writes from piling up at the ~66 Hz
        // send rate, which is what was corrupting the stream and desyncing.
        if (Interlocked.Exchange(ref stateSendInFlight, 1) == 1)
        {
            return;
        }

        try
        {
            Transform player = PlayerCache.ResolvePlayerTransform();
            if (player == null)
            {
                return;
            }

            Vector3 position = player.position;
            float yaw = player.eulerAngles.y;
            await SendClientPacketAsync(new MultiplayerPlayerStatePacket(localClientId, localPlayerName, position, yaw));
        }
        finally
        {
            Interlocked.Exchange(ref stateSendInFlight, 0);
        }
    }

    private async Task SendClientPacketAsync(Packet packet)
    {
        if (!IsClientConnected || packet == null)
        {
            return;
        }

        byte[] payload = packet.EncodeRaw();
        byte[] frame = new byte[4 + payload.Length];
        byte[] lengthBytes = Packet.IntToBigEndian(payload.Length);
        Buffer.BlockCopy(lengthBytes, 0, frame, 0, 4);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

        NetworkStream stream = clientStream;
        if (stream == null)
        {
            return;
        }

        // Serialize every write to the client socket. Without this, the periodic
        // state send and the join handshake can issue overlapping WriteAsync
        // calls on the same NetworkStream, interleaving the length-prefixed
        // frames and permanently desyncing the receiver's frame parser.
        await clientSendLock.WaitAsync();
        try
        {
            await stream.WriteAsync(frame, 0, frame.Length, CancellationToken.None);
        }
        catch (Exception ex)
        {
            EnqueueMainThread(() => SetStatus("Send failed: " + ex.Message));
        }
        finally
        {
            clientSendLock.Release();
        }
    }

    private async Task SendServerPacketAsync(NetworkPeer peer, Packet packet)
    {
        if (peer == null || packet == null || peer.Stream == null || !peer.IsConnected)
        {
            return;
        }

        byte[] payload = packet.EncodeRaw();
        byte[] frame = new byte[4 + payload.Length];
        byte[] lengthBytes = Packet.IntToBigEndian(payload.Length);
        Buffer.BlockCopy(lengthBytes, 0, frame, 0, 4);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

        await peer.SendLock.WaitAsync();
        try
        {
            await peer.Stream.WriteAsync(frame, 0, frame.Length, CancellationToken.None);
        }
        catch
        {
            // The disconnect path will clean this up.
        }
        finally
        {
            peer.SendLock.Release();
        }
    }

    private void BroadcastServerPacket(Packet packet, string excludeClientId = null)
    {
        foreach (NetworkPeer peer in serverPeers.Values)
        {
            if (peer == null || string.IsNullOrEmpty(peer.ClientId) || peer.ClientId == excludeClientId)
            {
                continue;
            }

            _ = SendServerPacketAsync(peer, packet);
        }
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken token)
    {
        if (stream == null)
        {
            return null;
        }

        byte[] lengthBytes = await ReadExactAsync(stream, 4, token);
        if (lengthBytes == null)
        {
            return null;
        }

        int length = Packet.BigEndianToInt(lengthBytes, 0);
        if (length <= 0 || length > 65536)
        {
            return null;
        }

        return await ReadExactAsync(stream, length, token);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken token)
    {
        byte[] buffer = new byte[length];
        int offset = 0;

        while (offset < length)
        {
            int read = await stream.ReadAsync(buffer, offset, length - offset, token);
            if (read <= 0)
            {
                return null;
            }

            offset += read;
        }

        return buffer;
    }

    private void RefreshLocalNameLabel()
    {
        // Only show the name label when actually in a session.
        if (mode == SessionMode.Idle)
        {
            if (localNameLabelRoot != null)
            {
                Destroy(localNameLabelRoot.gameObject);
                localNameLabelRoot = null;
                localNameLabelText = null;
                localLabelAttached = false;
            }
            return;
        }

        Transform player = PlayerCache.ResolvePlayerTransform();
        if (player == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(localPlayerName))
        {
            localPlayerName = "Player";
        }

        if (localNameLabelRoot == null || localNameLabelRoot.parent != player)
        {
            if (localNameLabelRoot != null)
            {
                Destroy(localNameLabelRoot.gameObject);
            }

            GameObject labelRoot = new GameObject("LocalPlayerNameLabel");
            labelRoot.transform.SetParent(player, false);
            labelRoot.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            labelRoot.AddComponent<BillboardToCamera>();

            localNameLabelText = labelRoot.AddComponent<TextMesh>();
            localNameLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            localNameLabelText.fontSize = 48;
            localNameLabelText.characterSize = 0.02f;
            localNameLabelText.anchor = TextAnchor.MiddleCenter;
            localNameLabelText.alignment = TextAlignment.Center;
            localNameLabelText.color = Color.black;

            localNameLabelRoot = labelRoot.transform;
            localLabelAttached = true;
        }

        if (localNameLabelText != null)
        {
            localNameLabelText.text = localPlayerName;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        localLabelAttached = false;
        localNameLabelRoot = null;
        localNameLabelText = null;
        RefreshLocalNameLabel();
    }

    private void EnqueueMainThread(Action action)
    {
        if (action != null)
        {
            mainThreadQueue.Enqueue(action);
        }
    }

    private static string NormalizePlayerName(string playerName)
    {
        string trimmed = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        return trimmed.Length > 18 ? trimmed.Substring(0, 18) : trimmed;
    }

    private string GetSavedName()
    {
        string defaultName = !string.IsNullOrWhiteSpace(PauseMenuManager.GetLoggedInChildName())
            ? PauseMenuManager.GetLoggedInChildName()
            : "Player";

        return NormalizePlayerName(PlayerPrefs.GetString(PlayerNamePrefKey, defaultName));
    }

    private void SetStatus(string message)
    {
        currentStatus = string.IsNullOrWhiteSpace(message) ? "Offline" : message;
        EnqueueMainThread(() => StatusChanged?.Invoke(currentStatus));
        Debug.Log("[Multiplayer] " + currentStatus);
    }
}
