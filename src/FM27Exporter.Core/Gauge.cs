using System.Collections.Generic;
using System.Linq;

namespace FM27Exporter.Core
{
    public static class Gauge
    {
        public const string StepperClass = "stepper";

        public static string? Value(IReadOnlyList<IReadOnlyCollection<string>> steps)
        {
            if (steps.Count == 0)
                return null;
            var selected = -1;
            for (var i = 0; i < steps.Count; i++)
                if (steps[i].Contains("stepper-step--selected") || steps[i].Contains("gauge-step--selected")
                    || steps[i].Contains("gauge-step-large--selected"))
                    selected = i;
            return selected < 0 ? null : $"{selected + 1}/{steps.Count}";
        }
    }
}
