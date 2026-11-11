using FM27Exporter.Core;
using Xunit;

namespace FM27Exporter.Tests
{
    public class IconLevelTests
    {
        [Theory]
        [InlineData("scaling-icons-condition-level-5-size-small-16px", "5")]
        [InlineData("scaling-icons-condition-level-1-size-small-16px", "1")]
        [InlineData("scaling-icons-sharpness-sharpness-5-size-small-16px", "5")]
        [InlineData("scaling-icons-form-morale-9-size-small-16px", "9")]
        [InlineData("scaling-icons-form-morale-20-size-small-16px", "20")]
        [InlineData("scaling-icons-condition-level-injured-size-small-16px", "Injured")]
        [InlineData("scaling-icons-condition-level-injured---white-size-small-16px", "Injured")]
        [InlineData("scaling-icons-condition-level-slightly-injured-size-small-16px", "Slightly Injured")]
        [InlineData("scaling-icons-condition-level-recurring-size-small-16px", "Recurring")]
        public void Reads_level_from_icon_class(string cls, string expected)
        {
            Assert.Equal(expected, IconLevel.Value(new[] { "unity-image", "game-concepts-size-small", cls }));
        }

        [Theory]
        [InlineData("scaling-icons-morale-happiness-neutral-size-small-16px-filled-false")]
        [InlineData("scaling-icons-sharpness-sharpness-large-size-small-16px")]
        [InlineData("ui-concepts-object-preview_small")]
        [InlineData("ui-concepts-chevron-down_small")]
        [InlineData("game-concepts-size-small")]
        public void Ignores_icons_without_a_level(string cls)
        {
            Assert.Null(IconLevel.Value(new[] { "unity-image", cls }));
        }
    }
}

namespace FM27Exporter.Tests
{
    public class GaugeTests
    {
        private static string[][] Steps(int selected, int count = 11)
        {
            var steps = new string[count][];
            for (var i = 0; i < count; i++)
            {
                var c = new System.Collections.Generic.List<string> { "gauge-step" };
                if (i == 0) c.Add("stepper-step--first");
                if (i == count - 1) c.Add("stepper-step--last");
                if (i == count / 2) c.Add("gauge-step--middle");
                if (i == selected) c.Add("stepper-step--selected");
                steps[i] = c.ToArray();
            }
            return steps;
        }

        [Theory]
        [InlineData(0, "1/11")]
        [InlineData(5, "6/11")]
        [InlineData(10, "11/11")]
        public void Reads_selected_step(int selected, string expected) => Assert.Equal(expected, FM27Exporter.Core.Gauge.Value(Steps(selected)));

        [Fact]
        public void No_selected_step_is_no_value() => Assert.Null(FM27Exporter.Core.Gauge.Value(Steps(-1)));
    }
}
