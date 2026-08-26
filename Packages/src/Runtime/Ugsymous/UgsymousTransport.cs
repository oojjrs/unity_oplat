using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Multiplayer;

namespace oojjrs.oplat.ugsymous
{
    internal sealed class UgsymousTransport : INetworkHandler, IDisposable
    {
        private enum MessageTypeEnum : byte
        {
            Request = 1,
            Response = 2,
        }

        private const int MessageByteCountMax = 64 * 1024;
        private readonly string _account;
        private readonly List<NetworkConnection> _connections = new();
        private readonly Queue<MyNetRequest> _requests = new();
        private readonly Queue<MyNetResponse> _responses = new();
        private NetworkConnection _connection;
        private NetworkDriver _driver;
        private NetworkPipeline _pipeline;
        private TaskCompletionSource<bool> _startSource;

        internal bool IsHost { get; private set; }
        internal UgsymousTransport(string account) => _account = account;

        Task INetworkHandler.StartAsync(NetworkConfiguration configuration)
        {
            DisposeDriver();
            IsHost = configuration.Role != NetworkRole.Client;
            var settings = new NetworkSettings(Allocator.Temp);
            settings.WithFragmentationStageParameters(MessageByteCountMax + 1);
            if (IsHost)
            {
                var relayData = configuration.RelayServerData;
                settings.WithRelayParameters(ref relayData);
            }
            else
            {
                var relayData = configuration.RelayClientData;
                settings.WithRelayParameters(ref relayData);
            }

            _driver = NetworkDriver.Create(settings);
            _pipeline = _driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            if (IsHost)
            {
                if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0)
                    throw new InvalidOperationException("UGSymous Relay transport failed to bind.");

                if (_driver.Listen() != 0)
                    throw new InvalidOperationException("UGSymous Relay transport failed to listen.");

                return Task.CompletedTask;
            }

            _connection = _driver.Connect(configuration.RelayClientData.Endpoint);
            _startSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _startSource.Task;
        }

        Task INetworkHandler.StopAsync()
        {
            DisposeDriver();
            return Task.CompletedTask;
        }

        internal void Update()
        {
            if (_driver.IsCreated == false)
                return;

            _driver.ScheduleUpdate().Complete();
            if (IsHost)
                UpdateHost();
            else
                UpdateClient();
        }

        private void UpdateHost()
        {
            NetworkConnection accepted;
            while ((accepted = _driver.Accept()) != default)
                _connections.Add(accepted);

            for (var i = _connections.Count - 1; i >= 0; --i)
            {
                var connection = _connections[i];
                NetworkEvent.Type type;
                while ((type = _driver.PopEventForConnection(connection, out var reader)) != NetworkEvent.Type.Empty)
                {
                    if (type == NetworkEvent.Type.Data)
                        Receive(reader);
                    else if (type == NetworkEvent.Type.Disconnect)
                    {
                        _connections.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void UpdateClient()
        {
            NetworkEvent.Type type;
            while ((type = _connection.PopEvent(_driver, out var reader)) != NetworkEvent.Type.Empty)
            {
                if (type == NetworkEvent.Type.Connect)
                    _startSource?.TrySetResult(true);
                else if (type == NetworkEvent.Type.Data)
                    Receive(reader);
                else if (type == NetworkEvent.Type.Disconnect)
                {
                    _connection = default;
                    _startSource?.TrySetException(new IOException($"UGSymous Relay connection for {_account} was disconnected."));
                }
            }
        }

        private void Receive(DataStreamReader reader)
        {
            if (reader.Length <= 1)
                return;

            var type = (MessageTypeEnum)reader.ReadByte();
            using var bytes = new NativeArray<byte>(reader.Length - 1, Allocator.Temp);
            reader.ReadBytes(bytes);
            using var stream = new MemoryStream(bytes.ToArray(), false);
            var packet = MyNetDeserializer.Deserialize(stream);
            if ((type == MessageTypeEnum.Request) && (packet is MyNetRequest request))
                _requests.Enqueue(request);
            else if ((type == MessageTypeEnum.Response) && (packet is MyNetResponse response))
                _responses.Enqueue(response);
        }

        internal void SendRequest(MyNetRequest request)
        {
            if (IsHost == false)
                Send(_connection, MessageTypeEnum.Request, MyNetSerializer.Serialize(request));
        }

        internal void SendResponse(MyNetResponse response)
        {
            if (IsHost == false)
                return;

            var bytes = MyNetSerializer.Serialize(response);
            foreach (var connection in _connections)
                Send(connection, MessageTypeEnum.Response, bytes);
        }

        private void Send(NetworkConnection connection, MessageTypeEnum type, byte[] bytes)
        {
            if ((bytes == null) || (bytes.Length > MessageByteCountMax) || (connection.IsCreated == false))
                return;

            if (_driver.BeginSend(_pipeline, connection, out var writer) != 0)
                return;

            writer.WriteByte((byte)type);
            using var nativeBytes = new NativeArray<byte>(bytes, Allocator.Temp);
            writer.WriteBytes(nativeBytes);
            _driver.EndSend(writer);
        }

        internal bool TryReceiveRequest(out MyNetRequest request)
        {
            if (_requests.Count > 0) { request = _requests.Dequeue(); return true; }
            request = null;
            return false;
        }

        internal bool TryReceiveResponse(out MyNetResponse response)
        {
            if (_responses.Count > 0) { response = _responses.Dequeue(); return true; }
            response = null;
            return false;
        }

        private void DisposeDriver()
        {
            _startSource?.TrySetCanceled();
            _startSource = null;
            _connections.Clear();
            _connection = default;
            if (_driver.IsCreated)
                _driver.Dispose();
        }

        public void Dispose() => DisposeDriver();
    }
}
