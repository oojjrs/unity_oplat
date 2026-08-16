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
    public static class MyNetSerializer
    {
        private static readonly Dictionary<Type, PropertyInfo[]> _cache = new();

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
            return propertyType.IsPrimitive || propertyType.IsEnum || (propertyType == typeof(string)) || (propertyType == typeof(DateTime));
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

        public static byte[] Serialize(object packet)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(packet.GetType().AssemblyQualifiedName);
                    WriteClass(writer, packet, GetProperties(packet.GetType()));
                }

                return ms.ToArray();
            }
        }

        private static void WriteClass(BinaryWriter bw, object value, PropertyInfo[] props)
        {
            foreach (var prop in props)
            {
                var propValue = prop.GetValue(value);
                var isExist = propValue is not null;
                bw.Write(isExist);

                if (isExist == false)
                    continue;

                if (prop.PropertyType.IsArray)
                {
                    var arr = (Array)propValue;
                    bw.Write(arr.Length);

                    if (arr.Length <= 0)
                        continue;

                    var elementType = arr.GetType().GetElementType();
                    if (IsItem(elementType))
                    {
                        for (int i = 0; i < arr.Length; ++i)
                            WriteItem(bw, arr.GetValue(i));
                    }
                    else if (IsTuple(elementType))
                    {
                        for (int i = 0; i < arr.Length; ++i)
                            WriteTuple(bw, arr.GetValue(i));
                    }
                    else
                    {
                        var propProps = GetProperties(elementType);
                        for (int i = 0; i < arr.Length; ++i)
                            WriteClass(bw, arr.GetValue(i), propProps);
                    }
                }
                else
                {
                    if (IsItem(prop.PropertyType))
                        WriteItem(bw, propValue);
                    else if (IsTuple(prop.PropertyType))
                        WriteTuple(bw, propValue);
                    else
                        WriteClass(bw, propValue, GetProperties(prop.PropertyType));
                }
            }
        }

        private static void WriteItem(BinaryWriter bw, object o)
        {
            if (o.GetType().IsEnum)
            {
                bw.Write(o.ToString());
                return;
            }

            switch (o)
            {
                case string value:
                    bw.Write(value);
                    break;
                case float value:
                    bw.Write(value);
                    break;
                case long value:
                    bw.Write(value);
                    break;
                case int value:
                    bw.Write(value);
                    break;
                case short value:
                    bw.Write(value);
                    break;
                case byte value:
                    bw.Write(value);
                    break;
                case bool value:
                    bw.Write(value);
                    break;
                case double value:
                    bw.Write(value);
                    break;
                case char value:
                    bw.Write(value);
                    break;
                case DateTime value:
                    bw.Write(value.ToBinary());
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private static void WriteTuple(BinaryWriter bw, object o)
        {
            foreach (var field in o.GetType().GetFields())
            {
                var fieldValue = field.GetValue(o);
                if (IsItem(field.FieldType))
                    WriteItem(bw, fieldValue);
                else if (IsTuple(field.FieldType))
                    WriteTuple(bw, fieldValue);
                else
                    WriteClass(bw, fieldValue, GetProperties(field.FieldType));
            }
        }
    }
}
