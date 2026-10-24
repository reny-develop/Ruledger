// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rulealize;
using Rulealize.Abstraction.Store;

namespace Ruledger.Verify
{
    /// <summary>A walk over a rule set that keeps instances, and whether stopping a step at what it reads loses anything.</summary>
    /// <remarks>
    /// <para>
    /// A month can always create another claim, so a walk that takes moves in the order they are
    /// offered goes on creating claims and reaches nothing else. A step reads only the month's
    /// document and the claim it is at — Rulealize refuses a guard that reads anything its
    /// transition does not write — so a step already followed from the same documents decides
    /// nothing new, and the trial walk does not follow it again.
    /// </para>
    /// <para>
    /// That settles what the step itself decides and not what follows it, so this holds the
    /// trial to a reference: every position of at most a few claims, walked breadth first to the
    /// end straight through the runtime and not through Ruledger, and every step taken in any of
    /// them — the month and the claim before, the input, the month and the claim after.
    /// </para>
    /// </remarks>
    public class Keeping(Xunit.Abstractions.ITestOutputHelper said)
    {
        private static Dictionary<string, string> Held(string claim) => new(StringComparer.Ordinal)
        {
            ["claim"] = Fixture.RuleSet(claim),
        };

        private static readonly Dictionary<string, string> Starts = new(StringComparer.Ordinal)
        {
            ["claim-start"] = Fixture.State("claim-start"),
        };

        // `claim-drawn` is the claim with who approves it left to chance, so a step there has a
        // branch per thing it can draw and each branch is a step of its own. Its reference stops
        // at three claims: at four it is 312,359 positions and nearly four minutes, and finds
        // the same 299 steps.
        [Theory]
        [InlineData(3, "claim", 112, 66)]
        [InlineData(4, "claim", 112, 66)]
        [InlineData(3, "claim-drawn", 299, 113)]
        public void StoppingAStepAtWhatItReadsLosesNoStepAFewClaimsTake(int Claims, string Claim, int Steps, int States)
        {
            Dictionary<string, string> held = Held(Claim);
            Stopwatch watch = Stopwatch.StartNew();
            (HashSet<string> steps, HashSet<string> offered, int positions) = Reference(Claims, held);
            TimeSpan reference = watch.Elapsed;

            watch.Restart();
            string month = Fixture.RuleSet("claim-month");
            TestDesign design = TestDesign.Derive(
                FixtureStore.Runtime(), month, held, new WalkSettings(States: 3000), ObservationScan.Of(month, held),
                null, null, Starts);
            TimeSpan trial = watch.Elapsed;

            (HashSet<string> walkedSteps, HashSet<string> walkedOffered) = Walked(design);
            string[] missing = [.. steps.Except(walkedSteps).Order(StringComparer.Ordinal)];
            string[] unoffered = [.. offered.Except(walkedOffered).Order(StringComparer.Ordinal)];

            said.WriteLine($"reference, at most {Claims} claims: {positions} positions, {steps.Count} steps, "
                + $"{offered.Count} moves offered, {reference.TotalSeconds:F1}s");
            said.WriteLine($"trial: {design.States.Count} states, at most {design.States.Max(state => state.Documents.Count) - 1} claims, "
                + $"{walkedSteps.Count} steps, {walkedOffered.Count} moves offered, unreached {design.Unreached}, {trial.TotalSeconds:F1}s");
            said.WriteLine($"reference steps the trial does not take: {missing.Length}; moves it is not offered: {unoffered.Length}");
            foreach (string step in missing.Take(10))
            {
                said.WriteLine("  missing " + step);
            }

            foreach (string move in unoffered.Take(10))
            {
                said.WriteLine("  unoffered " + move);
            }

            Assert.Empty(missing);
            Assert.Empty(unoffered);
            Assert.Equal(Steps, steps.Count);
            Assert.Equal(Steps, walkedSteps.Count);
            Assert.Equal(States, design.States.Count);
            Assert.Equal(0, design.Unreached);
        }

        // Every position of at most so many claims, breadth first, through the runtime alone.
        private static (HashSet<string> Steps, HashSet<string> Offered, int Positions) Reference(int claims, Dictionary<string, string> held)
        {
            MemoryStateStore store = new();
            RuleContext rules = FixtureStore.Runtime().UseStore(store).CreateContext(Fixture.RuleSet("claim-month"), held, Starts);

            HashSet<string> steps = new(StringComparer.Ordinal);
            HashSet<string> offered = new(StringComparer.Ordinal);
            HashSet<string> seen = new(StringComparer.Ordinal);
            Queue<IReadOnlyList<KeyValuePair<StateAddress, string>>> ahead = new();

            ahead.Enqueue([]);
            seen.Add(Position([], rules.InitialState));

            while (ahead.Count > 0)
            {
                IReadOnlyList<KeyValuePair<StateAddress, string>> here = ahead.Dequeue();
                string root = Root(here, rules.InitialState);
                int count = here.Count(static document => !document.Key.IsRoot);

                List<StateAddress> addresses = [StateAddress.Root, .. here.Where(static d => !d.Key.IsRoot).Select(static d => d.Key)];
                foreach (StateAddress address in addresses)
                {
                    store.Load(here);
                    foreach (ValidInput input in rules.GetValidInputsAsync(address, 10000).GetAwaiter().GetResult())
                    {
                        foreach ((string text, string document) in Filled(rules, input))
                        {
                            store.Load(here);
                            StoreOutcomeSet outcomes;
                            try
                            {
                                outcomes = rules.GetOutcomesAsync(document, 100).GetAwaiter().GetResult();
                            }
                            catch (IllegalInputException)
                            {
                                continue;
                            }

                            offered.Add(Offered(text, root, Instance(here, address)));
                            if (input.At.IsRoot && count >= claims)
                            {
                                continue;
                            }

                            // Each thing the input can draw is a step of its own.
                            foreach (StoreOutcome outcome in outcomes)
                            {
                                store.Load(here);
                                rules.ApplyAsync(document, outcome).GetAwaiter().GetResult();
                                IReadOnlyList<KeyValuePair<StateAddress, string>> there = store.Documents;

                                StateAddress written = input.At.IsRoot
                                    ? there.Last(document => !document.Key.IsRoot && !here.Any(was => was.Key.Equals(document.Key))).Key
                                    : address;
                                steps.Add(Taken(text, root, Instance(here, address), Root(there, rules.InitialState), Instance(there, written)));

                                if (seen.Add(Position(there, rules.InitialState)))
                                {
                                    ahead.Enqueue(there);
                                }
                            }
                        }
                    }
                }
            }

            return (steps, offered, seen.Count);
        }

        // An open parameter of this fixture is an amount from 0 to 3; anything else is settled.
        private static IEnumerable<(string Text, string Document)> Filled(RuleContext rules, ValidInput input)
        {
            if (input.IsComplete)
            {
                yield return (Write(input.Input, input.Arguments.ToDictionary(static a => a.Key, static a => a.Value)), input.ToInputDocument(rules.RuleSet));
                yield break;
            }

            for (int amount = 0; amount <= 3; amount++)
            {
                Dictionary<string, JsonNode?> values = input.Open.ToDictionary(static open => open.Name, _ => (JsonNode?)JsonValue.Create(amount));
                Dictionary<string, string> arguments = input.Arguments.ToDictionary(static a => a.Key, static a => a.Value);
                foreach (OpenParameter open in input.Open)
                {
                    arguments[open.Name] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                yield return (Write(input.Input, arguments), input.ToInputDocument(rules.RuleSet, values!));
            }
        }

        // The same steps, read off the trial's test design.
        private static (HashSet<string> Steps, HashSet<string> Offered) Walked(TestDesign design)
        {
            Dictionary<string, TestDesignState> named = design.States.ToDictionary(static state => state.Name, StringComparer.Ordinal);
            HashSet<string> steps = new(StringComparer.Ordinal);
            HashSet<string> offered = new(StringComparer.Ordinal);

            foreach (TestDesignState state in design.States)
            {
                List<KeyValuePair<StateAddress, string>> here = [.. state.Documents.Select(static d => KeyValuePair.Create(Address(d.Key), d.Value))];
                string root = Root(here, state.State);

                foreach (Move move in state.Moves)
                {
                    StateAddress address = StateAddress.Root;
                    foreach ((string alias, string? key) in move.At)
                    {
                        address = address.Child(alias, key!);
                    }

                    string text = Write(move.Input, move.Arguments);
                    offered.Add(Offered(text, root, Instance(here, address)));

                    foreach (Landing landing in move.Landings.Where(landing => landing.To is not null))
                    {
                        TestDesignState target = named[landing.To!];
                        List<KeyValuePair<StateAddress, string>> there = [.. target.Documents.Select(static d => KeyValuePair.Create(Address(d.Key), d.Value))];
                        StateAddress written = address.IsRoot
                            ? there.Last(document => !document.Key.IsRoot && !here.Any(was => was.Key.Equals(document.Key))).Key
                            : address;
                        steps.Add(Taken(text, root, Instance(here, address), Root(there, target.State), Instance(there, written)));
                    }
                }
            }

            return (steps, offered);
        }

        private static string Write(string input, IReadOnlyDictionary<string, string> arguments) =>
            input + "(" + string.Join(", ", arguments.OrderBy(static a => a.Key, StringComparer.Ordinal).Select(static a => $"{a.Key}: {a.Value}")) + ")";

        private static string Offered(string text, string root, string instance) => $"{text} at month {root}, claim {instance}";

        private static string Taken(string text, string root, string instance, string rootAfter, string instanceAfter) =>
            $"{text} at month {root}, claim {instance} -> month {rootAfter}, claim {instanceAfter}";

        private static string Root(IReadOnlyList<KeyValuePair<StateAddress, string>> documents, string initial) =>
            Data(documents.FirstOrDefault(static document => document.Key.IsRoot).Value ?? initial);

        private static string Instance(IReadOnlyList<KeyValuePair<StateAddress, string>> documents, StateAddress address) =>
            address.IsRoot ? "-" : Data(documents.First(document => document.Key.Equals(address)).Value);

        private static string Position(IReadOnlyList<KeyValuePair<StateAddress, string>> documents, string initial) =>
            Root(documents, initial) + string.Concat(documents.Where(static d => !d.Key.IsRoot).Select(static d => "|" + d.Key + "=" + Data(d.Value)));

        private static string Data(string state)
        {
            using JsonDocument document = JsonDocument.Parse(state);
            JsonElement data = document.RootElement.GetProperty("data");
            return string.Join(",", data.EnumerateObject().OrderBy(static p => p.Name, StringComparer.Ordinal).Select(static p => $"{p.Name}={p.Value.GetRawText()}"));
        }

        private static StateAddress Address(string written)
        {
            StateAddress address = StateAddress.Root;
            if (written == "/")
            {
                return address;
            }

            string[] segments = written.Split('/');
            for (int at = 0; at + 1 < segments.Length; at += 2)
            {
                address = address.Child(segments[at], segments[at + 1]);
            }

            return address;
        }
    }
}
