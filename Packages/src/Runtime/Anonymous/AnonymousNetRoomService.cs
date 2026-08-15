using oojjrs.oplat.anonymous.controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetRoomService : MyNetRoomServiceInterface
    {
        private readonly AnonymousNet _net;

        internal AnonymousNetRoomService(AnonymousNet net)
        {
            _net = net;
        }

        async Task MyNetRoomServiceInterface.CreateAsync(MyNetRoomServiceInterface.CreateConfigInterface config, MyNetRoomServiceInterface.CreateResultInterface result)
        {
            using (var cancellationSource = _net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                MyNetRoomInterface room;
                try
                {
                    var requestContent = JsonUtility.ToJson(new AnonymousServerCreateRoom.RequestArgument()
                    {
                        IsLocked = config.IsLocked,
                        IsPrivate = config.IsPrivate,
                        MaxPlayers = config.MaxPlayers,
                        Password = config.Password,
                        PlayerFields = ConvertFields(config.PlayerFields),
                        PlayerNickname = config.PlayerNickname,
                        RoomFields = ConvertFields(config.RoomFields),
                        Title = config.Title,
                    });
                    using (var content = new StringContent(requestContent, Encoding.UTF8, "application/json"))
                    {
                        using (var response = await _net.PostAsync(AnonymousServer.ApiCreateRoom, content, cancellationToken))
                        {
                            response.EnsureSuccessStatusCode();

                            var responseContent = await response.Content.ReadAsStringAsync();
                            room = ConvertRoom(JsonUtility.FromJson<AnonymousServerCreateRoom.ResponseArgument>(responseContent));
                        }
                    }
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to create anonymous room.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                result.OnOk(room);
            }
        }

        async Task MyNetRoomServiceInterface.ExitAsync(MyNetRoomServiceInterface.ExitConfigInterface config, MyNetRoomServiceInterface.ExitResultInterface result)
        {
            using (var cancellationSource = _net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                var playerId = config.PlayerId;
                var roomId = config.RoomId;
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyPlayerId);
                    return;
                }

                if (string.IsNullOrWhiteSpace(roomId))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                try
                {
                    var requestContent = JsonUtility.ToJson(new AnonymousServerExitRoom.RequestArgument()
                    {
                        PlayerId = playerId,
                        RoomId = roomId,
                    });
                    using (var content = new StringContent(requestContent, Encoding.UTF8, "application/json"))
                    {
                        using (var response = await _net.PostAsync(AnonymousServer.ApiExitRoom, content, cancellationToken))
                        {
                            switch (response.StatusCode)
                            {
                                case HttpStatusCode.NotFound:
                                    failure = MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom;
                                    break;
                                case HttpStatusCode.Forbidden:
                                    failure = MyNetInterface.CatchInterface.FailureEnum.NotPermitted;
                                    break;
                                default:
                                    response.EnsureSuccessStatusCode();
                                    break;
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to exit anonymous room.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                {
                    result.OnFailed(failure.Value);
                    return;
                }

                result.OnOk(roomId, playerId);
            }
        }

        Task MyNetRoomServiceInterface.JoinAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            throw new NotImplementedException();
        }

        Task MyNetRoomServiceInterface.UpdateAsync(MyNetRoomServiceInterface.UpdateConfigInterface config, MyNetRoomServiceInterface.UpdateResultInterface result)
        {
            throw new NotImplementedException();
        }

        private static AnonymousServerCreateRoom.FieldData[] ConvertFields(IEnumerable<MyNetInterface.Field> fields)
        {
            if (fields == null)
                return Array.Empty<AnonymousServerCreateRoom.FieldData>();

            return fields.Select(field => new AnonymousServerCreateRoom.FieldData()
            {
                Key = field.key,
                Value = field.value,
                Visibility = field.visibility,
            }).ToArray();
        }

        private static MyNetInterface.Field[] ConvertFields(AnonymousServerCreateRoom.FieldData[] fields)
        {
            if (fields == null)
                return Array.Empty<MyNetInterface.Field>();

            var data = new MyNetInterface.Field[fields.Length];
            for (var index = 0; index < fields.Length; ++index)
            {
                if ((fields[index] == null) || (Enum.IsDefined(typeof(MyNetInterface.Field.VisibilityEnum), fields[index].Visibility) == false))
                    throw new FormatException("Invalid anonymous field data.");

                data[index] = new MyNetInterface.Field
                {
                    key = fields[index].Key,
                    value = fields[index].Value,
                    visibility = fields[index].Visibility,
                };
            }

            return data;
        }

        private static MyNetPlayerInterface ConvertPlayer(AnonymousServerCreateRoom.PlayerData player)
        {
            if ((player == null) || string.IsNullOrEmpty(player.Id))
                throw new FormatException("Invalid anonymous player response.");

            return new AnonymousPlayer(ConvertFields(player.Fields), player.Id, player.IsHost, player.Nickname);
        }

        internal static MyNetRoomInterface ConvertRoom(AnonymousServerCreateRoom.ResponseArgument room)
        {
            if ((room == null) || string.IsNullOrEmpty(room.Code) || string.IsNullOrEmpty(room.HostId) || string.IsNullOrEmpty(room.Id) || (room.MaxPlayers < 1))
                throw new FormatException("Invalid anonymous room response.");

            var playerData = room.Players ?? Array.Empty<AnonymousServerCreateRoom.PlayerData>();
            var players = playerData.Select(player => ConvertPlayer(player)).ToArray();

            return new AnonymousRoom(room.Code, ConvertFields(room.Fields), room.HasPassword, room.HostId, room.Id, room.IsLocked, room.IsPrivate, room.MaxPlayers, players, room.Title);
        }
    }
}
