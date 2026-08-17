using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousServerRoom
    {
        public record FieldData
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public MyNetInterface.Field.VisibilityEnum Visibility { get; set; }

            internal static FieldData[] FromNetFields(IEnumerable<MyNetInterface.Field> fields)
            {
                if (fields == null)
                    return Array.Empty<FieldData>();

                return fields.Select(field => new FieldData()
                {
                    Key = field.key,
                    Value = field.value,
                    Visibility = field.visibility,
                }).ToArray();
            }

            internal static FieldData[] Merge(FieldData[] fields, FieldData[] updatedFields)
            {
                fields ??= Array.Empty<FieldData>();
                updatedFields ??= Array.Empty<FieldData>();
                if (updatedFields.Length == 0)
                    return fields;

                return fields.Where(field => updatedFields.Any(updatedField => updatedField.Key == field.Key) == false).Concat(updatedFields).ToArray();
            }

            internal static MyNetInterface.Field[] ToNetFields(FieldData[] fields)
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

            internal static void Validate(FieldData[] fields)
            {
                foreach (var field in fields)
                {
                    if ((field == null) || (Enum.IsDefined(typeof(MyNetInterface.Field.VisibilityEnum), field.Visibility) == false))
                        throw new FormatException("Invalid anonymous field data.");
                }
            }
        }

        public record PlayerData
        {
            public FieldData[] Fields { get; set; }
            public string Id { get; set; }
            public bool IsHost { get; set; }
            public string Nickname { get; set; }

            internal MyNetPlayerInterface ToNetPlayer()
            {
                if (string.IsNullOrEmpty(Id))
                    throw new FormatException("Invalid anonymous player response.");

                return new AnonymousNetPlayer(FieldData.ToNetFields(Fields), Id, IsHost, Nickname);
            }
        }

        public record RoomData
        {
            public string Code { get; set; }
            public FieldData[] Fields { get; set; }
            public bool HasPassword { get; set; }
            public string HostId { get; set; }
            public string Id { get; set; }
            public bool IsLocked { get; set; }
            public bool IsPrivate { get; set; }
            public int MaxPlayers { get; set; }
            public PlayerData[] Players { get; set; }
            public string Title { get; set; }

            internal RoomData GetMemberResponseArgument(string account)
            {
                var isHost = HostId == account;
                return this with
                {
                    Fields = (Fields ?? Array.Empty<FieldData>()).Where(field => (field.Visibility != MyNetInterface.Field.VisibilityEnum.Private) || isHost).ToArray(),
                    Players = (Players ?? Array.Empty<PlayerData>()).Select(player => player with
                    {
                        Fields = (player.Fields ?? Array.Empty<FieldData>()).Where(field => (field.Visibility != MyNetInterface.Field.VisibilityEnum.Private) || player.Id == account).ToArray(),
                    }).ToArray(),
                };
            }

            internal MyNetRoomInterface ToNetRoom()
            {
                if (string.IsNullOrEmpty(Code) || string.IsNullOrEmpty(HostId) || string.IsNullOrEmpty(Id) || (MaxPlayers < 1))
                    throw new FormatException("Invalid anonymous room response.");

                var playerData = Players ?? Array.Empty<PlayerData>();
                var players = new MyNetPlayerInterface[playerData.Length];
                for (var index = 0; index < playerData.Length; ++index)
                {
                    if (playerData[index] == null)
                        throw new FormatException("Invalid anonymous player response.");

                    players[index] = playerData[index].ToNetPlayer();
                }

                return new AnonymousNetRoom(Code, FieldData.ToNetFields(Fields), HasPassword, HostId, Id, IsLocked, IsPrivate, MaxPlayers, players, Title);
            }
        }

        internal sealed class RoomSecret
        {
            internal string Password { get; }
            internal RoomData Room { get; }

            internal RoomSecret(string password, RoomData room)
            {
                Password = password;
                Room = room;
            }
        }

        internal sealed class State
        {
            internal readonly HashSet<string> RoomCodes = new();
            internal readonly List<RoomSecret> Rooms = new();
        }

        internal static void NotifyExited(RoomData room, IReadOnlyDictionary<string, AnonymousServerSession> sessions, string excludedPlayerId)
        {
            foreach (var player in room.Players ?? Array.Empty<PlayerData>())
            {
                if ((player.Id == excludedPlayerId) || (sessions.TryGetValue(player.Id, out var playerSession) == false))
                    continue;

                playerSession.Messages.Send(AnonymousTransport.Message.CreateRoomExited(room.Id));
            }
        }

        internal static async Task NotifyUpdatedAsync(RoomData room, IReadOnlyDictionary<string, AnonymousServerSession> sessions, string excludedPlayerId)
        {
            foreach (var player in room.Players ?? Array.Empty<PlayerData>())
            {
                if ((player.Id == excludedPlayerId) || (sessions.TryGetValue(player.Id, out var playerSession) == false))
                    continue;

                var memberResponse = await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, room.GetMemberResponseArgument(player.Id));
                playerSession.Messages.Send(AnonymousTransport.Message.CreateRoomUpdated(memberResponse.Content));
            }
        }
    }
}
