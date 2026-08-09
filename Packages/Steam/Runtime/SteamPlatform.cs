using oojjrs.oplat;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.steam
{
    public sealed class SteamPlatform : IDisposable, PlatformAuthenticationInterface, PlatformInterface
    {
        enum LifecycleStateEnum
        {
            Stopped,
            Initializing,
            Initialized,
            ShuttingDown
        }

        sealed class PendingTicket
        {
            public CancellationTokenRegistration CancellationRegistration { get; set; }
            public TaskCompletionSource<PlatformAuthenticationTicket> Completion { get; }

            public PendingTicket()
            {
                Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        static readonly object __lifecycleLock = new();
        readonly uint? _appId;
        readonly string _authenticationIdentity;
        readonly ConcurrentQueue<HAuthTicket> _cancelTicketHandles = new();
        readonly ConcurrentDictionary<HAuthTicket, byte> _issuedTickets = new();
        readonly ConcurrentDictionary<HAuthTicket, PendingTicket> _pendingTickets = new();
        static SteamPlatform __owner;
        int _ownerThreadId;
        LifecycleStateEnum _state;
        Callback<GetTicketForWebApiResponse_t> _ticketCallback;
        PlatformUser _user;

        bool PlatformInterface.IsInitialized
        {
            get
            {
                lock (__lifecycleLock)
                    return _state == LifecycleStateEnum.Initialized;
            }
        }
        PlatformKindEnum PlatformInterface.Kind => PlatformKindEnum.Steam;
        PlatformUser PlatformInterface.User => _user;

        public SteamPlatform(string authenticationIdentity = "unityauthenticationservice")
            : this(default(uint?), authenticationIdentity)
        {
        }

        public SteamPlatform(uint appId, string authenticationIdentity = "unityauthenticationservice")
            : this((uint?)appId, authenticationIdentity)
        {
            if (appId == 0)
                throw new ArgumentOutOfRangeException(nameof(appId), "Steam App ID must be greater than zero.");
        }

        SteamPlatform(uint? appId, string authenticationIdentity)
        {
            if (string.IsNullOrWhiteSpace(authenticationIdentity))
                throw new ArgumentException("Authentication identity is required.", nameof(authenticationIdentity));

            _appId = appId;
            _authenticationIdentity = authenticationIdentity;
        }

        void IDisposable.Dispose()
        {
            ((PlatformInterface)this).Shutdown();
        }

        Task<PlatformAuthenticationTicket> PlatformAuthenticationInterface.CreateTicketAsync(CancellationToken cancellationToken)
        {
            lock (__lifecycleLock)
            {
                if (_state != LifecycleStateEnum.Initialized)
                    throw new InvalidOperationException("Steam must be initialized before requesting an authentication ticket.");

                EnsureOwningThread();
                cancellationToken.ThrowIfCancellationRequested();
                var handle = SteamUser.GetAuthTicketForWebApi(_authenticationIdentity);
                if (handle == HAuthTicket.Invalid)
                    throw new PlatformException("Steam did not issue an authentication ticket handle.");

                var pending = new PendingTicket();
                if (_pendingTickets.TryAdd(handle, pending) == false)
                {
                    SteamUser.CancelAuthTicket(handle);
                    throw new PlatformException("Steam issued a duplicate authentication ticket handle.");
                }

                pending.CancellationRegistration = cancellationToken.Register(() => CancelPendingTicket(handle, cancellationToken));
                if (pending.Completion.Task.IsCompleted)
                    pending.CancellationRegistration.Dispose();

                return pending.Completion.Task;
            }
        }

        Task PlatformInterface.InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (__lifecycleLock)
            {
                if (_state == LifecycleStateEnum.Initialized)
                    return Task.CompletedTask;

                if (_state != LifecycleStateEnum.Stopped)
                    throw new PlatformException($"SteamPlatform cannot initialize while it is {_state}.");

                if ((__owner != default) && (ReferenceEquals(__owner, this) == false))
                    throw new PlatformException("Only one SteamPlatform instance can own the process-wide Steam API.");

                __owner = this;
                _ownerThreadId = Environment.CurrentManagedThreadId;
                _state = LifecycleStateEnum.Initializing;
                var steamInitialized = false;
                try
                {
#if !UNITY_EDITOR
                    if ((_appId.HasValue) && (SteamAPI.RestartAppIfNecessary(new(_appId.Value))))
                        throw new SteamRestartRequiredException(_appId.Value);
#endif
                    var result = SteamAPI.InitEx(out var message);
                    if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                        throw new PlatformException($"Steam initialization failed ({result}): {message}");

                    steamInitialized = true;
                    if (_appId.HasValue)
                    {
                        var actualAppId = SteamUtils.GetAppID().m_AppId;
                        if (actualAppId != _appId.Value)
                            throw new PlatformException($"Steam initialized with App ID {actualAppId}, but {_appId.Value} was expected.");
                    }

                    _ticketCallback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicketCreated);
                    _user = new(SteamUser.GetSteamID().m_SteamID.ToString(), SteamFriends.GetPersonaName());
                    _state = LifecycleStateEnum.Initialized;
                }
                catch
                {
                    _ticketCallback?.Dispose();
                    _ticketCallback = null;
                    if (steamInitialized)
                        SteamAPI.Shutdown();

                    __owner = null;
                    _ownerThreadId = 0;
                    _state = LifecycleStateEnum.Stopped;
                    _user = default;
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        void PlatformInterface.RunCallbacks()
        {
            lock (__lifecycleLock)
            {
                if (_state != LifecycleStateEnum.Initialized)
                    return;

                EnsureOwningThread();
                CancelQueuedTickets();
                SteamAPI.RunCallbacks();
                CancelQueuedTickets();
            }
        }

        void PlatformInterface.Shutdown()
        {
            lock (__lifecycleLock)
            {
                if (_state == LifecycleStateEnum.Stopped)
                    return;

                if ((ReferenceEquals(__owner, this) == false) || (_state != LifecycleStateEnum.Initialized))
                    throw new PlatformException("This SteamPlatform instance does not own the process-wide Steam API.");

                EnsureOwningThread();
                _state = LifecycleStateEnum.ShuttingDown;
                try
                {
                    foreach (var pair in _pendingTickets)
                    {
                        if (_pendingTickets.TryRemove(pair.Key, out var pending))
                        {
                            pending.CancellationRegistration.Dispose();
                            pending.Completion.TrySetException(new PlatformException("Steam shut down before the authentication ticket completed."));
                            SteamUser.CancelAuthTicket(pair.Key);
                        }
                    }

                    foreach (var pair in _issuedTickets)
                    {
                        if (_issuedTickets.TryRemove(pair.Key, out _))
                            SteamUser.CancelAuthTicket(pair.Key);
                    }

                    CancelQueuedTickets();
                }
                finally
                {
                    try
                    {
                        _ticketCallback?.Dispose();
                        _ticketCallback = null;
                        SteamAPI.Shutdown();
                    }
                    finally
                    {
                        _pendingTickets.Clear();
                        _issuedTickets.Clear();
                        while (_cancelTicketHandles.TryDequeue(out _))
                        {
                        }

                        __owner = null;
                        _ownerThreadId = 0;
                        _state = LifecycleStateEnum.Stopped;
                        _user = default;
                    }
                }
            }
        }

        void CancelPendingTicket(HAuthTicket handle, CancellationToken cancellationToken)
        {
            lock (__lifecycleLock)
            {
                if (_pendingTickets.TryRemove(handle, out var pending) == false)
                    return;

                if ((ReferenceEquals(__owner, this)) && (_state == LifecycleStateEnum.Initialized))
                    _cancelTicketHandles.Enqueue(handle);

                pending.CancellationRegistration.Dispose();
                pending.Completion.TrySetCanceled(cancellationToken);
            }
        }

        void CancelQueuedTickets()
        {
            while (_cancelTicketHandles.TryDequeue(out var handle))
                SteamUser.CancelAuthTicket(handle);
        }

        void EnsureOwningThread()
        {
            if (Environment.CurrentManagedThreadId != _ownerThreadId)
                throw new PlatformException("Steam native API calls must run on the thread that initialized SteamPlatform.");
        }

        void OnTicketCreated(GetTicketForWebApiResponse_t response)
        {
            if (_pendingTickets.TryRemove(response.m_hAuthTicket, out var pending) == false)
                return;

            pending.CancellationRegistration.Dispose();
            if (response.m_eResult != EResult.k_EResultOK)
            {
                _cancelTicketHandles.Enqueue(response.m_hAuthTicket);
                pending.Completion.TrySetException(new PlatformException($"Steam authentication ticket failed ({response.m_eResult})."));
                return;
            }

            if (_issuedTickets.TryAdd(response.m_hAuthTicket, 0) == false)
            {
                _cancelTicketHandles.Enqueue(response.m_hAuthTicket);
                pending.Completion.TrySetException(new PlatformException("Steam issued a duplicate completed authentication ticket handle."));
                return;
            }

            var value = BitConverter.ToString(response.m_rgubTicket, 0, response.m_cubTicket).Replace("-", string.Empty);
            pending.Completion.TrySetResult(new("steam", _authenticationIdentity, value, () => ReleaseTicket(response.m_hAuthTicket)));
        }

        void ReleaseTicket(HAuthTicket handle)
        {
            lock (__lifecycleLock)
            {
                if ((_issuedTickets.TryRemove(handle, out _)) && (ReferenceEquals(__owner, this)) && (_state == LifecycleStateEnum.Initialized))
                    _cancelTicketHandles.Enqueue(handle);
            }
        }
    }
}
