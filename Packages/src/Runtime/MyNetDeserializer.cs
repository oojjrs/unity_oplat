using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace oojjrs.oplat
{
    /// <summary>
    /// 여기저기 아류작들이 있지만, 앞으로는 여기 것을 본진으로 삼겠습니다.
    /// </summary>
    public static class MyNetDeserializer
    {
        private static readonly Dictionary<Type, PropertyInfo[]> _cache = new();

        public static object Deserialize(Stream stream)
        {
            // 스트림을 닫지 않기 위해 using하지 않음.
            var reader = new BinaryReader(stream);
            var name = reader.ReadString();
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var type = Type.GetType(name);
            if (type is null)
                return null;

            return ReadClass(reader, type, GetProperties(type));
        }

        private static PropertyInfo[] GetProperties(Type type)
        {
            if (_cache.TryGetValue(type, out var value) == false)
            {
                value = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.SetProperty).Where(t => t.CanRead && t.CanWrite).OrderBy(t => t.Name).ToArray();
                _cache[type] = value;
            }

            return value;
        }

        private static bool IsItem(Type propertyType)
        {
            return propertyType.IsPrimitive || (propertyType == typeof(string)) || (propertyType == typeof(DateTime));
        }

        private static bool IsTuple(Type type)
        {
            if (type.IsGenericType == false)
                return false;

            var openType = type.GetGenericTypeDefinition();
            return openType == typeof(ValueTuple<>)
                || openType == typeof(ValueTuple<,>)
                || openType == typeof(ValueTuple<,,>)
                || openType == typeof(ValueTuple<,,,>)
                || openType == typeof(ValueTuple<,,,,>)
                || openType == typeof(ValueTuple<,,,,,>)
                || openType == typeof(ValueTuple<,,,,,,>)
                || (openType == typeof(ValueTuple<,,,,,,,>) && IsTuple(type.GetGenericArguments()[7]));
        }

        private static object ReadEnum(BinaryReader br, Type enumType)
        {
            var s = br.ReadString();
            return Enum.Parse(enumType, s);
        }

        private static object ReadClass(BinaryReader br, Type type, PropertyInfo[] props)
        {
            var value = Activator.CreateInstance(type);
            foreach (var prop in props)
            {
                if (br.BaseStream.Position >= br.BaseStream.Length)
                    break;

                var isExist = br.ReadBoolean();
                if (isExist == false)
                    continue;

                if (prop.PropertyType.IsArray)
                {
                    var elementType = prop.PropertyType.GetElementType();

                    var length = br.ReadInt32();
                    if (length <= 0)
                    {
                        prop.SetValue(value, Array.CreateInstance(elementType, 0));
                        continue;
                    }

                    var ret = Array.CreateInstance(elementType, length);
                    if (elementType.IsEnum)
                    {
                        for (int i = 0; i < length; ++i)
                            ret.SetValue(ReadEnum(br, elementType), i);
                    }
                    else if (IsItem(elementType))
                    {
                        for (int i = 0; i < length; ++i)
                            ret.SetValue(ReadItem(br, elementType), i);
                    }
                    else if (IsTuple(elementType))
                    {
                        for (int i = 0; i < length; ++i)
                            ret.SetValue(ReadTuple(br, elementType), i);
                    }
                    else
                    {
                        var propProps = GetProperties(elementType);
                        for (int i = 0; i < length; ++i)
                            ret.SetValue(ReadClass(br, elementType, propProps), i);
                    }
                    prop.SetValue(value, ret);
                }
                else
                {
                    if (prop.PropertyType.IsEnum)
                        prop.SetValue(value, ReadEnum(br, prop.PropertyType));
                    else if (IsItem(prop.PropertyType))
                        prop.SetValue(value, ReadItem(br, prop.PropertyType));
                    else if (IsTuple(prop.PropertyType))
                        prop.SetValue(value, ReadTuple(br, prop.PropertyType));
                    else
                        prop.SetValue(value, ReadClass(br, prop.PropertyType, GetProperties(prop.PropertyType)));
                }
            }

            return value;
        }

        private static object ReadItem(BinaryReader br, Type type)
        {
            if (type == typeof(string))
                return br.ReadString();
            else if (type == typeof(float))
                return br.ReadSingle();
            else if (type == typeof(long))
                return br.ReadInt64();
            else if (type == typeof(int))
                return br.ReadInt32();
            else if (type == typeof(short))
                return br.ReadInt16();
            else if (type == typeof(byte))
                return br.ReadByte();
            else if (type == typeof(bool))
                return br.ReadBoolean();
            else if (type == typeof(double))
                return br.ReadDouble();
            else if (type == typeof(char))
                return br.ReadChar();
            else if (type == typeof(DateTime))
                return DateTime.FromBinary(br.ReadInt64());
            else
                throw new NotImplementedException();
        }

        private static object ReadTuple(BinaryReader br, Type type)
        {
            var value = Activator.CreateInstance(type);
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType.IsEnum)
                    field.SetValue(value, ReadEnum(br, field.FieldType));
                else if (IsItem(field.FieldType))
                    field.SetValue(value, ReadItem(br, field.FieldType));
                else if (IsTuple(field.FieldType))
                    field.SetValue(value, ReadTuple(br, field.FieldType));
                else
                    field.SetValue(value, ReadClass(br, field.FieldType, GetProperties(field.FieldType)));
            }

            return value;
        }
    }
}
