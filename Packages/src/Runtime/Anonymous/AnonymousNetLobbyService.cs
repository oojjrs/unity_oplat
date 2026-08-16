using oojjrs.oplat.anonymous.controllers;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetLobbyService : MyNetLobbyServiceInterface
    {
        private const int MinimumPollingDelaySeconds = 1;

        private readonly AnonymousNet Net;

        private MyNetLobbyServiceInterface.ConfigInterface _config;
        private float _nextUpdateTimeSeconds;
        private MyNetLobbyServiceInterface.ResultInterface _result;

        internal AnonymousNetLobbyService(AnonymousNet net)
        {
            Net = net;
        }

        Task MyNetLobbyServiceInterface.RefreshAsync(MyNetLobbyServiceInterface.ResultInterface result)
        {
            return RefreshAsync(CancellationToken.None, result);
        }

        Task MyNetLobbyServiceInterface.StartAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result)
        {
            _config = config;
            _result = result;
            return RefreshAsync(config.CancellationToken, result);
        }

        void MyNetLobbyServiceInterface.Stop()
        {
            _config = null;
            _result = null;
        }

        private async Task RefreshAsync(CancellationToken callerCancellationToken, MyNetLobbyServiceInterface.ResultInterface result)
        {
            if (_config != null)
                _nextUpdateTimeSeconds = Time.realtimeSinceStartup + Math.Max(MinimumPollingDelaySeconds, _config.PollingDelaySeconds);

            using (var cancellationSource = Net.CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                MyNetRoomInterface[] rooms;
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.GetRooms, null, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.GetRooms, cancellationToken);
                    response.EnsureSuccess();

                    var roomsData = await response.GetContentAsync<AnonymousServerGetRooms.ResponseArgument>();
                    if ((roomsData == null) || (roomsData.Rooms == null))
                        throw new FormatException("Invalid anonymous rooms response.");

                    rooms = new MyNetRoomInterface[roomsData.Rooms.Length];
                    for (var index = 0; index < roomsData.Rooms.Length; ++index)
                        rooms[index] = roomsData.Rooms[index].ToNetRoom();
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to get anonymous rooms.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                result.OnOk(rooms);
            }
        }

        internal async void Update()
        {
            var config = _config;
            if ((config == null) || (Time.realtimeSinceStartup < _nextUpdateTimeSeconds))
                return;

            _nextUpdateTimeSeconds = Time.realtimeSinceStartup + Math.Max(MinimumPollingDelaySeconds, config.PollingDelaySeconds);
            try
            {
                var room = await Net.GetCurrentRoomAsync(config.CancellationToken);
                if ((_config == config) && (room == null))
                    await RefreshAsync(config.CancellationToken, _result);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (_config == config)
                    _result.OnException(new MyNetSessionException("Failed to update anonymous lobby.", exception));
            }
        }
    }
}
