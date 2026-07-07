using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
    private const string PlayerModelPrefKey = "MultiplayerPlayerModel";
    private const string FemaleModelId = "female";
    private const string MaleModelId = "male";
    private const string FemaleModelResourcePath = "Characters/SM_Bean_Female_01";
    private const string MaleModelResourcePath = "Characters/SM_Bean_Cowboy_01";
    private const int DefaultPort = 7777;
    private const int DiscoveryPort = 7776;
    private const string DiscoveryPrefix = "MENTORA_MP_DISCOVERY_V1";
    private const float DiscoveryBeaconIntervalSeconds = 1f;
    private const float DiscoveryCleanupIntervalSeconds = 1f;
    private const float DiscoveryTimeoutSeconds = 5f;
    private const float SendIntervalSeconds = 0.015f;
    private const float ForcedStateIntervalSeconds = 0.1f;
    private const float MovementSendEpsilonSqr = 0.000025f;
    private const float YawSendEpsilon = 0.15f;
    private const float RemoteSnapDistance = 4f;
    private const float VoiceLevelDecayPerSecond = 3f;
    private const int VoiceSampleRate = 16000;
    private const int VoiceFrameSamples = 320;
    private const int MaxVoiceFramesPerUpdate = 6;
    private const int VoiceMeterBars = 5;
    private const int HudVoiceMeterBars = 12;
    private const float MaxVoiceQueuedSeconds = 0.5f;
    private const float VoicePlaybackStartBufferSeconds = 0.08f;
    private const float RemoteInterpolationLeadSeconds = 0.055f;
    private const float RemoteMaxExtrapolationMeters = 0.45f;

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

    public event Action<byte[], int, float> LocalVoiceFrameCaptured;

    public event Action FriendSessionsChanged;

    public string LocalClientId => localClientId;
    public string LocalPlayerName => localPlayerName;
    public int ConnectedPlayerCount => remoteAvatars.Count + 1; // remotes + self

    public List<string> GetConnectedPlayerIds(string localFallbackId = null)
    {
        List<string> ids = new List<string>();

        string localId = !string.IsNullOrWhiteSpace(localClientId) ? localClientId : localFallbackId;
        if (!string.IsNullOrWhiteSpace(localId))
        {
            ids.Add(localId);
        }

        foreach (string clientId in serverPeers.Keys)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                continue;
            }

            if (!ids.Contains(clientId))
            {
                ids.Add(clientId);
            }
        }

        foreach (string clientId in remoteAvatars.Keys)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                continue;
            }

            if (!ids.Contains(clientId))
            {
                ids.Add(clientId);
            }
        }

        return ids;
    }

    public string ResolvePlayerName(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return "Player";
        }

        if (string.Equals(clientId, localClientId, StringComparison.Ordinal))
        {
            return NormalizePlayerName(localPlayerName);
        }

        if (serverPeers.TryGetValue(clientId, out NetworkPeer peer) && peer != null && !string.IsNullOrWhiteSpace(peer.PlayerName))
        {
            return NormalizePlayerName(peer.PlayerName);
        }

        if (remoteAvatars.TryGetValue(clientId, out RemoteAvatar avatar) && avatar?.NameText != null && !string.IsNullOrWhiteSpace(avatar.NameText.text))
        {
            return NormalizePlayerName(avatar.NameText.text);
        }

        return "Player";
    }
    public bool IsHosting => mode == SessionMode.Hosting;
    public bool IsConnectedToSession => !string.IsNullOrEmpty(localClientId);

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
        public string ModelId = FemaleModelId;
        public TcpClient TcpClient;
        public NetworkStream Stream;
        public IPEndPoint UdpEndPoint;
        public readonly SemaphoreSlim SendLock = new SemaphoreSlim(1, 1);
        public bool IsConnected => TcpClient != null && TcpClient.Connected;
    }

    private sealed class RemoteAvatar
    {
        public GameObject Root;
        public Transform NameRoot;
        public TextMesh NameText;
        public VoicePlaybackSource Voice;
        public Transform VoiceMeterRoot;
        public Renderer[] VoiceMeterBars;
        public TextMesh VoiceIconText;
        public GameObject Body;
        public CapsuleCollider Collision;
        public string ModelId;
        public Vector3 TargetPosition;
        public Vector3 Velocity;
        public float TargetYaw;
        public float LastReceiveTime;
        public float VoiceLevel;
        public float LastVoiceTime;
        public int LastStateSequence = -1;
        public bool HasFirstPacket;
    }

    public struct FriendSession
    {
        public string PlayerName;
        public string Address;
        public int Port;
        public float AgeSeconds;

        public string DisplayName => string.IsNullOrWhiteSpace(PlayerName) ? "LAN Friend" : PlayerName;
        public string Endpoint => Address + ":" + Port;
    }

    private sealed class FriendSessionRecord
    {
        public string InstanceId;
        public string PlayerName;
        public string Address;
        public int Port;
        public DateTime LastSeenUtc;
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

            transform.rotation = cam.transform.rotation;
        }
    }

    private sealed class VoicePlaybackSource : MonoBehaviour
    {
        private const float PlaybackGain = 1.8f;
        private readonly Queue<float> queuedSamples = new Queue<float>();
        private readonly object queueLock = new object();
        private AudioSource audioSource;
        private AudioClip streamClip;
        private int sampleRate = VoiceSampleRate;
        private bool playbackStarted;

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
            audioSource.spatialBlend = 0.35f;
            audioSource.minDistance = 10f;
            audioSource.maxDistance = 60f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.dopplerLevel = 0f;
            audioSource.spread = 130f;
            audioSource.volume = 1f;

            streamClip = AudioClip.Create("RemoteVoiceStream", sampleRate, 1, sampleRate, true, OnAudioRead);
            audioSource.clip = streamClip;
            playbackStarted = false;
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
                int maxQueued = Mathf.RoundToInt(sampleRate * MaxVoiceQueuedSeconds);
                while (queuedSamples.Count > maxQueued)
                {
                    queuedSamples.Dequeue();
                }

                for (int i = 0; i + 1 < pcm16.Length; i += 2)
                {
                    short sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
                    queuedSamples.Enqueue(Mathf.Clamp(sample / 32768f, -1f, 1f));
                }

                while (queuedSamples.Count > maxQueued)
                {
                    queuedSamples.Dequeue();
                }
            }

            int startBufferSamples = Mathf.RoundToInt(sampleRate * VoicePlaybackStartBufferSeconds);
            if (!playbackStarted && queuedSamples.Count >= startBufferSamples)
            {
                audioSource.Play();
                playbackStarted = true;
            }
        }

        private void OnAudioRead(float[] data)
        {
            lock (queueLock)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = queuedSamples.Count > 0 ? Mathf.Clamp(queuedSamples.Dequeue() * PlaybackGain, -1f, 1f) : 0f;
                }
            }
        }
    }

    private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
    private readonly ConcurrentDictionary<string, RemoteAvatar> remoteAvatars = new ConcurrentDictionary<string, RemoteAvatar>();
    private readonly ConcurrentDictionary<string, NetworkPeer> serverPeers = new ConcurrentDictionary<string, NetworkPeer>();
    private readonly ConcurrentDictionary<string, FriendSessionRecord> discoveredFriendSessions = new ConcurrentDictionary<string, FriendSessionRecord>();
    private readonly string discoveryInstanceId = Guid.NewGuid().ToString("N");

    private SessionMode mode = SessionMode.Idle;
    private TcpListener serverListener;
    private CancellationTokenSource serverCts;
    private UdpClient serverUdp;
    private Task serverUdpTask;
    private UdpClient discoveryUdp;
    private UdpClient discoverySender;
    private CancellationTokenSource discoveryCts;
    private float lastDiscoveryBeaconTime = -999f;
    private float lastDiscoveryCleanupTime = -999f;

    private TcpClient clientSocket;
    private NetworkStream clientStream;
    private CancellationTokenSource clientCts;
    private Task clientReceiveTask;
    private UdpClient clientUdp;
    private IPEndPoint clientUdpRemoteEndPoint;
    private Task clientUdpReceiveTask;
    private string localClientId = string.Empty;
    private string localPlayerName = "Player";
    private string localModelId = FemaleModelId;
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
    private int voiceSendInFlight;
    private int stateSequence;
    private float lastUdpHelloTime = -999f;
    private float lastVoiceSendLogTime = -999f;
    private float lastVoiceReceiveLogTime = -999f;
    private int clientConnectionVersion;
    private int sendFailureReported;
    private bool localLabelAttached;
    private string appliedLocalModelId = string.Empty;
    private Transform localNameLabelRoot;
    private TextMesh localNameLabelText;
    private Transform localVoiceMeterRoot;
    private Renderer[] localVoiceMeterBars;
    private TextMesh localVoiceIconText;
    private Canvas localVoiceHudCanvas;
    private Image[] localVoiceHudBars;
    private Text localVoiceHudLabel;
    private float localVoiceLevel;
    private int externalVoiceCaptureRequests;
    private string currentStatus = "Offline";

    public string CurrentStatus => currentStatus;
    public bool IsVoiceChatEnabled => voiceMode != VoiceChatMode.Muted;
    public VoiceChatMode CurrentVoiceMode => voiceMode;
    public string CurrentPlayerModelLabel => GetModelLabel(localModelId);
    public float CurrentLocalVoiceLevel => localVoiceLevel;
    public bool IsMicrophoneCapturing => microphoneClip != null;
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
        localModelId = NormalizeModelId(PlayerPrefs.GetString(PlayerModelPrefKey, FemaleModelId));
        voiceMode = (VoiceChatMode)Mathf.Clamp(PlayerPrefs.GetInt(VoiceModePrefKey, (int)VoiceChatMode.AlwaysOn), 0, 2);
        preferredMicrophoneDevice = PlayerPrefs.GetString(MicrophoneDevicePrefKey, string.Empty);
        SetStatus(currentStatus);
        ApplyLocalPlayerModel();
        StartDiscoveryTransport();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopAllNetworking();
            StopDiscoveryTransport();
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
            SendLocalState();
        }

        if (IsClientConnected &&
            !string.IsNullOrEmpty(localClientId) &&
            Time.unscaledTime - lastUdpHelloTime >= 1f)
        {
            lastUdpHelloTime = Time.unscaledTime;
            SendUdpHello();
        }

        CaptureAndSendVoice();
        InterpolateRemoteAvatars();
        UpdateVoiceMeters();
        TickLanDiscovery();
        RefreshLocalNameLabel();
        ApplyLocalPlayerModelIfNeeded();
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
            float receiveAge = Mathf.Clamp(Time.unscaledTime - avatar.LastReceiveTime, 0f, RemoteInterpolationLeadSeconds);
            Vector3 predictedOffset = avatar.Velocity * receiveAge;
            if (predictedOffset.sqrMagnitude > RemoteMaxExtrapolationMeters * RemoteMaxExtrapolationMeters)
            {
                predictedOffset = predictedOffset.normalized * RemoteMaxExtrapolationMeters;
            }

            displayTarget += predictedOffset;
            float dist = Vector3.Distance(avatar.Root.transform.position, displayTarget);

            if (dist > RemoteSnapDistance)
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

    private void UpdateVoiceMeters()
    {
        float decay = VoiceLevelDecayPerSecond * Time.unscaledDeltaTime;
        localVoiceLevel = Mathf.Max(0f, localVoiceLevel - decay);
        SetVoiceMeterLevel(localVoiceMeterBars, localVoiceIconText, localVoiceLevel);
        EnsureLocalVoiceHud();
        SetVoiceHudLevel(localVoiceLevel);

        foreach (RemoteAvatar avatar in remoteAvatars.Values)
        {
            if (avatar == null)
            {
                continue;
            }

            avatar.VoiceLevel = Mathf.Max(0f, avatar.VoiceLevel - decay);
            if (Time.unscaledTime - avatar.LastVoiceTime > 0.35f)
            {
                avatar.VoiceLevel = Mathf.MoveTowards(avatar.VoiceLevel, 0f, decay * 2f);
            }

            SetVoiceMeterLevel(avatar.VoiceMeterBars, avatar.VoiceIconText, avatar.VoiceLevel);
        }
    }

    public void SetPlayerName(string playerName)
    {
        localPlayerName = NormalizePlayerName(playerName);
        PlayerPrefs.SetString(PlayerNamePrefKey, localPlayerName);
        PlayerPrefs.Save();
        RefreshLocalNameLabel();
    }

    public string CyclePlayerModel()
    {
        localModelId = localModelId == MaleModelId ? FemaleModelId : MaleModelId;
        PlayerPrefs.SetString(PlayerModelPrefKey, localModelId);
        PlayerPrefs.Save();
        appliedLocalModelId = string.Empty;
        ApplyLocalPlayerModel();
        hasSentState = false;
        lastForcedStateTime = -999f;
        SetStatus("Player model: " + GetModelLabel(localModelId));
        return CurrentPlayerModelLabel;
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

        if (voiceMode == VoiceChatMode.Muted && externalVoiceCaptureRequests <= 0)
        {
            StopVoiceCapture();
        }

        SetStatus("Voice chat: " + GetVoiceModeLabel());
    }

    public void AcquireExternalVoiceCapture()
    {
        externalVoiceCaptureRequests++;
    }

    public void ReleaseExternalVoiceCapture()
    {
        externalVoiceCaptureRequests = Mathf.Max(0, externalVoiceCaptureRequests - 1);
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
        if (restartCapture && (voiceMode != VoiceChatMode.Muted || externalVoiceCaptureRequests > 0))
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

    public bool IsVoiceInputAllowedByMode()
    {
        return voiceMode switch
        {
            VoiceChatMode.AlwaysOn => true,
            VoiceChatMode.PushToTalk => IsPushToTalkPressed(),
            _ => false,
        };
    }

    public List<FriendSession> GetDiscoveredFriendSessions()
    {
        CleanupDiscoverySessions(false);
        DateTime now = DateTime.UtcNow;
        List<FriendSession> sessions = new List<FriendSession>();
        foreach (FriendSessionRecord record in discoveredFriendSessions.Values)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Address) || record.Port <= 0)
            {
                continue;
            }

            sessions.Add(new FriendSession
            {
                PlayerName = record.PlayerName,
                Address = record.Address,
                Port = record.Port,
                AgeSeconds = Mathf.Max(0f, (float)(now - record.LastSeenUtc).TotalSeconds)
            });
        }

        sessions.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return sessions;
    }

    public void RefreshFriendSessions()
    {
        StartDiscoveryTransport();
        CleanupDiscoverySessions(true);
        if (mode == SessionMode.Hosting)
        {
            SendDiscoveryBeacon();
        }

        FriendSessionsChanged?.Invoke();
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
            StartServerUdpTransport(serverCts.Token);
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
        if (player != null)
        {
            player.position = new Vector3(x, y, z);
            Instance.hasSentState = false;
            Instance.lastForcedStateTime = -999f;
        }
    }

    private void ApplyLocalPlayerModelIfNeeded()
    {
        if (appliedLocalModelId != localModelId)
        {
            ApplyLocalPlayerModel();
        }
    }

    private void ApplyLocalPlayerModel()
    {
        Transform player = PlayerCache.ResolvePlayerTransform();
        if (player == null)
        {
            return;
        }

        localModelId = NormalizeModelId(localModelId);
        DestroyExistingLocalBodies(player);

        GameObject prefab = LoadModelPrefab(localModelId);
        if (prefab == null)
        {
            appliedLocalModelId = localModelId;
            return;
        }

        GameObject body = UnityEngine.Object.Instantiate(prefab, player);
        body.name = "MultiplayerLocalBody";
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one * GetModelVisualScale(localModelId);
        RemoveVisualColliders(body);
        SetLayerRecursively(body, 3);

        Camera fpCamera = player.GetComponentInChildren<Camera>();
        if (fpCamera != null)
        {
            fpCamera.cullingMask &= ~(1 << 3);
        }

        FirstPersonHeadBinder binder = player.GetComponent<FirstPersonHeadBinder>();
        if (binder != null)
        {
            binder.characterRoot = body.transform;
            binder.TryBind();
        }

        FirstPersonAnimatorDriver animatorDriver = player.GetComponent<FirstPersonAnimatorDriver>();
        if (animatorDriver != null)
        {
            animatorDriver.characterAnimator = body.GetComponentInChildren<Animator>();
        }

        appliedLocalModelId = localModelId;
    }

    private static void DestroyExistingLocalBodies(Transform player)
    {
        string[] bodyNames =
        {
            "BeanBody",
            "FPS_Character",
            "FPS_ProxyBody",
            "MultiplayerLocalBody"
        };

        for (int i = 0; i < bodyNames.Length; i++)
        {
            Transform body = player.Find(bodyNames[i]);
            if (body != null)
            {
                Destroy(body.gameObject);
            }
        }
    }

    public void BroadcastQuizPacket(Packet packet)
    {
        BroadcastServerPacket(packet);
        // Also deliver locally so the host's own QuizManager receives it.
        EnqueueMainThread(() => OnQuizPacket?.Invoke(packet));
    }

    public void BroadcastQuizPacketToRemotes(Packet packet)
    {
        BroadcastServerPacket(packet);
    }

    /// <summary>Client: send a quiz answer packet to the host.</summary>
    public void SendQuizPacketToHost(Packet packet)
    {
        _ = SendClientPacketAsync(packet);
    }

    public void StopAllNetworking()    {
        Interlocked.Increment(ref clientConnectionVersion);
        Interlocked.Exchange(ref sendFailureReported, 0);
        mode = SessionMode.Idle;
        localClientId = string.Empty;
        hasSentState = false;
        stateSequence = 0;
        lastUdpHelloTime = -999f;
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

        if (clientUdp != null)
        {
            try
            {
                clientUdp.Close();
                clientUdp.Dispose();
            }
            catch { }
            clientUdp = null;
        }
        clientUdpRemoteEndPoint = null;
        clientUdpReceiveTask = null;

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

        if (serverUdp != null)
        {
            try
            {
                serverUdp.Close();
                serverUdp.Dispose();
            }
            catch { }
            serverUdp = null;
        }
        serverUdpTask = null;

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
            localVoiceMeterRoot = null;
            localVoiceMeterBars = null;
        }

        localLabelAttached = false;
        DestroyLocalVoiceHud();
        SetStatus("Offline");
    }

    private bool IsClientConnected => IsTcpClientUsable(clientSocket) && clientStream != null;

    private void StartDiscoveryTransport()
    {
        if (discoveryCts != null)
        {
            return;
        }

        discoveryCts = new CancellationTokenSource();

        try
        {
            discoveryUdp = new UdpClient();
            discoveryUdp.EnableBroadcast = true;
            discoveryUdp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            discoveryUdp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            _ = Task.Run(() => RunDiscoveryLoopAsync(discoveryUdp, discoveryCts.Token));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Multiplayer] LAN friends discovery listen failed: " + ex.Message);
            try
            {
                discoveryUdp?.Close();
                discoveryUdp?.Dispose();
            }
            catch { }
            discoveryUdp = null;
        }

        try
        {
            discoverySender = new UdpClient();
            discoverySender.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Multiplayer] LAN friends discovery broadcast failed: " + ex.Message);
            discoverySender = null;
        }
    }

    private void StopDiscoveryTransport()
    {
        if (discoveryCts != null)
        {
            discoveryCts.Cancel();
            discoveryCts.Dispose();
            discoveryCts = null;
        }

        try
        {
            discoveryUdp?.Close();
            discoveryUdp?.Dispose();
        }
        catch { }
        discoveryUdp = null;

        try
        {
            discoverySender?.Close();
            discoverySender?.Dispose();
        }
        catch { }
        discoverySender = null;
    }

    private async Task RunDiscoveryLoopAsync(UdpClient udp, CancellationToken token)
    {
        while (!token.IsCancellationRequested && udp != null)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (token.IsCancellationRequested) break;
                continue;
            }
            catch
            {
                if (token.IsCancellationRequested) break;
                continue;
            }

            TryApplyDiscoveryBeacon(result.Buffer, result.RemoteEndPoint);
        }
    }

    private void TryApplyDiscoveryBeacon(byte[] payload, IPEndPoint remoteEndPoint)
    {
        if (payload == null || payload.Length == 0 || remoteEndPoint == null)
        {
            return;
        }

        string message;
        try
        {
            message = Encoding.UTF8.GetString(payload);
        }
        catch
        {
            return;
        }

        string[] parts = message.Split('|');
        if (parts.Length < 5 || parts[0] != DiscoveryPrefix)
        {
            return;
        }

        string remoteInstanceId = parts[1];
        if (remoteInstanceId == discoveryInstanceId)
        {
            return;
        }

        if (!int.TryParse(parts[2], out int port) || port <= 0)
        {
            return;
        }

        string playerName = NormalizePlayerName(parts[3]);
        string address = remoteEndPoint.Address.ToString();
        string key = remoteInstanceId + "@" + address + ":" + port;
        discoveredFriendSessions[key] = new FriendSessionRecord
        {
            InstanceId = remoteInstanceId,
            PlayerName = playerName,
            Address = address,
            Port = port,
            LastSeenUtc = DateTime.UtcNow
        };

        EnqueueMainThread(() => FriendSessionsChanged?.Invoke());
    }

    private void TickLanDiscovery()
    {
        if (mode == SessionMode.Hosting && Time.unscaledTime - lastDiscoveryBeaconTime >= DiscoveryBeaconIntervalSeconds)
        {
            lastDiscoveryBeaconTime = Time.unscaledTime;
            SendDiscoveryBeacon();
        }

        if (Time.unscaledTime - lastDiscoveryCleanupTime >= DiscoveryCleanupIntervalSeconds)
        {
            lastDiscoveryCleanupTime = Time.unscaledTime;
            CleanupDiscoverySessions(true);
        }
    }

    private void SendDiscoveryBeacon()
    {
        if (discoverySender == null)
        {
            return;
        }

        string safePlayerName = SanitizeDiscoveryField(localPlayerName);
        string safeMachine = SanitizeDiscoveryField(Environment.MachineName);
        string message = DiscoveryPrefix + "|" + discoveryInstanceId + "|" + hostPort + "|" + safePlayerName + "|" + safeMachine;
        byte[] payload = Encoding.UTF8.GetBytes(message);
        try
        {
            _ = discoverySender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
        }
        catch { }
    }

    private void CleanupDiscoverySessions(bool notifyIfChanged)
    {
        DateTime cutoff = DateTime.UtcNow.AddSeconds(-DiscoveryTimeoutSeconds);
        bool changed = false;
        foreach (var pair in discoveredFriendSessions)
        {
            if (pair.Value == null || pair.Value.LastSeenUtc < cutoff)
            {
                changed |= discoveredFriendSessions.TryRemove(pair.Key, out _);
            }
        }

        if (changed && notifyIfChanged)
        {
            FriendSessionsChanged?.Invoke();
        }
    }

    private static string SanitizeDiscoveryField(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Player";
        }

        return value.Replace("|", " ").Replace("\n", " ").Replace("\r", " ").Trim();
    }

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
        stateSequence = 0;
        lastUdpHelloTime = -999f;
        StartClientUdpTransport(address, port);
        Interlocked.Exchange(ref sendFailureReported, 0);
        Interlocked.Increment(ref clientConnectionVersion);

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

    private void StartServerUdpTransport(CancellationToken token)
    {
        try
        {
            serverUdp = new UdpClient(hostPort);
            serverUdp.Client.ReceiveBufferSize = 1024 * 256;
            serverUdp.Client.SendBufferSize = 1024 * 256;
            serverUdpTask = Task.Run(() => RunServerUdpLoopAsync(serverUdp, token));
        }
        catch (Exception ex)
        {
            SetStatus("UDP host failed: " + ex.Message);
        }
    }

    private async Task RunServerUdpLoopAsync(UdpClient udp, CancellationToken token)
    {
        PacketManager udpPacketManager = new PacketManager();
        while (!token.IsCancellationRequested && udp != null)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (token.IsCancellationRequested) break;
                continue;
            }
            catch
            {
                if (token.IsCancellationRequested) break;
                continue;
            }

            try
            {
                Packet packet = Packet.DecodeRaw(result.Buffer, udpPacketManager);
                HandleServerUdpPacket(packet, result.RemoteEndPoint);
            }
            catch
            {
                // Drop malformed UDP datagrams.
            }
        }
    }

    private void HandleServerUdpPacket(Packet packet, IPEndPoint remoteEndPoint)
    {
        if (packet == null || remoteEndPoint == null)
        {
            return;
        }

        switch (packet)
        {
            case MultiplayerUdpHelloPacket helloPacket:
                if (serverPeers.TryGetValue(helloPacket.ClientId, out NetworkPeer helloPeer))
                {
                    helloPeer.UdpEndPoint = remoteEndPoint;
                    helloPeer.PlayerName = NormalizePlayerName(helloPacket.PlayerName);
                }
                break;

            case MultiplayerPlayerStatePacket statePacket:
                if (TryResolveUdpPeer(statePacket.ClientId, remoteEndPoint, out NetworkPeer statePeer))
                {
                    statePeer.PlayerName = NormalizePlayerName(statePacket.PlayerName);
                    statePeer.ModelId = NormalizeModelId(statePacket.ModelId);
                    statePeer.Position = new Vector3(statePacket.PositionX, statePacket.PositionY, statePacket.PositionZ);
                    statePeer.Yaw = statePacket.Yaw;
                    statePacket.ClientId = statePeer.ClientId;
                    statePacket.PlayerName = statePeer.PlayerName;
                    statePacket.ModelId = statePeer.ModelId;
                    BroadcastServerUdpPacket(statePacket, statePeer.ClientId);
                }
                break;

            case MultiplayerVoicePacket voicePacket:
                if (TryResolveUdpPeer(voicePacket.ClientId, remoteEndPoint, out NetworkPeer voicePeer))
                {
                    voicePacket.ClientId = voicePeer.ClientId;
                    BroadcastServerUdpPacket(voicePacket, voicePeer.ClientId);
                }
                break;
        }
    }

    private bool TryResolveUdpPeer(string clientId, IPEndPoint remoteEndPoint, out NetworkPeer peer)
    {
        peer = null;
        if (string.IsNullOrEmpty(clientId) || !serverPeers.TryGetValue(clientId, out peer))
        {
            return false;
        }

        if (peer.UdpEndPoint == null || !peer.UdpEndPoint.Equals(remoteEndPoint))
        {
            peer.UdpEndPoint = remoteEndPoint;
        }

        return true;
    }

    private void StartClientUdpTransport(string address, int port)
    {
        try
        {
            IPAddress hostIp = ResolveHostIPAddress(address);
            clientUdpRemoteEndPoint = new IPEndPoint(hostIp, port);
            clientUdp = new UdpClient(0);
            clientUdp.Client.ReceiveBufferSize = 1024 * 256;
            clientUdp.Client.SendBufferSize = 1024 * 256;
            clientUdpReceiveTask = Task.Run(() => RunClientUdpLoopAsync(clientUdp));
        }
        catch (Exception ex)
        {
            SetStatus("UDP client failed: " + ex.Message);
        }
    }

    private async Task RunClientUdpLoopAsync(UdpClient udp)
    {
        PacketManager udpPacketManager = new PacketManager();
        while (udp != null)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
            catch
            {
                continue;
            }

            try
            {
                Packet packet = Packet.DecodeRaw(result.Buffer, udpPacketManager);
                HandleClientPacket(packet);
            }
            catch
            {
                // Drop malformed realtime datagrams.
            }
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
                        peer.ModelId = FemaleModelId;
                        serverPeers[peer.ClientId] = peer;
                        await SendServerPacketAsync(peer, new MultiplayerWelcomePacket(peer.ClientId, peer.PlayerName));
                        await SendExistingPlayersToClientAsync(peer);
                        if (CodeWorldRuntime.TryCreateSnapshotPacket(out CodeWorldStatePacket codeWorldStatePacket))
                        {
                            await SendServerPacketAsync(peer, codeWorldStatePacket);
                        }
                        SetStatus(peer.PlayerName + " joined the session.");
                        break;

                    case MultiplayerPlayerStatePacket statePacket:
                        if (string.IsNullOrEmpty(peer.ClientId))
                        {
                            break;
                        }

                        peer.PlayerName = NormalizePlayerName(statePacket.PlayerName);
                        peer.ModelId = NormalizeModelId(statePacket.ModelId);
                        peer.Position = new Vector3(statePacket.PositionX, statePacket.PositionY, statePacket.PositionZ);
                        peer.Yaw = statePacket.Yaw;
                        statePacket.ClientId = peer.ClientId;
                        statePacket.PlayerName = peer.PlayerName;
                        statePacket.ModelId = peer.ModelId;
                        BroadcastServerPacket(statePacket, excludeClientId: peer.ClientId);
                        break;

                    case QuizAnswerPacket quizAnswer:
                        // Forward the client's answer to the host's local client connection
                        // by broadcasting to all — the host's own client loop receives it.
                        BroadcastServerPacket(quizAnswer);
                        break;

                    case CodeWorldCommandPacket codeWorldCommand:
                        codeWorldCommand.AuthorClientId = peer.ClientId ?? string.Empty;
                        BroadcastServerPacket(codeWorldCommand);
                        break;

                    case CodeWorldEditorSyncPacket codeWorldEditorSync:
                        codeWorldEditorSync.AuthorClientId = peer.ClientId ?? string.Empty;
                        BroadcastServerPacket(codeWorldEditorSync);
                        break;

                    case CodeWorldCursorPacket codeWorldCursor:
                        codeWorldCursor.AuthorClientId = peer.ClientId ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(codeWorldCursor.PlayerName))
                        {
                            codeWorldCursor.PlayerName = peer.PlayerName;
                        }
                        BroadcastServerPacket(codeWorldCursor);
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
                existing.Yaw,
                0,
                existing.ModelId));
        }
    }

    private async Task ClientReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && IsTcpClientUsable(clientSocket))
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
            EnqueueMainThread(() => MarkClientConnectionLost("Connection lost: " + ex.Message));
        }
    }

    private void HandleClientPacket(Packet packet)
    {
        switch (packet)
        {
            case MultiplayerWelcomePacket welcomePacket:
                localClientId = welcomePacket.ClientId;
                localPlayerName = NormalizePlayerName(welcomePacket.PlayerName);
                SendUdpHello();
                EnqueueMainThread(() =>
                {
                    RefreshLocalNameLabel();
                    ApplyLocalPlayerModel();
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

            case CodeWorldCommandPacket _:
            case CodeWorldStatePacket _:
            case CodeWorldEditorSyncPacket _:
            case CodeWorldCursorPacket _:
                EnqueueMainThread(() => OnQuizPacket?.Invoke(packet));
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

        string modelId = NormalizeModelId(statePacket.ModelId);
        RemoteAvatar avatar = remoteAvatars.GetOrAdd(statePacket.ClientId, _ => CreateRemoteAvatar(statePacket.PlayerName, modelId));
        if (avatar == null || avatar.Root == null)
        {
            return;
        }

        if (avatar.ModelId != modelId || avatar.Body == null)
        {
            ApplyRemoteAvatarModel(avatar, modelId);
        }

        if (statePacket.Sequence <= avatar.LastStateSequence)
        {
            return;
        }
        avatar.LastStateSequence = statePacket.Sequence;

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
        avatar.VoiceLevel = Mathf.Max(avatar.VoiceLevel, CalculatePcm16Level(voicePacket.Pcm16));
        avatar.LastVoiceTime = Time.unscaledTime;
        if (Time.unscaledTime - lastVoiceReceiveLogTime > 2f)
        {
            lastVoiceReceiveLogTime = Time.unscaledTime;
            Debug.Log("[Multiplayer] Received voice from " + voicePacket.ClientId + " bytes=" + voicePacket.Pcm16.Length);
        }
    }

    private RemoteAvatar CreateRemoteAvatar(string playerName, string modelId)
    {
        GameObject root = new GameObject("RemoteBean_" + playerName);

        GameObject labelRoot = new GameObject("NameLabel");
        labelRoot.transform.SetParent(root.transform, false);
        labelRoot.AddComponent<BillboardToCamera>();

        TextMesh textMesh = labelRoot.AddComponent<TextMesh>();
        textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textMesh.fontSize = 48;
        textMesh.characterSize = 0.02f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.black;
        textMesh.text = NormalizePlayerName(playerName);

        TextMesh voiceIconText;
        Renderer[] voiceBars;
        Transform voiceMeterRoot = CreateVoiceMeter(labelRoot.transform, out voiceBars, out voiceIconText);

        RemoteAvatar avatar = new RemoteAvatar
        {
            Root = root,
            NameRoot = labelRoot.transform,
            NameText = textMesh,
            VoiceMeterRoot = voiceMeterRoot,
            VoiceMeterBars = voiceBars,
            VoiceIconText = voiceIconText
        };

        ApplyRemoteAvatarModel(avatar, modelId);
        return avatar;
    }

    private void ApplyRemoteAvatarModel(RemoteAvatar avatar, string modelId)
    {
        if (avatar == null || avatar.Root == null)
        {
            return;
        }

        modelId = NormalizeModelId(modelId);
        if (avatar.ModelId == modelId && avatar.Body != null)
        {
            return;
        }

        if (avatar.Body != null)
        {
            Destroy(avatar.Body);
            avatar.Body = null;
        }

        GameObject prefab = LoadModelPrefab(modelId);
        if (prefab != null)
        {
            GameObject body = UnityEngine.Object.Instantiate(prefab, avatar.Root.transform);
            body.name = "RemoteBody_" + GetModelLabel(modelId);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * GetModelVisualScale(modelId);
            RemoveVisualColliders(body);
            avatar.Body = body;
        }
        else
        {
            avatar.Body = CreateFallbackBody(avatar.Root.transform, avatar.NameText != null ? avatar.NameText.text : "Player");
        }

        avatar.ModelId = modelId;
        if (avatar.NameRoot != null)
        {
            avatar.NameRoot.localPosition = CalculateLabelLocalPosition(avatar.Root.transform, 2.4f);
        }

        ConfigureRemoteAvatarCollision(avatar);
    }

    private static GameObject CreateFallbackBody(Transform parent, string playerName)
    {
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "RemoteBody_Fallback";
        capsule.transform.SetParent(parent, false);
        capsule.transform.localScale = new Vector3(1.3f, 1.7f, 1.3f);
        Collider collider = capsule.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = capsule.GetComponent<Renderer>();
        if (renderer != null)
        {
            int hash = Mathf.Abs((playerName ?? "Player").GetHashCode());
            float hue = (hash % 360) / 360f;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = Color.HSVToRGB(hue, 0.45f, 0.95f);
            renderer.material = material;
        }

        return capsule;
    }

    private static void ConfigureRemoteAvatarCollision(RemoteAvatar avatar)
    {
        if (avatar == null || avatar.Root == null)
        {
            return;
        }

        CapsuleCollider collider = avatar.Collision != null
            ? avatar.Collision
            : avatar.Root.GetComponent<CapsuleCollider>();
        if (collider == null)
        {
            collider = avatar.Root.AddComponent<CapsuleCollider>();
        }

        float height = 2f;
        float radius = 0.35f;
        float centerY = 1f;
        Bounds? bounds = CalculateWorldBounds(avatar.Root.transform);
        if (bounds.HasValue)
        {
            Bounds worldBounds = bounds.Value;
            height = Mathf.Max(1.2f, worldBounds.size.y);
            radius = Mathf.Clamp(Mathf.Max(worldBounds.extents.x, worldBounds.extents.z) * 0.75f, 0.25f, 0.65f);
            centerY = avatar.Root.transform.InverseTransformPoint(worldBounds.center).y;
        }

        collider.isTrigger = false;
        collider.direction = 1;
        collider.height = height;
        collider.radius = radius;
        collider.center = new Vector3(0f, Mathf.Max(height * 0.5f, centerY), 0f);
        avatar.Collision = collider;

        Rigidbody rigidbody = avatar.Root.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = avatar.Root.AddComponent<Rigidbody>();
        }

        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private static Transform CreateVoiceMeter(Transform parent, out Renderer[] bars, out TextMesh iconText)
    {
        GameObject root = new GameObject("VoiceMeter");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0.72f, -0.02f, 0f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject iconObject = new GameObject("VoiceIcon");
        iconObject.transform.SetParent(root.transform, false);
        iconObject.transform.localPosition = new Vector3(-0.09f, 0f, 0f);
        iconObject.transform.localRotation = Quaternion.identity;

        iconText = iconObject.AddComponent<TextMesh>();
        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconText.fontSize = 30;
        iconText.characterSize = 0.012f;
        iconText.anchor = TextAnchor.MiddleCenter;
        iconText.alignment = TextAlignment.Center;
        iconText.color = new Color(0.45f, 0.55f, 0.65f, 1f);
        iconText.text = "MIC";

        bars = new Renderer[VoiceMeterBars];
        for (int i = 0; i < VoiceMeterBars; i++)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bar.name = "VoiceBar" + i;
            bar.transform.SetParent(root.transform, false);
            float height = 0.035f + (i * 0.012f);
            bar.transform.localPosition = new Vector3(0.02f + (i * 0.028f), -0.04f + (height * 0.5f), 0f);
            bar.transform.localRotation = Quaternion.identity;
            bar.transform.localScale = new Vector3(0.018f, height, 1f);

            Collider collider = bar.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Standard");
                Material material = new Material(shader);
                material.color = new Color(0.18f, 0.24f, 0.30f, 0.65f);
                renderer.material = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                bars[i] = renderer;
            }
        }

        SetVoiceMeterLevel(bars, iconText, 0f);
        return root.transform;
    }

    private static void SetVoiceMeterLevel(Renderer[] bars, TextMesh iconText, float level)
    {
        float clampedLevel = Mathf.Clamp01(level);
        if (iconText != null)
        {
            iconText.color = Color.Lerp(
                new Color(0.45f, 0.55f, 0.65f, 1f),
                new Color(0.25f, 0.95f, 0.55f, 1f),
                clampedLevel);
        }

        if (bars == null)
        {
            return;
        }

        int activeBars = Mathf.CeilToInt(clampedLevel * bars.Length);
        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] == null || bars[i].material == null)
            {
                continue;
            }

            bars[i].material.color = i < activeBars
                ? Color.Lerp(new Color(0.25f, 0.85f, 0.45f, 1f), new Color(1f, 0.86f, 0.25f, 1f), clampedLevel)
                : new Color(0.18f, 0.24f, 0.30f, 0.65f);
        }
    }

    private void EnsureLocalVoiceHud()
    {
        if ((mode == SessionMode.Idle && externalVoiceCaptureRequests <= 0) ||
            (voiceMode == VoiceChatMode.Muted && externalVoiceCaptureRequests <= 0))
        {
            DestroyLocalVoiceHud();
            return;
        }

        if (localVoiceHudCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("LocalVoiceHud");
        DontDestroyOnLoad(canvasObject);

        localVoiceHudCanvas = canvasObject.AddComponent<Canvas>();
        localVoiceHudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        localVoiceHudCanvas.sortingOrder = 5000;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-18f, 18f);
        panelRect.sizeDelta = new Vector2(150f, 38f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.07f, 0.10f, 0.72f);

        localVoiceHudLabel = CreateHudText(panel.transform, "MIC", new Vector2(-54f, 0f), new Vector2(38f, 24f), 13, FontStyle.Bold);

        localVoiceHudBars = new Image[HudVoiceMeterBars];
        for (int i = 0; i < HudVoiceMeterBars; i++)
        {
            GameObject bar = new GameObject("VoiceHudBar" + i);
            bar.transform.SetParent(panel.transform, false);
            RectTransform barRect = bar.AddComponent<RectTransform>();
            float height = 7f + (i * 1.8f);
            barRect.sizeDelta = new Vector2(6f, height);
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(-24f + (i * 8f), -13f);

            Image image = bar.AddComponent<Image>();
            image.color = new Color(0.18f, 0.24f, 0.30f, 0.9f);
            localVoiceHudBars[i] = image;
        }
    }

    private static Text CreateHudText(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, FontStyle style)
    {
        GameObject textObject = new GameObject(text + "Text");
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Text label = textObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.75f, 0.85f, 0.95f, 1f);
        return label;
    }

    private void SetVoiceHudLevel(float level)
    {
        if (localVoiceHudBars == null)
        {
            return;
        }

        float clampedLevel = Mathf.Clamp01(level);
        int activeBars = Mathf.CeilToInt(clampedLevel * localVoiceHudBars.Length);
        if (localVoiceHudLabel != null)
        {
            localVoiceHudLabel.color = Color.Lerp(
                new Color(0.75f, 0.85f, 0.95f, 1f),
                new Color(0.25f, 0.95f, 0.55f, 1f),
                clampedLevel);
        }

        for (int i = 0; i < localVoiceHudBars.Length; i++)
        {
            if (localVoiceHudBars[i] == null)
            {
                continue;
            }

            localVoiceHudBars[i].color = i < activeBars
                ? Color.Lerp(new Color(0.25f, 0.85f, 0.45f, 1f), new Color(1f, 0.86f, 0.25f, 1f), clampedLevel)
                : new Color(0.18f, 0.24f, 0.30f, 0.9f);
        }
    }

    private void DestroyLocalVoiceHud()
    {
        if (localVoiceHudCanvas != null)
        {
            Destroy(localVoiceHudCanvas.gameObject);
        }

        localVoiceHudCanvas = null;
        localVoiceHudBars = null;
        localVoiceHudLabel = null;
    }

    private void SetAvatarName(RemoteAvatar avatar, string playerName)
    {
        if (avatar == null) return;
        string name = NormalizePlayerName(playerName);
        if (avatar.NameText != null) avatar.NameText.text = name;
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

    private void SendLocalState()
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

            bool sent = SendClientRealtimePacket(new MultiplayerPlayerStatePacket(
                localClientId,
                localPlayerName,
                position,
                yaw,
                stateSequence++,
                localModelId));
            if (sent)
            {
                lastSentPosition = position;
                lastSentYaw = yaw;
                hasSentState = true;
                if (forcedState)
                {
                    lastForcedStateTime = Time.unscaledTime;
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref stateSendInFlight, 0);
        }
    }

    private void CaptureAndSendVoice()
    {
        bool canTransmit = IsClientConnected && !string.IsNullOrEmpty(localClientId);
        bool shouldCapture = (voiceMode != VoiceChatMode.Muted && canTransmit) || externalVoiceCaptureRequests > 0;
        if (!shouldCapture)
        {
            StopVoiceCapture();
            return;
        }

        if (microphoneClip == null)
        {
            StartVoiceCapture();
            return;
        }

        bool shouldTransmit = canTransmit && (voiceMode == VoiceChatMode.AlwaysOn || IsPushToTalkPressed());
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

        int framesSent = 0;
        while (available >= VoiceFrameSamples && framesSent < MaxVoiceFramesPerUpdate)
        {
            ReadMicrophoneFrame(sampleCount);
            float frameLevel = CalculatePcmLevel(microphoneFrame);
            localVoiceLevel = Mathf.Max(localVoiceLevel, frameLevel);
            byte[] pcm16 = EncodePcm16(microphoneFrame);
            LocalVoiceFrameCaptured?.Invoke(pcm16, VoiceSampleRate, frameLevel);

            if (!shouldTransmit)
            {
                available -= VoiceFrameSamples;
                framesSent++;
                continue;
            }

            TrySendVoiceFrame(new MultiplayerVoicePacket(localClientId, voiceSequence++, VoiceSampleRate, pcm16));
            if (Time.unscaledTime - lastVoiceSendLogTime > 2f)
            {
                lastVoiceSendLogTime = Time.unscaledTime;
                Debug.Log("[Multiplayer] Sent voice bytes=" + pcm16.Length + " level=" + frameLevel.ToString("0.00"));
            }

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

    private static float CalculatePcmLevel(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return 0f;
        }

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }

        return Mathf.Clamp01(Mathf.Sqrt(sum / samples.Length) * 5f);
    }

    private static float CalculatePcm16Level(byte[] pcm16)
    {
        if (pcm16 == null || pcm16.Length < 2)
        {
            return 0f;
        }

        float sum = 0f;
        int sampleCount = 0;
        for (int i = 0; i + 1 < pcm16.Length; i += 2)
        {
            short sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
            float normalized = sample / 32768f;
            sum += normalized * normalized;
            sampleCount++;
        }

        return sampleCount > 0 ? Mathf.Clamp01(Mathf.Sqrt(sum / sampleCount) * 5f) : 0f;
    }

    private void TrySendVoiceFrame(Packet packet)
    {
        if (packet == null ||
            Interlocked.Exchange(ref voiceSendInFlight, 1) == 1)
        {
            return;
        }

        try
        {
            SendClientRealtimePacket(packet);
        }
        finally
        {
            Interlocked.Exchange(ref voiceSendInFlight, 0);
        }
    }

    private async Task<bool> SendClientPacketAsync(Packet packet, bool reportFailure = true)
    {
        if (!IsClientConnected || packet == null)
        {
            return false;
        }

        byte[] payload = packet.EncodeRaw();
        byte[] frame = new byte[4 + payload.Length];
        byte[] lengthBytes = Packet.IntToBigEndian(payload.Length);
        Buffer.BlockCopy(lengthBytes, 0, frame, 0, 4);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

        NetworkStream stream = clientStream;
        CancellationTokenSource tokenSource = clientCts;
        int connectionVersion = clientConnectionVersion;
        if (stream == null || tokenSource == null || tokenSource.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            await clientSendLock.WaitAsync(tokenSource.Token);
        }
        catch
        {
            return false;
        }

        try
        {
            if (connectionVersion != clientConnectionVersion ||
                stream != clientStream ||
                !IsClientConnected ||
                tokenSource.IsCancellationRequested)
            {
                return false;
            }

            await stream.WriteAsync(frame, 0, frame.Length, tokenSource.Token);
            return true;
        }
        catch (Exception ex)
        {
            if (reportFailure && Interlocked.Exchange(ref sendFailureReported, 1) == 0)
            {
                EnqueueMainThread(() => MarkClientConnectionLost("Connection lost while sending: " + ex.Message));
            }

            return false;
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

        try
        {
            await peer.SendLock.WaitAsync();
        }
        catch
        {
            return;
        }

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

    private void BroadcastServerUdpPacket(Packet packet, string excludeClientId = null)
    {
        if (serverUdp == null || packet == null)
        {
            return;
        }

        byte[] payload = packet.EncodeRaw();
        foreach (NetworkPeer peer in serverPeers.Values)
        {
            if (peer == null ||
                string.IsNullOrEmpty(peer.ClientId) ||
                peer.ClientId == excludeClientId ||
                peer.UdpEndPoint == null)
            {
                continue;
            }

            try
            {
                _ = serverUdp.SendAsync(payload, payload.Length, peer.UdpEndPoint);
            }
            catch { }
        }
    }

    private bool SendClientRealtimePacket(Packet packet)
    {
        if (packet == null ||
            clientUdp == null ||
            clientUdpRemoteEndPoint == null ||
            string.IsNullOrEmpty(localClientId))
        {
            return false;
        }

        try
        {
            byte[] payload = packet.EncodeRaw();
            _ = clientUdp.SendAsync(payload, payload.Length, clientUdpRemoteEndPoint);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SendUdpHello()
    {
        if (string.IsNullOrEmpty(localClientId))
        {
            return;
        }

        SendClientRealtimePacket(new MultiplayerUdpHelloPacket(localClientId, localPlayerName));
    }

    private static IPAddress ResolveHostIPAddress(string address)
    {
        if (IPAddress.TryParse(address, out IPAddress parsed))
        {
            return parsed;
        }

        IPAddress[] addresses = Dns.GetHostAddresses(address);
        for (int i = 0; i < addresses.Length; i++)
        {
            if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
            {
                return addresses[i];
            }
        }

        if (addresses.Length > 0)
        {
            return addresses[0];
        }

        throw new InvalidOperationException("Could not resolve host address.");
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
        // Do not render the local player's world-space name/mic label. In first person
        // it sits in front of the camera; the local voice HUD covers self feedback.
        if (localNameLabelRoot != null)
        {
            Destroy(localNameLabelRoot.gameObject);
            localNameLabelRoot = null;
            localNameLabelText = null;
            localVoiceMeterRoot = null;
            localVoiceMeterBars = null;
            localVoiceIconText = null;
            localLabelAttached = false;
        }

        localVoiceLevel = Mathf.Max(0f, localVoiceLevel);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        localLabelAttached = false;
        localNameLabelRoot = null;
        localNameLabelText = null;
        localVoiceMeterRoot = null;
        localVoiceMeterBars = null;
        localVoiceIconText = null;
        appliedLocalModelId = string.Empty;
        hasSentState = false;
        lastForcedStateTime = -999f;
        RefreshLocalNameLabel();
        ApplyLocalPlayerModel();
    }

    private void MarkClientConnectionLost(string message)
    {
        Interlocked.Increment(ref clientConnectionVersion);
        localClientId = string.Empty;
        hasSentState = false;
        StopVoiceCapture();

        try
        {
            clientCts?.Cancel();
            clientCts?.Dispose();
        }
        catch { }
        clientCts = null;

        try
        {
            clientStream?.Dispose();
        }
        catch { }
        clientStream = null;

        try
        {
            clientSocket?.Close();
            clientSocket?.Dispose();
        }
        catch { }
        clientSocket = null;

        try
        {
            clientUdp?.Close();
            clientUdp?.Dispose();
        }
        catch { }
        clientUdp = null;
        clientUdpRemoteEndPoint = null;
        clientUdpReceiveTask = null;

        SetStatus(message);
    }

    private void EnqueueMainThread(Action action)
    {
        if (action != null)
        {
            mainThreadQueue.Enqueue(action);
        }
    }

    private static string NormalizeModelId(string modelId)
    {
        return string.Equals(modelId, MaleModelId, StringComparison.OrdinalIgnoreCase)
            ? MaleModelId
            : FemaleModelId;
    }

    private static string GetModelLabel(string modelId)
    {
        return NormalizeModelId(modelId) == MaleModelId ? "Boy" : "Girl";
    }

    private static string GetModelResourcePath(string modelId)
    {
        return NormalizeModelId(modelId) == MaleModelId
            ? MaleModelResourcePath
            : FemaleModelResourcePath;
    }

    private static float GetModelVisualScale(string modelId)
    {
        return 2.2f;
    }

    private static GameObject LoadModelPrefab(string modelId)
    {
        return Resources.Load<GameObject>(GetModelResourcePath(modelId));
    }

    private static void RemoveVisualColliders(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                Destroy(colliders[i]);
            }
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static string NormalizePlayerName(string playerName)
    {
        string trimmed = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        return trimmed.Length > 18 ? trimmed.Substring(0, 18) : trimmed;
    }

    private static Vector3 CalculateLabelLocalPosition(Transform owner, float fallbackY)
    {
        if (owner == null)
        {
            return new Vector3(0f, fallbackY, 0f);
        }

        Bounds? bounds = CalculateWorldBounds(owner);
        if (!bounds.HasValue)
        {
            CharacterController controller = owner.GetComponentInChildren<CharacterController>();
            if (controller != null)
            {
                return new Vector3(0f, controller.center.y + controller.height + 0.35f, 0f);
            }

            Collider collider = owner.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                Vector3 colliderTop = owner.InverseTransformPoint(new Vector3(collider.bounds.center.x, collider.bounds.max.y, collider.bounds.center.z));
                return new Vector3(0f, colliderTop.y + 0.35f, 0f);
            }

            return new Vector3(0f, fallbackY, 0f);
        }

        Bounds worldBounds = bounds.Value;
        Vector3 top = owner.InverseTransformPoint(new Vector3(worldBounds.center.x, worldBounds.max.y, worldBounds.center.z));
        return new Vector3(0f, Mathf.Max(fallbackY, top.y + 0.35f), 0f);
    }

    private static Bounds? CalculateWorldBounds(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds? bounds = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<TextMesh>() != null)
            {
                continue;
            }

            if (renderer.gameObject.name.Contains("Voice") ||
                renderer.gameObject.name.Contains("NameLabel") ||
                renderer.gameObject.name.Contains("Badge"))
            {
                continue;
            }

            bounds = bounds.HasValue ? Encapsulate(bounds.Value, renderer.bounds) : renderer.bounds;
        }

        return bounds;
    }

    private static Bounds Encapsulate(Bounds current, Bounds additional)
    {
        current.Encapsulate(additional.min);
        current.Encapsulate(additional.max);
        return current;
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

    private static bool IsTcpClientUsable(TcpClient tcpClient)
    {
        if (tcpClient == null || !tcpClient.Connected)
        {
            return false;
        }

        try
        {
            Socket socket = tcpClient.Client;
            return socket != null && !(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
        }
        catch
        {
            return false;
        }
    }
}
