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
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerSessionManager : MonoBehaviour
{
    private const string PlayerNamePrefKey = "MultiplayerPlayerName";
    private const string HostAddressPrefKey = "MultiplayerHostAddress";
    private const string PortPrefKey = "MultiplayerPort";
    private const string VoiceModePrefKey = "MultiplayerVoiceMode";
    private const string MicrophoneDevicePrefKey = "MultiplayerMicrophoneDevice";
    private const int DefaultPort = 7777;
    private const float SendIntervalSeconds = 0.03f;
    private const float ForcedStateIntervalSeconds = 0.5f;
    private const float MovementSendEpsilonSqr = 0.0004f;
    private const float YawSendEpsilon = 0.4f;
    private const float RemoteExtrapolationLimitSeconds = 0.1f;
    private const int VoiceSampleRate = 16000;
    private const int VoiceFrameSamples = 320;
    private const int MaxVoiceFramesPerUpdate = 3;

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

    public event Action<Packet> OnQuizPacket;

    public string LocalClientId => localClientId;

    public enum VoiceChatMode
    {
        AlwaysOn,
        PushToTalk,
        Muted
    }

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
        public TextMesh BadgeText;
        public VoicePlaybackSource Voice;
        public Vector3 TargetPosition;
        public Vector3 Velocity;
        public float TargetYaw;
        public float LastReceiveTime;
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

    private sealed class VoicePlaybackSource : MonoBehaviour
    {
        private readonly Queue<float> queuedSamples = new Queue<float>();
        private readonly object queueLock = new object();
        private AudioSource audioSource;
        private AudioClip streamClip;
        private int sampleRate = VoiceSampleRate;

        public void Initialize(int newSampleRate)
        {
            sampleRate = Mathf.Clamp(newSampleRate, 8000, 48000);
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 1.5f;
            audioSource.maxDistance = 20f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.volume = 0.9f;

            streamClip = AudioClip.Create("RemoteVoiceStream", sampleRate, 1, sampleRate, true, OnAudioRead);
            audioSource.clip = streamClip;
            audioSource.Play();
        }

        public void EnqueuePcm16(byte[] pcm16, int newSampleRate)
        {
            if (pcm16 == null || pcm16.Length < 2)
            {
                return;
            }

            if (audioSource == null || newSampleRate != sampleRate)
            {
                Initialize(newSampleRate);
            }

            lock (queueLock)
            {
                int maxQueued = sampleRate * 2;
                while (queuedSamples.Count > maxQueued)
                {
                    queuedSamples.Dequeue();
                }

                for (int i = 0; i + 1 < pcm16.Length; i += 2)
                {
                    short sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
                    queuedSamples.Enqueue(Mathf.Clamp(sample / 32768f, -1f, 1f));
                }
            }
        }

        private void OnAudioRead(float[] data)
        {
            lock (queueLock)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = queuedSamples.Count > 0 ? queuedSamples.Dequeue() : 0f;
                }
            }
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
    private float lastForcedStateTime = -999f;
    private Vector3 lastSentPosition;
    private float lastSentYaw;
    private bool hasSentState;
    private readonly SemaphoreSlim clientSendLock = new SemaphoreSlim(1, 1);
    private int stateSendInFlight;
    private VoiceChatMode voiceMode = VoiceChatMode.AlwaysOn;
    private string preferredMicrophoneDevice = string.Empty;
    private AudioClip microphoneClip;
    private string microphoneDevice;
    private int microphoneReadPosition;
    private readonly float[] microphoneFrame = new float[VoiceFrameSamples];
    private int voiceSequence;
    private bool localLabelAttached;
    private Transform localNameLabelRoot;
    private TextMesh localNameLabelText;
    private string currentStatus = "Offline";

    public string CurrentStatus => currentStatus;
    public bool IsVoiceChatEnabled => voiceMode != VoiceChatMode.Muted;
    public VoiceChatMode CurrentVoiceMode => voiceMode;
    public string CurrentMicrophoneDevice
    {
        get
        {
            string resolvedDevice = ResolveMicrophoneDevice();
            if (!string.IsNullOrWhiteSpace(resolvedDevice))
            {
                return resolvedDevice;
            }

            string[] devices = GetMicrophoneDevices();
            return devices.Length > 0 ? "Default (" + devices[0] + ")" : "No microphone";
        }
    }

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
        voiceMode = (VoiceChatMode)Mathf.Clamp(PlayerPrefs.GetInt(VoiceModePrefKey, (int)VoiceChatMode.AlwaysOn), 0, 2);
        preferredMicrophoneDevice = PlayerPrefs.GetString(MicrophoneDevicePrefKey, string.Empty);
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

        CaptureAndSendVoice();
        InterpolateRemoteAvatars();
        RefreshLocalNameLabel();
    }

    private void InterpolateRemoteAvatars()
    {
        const float posSpeed = 60f;
        const float rotSpeed = 40f;
        float dt = Time.unscaledDeltaTime;

        foreach (RemoteAvatar avatar in remoteAvatars.Values)
        {
            if (avatar == null || avatar.Root == null || !avatar.HasFirstPacket)
            {
                continue;
            }

            Vector3 displayTarget = avatar.TargetPosition;
            float extrapolateSeconds = Mathf.Clamp(Time.unscaledTime - avatar.LastReceiveTime, 0f, RemoteExtrapolationLimitSeconds);
            displayTarget += avatar.Velocity * extrapolateSeconds;

            float dist = Vector3.Distance(avatar.Root.transform.position, displayTarget);

            // If we're more than 2 units behind just snap — avoids visible lag spikes
            // after a packet loss or stall.
            if (dist > 2f)
            {
                avatar.Root.transform.position = displayTarget;
                avatar.Root.transform.rotation = Quaternion.Euler(0f, avatar.TargetYaw, 0f);
                continue;
            }

            avatar.Root.transform.position = Vector3.Lerp(
                avatar.Root.transform.position,
                displayTarget,
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

    public bool ToggleVoiceChat()
    {
        SetVoiceMode(voiceMode == VoiceChatMode.Muted ? VoiceChatMode.AlwaysOn : VoiceChatMode.Muted);
        return IsVoiceChatEnabled;
    }

    public VoiceChatMode CycleVoiceMode()
    {
        VoiceChatMode next = voiceMode switch
        {
            VoiceChatMode.AlwaysOn => VoiceChatMode.PushToTalk,
            VoiceChatMode.PushToTalk => VoiceChatMode.Muted,
            _ => VoiceChatMode.AlwaysOn,
        };
        SetVoiceMode(next);
        return voiceMode;
    }

    public void SetVoiceChatEnabled(bool enabled)
    {
        SetVoiceMode(enabled ? VoiceChatMode.AlwaysOn : VoiceChatMode.Muted);
    }

    public void SetVoiceMode(VoiceChatMode modeToSet)
    {
        voiceMode = modeToSet;
        PlayerPrefs.SetInt(VoiceModePrefKey, (int)voiceMode);
        PlayerPrefs.Save();

        if (voiceMode == VoiceChatMode.Muted)
        {
            StopVoiceCapture();
        }

        SetStatus("Voice chat: " + GetVoiceModeLabel());
    }

    public string[] GetMicrophoneDevices()
    {
        return Microphone.devices ?? Array.Empty<string>();
    }

    public void CycleMicrophoneDevice()
    {
        string[] devices = GetMicrophoneDevices();
        if (devices.Length == 0)
        {
            SetMicrophoneDevice(string.Empty);
            return;
        }

        int currentIndex = Array.IndexOf(devices, preferredMicrophoneDevice);
        int nextIndex = currentIndex + 1;
        if (nextIndex >= devices.Length)
        {
            SetMicrophoneDevice(string.Empty);
            return;
        }

        SetMicrophoneDevice(devices[nextIndex]);
    }

    public void SetMicrophoneDevice(string deviceName)
    {
        preferredMicrophoneDevice = string.IsNullOrWhiteSpace(deviceName) ? string.Empty : deviceName;
        PlayerPrefs.SetString(MicrophoneDevicePrefKey, preferredMicrophoneDevice);
        PlayerPrefs.Save();

        bool restartCapture = microphoneClip != null;
        StopVoiceCapture();
        if (restartCapture && voiceMode != VoiceChatMode.Muted)
        {
            StartVoiceCapture();
        }

        SetStatus("Microphone: " + CurrentMicrophoneDevice);
    }

    public string GetVoiceModeLabel()
    {
        return voiceMode switch
        {
            VoiceChatMode.AlwaysOn => "Always On",
            VoiceChatMode.PushToTalk => "Push To Talk",
            _ => "Muted",
        };
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

    /// <summary>Host: send a quiz packet to all connected clients.</summary>
    private static void ApplyCustomSpawnIfSet()
    {
        if (PlayerPrefs.GetInt("MP_UseCustomSpawn", 0) != 1) return;
        float x = PlayerPrefs.GetFloat("MP_SpawnX", 0f);
        float y = PlayerPrefs.GetFloat("MP_SpawnY", 0f);
        float z = PlayerPrefs.GetFloat("MP_SpawnZ", 0f);
        Transform player = PlayerCache.ResolvePlayerTransform();
        if (player != null) player.position = new Vector3(x, y, z);
    }

    public void BroadcastQuizPacket(Packet packet)
    {
        BroadcastServerPacket(packet);
        // Also deliver locally so the host's own QuizManager receives it.
        EnqueueMainThread(() => OnQuizPacket?.Invoke(packet));
    }

    /// <summary>Client: send a quiz answer packet to the host.</summary>
    public void SendQuizPacketToHost(Packet packet)
    {
        _ = SendClientPacketAsync(packet);
    }

    public void StopAllNetworking()    {
        mode = SessionMode.Idle;
        localClientId = string.Empty;
        hasSentState = false;
        StopVoiceCapture();

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
                        statePacket.ClientId = peer.ClientId;
                        statePacket.PlayerName = peer.PlayerName;
                        BroadcastServerPacket(statePacket, excludeClientId: peer.ClientId);
                        break;

                    case QuizAnswerPacket quizAnswer:
                        // Forward the client's answer to the host's local client connection
                        // by broadcasting to all — the host's own client loop receives it.
                        BroadcastServerPacket(quizAnswer);
                        break;

                    case MultiplayerVoicePacket voicePacket:
                        if (string.IsNullOrEmpty(peer.ClientId))
                        {
                            break;
                        }

                        voicePacket.ClientId = peer.ClientId;
                        BroadcastServerPacket(voicePacket, excludeClientId: peer.ClientId);
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
                    ApplyCustomSpawnIfSet();
                });
                break;

            case MultiplayerPlayerStatePacket statePacket:
                // Drop packets with no clientId (malformed) or that echo our own position back.
                if (string.IsNullOrEmpty(statePacket.ClientId))
                {
                    break;
                }
                if (statePacket.ClientId == localClientId)
                {
                    break;
                }

                EnqueueMainThread(() => ApplyRemoteState(statePacket));
                break;

            case MultiplayerPlayerLeftPacket leftPacket:
                EnqueueMainThread(() => RemoveRemoteAvatar(leftPacket.ClientId));
                break;

            case QuizStartPacket _:
            case QuizResultPacket _:
                // Delivered to all clients by the host
                EnqueueMainThread(() => OnQuizPacket?.Invoke(packet));
                break;

            case QuizAnswerPacket answerPacket:
                // Route answer back to host's quiz manager (host receives its own broadcast echo)
                EnqueueMainThread(() => OnQuizPacket?.Invoke(answerPacket));
                break;

            case MultiplayerVoicePacket voicePacket:
                if (!string.IsNullOrEmpty(voicePacket.ClientId) && voicePacket.ClientId != localClientId)
                {
                    EnqueueMainThread(() => ApplyRemoteVoice(voicePacket));
                }
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
        else
        {
            float dt = Mathf.Max(0.001f, Time.unscaledTime - avatar.LastReceiveTime);
            avatar.Velocity = (newPos - avatar.TargetPosition) / dt;
        }

        avatar.TargetPosition = newPos;
        avatar.TargetYaw = statePacket.Yaw;
        avatar.LastReceiveTime = Time.unscaledTime;
        SetAvatarName(avatar, statePacket.PlayerName);
    }

    private void ApplyRemoteVoice(MultiplayerVoicePacket voicePacket)
    {
        if (voicePacket?.Pcm16 == null || voicePacket.Pcm16.Length == 0)
        {
            return;
        }

        if (!remoteAvatars.TryGetValue(voicePacket.ClientId, out RemoteAvatar avatar) || avatar?.Root == null)
        {
            return;
        }

        if (avatar.Voice == null)
        {
            avatar.Voice = avatar.Root.AddComponent<VoicePlaybackSource>();
            avatar.Voice.Initialize(voicePacket.SampleRate > 0 ? voicePacket.SampleRate : VoiceSampleRate);
        }

        avatar.Voice.EnqueuePcm16(voicePacket.Pcm16, voicePacket.SampleRate > 0 ? voicePacket.SampleRate : VoiceSampleRate);
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

        // Chest badge — a small quad with a dark backing + name text, parented to the
        // bean body at chest height, facing forward (no billboard, rotates with body).
        // At scale 2.2 the bean's chest sits at roughly local y=1.1, z=0.28 forward.
        TextMesh badgeText = CreateChestBadge(root.transform, playerName);

        return new RemoteAvatar
        {
            Root = root,
            NameRoot = labelRoot.transform,
            NameText = textMesh,
            BadgeText = badgeText
        };
    }

    private TextMesh CreateChestBadge(Transform parent, string playerName)
    {
        // Badge backing quad
        GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Quad);
        badge.name = "ChestBadge";
        badge.transform.SetParent(parent, false);
        // chest height ~1.1, slightly in front of the body surface
        badge.transform.localPosition = new Vector3(0f, 1.1f, 0.32f);
        badge.transform.localRotation = Quaternion.identity;
        badge.transform.localScale = new Vector3(0.55f, 0.22f, 1f);

        Collider badgeCol = badge.GetComponent<Collider>();
        if (badgeCol != null) Destroy(badgeCol);

        Renderer badgeRend = badge.GetComponent<Renderer>();
        if (badgeRend != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            badgeRend.material = mat;
            badgeRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            badgeRend.receiveShadows = false;
        }

        // Badge text sits just in front of the quad
        GameObject textObj = new GameObject("BadgeText");
        textObj.transform.SetParent(parent, false);
        textObj.transform.localPosition = new Vector3(0f, 1.1f, 0.33f);
        textObj.transform.localRotation = Quaternion.identity;

        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 28;
        tm.characterSize = 0.028f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        tm.text = NormalizePlayerName(playerName);

        return tm;
    }

    private void SetAvatarName(RemoteAvatar avatar, string playerName)
    {
        if (avatar == null) return;
        string name = NormalizePlayerName(playerName);
        if (avatar.NameText != null) avatar.NameText.text = name;
        if (avatar.BadgeText != null) avatar.BadgeText.text = name;
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

        // Don't send state until the server has assigned us a clientId via WelcomePacket.
        // Early state packets with an empty clientId create ghost avatars on the remote side.
        if (string.IsNullOrEmpty(localClientId))
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
            bool forcedState = Time.unscaledTime - lastForcedStateTime >= ForcedStateIntervalSeconds;
            if (hasSentState &&
                !forcedState &&
                (position - lastSentPosition).sqrMagnitude < MovementSendEpsilonSqr &&
                Mathf.Abs(Mathf.DeltaAngle(yaw, lastSentYaw)) < YawSendEpsilon)
            {
                return;
            }

            await SendClientPacketAsync(new MultiplayerPlayerStatePacket(localClientId, localPlayerName, position, yaw));
            lastSentPosition = position;
            lastSentYaw = yaw;
            hasSentState = true;
            if (forcedState)
            {
                lastForcedStateTime = Time.unscaledTime;
            }
        }
        finally
        {
            Interlocked.Exchange(ref stateSendInFlight, 0);
        }
    }

    private void CaptureAndSendVoice()
    {
        if (voiceMode == VoiceChatMode.Muted || !IsClientConnected || string.IsNullOrEmpty(localClientId))
        {
            StopVoiceCapture();
            return;
        }

        if (microphoneClip == null)
        {
            StartVoiceCapture();
            return;
        }

        bool shouldTransmit = voiceMode == VoiceChatMode.AlwaysOn || IsPushToTalkPressed();
        int writePosition = Microphone.GetPosition(microphoneDevice);
        if (writePosition < 0 || microphoneClip == null)
        {
            return;
        }

        int sampleCount = microphoneClip.samples;
        int available = writePosition >= microphoneReadPosition
            ? writePosition - microphoneReadPosition
            : (sampleCount - microphoneReadPosition) + writePosition;

        if (available > VoiceSampleRate / 2)
        {
            microphoneReadPosition = (writePosition - VoiceFrameSamples + sampleCount) % sampleCount;
            available = VoiceFrameSamples;
        }

        if (!shouldTransmit)
        {
            microphoneReadPosition = writePosition;
            return;
        }

        int framesSent = 0;
        while (available >= VoiceFrameSamples && framesSent < MaxVoiceFramesPerUpdate)
        {
            ReadMicrophoneFrame(sampleCount);
            byte[] pcm16 = EncodePcm16(microphoneFrame);
            _ = SendClientPacketAsync(new MultiplayerVoicePacket(localClientId, voiceSequence++, VoiceSampleRate, pcm16));

            available -= VoiceFrameSamples;
            framesSent++;
        }
    }

    private void StartVoiceCapture()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return;
        }
#endif
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            return;
        }

        microphoneDevice = ResolveMicrophoneDevice();
        microphoneClip = Microphone.Start(microphoneDevice, true, 1, VoiceSampleRate);
        microphoneReadPosition = 0;
    }

    private void StopVoiceCapture()
    {
        if (microphoneClip != null)
        {
            Microphone.End(microphoneDevice);
        }

        microphoneClip = null;
        microphoneReadPosition = 0;
    }

    private static bool IsPushToTalkPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.vKey.isPressed;
    }

    private string ResolveMicrophoneDevice()
    {
        if (string.IsNullOrWhiteSpace(preferredMicrophoneDevice))
        {
            return null;
        }

        string[] devices = GetMicrophoneDevices();
        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i] == preferredMicrophoneDevice)
            {
                return preferredMicrophoneDevice;
            }
        }

        preferredMicrophoneDevice = string.Empty;
        PlayerPrefs.SetString(MicrophoneDevicePrefKey, preferredMicrophoneDevice);
        PlayerPrefs.Save();
        return null;
    }

    private void ReadMicrophoneFrame(int sampleCount)
    {
        if (microphoneReadPosition + VoiceFrameSamples <= sampleCount)
        {
            microphoneClip.GetData(microphoneFrame, microphoneReadPosition);
            microphoneReadPosition = (microphoneReadPosition + VoiceFrameSamples) % sampleCount;
            return;
        }

        int firstCount = sampleCount - microphoneReadPosition;
        float[] wrapBuffer = new float[VoiceFrameSamples];
        microphoneClip.GetData(wrapBuffer, microphoneReadPosition);
        Array.Copy(wrapBuffer, 0, microphoneFrame, 0, firstCount);
        microphoneClip.GetData(wrapBuffer, 0);
        Array.Copy(wrapBuffer, 0, microphoneFrame, firstCount, VoiceFrameSamples - firstCount);
        microphoneReadPosition = VoiceFrameSamples - firstCount;
    }

    private static byte[] EncodePcm16(float[] samples)
    {
        byte[] pcm16 = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
            pcm16[i * 2] = (byte)(value & 0xff);
            pcm16[(i * 2) + 1] = (byte)((value >> 8) & 0xff);
        }

        return pcm16;
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
