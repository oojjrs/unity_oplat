using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetLobbyService : MyNetLobbyServiceInterface
    {
        private readonly HttpClient _client;
        private readonly CancellationToken _lifetimeCancellationToken;

        internal AnonymousNetLobbyService(HttpClient client, CancellationToken lifetimeCancellationToken)
        {
            _client = client;
            _lifetimeCancellationToken = lifetimeCancellationToken;
        }

        void MyNetLobbyServiceInterface.Refresh()
        {
            throw new NotImplementedException();
        }

        async Task MyNetLobbyServiceInterface.StartAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result)
        {
            var cancellationToken = config.CancellationToken;
            MyNetRoomInterface[] rooms;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();
                using (var response = await _client.GetAsync(AnonymousServer.GetUri(AnonymousServer.ApiGetRooms), cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    cancellationToken.ThrowIfCancellationRequested();

                    var roomsData = JsonUtility.FromJson<AnonymousNetRoomProtocol.RoomsData>(content);
                    if ((roomsData == null) || (roomsData.Rooms == null))
                        throw new FormatException("Invalid anonymous rooms response.");

                    rooms = new MyNetRoomInterface[roomsData.Rooms.Length];
                    for (var index = 0; index < roomsData.Rooms.Length; ++index)
                        rooms[index] = AnonymousNetRoomProtocol.ConvertRoom(roomsData.Rooms[index]);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _lifetimeCancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();
                result.OnException(new MyNetSessionException("Failed to get anonymous rooms.", exception));
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _lifetimeCancellationToken.ThrowIfCancellationRequested();
            result.OnOk(rooms);
        }

        void MyNetLobbyServiceInterface.Stop()
        {
            throw new NotImplementedException();
        }
    }
}
