using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousNetRoomProtocol
    {
        [Serializable]
        internal record RoomRequestArgument
        {
            public bool IsLocked;
            public bool IsPrivate;
            public int MaxPlayers;
            public string Password;
            public FieldData[] PlayerFields;
            public string PlayerNickname;
            public FieldData[] RoomFields;
            public string Title;

            public RoomRequestArgument(MyNetRoomServiceInterface.CreateConfigInterface config)
            {
                IsLocked = config.IsLocked;
                IsPrivate = config.IsPrivate;
                MaxPlayers = config.MaxPlayers;
                Password = config.Password;
                PlayerFields = ConvertFields(config.PlayerFields);
                PlayerNickname = config.PlayerNickname;
                RoomFields = ConvertFields(config.RoomFields);
                Title = config.Title;
            }

            internal string ToJson()
            {
                return JsonUtility.ToJson(this);
            }
        }

        [Serializable]
        internal record CreateResponseArgument
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
        }

        [Serializable]
        internal record FieldData
        {
            public string Key;
            public string Value;
            public MyNetInterface.Field.VisibilityEnum Visibility;
        }

        [Serializable]
        internal record PlayerData
        {
            public FieldData[] Fields;
            public string Id;
            public bool IsHost;
            public string Nickname;
        }

        [Serializable]
        internal record RoomsData
        {
            public CreateResponseArgument[] Rooms;
        }

        private static FieldData[] ConvertFields(IEnumerable<MyNetInterface.Field> fields)
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

        private static MyNetInterface.Field[] ConvertFields(FieldData[] fields)
        {
            if (fields == null)
                return Array.Empty<MyNetInterface.Field>();

            ValidateFields(fields);
            var data = new MyNetInterface.Field[fields.Length];
            for (var index = 0; index < fields.Length; ++index)
            {
                data[index] = new MyNetInterface.Field
                {
                    key = fields[index].Key,
                    value = fields[index].Value,
                    visibility = fields[index].Visibility,
                };
            }

            return data;
        }

        private static MyNetPlayerInterface ConvertPlayer(PlayerData player)
        {
            if ((player == null) || string.IsNullOrEmpty(player.Id))
                throw new FormatException("Invalid anonymous player response.");

            return new AnonymousPlayer(ConvertFields(player.Fields), player.Id, player.IsHost, player.Nickname);
        }

        internal static MyNetRoomInterface ConvertRoom(CreateResponseArgument room)
        {
            if ((room == null) || string.IsNullOrEmpty(room.Code) || string.IsNullOrEmpty(room.HostId) || string.IsNullOrEmpty(room.Id) || (room.MaxPlayers < 1))
                throw new FormatException("Invalid anonymous room response.");

            var playerData = room.Players ?? Array.Empty<PlayerData>();
            var players = playerData.Select(t => ConvertPlayer(t)).ToArray();

            return new AnonymousRoom(room.Code, ConvertFields(room.Fields), room.HasPassword, room.HostId, room.Id, room.IsLocked, room.IsPrivate, room.MaxPlayers, players, room.Title);
        }

        internal static MyNetRoomInterface GetRoomFromJson(string content)
        {
            return ConvertRoom(JsonUtility.FromJson<CreateResponseArgument>(content));
        }

        internal static void ValidateFields(FieldData[] fields)
        {
            foreach (var field in fields)
            {
                if ((field == null) || (Enum.IsDefined(typeof(MyNetInterface.Field.VisibilityEnum), field.Visibility) == false))
                    throw new FormatException("Invalid anonymous field data.");
            }
        }
    }
}
