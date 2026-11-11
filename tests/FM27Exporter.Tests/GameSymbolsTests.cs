using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Xunit;

namespace FM27Exporter.Tests
{
    public class GameSymbolsTests
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        private static readonly string Dummy = Path.Combine(Root, "vendor", "dummy");

        private static bool Required => Environment.GetEnvironmentVariable("FM27_REQUIRE_GAME") == "1";

        private static string? GameDir()
        {
            var env = Environment.GetEnvironmentVariable("FM27_GAME_DIR");
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(env)) candidates.Add(env);
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Football Manager 27"));
            candidates.Add(Path.Combine(home, ".local/share/Steam/steamapps/common/Football Manager 27"));
            candidates.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Football Manager 27");
            return candidates.FirstOrDefault(d => File.Exists(Path.Combine(d, "GameAssembly.dll")));
        }

        private static bool Available(bool ok, string what)
        {
            if (!ok && Required)
                Assert.Fail(what + " not found");
            return ok;
        }

        private sealed class TypeInfo
        {
            public string FullName = "";
            public string? BaseFullName;
            public HashSet<string> Methods = new HashSet<string>();
        }

        private static Dictionary<string, TypeInfo> ReadTypes(string dll)
        {
            using var pe = new PEReader(File.OpenRead(dll));
            var md = pe.GetMetadataReader();
            var result = new Dictionary<string, TypeInfo>();
            foreach (var h in md.TypeDefinitions)
            {
                var t = md.GetTypeDefinition(h);
                var info = new TypeInfo { FullName = Name(md, t.Namespace, t.Name) };
                if (!t.BaseType.IsNil)
                    info.BaseFullName = BaseName(md, t.BaseType);
                foreach (var mh in t.GetMethods())
                    info.Methods.Add(md.GetString(md.GetMethodDefinition(mh).Name));
                result[info.FullName] = info;
            }
            return result;
        }

        private static string Name(MetadataReader md, StringHandle ns, StringHandle name)
        {
            var n = md.GetString(ns);
            return n.Length > 0 ? n + "." + md.GetString(name) : md.GetString(name);
        }

        private static string? BaseName(MetadataReader md, EntityHandle h) => h.Kind switch
        {
            HandleKind.TypeDefinition => Name(md, md.GetTypeDefinition((TypeDefinitionHandle)h).Namespace, md.GetTypeDefinition((TypeDefinitionHandle)h).Name),
            HandleKind.TypeReference => Name(md, md.GetTypeReference((TypeReferenceHandle)h).Namespace, md.GetTypeReference((TypeReferenceHandle)h).Name),
            _ => null,
        };

        [Fact]
        public void StreamedTable_and_the_getters_the_plugin_invokes_exist_in_the_game()
        {
            var dll = Path.Combine(Dummy, "SI.Bindable.dll");
            if (!Available(File.Exists(dll), dll))
                return;
            var types = ReadTypes(dll);
            Assert.True(types.ContainsKey("SI.Bindable.StreamedTable"), "SI.Bindable.StreamedTable");
            var table = types["SI.Bindable.StreamedTable"];
            Assert.Equal("SI.Bindable.StreamedListView", table.BaseFullName);
            var list = types["SI.Bindable.StreamedListView"];
            Assert.Contains("get_ItemCount", list.Methods);          // TableView.ItemCount
            Assert.Contains("get_ScrollViewToScroll", list.Methods); // TableView.RowScrollView
            Assert.Contains("get_IsPaginated", list.Methods);        // TableView.IsPaginated
            Assert.Contains("get_ListElement", list.Methods);        // TableView.SelectedIndices
        }

        [Fact]
        public void The_selection_the_plugin_reads_exists_in_the_game()
        {
            var dll = Path.Combine(Dummy, "SI.UI.dll");
            if (!Available(File.Exists(dll), dll))
                return;
            var types = ReadTypes(dll);
            Assert.Contains("get_Selection", types["SI.UI.VirtualisedList"].Methods);
            var selection = types.Keys.Single(k => k.StartsWith("SI.UI.Selection`1", StringComparison.Ordinal));
            Assert.Contains("get_Count", types[selection].Methods);
            Assert.Contains("get_Item", types[selection].Methods);
        }

        [Fact]
        public void Unity_UI_Toolkit_types_used_by_the_plugin_exist_in_the_game()
        {
            var dll = Path.Combine(Dummy, "UnityEngine.UIElementsModule.dll");
            if (!Available(File.Exists(dll), dll))
                return;
            var types = ReadTypes(dll);
            Assert.Contains("GetSortedPlayerPanels", types["UnityEngine.UIElements.UIElementsRuntimeUtility"].Methods);
            Assert.Contains("get_scrollOffset", types["UnityEngine.UIElements.ScrollView"].Methods);
            Assert.Contains("set_scrollOffset", types["UnityEngine.UIElements.ScrollView"].Methods);
            Assert.Contains("get_text", types["UnityEngine.UIElements.TextElement"].Methods);
            Assert.Contains("GetClassesForIteration", types["UnityEngine.UIElements.VisualElement"].Methods);
        }

        [Theory]
        [InlineData("column-headers")]
        [InlineData("streamed-table__row")]
        [InlineData("streamed-table__cell")]
        [InlineData("streamed-table__column")]
        [InlineData("StreamedTable")]
        [InlineData("virtualised-list__item--selected")] // VirtualisedList.UpdateSelectionStyling
        public void Element_and_class_names_are_string_literals_of_the_game(string literal)
        {
            var game = GameDir();
            if (!Available(game != null, "FM27 install"))
                return;
            var metadata = File.ReadAllBytes(Path.Combine(game!, "fm_Data", "il2cpp_data", "Metadata", "global-metadata.dat"));
            Assert.True(IndexOf(metadata, Encoding.UTF8.GetBytes(literal)) >= 0, literal);
        }

        [Fact]
        public void Negative_control_unknown_names_are_not_found()
        {
            var game = GameDir();
            if (Available(game != null, "FM27 install"))
            {
                var metadata = File.ReadAllBytes(Path.Combine(game!, "fm_Data", "il2cpp_data", "Metadata", "global-metadata.dat"));
                Assert.True(metadata.Length > 1_000_000);
                Assert.Equal(-1, IndexOf(metadata, Encoding.UTF8.GetBytes("streamed-table__rowz-not-real-9f3")));
            }
            var dll = Path.Combine(Dummy, "SI.Bindable.dll");
            if (Available(File.Exists(dll), dll))
                Assert.DoesNotContain("get_ItemCountX", ReadTypes(dll)["SI.Bindable.StreamedListView"].Methods);
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                if (haystack[i] != needle[0]) continue;
                var j = 1;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
