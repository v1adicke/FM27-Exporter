using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine.UIElements;

namespace FM27Exporter
{
    internal static class Ui
    {
        public static List<VisualElement> PanelRoots()
        {
            var roots = new List<VisualElement>();
            var seen = new HashSet<IntPtr>();
            try
            {
                var panels = UIElementsRuntimeUtility.GetSortedPlayerPanels();
                for (var i = 0; panels != null && i < panels.Count; i++)
                {
                    var root = panels[i]?.visualTree;
                    if (root != null && seen.Add(root.Pointer))
                        roots.Add(root);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"UIElementsRuntimeUtility.GetSortedPlayerPanels failed: {ex.Message}");
            }
            if (roots.Count == 0)
            {
                foreach (var obj in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<UIDocument>()))
                {
                    var root = obj.TryCast<UIDocument>()?.rootVisualElement;
                    if (root != null && seen.Add(root.Pointer))
                        roots.Add(root);
                }
            }
            return roots;
        }

        private static readonly Dictionary<IntPtr, string> ClassNames = new Dictionary<IntPtr, string>();

        public static string TypeName(Il2CppObjectBase obj)
        {
            var klass = IL2CPP.il2cpp_object_get_class(obj.Pointer);
            if (ClassNames.TryGetValue(klass, out var name))
                return name;
            var ns = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_namespace(klass)) ?? string.Empty;
            var n = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass)) ?? "?";
            name = ns.Length > 0 ? ns + "." + n : n;
            ClassNames[klass] = name;
            return name;
        }

        public static bool IsA(Il2CppObjectBase obj, string fullName)
        {
            for (var klass = IL2CPP.il2cpp_object_get_class(obj.Pointer); klass != IntPtr.Zero; klass = IL2CPP.il2cpp_class_get_parent(klass))
            {
                var ns = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_namespace(klass)) ?? string.Empty;
                var n = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass)) ?? string.Empty;
                if ((ns.Length > 0 ? ns + "." + n : n) == fullName)
                    return true;
            }
            return false;
        }

        public static unsafe IntPtr Invoke(Il2CppObjectBase obj, string method)
        {
            for (var klass = IL2CPP.il2cpp_object_get_class(obj.Pointer); klass != IntPtr.Zero; klass = IL2CPP.il2cpp_class_get_parent(klass))
            {
                var m = IL2CPP.il2cpp_class_get_method_from_name(klass, method, 0);
                if (m == IntPtr.Zero)
                    continue;
                var exc = IntPtr.Zero;
                var result = IL2CPP.il2cpp_runtime_invoke(m, obj.Pointer, (void**)IntPtr.Zero, ref exc);
                if (exc != IntPtr.Zero)
                    throw new InvalidOperationException($"{method} threw inside the game");
                return result;
            }
            return IntPtr.Zero;
        }

        public static unsafe IntPtr InvokePtr(IntPtr obj, string method, int? arg = null)
        {
            if (obj == IntPtr.Zero)
                return IntPtr.Zero;
            var argc = arg.HasValue ? 1 : 0;
            for (var klass = IL2CPP.il2cpp_object_get_class(obj); klass != IntPtr.Zero; klass = IL2CPP.il2cpp_class_get_parent(klass))
            {
                var m = IL2CPP.il2cpp_class_get_method_from_name(klass, method, argc);
                if (m == IntPtr.Zero)
                    continue;
                var exc = IntPtr.Zero;
                IntPtr result;
                if (arg.HasValue)
                {
                    var value = arg.Value;
                    var args = stackalloc void*[1];
                    args[0] = &value;
                    result = IL2CPP.il2cpp_runtime_invoke(m, obj, args, ref exc);
                }
                else
                    result = IL2CPP.il2cpp_runtime_invoke(m, obj, (void**)IntPtr.Zero, ref exc);
                if (exc != IntPtr.Zero)
                    throw new InvalidOperationException($"{method} threw inside the game");
                return result;
            }
            return IntPtr.Zero;
        }

        public static unsafe int? UnboxInt(IntPtr boxed) => boxed == IntPtr.Zero ? null : *(int*)IL2CPP.il2cpp_object_unbox(boxed);

        public static unsafe int? InvokeInt(Il2CppObjectBase obj, string method)
        {
            var boxed = Invoke(obj, method);
            if (boxed == IntPtr.Zero)
                return null;
            return *(int*)IL2CPP.il2cpp_object_unbox(boxed);
        }

        public static unsafe bool? InvokeBool(Il2CppObjectBase obj, string method)
        {
            var boxed = Invoke(obj, method);
            if (boxed == IntPtr.Zero)
                return null;
            return *(byte*)IL2CPP.il2cpp_object_unbox(boxed) != 0;
        }

        public static bool IsDescendant(VisualElement e, VisualElement ancestor)
        {
            for (var x = e; x != null; x = x.parent)
                if (x.Pointer == ancestor.Pointer)
                    return true;
            return false;
        }

        public static bool IsHidden(VisualElement e)
        {
            try
            {
                return !e.visible || e.resolvedStyle.display == DisplayStyle.None;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsHiddenUpTo(VisualElement e, VisualElement? stop)
        {
            for (var x = e; x != null && (stop == null || x.Pointer != stop.Pointer); x = x.parent)
                if (IsHidden(x))
                    return true;
            return false;
        }

        public static List<VisualElement> FindAll(VisualElement root, Func<VisualElement, bool> match, Func<VisualElement, bool>? prune = null)
        {
            var result = new List<VisualElement>();
            var stack = new Stack<VisualElement>();
            PushChildren(stack, root);
            while (stack.Count > 0)
            {
                var e = stack.Pop();
                if (match(e))
                {
                    result.Add(e);
                    continue;
                }
                if (prune != null && prune(e))
                    continue;
                PushChildren(stack, e);
            }
            return result;
        }

        public static VisualElement? FindFirst(VisualElement root, Func<VisualElement, bool> match)
        {
            var stack = new Stack<VisualElement>();
            PushChildren(stack, root);
            while (stack.Count > 0)
            {
                var e = stack.Pop();
                if (match(e))
                    return e;
                PushChildren(stack, e);
            }
            return null;
        }

        private static void PushChildren(Stack<VisualElement> stack, VisualElement e)
        {
            for (var i = e.childCount - 1; i >= 0; i--)
                stack.Push(e[i]);
        }

        public static List<string> Classes(VisualElement e)
        {
            var list = new List<string>();
            try
            {
                var classes = e.GetClassesForIteration();
                for (var i = 0; classes != null && i < classes.Count; i++)
                    list.Add(classes[i]);
            }
            catch
            {
            }
            return list;
        }

        private static readonly Dictionary<IntPtr, bool> TextClasses = new Dictionary<IntPtr, bool>();

        public static TextElement? AsText(VisualElement e)
        {
            var klass = IL2CPP.il2cpp_object_get_class(e.Pointer);
            if (!TextClasses.TryGetValue(klass, out var isText))
            {
                isText = e.TryCast<TextElement>() != null;
                TextClasses[klass] = isText;
            }
            return isText ? new TextElement(e.Pointer) : null;
        }

        public static List<string> Texts(VisualElement root, VisualElement? visibilityStop = null)
        {
            var texts = new List<string>();
            Walk(root, e =>
            {
                if (IsHidden(e))
                    return false;
                var t = AsText(e)?.text;
                if (!string.IsNullOrEmpty(t))
                    texts.Add(t!);
                return true;
            });
            return texts;
        }

        public static void Walk(VisualElement e, Func<VisualElement, bool> enter)
        {
            if (!enter(e))
                return;
            for (var i = 0; i < e.childCount; i++)
                Walk(e[i], enter);
        }

        public static float Area(VisualElement e)
        {
            var b = e.worldBound;
            return float.IsNaN(b.width) || float.IsNaN(b.height) ? 0f : b.width * b.height;
        }
    }
}
