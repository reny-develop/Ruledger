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
    public sealed class TestDesign
    {
        internal TestDesign(
            string ruleSet,
            WalkSettings settings,
            IReadOnlyList<string> observed,
            IReadOnlyList<string> collapsed,
            IReadOnlyList<TestDesignState> states,
            IReadOnlyList<EditResult> edits,
            int unreached)
        {
            RuleSet = ruleSet;
            Settings = settings;
            Observed = observed;
            Collapsed = collapsed;
            States = states;
            Edits = edits;
            Unreached = unreached;
        }

        /// <summary>Gets what the rule set calls itself, as <c>id@version</c>.</summary>
        public string RuleSet { get; }

        /// <summary>Gets how the walk was told to go, which is recorded because none of it is derived.</summary>
        public WalkSettings Settings { get; }

        /// <summary>Gets the state a position was compared on, in the order the schema declares it.</summary>
        public IReadOnlyList<string> Observed { get; }

        /// <summary>Gets the state that was dropped before comparing, because nothing observable reads it.</summary>
        public IReadOnlyList<string> Collapsed { get; }

        /// <summary>Gets the states, in the order they were first arrived at.</summary>
        public IReadOnlyList<TestDesignState> States { get; }

        /// <summary>Gets the choices a person made, and what became of each.</summary>
        /// <remarks>
        /// Every one of them, carried or not. A choice that could not be taken is reported and
        /// never quietly dropped back to what the machine would have picked.
        /// </remarks>
        public IReadOnlyList<EditResult> Edits { get; }

        /// <summary>Gets how many landings there were no states left to visit.</summary>
        /// <remarks>
        /// Not "how much is missing", which nobody can say. It is how many places the walk was
        /// standing in front of when it ran out, and it is reported rather than rounded off, so
        /// that where a walk stopped for want of states is never read as what is not there.
        /// </remarks>
        public int Unreached { get; }

        /// <summary>Walks a rule set and writes down what is observable along the way.</summary>
        /// <param name="runtime">A runtime with the vocabularies the rule set draws on loaded.</param>
        /// <param name="ruleSet">The rule set, as text.</param>
        /// <param name="settings">How far to go. The default is three thousand states.</param>
        /// <returns>The test design.</returns>
        /// <exception cref="RuleSetBuildException">The text is not a rule set this runtime can compile.</exception>
        public static TestDesign Derive(RuleRuntime runtime, string ruleSet, WalkSettings? settings = null) =>
            Derive(runtime, ruleSet, null, settings);

        /// <summary>Walks a rule set that holds others, and writes down what is observable along the way.</summary>
        /// <param name="runtime">A runtime with the vocabularies the rule set draws on loaded.</param>
        /// <param name="ruleSet">The rule set, as text.</param>
        /// <param name="components">The document of every rule set reachable through <c>uses</c>, by identifier.</param>
        /// <param name="settings">How far to go. The default is three thousand states.</param>
        /// <param name="edits">Choices a person made, which the walk takes first where it can.</param>
        /// <returns>The test design.</returns>
        /// <remarks>
        /// Splitting a rule set into a composite and the parts it holds is a way of writing it,
        /// not a different set of rules, so the test design does not turn on which way it was
        /// written: the composite is walked, exactly as the one document it stands for would
        /// be. Only the names differ, because a composite offers a component's input under the
        /// alias it gave it.
        /// </remarks>
        /// <exception cref="RuleSetBuildException">The text is not a rule set this runtime can compile.</exception>
        /// <exception cref="InvalidOperationException">A rule set named in <c>uses</c> was not supplied.</exception>
        public static TestDesign Derive(
            RuleRuntime runtime,
            string ruleSet,
            IReadOnlyDictionary<string, string>? components,
            WalkSettings? settings = null,
            IReadOnlyList<TestDesignEdit>? edits = null)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(ruleSet);

            return Derive(runtime, ruleSet, components, settings, ObservationScan.Of(ruleSet, components), edits);
        }

        /// <summary>Reads a test design back.</summary>
        /// <param name="testDesign">A <c>ruledger/test-design/v1</c> document.</param>
        /// <returns>What it says.</returns>
        /// <exception cref="JsonException">The text is not JSON.</exception>
        /// <exception cref="InvalidOperationException">The text is not a test design.</exception>
        public static TestDesign FromJson(string testDesign) => TestDesignDocument.Read(testDesign);

        /// <summary>Writes this test design as a <c>ruledger/test-design/v1</c> document.</summary>
        /// <returns>The document.</returns>
        /// <remarks>
        /// This is the test design — what a person reads against what they meant, what they put
        /// their choices into, and what gets committed. A line-oriented rendering of it is a
        /// view of this, and not a second copy of it.
        /// </remarks>
        public string ToJson() => TestDesignDocument.Write(this);

        internal static TestDesign Derive(
            RuleRuntime runtime,
            string ruleSet,
            IReadOnlyDictionary<string, string>? components,
            WalkSettings? settings,
            ObservationScan observation,
            IReadOnlyList<TestDesignEdit>? edits = null)
        {
            Walker walker = new(
                runtime.CreateContext(ruleSet, components),
                observation,
                settings ?? new WalkSettings(),
                edits ?? []);

            return walker.Walk();
        }

        private sealed class Walker(
            RuleContext rules,
            ObservationScan observation,
            WalkSettings settings,
            IReadOnlyList<TestDesignEdit> edits)
        {
            private readonly Dictionary<string, string> seen = new(StringComparer.Ordinal);
            private readonly List<Entry> order = [];
            private readonly Stack<Entry> descending = new();
            private readonly Dictionary<TestDesignEdit, EditResult> outcomes = [];

            // How the walk got where it is standing, which is the name of the state it is
            // standing in. It grows and shrinks with the descent, so it costs the depth of the
            // walk and not its size.
            private readonly StringBuilder here = new("#0");
            private int unreached;

            public TestDesign Walk()
            {
                Arrive(rules.InitialState, null, null);

                // The recursion is written out because its depth is the depth of the walk, and
                // only the count of states it may visit keeps that bounded: a rule set holding a
                // history never returns to a state it has been in, so one route can be as long
                // as that count.
                while (this.descending.Count > 0)
                {
                    Entry entry = this.descending.Peek();
                    if (entry.Next >= entry.Ahead.Count)
                    {
                        this.here.Length = this.descending.Pop().Back;
                        continue;
                    }

                    Step step = entry.Ahead[entry.Next++];
                    step.Landing.To = Arrive(step.State, entry.Name, step);
                }

                return new TestDesign(
                    rules.RuleSet,
                    settings,
                    observation.Observed,
                    observation.Collapsed,
                    [.. this.order.Select(static entry => entry.Close())],
                    [.. edits.Select(edit =>
                        this.outcomes.TryGetValue(edit, out EditResult? result)
                            ? result
                            : new EditResult(edit, EditOutcome.NotReached))],
                    this.unreached);
            }

            // A composite keeps each component's state under its alias, and keeps it as a state
            // document rather than as bare fields, so `req.stage` is one step through the alias
            // and one through what the component wrote.
            private static JsonElement? Locate(JsonElement data, string field)
            {
                JsonElement node = data;
                foreach (string segment in field.Split('.'))
                {
                    if (node.ValueKind == JsonValueKind.Object
                        && node.TryGetProperty("ruleSet", out _)
                        && node.TryGetProperty("data", out JsonElement held))
                    {
                        node = held;
                    }

                    if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(segment, out node))
                    {
                        return null;
                    }
                }

                return node;
            }

            private static bool Matches(TestDesignEdit edit, Move move) =>
                string.Equals(edit.Input, move.Input, StringComparison.Ordinal)
                && edit.Arguments.Count == move.Arguments.Count
                && edit.Arguments.All(argument =>
                    move.Arguments.TryGetValue(argument.Key, out string? value)
                    && string.Equals(value, argument.Value, StringComparison.Ordinal));

            private string? Arrive(string state, string? from, Step? step)
            {
                string key = Key(state);
                if (this.seen.TryGetValue(key, out string? seenAs))
                {
                    return seenAs;
                }

                if (this.order.Count >= settings.States)
                {
                    this.unreached++;
                    return null;
                }

                // Numbered on arrival rather than on departure, so that a state's name says
                // where the walk found it and the first thing #0 leads to is #1. The number is
                // the short way to write the name; how the walk arrived is the whole of it, and
                // it is the only one of the two that means the same thing in the next version.
                string name = "#" + this.order.Count;
                this.seen[key] = name;

                int back = this.here.Length;
                if (step is not null)
                {
                    this.here.Append(" + ").Append(Route.Step(step.By, step.Landing.Draw));
                }

                Entry entry = new(name, from, step?.By, state) { Back = back };
                this.order.Add(entry);

                TerminalStatus terminal = rules.GetTerminalStatus(state);
                entry.IsTerminal = terminal.IsTerminal;
                entry.Result = terminal.Result;

                ValidInputSet legal = rules.GetValidInputs(state, settings.Candidates);
                entry.Evaluated = legal.Evaluated;
                entry.Truncated = legal.Truncated;

                List<List<Step>> onward = [];
                foreach (ValidInput input in legal)
                {
                    string document = input.ToInputDocument(rules.RuleSet);
                    List<Landing> landings = [];
                    List<Step> ahead = [];

                    // Past the end there is nothing to reach, so the moves a rule set still
                    // offers are written down and none of them is followed.
                    if (!terminal.IsTerminal)
                    {
                        foreach (Outcome outcome in rules.GetOutcomes(document, state, settings.Outcomes))
                        {
                            Landing landing = new(
                                outcome.Probability,
                                outcome.Draws.IsEmpty ? null : outcome.ToOutcomeDocument(rules.RuleSet));

                            landings.Add(landing);
                            ahead.Add(new Step(landing, outcome.Result.State, input.ToString()));
                        }
                    }

                    entry.Moves.Add(new Move(
                        input.Input,
                        input.Arguments.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal),
                        input.ToString(),
                        input.Actor,
                        document,
                        landings));

                    onward.Add(ahead);
                }

                entry.Ahead.AddRange(Ordered(name, entry.Moves, onward));
                this.descending.Push(entry);
                return name;
            }

            // The one place a person's writing reaches. Which inputs are legal here is an
            // observation and keeps the order the runtime offered them in; which one the walk
            // goes down first is a choice, and that is what an edit replaces.
            private IEnumerable<Step> Ordered(string name, List<Move> moves, List<List<Step>> onward)
            {
                List<int> taken = [];
                foreach (TestDesignEdit edit in edits.Where(edit => Names(edit, name)))
                {
                    int found = moves.FindIndex(move => Matches(edit, move));

                    // Written back with the name spelled out, whichever way it was written
                    // here: the number is only a name inside the test design it was read from.
                    this.outcomes[edit] = new EditResult(
                        edit,
                        found < 0 ? EditOutcome.NotLegal : EditOutcome.Carried,
                        this.here.ToString());

                    if (found >= 0 && !taken.Contains(found))
                    {
                        taken.Add(found);
                    }
                }

                return taken
                    .Concat(Enumerable.Range(0, onward.Count).Where(index => !taken.Contains(index)))
                    .SelectMany(index => onward[index]);
            }

            // A state has two names and this answers to either. The number is the one a person
            // reads off the test design in front of them; how the walk arrived is the one that still
            // means the same place after the rule set changes, and it is what a choice read out
            // of an earlier test design is written with.
            private bool Names(TestDesignEdit edit, string name) =>
                string.Equals(edit.State, name, StringComparison.Ordinal)
                || this.here.Equals(edit.State.AsSpan());

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
                    if (Locate(data, field) is JsonElement value)
                    {
                        Canonical.Write(value, key);
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

                /// <summary>How much of the walk's route belongs to the state this was reached from.</summary>
                public int Back { get; init; }

                public List<Move> Moves { get; } = [];

                public List<Step> Ahead { get; } = [];

                public TestDesignState Close() =>
                    new(name, from, by, state, IsTerminal, Result, Evaluated, Truncated, Moves);
            }
        }
    }
}
