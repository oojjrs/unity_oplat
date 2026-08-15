using oojjrs.oplat.anonymous.controllers;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetLobbyService : MyNetLobbyServiceInterface
    {
        private readonly AnonymousNet _net;

        internal AnonymousNetLobbyService(AnonymousNet net)
        {
            _net = net;
        }

        Task MyNetLobbyServiceInterface.RefreshAsync(MyNetLobbyServiceInterface.ResultInterface result)
        {
            return RefreshAsync(CancellationToken.None, result);
        }

        Task MyNetLobbyServiceInterface.StartAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result)
        {
            return RefreshAsync(config.CancellationToken, result);
        }

        void MyNetLobbyServiceInterface.Stop()
        {
        }

        private async Task RefreshAsync(CancellationToken callerCancellationToken, MyNetLobbyServiceInterface.ResultInterface result)
        {
            using (var cancellationSource = _net.CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                MyNetRoomInterface[] rooms;
                try
                {
                    using (var response = await _net.GetAsync(AnonymousServer.ApiGetRooms, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();

                        var content = await response.Content.ReadAsStringAsync();
                        var roomsData = JsonUtility.FromJson<AnonymousServerGetRooms.ResponseArgument>(content);
                        if ((roomsData == null) || (roomsData.Rooms == null))
                            throw new FormatException("Invalid anonymous rooms response.");

                        rooms = new MyNetRoomInterface[roomsData.Rooms.Length];
                        for (var index = 0; index < roomsData.Rooms.Length; ++index)
                            rooms[index] = AnonymousNetRoomService.ConvertRoom(roomsData.Rooms[index]);
                    }
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
    }
}
