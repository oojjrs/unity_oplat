using oojjrs.oplat.anonymous.controllers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetLobbyService : MyNetLobbyServiceInterface
    {
        private readonly AnonymousNet Net;

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
            return RefreshAsync(config.CancellationToken, result);
        }

        void MyNetLobbyServiceInterface.Stop()
        {
        }

        private async Task RefreshAsync(CancellationToken callerCancellationToken, MyNetLobbyServiceInterface.ResultInterface result)
        {
            using (var cancellationSource = Net.CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                MyNetRoomInterface[] rooms;
                try
                {
                    var response = await Net.SendAsync(AnonymousTransport.OperationEnum.GetRooms, null, cancellationToken);
                    response.EnsureSuccess();

                    var roomsData = AnonymousTransport.Deserialize<AnonymousServerGetRooms.ResponseArgument>(response.Content);
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
    }
}
