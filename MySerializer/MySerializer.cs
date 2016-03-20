using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace MySerializer
{

    public class MySerializerException : Exception
    {
        public MySerializerException() : base() { }
        public MySerializerException(string ms) : base(ms) { }
    }
    public static class MySerializer
    {
        public static void Serialize(Stream stream, object graph)
        {
            using (var bw = new BinaryWriter(stream))
            {
                Type type = graph.GetType();
                bw.Write(type.AssemblyQualifiedName);
                Write(type, graph, bw);
            }
        }
        private static void WritePrimitive(dynamic obj, BinaryWriter bw)
        {
            bw.Write(obj);
        }
        private static void Write(Type type, object obj, BinaryWriter bw)
        {
            if (type.IsPrimitive && type != typeof(string))
            {
                WritePrimitive(obj, bw);
            }
            else
            {
                bw.Write(obj == null);
                if (obj != null)
                {
                    var basetype = type.BaseType;
                    if (basetype == typeof(Array))
                    {
                        bw.Write((obj as Array).Length);
                        foreach (var item in (obj as Array))
                        {
                            Write(item.GetType(), item, bw);
                        }
                        return;
                    }
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        Write(type.GetGenericArguments()[0], obj, bw);
                        return;
                    }
                    if (type == typeof(string))
                    {
                        bw.Write(obj as string);
                        return;
                    }
                    if (basetype != typeof(object)) Write(basetype, obj, bw);
                    FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    foreach (var fieldinfo in fields)
                    {
                        if (!fieldinfo.GetCustomAttributes<NonSerializedAttribute>().Any())
                            Write(fieldinfo.FieldType, fieldinfo.GetValue(obj), bw);
                    }
                }

            }
        }
        public static object Deserialize(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                var type = Type.GetType(br.ReadString());
                object obj = null;
                Read(br, type, ref obj);
                return obj;
            }
        }
        private static object ReadPrimitive(BinaryReader br, Type type)
        {
            if (type == typeof(Int32))
            {
                return br.ReadInt32();
            }
            else if (type == typeof(bool))
            {
                return br.ReadBoolean();
            }
            else if (type == typeof(string))
            {
                return br.ReadString();
            }
            else if (type == typeof(double))
            {
                return br.ReadDouble();
            }
            else if (type == typeof(char))
            {
                return br.ReadChar();
            }
            else if (type == typeof(byte))
            {
                return br.ReadByte();
            }
            throw new MySerializerException("sorry, not implement yet deserialization of type" + type.Name);
        }
        private static void Read(BinaryReader br, Type type, ref object obj)
        {
            if (type.IsPrimitive && type != typeof(string))
            {
                obj = ReadPrimitive(br, type);
                return;
            }
            else
            {
                if (br.ReadBoolean()) return;
                if (type == typeof(string))
                {
                    obj = br.ReadString();
                    return;
                }
                if (type.BaseType == typeof(Array))
                {
                    var count = br.ReadInt32();
                    var ellementtype = type.GetElementType();
                    var newobject = Array.CreateInstance(ellementtype, count);
                    for (int i = 0; i < count; i++)
                    {
                        object temp = null;
                        Read(br, ellementtype, ref temp);
                        newobject.SetValue(temp, i);
                    }
                    obj = newobject;
                    return;
                }
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    Read(br, type.GetGenericArguments()[0], ref  obj);
                    return;
                }
                if (obj == null)
                {
                    obj = FormatterServices.GetUninitializedObject(type);
                }
                if (type.BaseType != typeof(object)) Read(br, type.BaseType, ref obj);
                foreach (var fieldinfo in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!fieldinfo.GetCustomAttributes<NonSerializedAttribute>().Any())
                    {
                        object value = null;
                        Read(br, fieldinfo.FieldType, ref value);
                        fieldinfo.SetValue(obj, value);
                    }
                }
                return;
            }
        }
    }
}
