// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Rulealize;

namespace Ruledger.Verify
{
    /// <summary>The material the measurements are about.</summary>
    /// <remarks>
    /// Nothing here reaches outside this repository. Rule-Derived Test Design is published,
    /// and somebody with one clone has to be able to get these numbers; a measurement that
    /// read a sibling repository could only be run by whoever had the whole workspace.
    /// </remarks>
    internal static class Fixture
    {
        private static readonly Lazy<RuleRuntime> Loaded =
            new(static () => new RuleRuntime().LoadPluginsFrom(AppContext.BaseDirectory));

        private static readonly JsonDocumentOptions Reading = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>Gets the runtime, with the vocabularies that landed beside this assembly.</summary>
        public static RuleRuntime Runtime => Loaded.Value;

        /// <summary>Reads a rule set out of <c>verify/ruleset</c>.</summary>
        public static string RuleSet(string ruleSet) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ruleset", ruleSet + ".json"));

        /// <summary>Reads the choices a person made, out of <c>verify/choice</c>.</summary>
        /// <param name="ruleSet">Which rule set they were made on.</param>
        /// <returns>The choices, in the order they were made.</returns>
        /// <remarks>
        /// Fixed and committed rather than worked out on every run. A measurement is a claim
        /// about a fixture, and choices a procedure regenerates each time would make the claim
        /// a report about whatever that procedure produced that day. How they were arrived at
        /// is written in the file.
        /// </remarks>
        public static IReadOnlyList<TestDesignEdit> Choices(string ruleSet)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "choice", ruleSet + ".json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path), Reading);

            return
            [
                .. document.RootElement.GetProperty("choices").EnumerateArray().Select(static choice =>
                    new TestDesignEdit(
                        choice.GetProperty("state").GetString() ?? string.Empty,
                        choice.GetProperty("input").GetString() ?? string.Empty,
                        Arguments(choice))),
            ];
        }

        /// <summary>Walks a rule set, with the choices a person made where there are any.</summary>
        public static TestDesign Walk(string ruleSet, int states, IReadOnlyList<TestDesignEdit>? choices = null) =>
            TestDesign.Derive(Runtime, RuleSet(ruleSet), null, new WalkSettings(States: states), choices);

        /// <summary>Applies a test design to a version of the rule set, which is to say diffs the two.</summary>
        public static TestDesignDiff Apply(TestDesign design, string ruleSet) =>
            TestDesignDiff.Of(Runtime, design.ToJson(), RuleSet(ruleSet));

        /// <summary>Counts the landings that arrived somewhere the walk had already been.</summary>
        /// <remarks>
        /// Every landing that leads somewhere, less the states themselves: a walk of n states
        /// arrives at n - 1 of them for the first time, and everything beyond that is a route
        /// coming back to a position it has been in.
        /// </remarks>
        public static int Rejoins(TestDesign design) =>
            design.States.Sum(state => state.Moves.Sum(move => move.Landings.Count(landing => landing.To is not null)))
                - (design.States.Count - 1);

        /// <summary>The results the rules gave, in order, without repeats.</summary>
        public static IReadOnlyList<string> Results(TestDesign design) =>
        [
            .. design.States
                .Where(state => state.IsTerminal)
                .Select(state => state.Result ?? "(no result)")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        private static IReadOnlyDictionary<string, string> Arguments(JsonElement choice)
        {
            Dictionary<string, string> arguments = new(StringComparer.Ordinal);

            if (choice.TryGetProperty("args", out JsonElement args))
            {
                foreach (JsonProperty argument in args.EnumerateObject())
                {
                    arguments[argument.Name] = argument.Value.GetString() ?? string.Empty;
                }
            }

            return arguments;
        }
    }
}
