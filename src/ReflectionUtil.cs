using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace CalendarQuestsPins
{
    internal static class ReflectionUtil
    {
        internal const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        internal static object FindActiveUnityInstance(Type type)
        {
            if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type)) return null;
            UnityEngine.Object[] objects;
            try { objects = Resources.FindObjectsOfTypeAll(type); }
            catch { return null; }
            foreach (var item in objects)
            {
                var component = item as Component;
                if (component != null && component.gameObject.activeInHierarchy) return component;
                var go = item as GameObject;
                if (go != null && go.activeInHierarchy) return go;
            }
            return objects.Length > 0 ? objects[0] : null;
        }

        internal static bool TryRead(object owner, string name, out object value)
        {
            value = null;
            if (owner == null) return false;
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                try
                {
                    var field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                    if (field != null) { value = field.GetValue(owner); return true; }
                    var prop = type.GetProperty(name, AnyInstance | BindingFlags.DeclaredOnly);
                    if (prop != null && prop.GetIndexParameters().Length == 0) { value = prop.GetValue(owner, null); return true; }
                }
                catch { return false; }
            }
            return false;
        }

        internal static bool TryWrite(object owner, string name, object value)
        {
            if (owner == null) return false;
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                try
                {
                    var field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                    if (field != null) { field.SetValue(owner, value); return true; }
                    var prop = type.GetProperty(name, AnyInstance | BindingFlags.DeclaredOnly);
                    if (prop != null && prop.CanWrite && prop.GetIndexParameters().Length == 0) { prop.SetValue(owner, value, null); return true; }
                }
                catch { return false; }
            }
            return false;
        }

        internal static MethodInfo FindMethod(Type type, string name, int parameterCount, bool isStatic)
        {
            if (type == null) return null;
            var flags = isStatic ? AnyStatic : AnyInstance;
            foreach (var method in type.GetMethods(flags))
                if (method.Name == name && method.GetParameters().Length == parameterCount) return method;
            return null;
        }

        internal static IEnumerable EnumerateMember(object owner, string name)
        {
            object value;
            if (!TryRead(owner, name, out value)) return null;
            return value as IEnumerable;
        }

        internal static bool IsUnityAlive(object obj)
        {
            if (obj == null) return false;
            var unity = obj as UnityEngine.Object;
            if ((object)unity == null) return true;
            return unity != null;
        }

        internal static string ReadString(object owner, string name)
        {
            object value;
            return TryRead(owner, name, out value) ? value as string : null;
        }
    }
}
