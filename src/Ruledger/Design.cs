// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;
using Rulealize;
using Rulealize.Abstraction;

namespace Ruledger
{
    /// <summary>A test design: the states a walk visited, and what is observable at each.</summary>
    /// <remarks>
    /// <para>
    /// It is derived, and it is tautological. Applied to the rule set it came from it always
    /// passes, because it says what that rule set decides and never what it should decide.
    /// Reading it against what was meant is the part that is not here.
    /// </para>
    /// <para>
    /// The walk takes every legal input from each state and follows one route to the end
    /// before coming back, because the alternative never finishes a game: spreading out by
    /// depth collects openings and reaches no ending at all, and an ending is where a result
    /// is observed. Nothing is chosen at random and there is no seed — the same rule set
    /// walked with the same settings visits the same states in the same order, which is what
    /// makes a difference between two designs a difference the rules made.
    /// </para>
    /// </remarks>
    public sealed class Design
    {
        private Design(
            string ruleSet,
            WalkSettings settings,
            ObservationScan observation,
            IReadOnlyList<DesignState> states,
            int unreached)
        {
            RuleSet = ruleSet;
            Settings = settings;
            Observation = observation;
            States = states;
            Unreached = unreached;
        }

        /// <summary>Gets what the rule set calls itself, as <c>id@version</c>.</summary>
        public string RuleSet { get; }

        /// <summary>Gets how the walk was told to go, which is recorded because none of it is derived.</summary>
        public WalkSettings Settings { get; }

        /// <summary>Gets which state the walk compared positions on.</summary>
        public ObservationScan Observation { get; }

        /// <summary>Gets the states, in the order they were first arrived at.</summary>
        public IReadOnlyList<DesignState> States { get; }

        /// <summary>Gets how many landings the budget stopped before.</summary>
        /// <remarks>
        /// Not "how much is missing", which nobody can say. It is how many places the walk was
        /// standing in front of when it ran out, and it is reported rather than rounded off, so
        /// that what a budget did not reach is never read as what is not there.
        /// </remarks>
        public int Unreached { get; }

        /// <summary>Walks a rule set and writes down what is observable along the way.</summary>
        /// <param name="runtime">A runtime with the vocabularies the rule set draws on loaded.</param>
        /// <param name="ruleSet">The rule set, as text.</param>
        /// <param name="settings">How far to go. The default budget is three thousand states.</param>
        /// <returns>The design.</returns>
        /// <exception cref="RuleSetBuildException">The text is not a rule set this runtime can compile.</exception>
        /// <exception cref="NotSupportedException">The rule set holds other rule sets.</exception>
        public static Design Derive(RuleRuntime runtime, string ruleSet, WalkSettings? settings = null)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(ruleSet);

            return Derive(runtime, ruleSet, settings, ObservationScan.Of(ruleSet));
        }

        internal static Design Derive(
            RuleRuntime runtime,
            string ruleSet,
            WalkSettings? settings,
            ObservationScan observation)
        {
            Walker walker = new(runtime.CreateContext(ruleSet), observation, settings ?? new WalkSettings());
            return walker.Walk();
        }

        private sealed class Walker(RuleContext rules, ObservationScan observation, WalkSettings settings)
        {
            private readonly Dictionary<string, string> seen = new(StringComparer.Ordinal);
            private readonly List<Entry> order = [];
            private readonly Stack<Entry> descending = new();
            private int unreached;

            public Design Walk()
            {
                Arrive(rules.InitialState, null, null);

                // The recursion is written out because its depth is the depth of the walk, and
                // that is a budget away from unbounded: a rule set holding a history never
                // returns to a state it has been in, so one route can be as long as the budget.
                while (this.descending.Count > 0)
                {
                    Entry entry = this.descending.Peek();
                    if (entry.Next >= entry.Ahead.Count)
                    {
                        this.descending.Pop();
                        continue;
                    }

                    Step step = entry.Ahead[entry.Next++];
                    step.Landing.To = Arrive(step.State, entry.Name, step.By);
                }

                return new Design(
                    rules.RuleSet,
                    settings,
                    observation,
                    [.. this.order.Select(static entry => entry.Close())],
                    this.unreached);
            }

            // Written out with the keys of every object in order, because whether two states are
            // the same is not a question about the order a writer happened to use.
            private static void Canonical(JsonElement value, StringBuilder into)
            {
                switch (value.ValueKind)
                {
                    case JsonValueKind.Object:
                        into.Append('{');
                        foreach (JsonProperty property in value.EnumerateObject().OrderBy(static p => p.Name, StringComparer.Ordinal))
                        {
                            into.Append(property.Name).Append(':');
                            Canonical(property.Value, into);
                            into.Append(',');
                        }

                        into.Append('}');
                        return;

                    case JsonValueKind.Array:
                        into.Append('[');
                        foreach (JsonElement item in value.EnumerateArray())
                        {
                            Canonical(item, into);
                            into.Append(',');
                        }

                        into.Append(']');
                        return;

                    default:
                        into.Append(value.GetRawText());
                        return;
                }
            }

            private string? Arrive(string state, string? from, string? by)
            {
                string key = Key(state);
                if (this.seen.TryGetValue(key, out string? seenAs))
                {
                    return seenAs;
                }

                if (this.order.Count >= settings.Budget)
                {
                    this.unreached++;
                    return null;
                }

                // Numbered on arrival rather than on departure, so that a state's name says
                // where the walk found it and the first thing #0 leads to is #1.
                string name = "#" + this.order.Count;
                this.seen[key] = name;

                Entry entry = new(name, from, by, state);
                this.order.Add(entry);

                TerminalStatus terminal = rules.GetTerminalStatus(state);
                entry.IsTerminal = terminal.IsTerminal;
                entry.Result = terminal.Result;

                ValidInputSet legal = rules.GetValidInputs(state, settings.ValidationLimit);
                entry.Evaluated = legal.Evaluated;
                entry.Truncated = legal.Truncated;

                foreach (ValidInput input in legal)
                {
                    string document = input.ToInputDocument(rules.RuleSet);
                    List<Landing> landings = [];

                    // Past the end there is nothing to reach, so the moves a rule set still
                    // offers are written down and none of them is followed.
                    if (!terminal.IsTerminal)
                    {
                        foreach (Outcome outcome in rules.GetOutcomes(document, state, settings.OutcomeLimit))
                        {
                            Landing landing = new(
                                outcome.Probability,
                                outcome.Draws.IsEmpty ? null : outcome.ToOutcomeDocument(rules.RuleSet));

                            landings.Add(landing);
                            entry.Ahead.Add(new Step(landing, outcome.Result.State, input.ToString()));
                        }
                    }

                    entry.Moves.Add(new Move(input.Input, input.ToString(), input.Actor, document, landings));
                }

                this.descending.Push(entry);
                return name;
            }

            // Two states are the same state when they agree on everything an observation can
            // depend on. Anything else a rule set keeps — an audit trail, a move history — is
            // dropped here, or a walk through a rule set that keeps one would never return to a
            // position it had already been in.
            private string Key(string state)
            {
                using JsonDocument document = JsonDocument.Parse(state);
                if (!document.RootElement.TryGetProperty("data", out JsonElement data))
                {
                    return state;
                }

                StringBuilder key = new();
                foreach (string field in observation.Observed)
                {
                    key.Append(field).Append('=');
                    if (data.TryGetProperty(field, out JsonElement value))
                    {
                        Canonical(value, key);
                    }

                    key.Append(';');
                }

                return key.ToString();
            }

            private sealed record Step(Landing Landing, string State, string By);

            private sealed class Entry(string name, string? from, string? by, string state)
            {
                public string Name => name;

                public bool IsTerminal { get; set; }

                public string? Result { get; set; }

                public int Evaluated { get; set; }

                public bool Truncated { get; set; }

                public int Next { get; set; }

                public List<Move> Moves { get; } = [];

                public List<Step> Ahead { get; } = [];

                public DesignState Close() =>
                    new(name, from, by, state, IsTerminal, Result, Evaluated, Truncated, Moves);
            }
        }
    }
}
