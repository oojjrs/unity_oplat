using System;
using System.Collections.Generic;
using System.Linq;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousServerRoom
    {
        [Serializable]
        internal record FieldData
        {
            public string Key;
            public string Value;
            public MyNetInterface.Field.VisibilityEnum Visibility;

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

        [Serializable]
        internal record PlayerData
        {
            public FieldData[] Fields;
            public string Id;
            public bool IsHost;
            public string Nickname;

            internal MyNetPlayerInterface ToNetPlayer()
            {
                if (string.IsNullOrEmpty(Id))
                    throw new FormatException("Invalid anonymous player response.");

                return new AnonymousNetPlayer(FieldData.ToNetFields(Fields), Id, IsHost, Nickname);
            }
        }

        [Serializable]
        internal record RoomData
        {
            public string Code;
            public FieldData[] Fields;
            public bool HasPassword;
            public string HostId;
            public string Id;
            public bool IsLocked;
            public bool IsPrivate;
            public int MaxPlayers;
            public PlayerData[] Players;
            public string Title;

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
            internal RoomSecret(string password, RoomData room)
            {
                Password = password;
                Room = room;
            }

            internal string Password { get; }
            internal RoomData Room { get; }
        }

        internal sealed class State
        {
            internal readonly HashSet<string> RoomCodes = new();
            internal readonly List<RoomSecret> Rooms = new();
        }
    }
}
