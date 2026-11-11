using System;
using System.IO;
using System.Linq;
using FM27Exporter.Core;
using Xunit;

namespace FM27Exporter.Tests
{
    public class CellTextTests
    {
        [Theory]
        [InlineData("<color=#FF0000>15</color>", "15")]
        [InlineData("<b>Max</b>  <i>Aarons</i>", "Max Aarons")]
        [InlineData("<link=\"person:123\"><u>Max Aarons</u></link>", "Max Aarons")]
        [InlineData("<#ffcc00>Wnt</color>", "Wnt")]
        [InlineData("<sprite name=\"flag\"> ENG", "ENG")]
        [InlineData("<size=80%>£50M</size>", "£50M")]
        [InlineData("<1", "<1")]
        [InlineData("< 18", "< 18")]
        [InlineData("a\r\n b\tc", "a b c")]
        [InlineData("1 227", "1 227")]
        [InlineData("Ex­tra​", "Extra")]
        [InlineData(null, "")]
        public void Clean(string? input, string expected) => Assert.Equal(expected, CellText.Clean(input));

        [Fact]
        public void Join_skips_empty_parts() =>
            Assert.Equal("D (C) DM", CellText.Join(new[] { " D (C)", "", null, "<b>DM</b>" }));
    }

    public class StarRatingTests
    {
        [Theory]
        [InlineData("ability-max-potential-level-fullfilled-youth-false", 1.0)]
        [InlineData("ability-half-potential-level-half-youth-true", 0.5)]
        [InlineData("ability-half-potential-level-none-youth-false", 0.5)]
        [InlineData("ability-minimum-potential-level-full-youth-false", 1.0)]
        [InlineData("ability-minimum-potential-level-half-youth-true", 0.5)]
        [InlineData("ability-minimum-potential-level-none-youth-false", 0.0)]
        public void Star_value_from_game_state_classes(string state, double expected) =>
            Assert.Equal(expected, StarRating.StarValue(new[] { StarRating.StarClass, state }));

        [Fact]
        public void Unknown_state_is_null() =>
            Assert.Null(StarRating.StarValue(new[] { StarRating.StarClass, "something-else" }));

        [Fact]
        public void Star_detection_and_format()
        {
            Assert.True(StarRating.IsStar(new[] { "x", StarRating.StarClassNoMargin }));
            Assert.False(StarRating.IsStar(new[] { "fm-star-rating-container" }));
            Assert.Equal("3.5", StarRating.Format(3.5));
            Assert.Equal("0.0", StarRating.Format(0));
        }
    }

    public class RowCollectorTests
    {
        [Fact]
        public void Merges_overlapping_scroll_steps_in_order()
        {
            var c = new RowCollector(2);
            Assert.True(c.Add(RowCollector.IndexOf(0, 24), new[] { "A", "1" }));
            c.Add(RowCollector.IndexOf(24, 24), new[] { "B", "2" });
            c.Add(RowCollector.IndexOf(48.4f, 24), new[] { "", "" });
            Assert.False(c.Add(RowCollector.IndexOf(47.6f, 24), new[] { "C", "3" }));
            c.Add(RowCollector.IndexOf(72, 24), new[] { "D", "4" });
            c.Add(2, new[] { "", "" });
            Assert.Equal(4, c.Count);
            Assert.Empty(c.MissingIndices(4));
            var rows = c.Rows();
            Assert.Equal(new[] { "A", "B", "C", "D" }, new[] { rows[0][0], rows[1][0], rows[2][0], rows[3][0] });
        }

        [Fact]
        public void Reports_gaps_and_pads_short_rows()
        {
            var c = new RowCollector(3);
            c.Add(0, new[] { "A" });
            c.Add(3, new[] { "D", "x", "y", "extra" });
            Assert.Equal(new[] { 1, 2 }, c.MissingIndices());
            Assert.Equal(new[] { 1, 2, 4 }, c.MissingIndices(5));
            Assert.Equal(new[] { "A", "", "" }, c.Rows()[0]);
            Assert.Equal(new[] { "D", "x", "y" }, c.Rows()[1]);
            Assert.False(c.Add(-1, new[] { "neg" }));
        }
    }

    public class StarRangeTests
    {
        private static string Value(string[] states)
        {
            double low = 0, high = 0;
            foreach (var st in states)
            {
                var classes = new[] { StarRating.StarClass, $"ability-{st}-youth-false" };
                low += StarRating.StarValue(classes, false) ?? 0;
                high += StarRating.StarValue(classes, true) ?? 0;
            }
            return StarRating.Format(low, high);
        }

        [Theory]
        [InlineData(new[] { "max-potential-level-fullfilled", "max-potential-level-fullfilled", "max-potential-level-fullfilled",
            "minimum-potential-level-full", "minimum-potential-level-none" }, "3.0-4.0")]
        [InlineData(new[] { "max-potential-level-fullfilled", "max-potential-level-fullfilled", "half-potential-level-half",
            "minimum-potential-level-half", "minimum-potential-level-none" }, "2.5-3.0")]
        [InlineData(new[] { "max-potential-level-fullfilled", "max-potential-level-fullfilled", "max-potential-level-fullfilled",
            "half-potential-level-none", "minimum-potential-level-none" }, "3.5")]
        public void Stars_with_a_white_part_are_a_range(string[] states, string expected) => Assert.Equal(expected, Value(states));
    }

    public class ExportNamesTests
    {
        [Theory]
        [InlineData(new[] { "playertable", "SquadScreen", "Root" }, "Squad")]
        [InlineData(new[] { "playertableshortlist", "ShortlistsPanel" }, "Shortlists")]
        [InlineData(new[] { null, "", "PlayerSearchScreen" }, "Player Search")]
        [InlineData(new[] { "unity-content", "ScoutingCentre" }, "Scouting Centre")]
        [InlineData(new string[0], "Table")]
        [InlineData(new[] { "playertable", "framebox", "frame-1000002298", "FirstTeamOverview", "GridLayoutElementContent",
            "ScalableBindableGridLayoutElement", "Content", "Overview", "SquadScreen", "MenuBody", "MainUIDocument-container" }, "Squad")]
        [InlineData(new[] { "table", "VisualElement-ClubTeam", "BindableSwitchElement-IsClub", "main-container", "main-block-container",
            "BindableSwitchElement_ListView", "ListViewFrame", "RecruitmentContentBlock", "GridLayoutElementContent", "FM", "Content",
            "Overview", "Card-frame", "RecruitmentScreen", "MenuBody" }, "Recruitment")]
        [InlineData(new[] { "playertable", "BindableSwitchElement-ClubTeam-IsShortlist", "BindableSwitchElement-IsClub", "PlayerTable",
            "search-table-remapper", "tables", "main-container", "block-variables", "block-expect", "PlayerSearchMainContentBlock",
            "GridLayoutElementContent", "MultiGridLayout", "GridSafeArea", "Card-frame", "BindingVariables", "SpeakToVariables" }, "Player Search")]
        public void Guess_screen(string?[] names, string expected) => Assert.Equal(expected, ExportNames.GuessScreen(names));

        [Fact]
        public void Base_name_is_safe_and_sortable()
        {
            var when = new DateTime(2026, 11, 11, 21, 5, 33);
            Assert.Equal("Squad 2026-11-11 21-05-33", ExportNames.BaseName("Squad", when));
            Assert.Equal("a_b_c", ExportNames.BaseName("a/b:c", when, timestamp: false));
            Assert.Equal("Table", ExportNames.Sanitize("..."));
        }

        [Fact]
        public void Unique_avoids_overwriting()
        {
            var dir = Directory.CreateTempSubdirectory().FullName;
            try
            {
                File.WriteAllText(Path.Combine(dir, "Squad.csv"), "");
                Assert.Equal("Squad (2)", ExportNames.Unique(dir, "Squad", new[] { ".html", ".csv" }));
                Assert.Equal("Other", ExportNames.Unique(dir, "Other", new[] { ".html", ".csv" }));
            }
            finally { Directory.Delete(dir, true); }
        }
    }

    public class HotkeyTests
    {
        [Theory]
        [InlineData("F9", 0x78, false, false, false)]
        [InlineData("Ctrl+P", 'P', true, false, false)]
        [InlineData("ctrl + shift + p", 'P', true, true, false)]
        [InlineData("Alt+F12", 0x7B, false, false, true)]
        [InlineData("Ctrl+Pause", 0x13, true, false, false)]
        public void Parse(string text, int vk, bool ctrl, bool shift, bool alt)
        {
            Assert.True(Hotkey.TryParse(text, out var h));
            Assert.Equal(vk, h.VirtualKey);
            Assert.Equal((ctrl, shift, alt), (h.Ctrl, h.Shift, h.Alt));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Ctrl")]
        [InlineData("P+Q")]
        [InlineData("F25")]
        [InlineData("Hyper+X")]
        [InlineData("PrintScreen")] // not reported reliably by GetAsyncKeyState
        public void Rejects(string text) => Assert.False(Hotkey.TryParse(text, out _));

        [Fact]
        public void Round_trips_to_string()
        {
            Hotkey.TryParse("shift+ctrl+e", out var h);
            Assert.Equal("Ctrl+Shift+E", h.ToString());
        }
    }
}
