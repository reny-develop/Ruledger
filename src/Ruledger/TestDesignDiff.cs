// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using Rulealize;
using Rulealize.Abstraction;

namespace Ruledger
{
    /// <summary>What changed between a test design and a later version of the rule set it came from.</summary>
    /// <remarks>
    /// <para>
    /// Applying the previous test design to the new rule set and diffing the two are the
    /// same operation described from two sides, so this is both. Ruledger runs the rule set to
    /// derive a test design at all — the expected value of a transition is what the runtime
    /// answers when the input is applied — and running last version's test design against this
    /// version's rules is where that first says something. Against the same version it cannot:
    /// a test design states what its own rule set decides, so it always passes.
    /// </para>
    /// <para>
    /// The two are held against each other by how the walk arrived at each state, not by what
    /// is in it. Both are walked with the settings the previous test design recorded, because
    /// a difference is only the rules' doing when everything else was the same.
    /// </para>
    /// </remarks>
    public sealed class TestDesignDiff
    {
        private TestDesignDiff(
            TestDesign before,
            TestDesign after,
            int common,
            IReadOnlyList<StateChange> changed,
            IReadOnlyList<TestDesignState> gone,
            IReadOnlyList<TestDesignState> appeared)
        {
            Before = before;
            After = after;
            Common = common;
            Changed = changed;
            Gone = gone;
            Appeared = appeared;
        }

        /// <summary>Gets the test design that was applied.</summary>
        public TestDesign Before { get; }

        /// <summary>Gets the test design the new version of the rule set gives, carrying the choices of the old.</summary>
        public TestDesign After { get; }

        /// <summary>Gets how many states both test designs have.</summary>
        public int Common { get; }

        /// <summary>Gets the states both test designs have and disagree about, in the order the walk arrived.</summary>
        public IReadOnlyList<StateChange> Changed { get; }

        /// <summary>Gets the states only the previous test design has.</summary>
        public IReadOnlyList<TestDesignState> Gone { get; }

        /// <summary>Gets the states only the new test design has.</summary>
        public IReadOnlyList<TestDesignState> Appeared { get; }

        /// <summary>Gets a value indicating whether the two versions are compared on the same state.</summary>
        /// <remarks>
        /// False says that the change moved which fields an observation depends on, so the two
        /// walks did not rejoin in the same places. The diff below it is still every difference
        /// there is, but part of it is the walk having gone elsewhere rather than the rules
        /// having decided differently, and it is said here rather than left to be guessed at.
        /// </remarks>
        public bool ComparedTheSameWay => Before.Observed.SequenceEqual(After.Observed, StringComparer.Ordinal);

        /// <summary>Gets a value indicating whether the new version decides everything the old test design says it does.</summary>
        public bool IsEmpty => Changed.Count == 0 && Gone.Count == 0 && Appeared.Count == 0;

        /// <summary>Applies a test design to a version of the rule set, which is to say diffs the two.</summary>
        /// <param name="runtime">A runtime with the vocabularies the rule set draws on loaded.</param>
        /// <param name="testDesign">A <c>ruledger/test-design/v1</c> document, derived from an earlier version.</param>
        /// <param name="ruleSet">The version of the rule set to apply it to, as text.</param>
        /// <param name="components">The document of every rule set reachable through <c>uses</c>, by identifier.</param>
        /// <returns>What the new version decides differently, and what became of the choices in the test design.</returns>
        /// <remarks>
        /// The new walk is given the settings the test design was walked with and the choices
        /// a person made in it. Both travel in it for this: settings so that the two
        /// walks are comparable, choices so that a change to the rules does not quietly cost
        /// somebody their work. What could not be carried is in <see cref="TestDesign.Edits"/> of
        /// <see cref="After"/>, every one of it, with which of the two ways it was lost.
        /// </remarks>
        /// <exception cref="JsonException">The text is not JSON.</exception>
        /// <exception cref="InvalidOperationException">The text is not a test design, or a rule set named in <c>uses</c> was not supplied.</exception>
        /// <exception cref="RuleSetBuildException">The text is not a rule set this runtime can compile.</exception>
        public static TestDesignDiff Of(
            RuleRuntime runtime,
            string testDesign,
            string ruleSet,
            IReadOnlyDictionary<string, string>? components = null)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(testDesign);
            ArgumentNullException.ThrowIfNull(ruleSet);

            TestDesign before = TestDesign.FromJson(testDesign);
            Route route = new();
            int[] routes = route.Of(before);

            TestDesign after = TestDesign.Derive(
                runtime, ruleSet, components, before.Settings, Carried(before, route, routes));

            return Between(before, after, route, routes, route.Of(after));
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"{Before.RuleSet} -> {After.RuleSet}: {Common} in both, {Changed.Count} moved, "
            + $"{Gone.Count} gone, {Appeared.Count} new";

        /// <summary>Holds two test designs against each other.</summary>
        /// <exception cref="InvalidOperationException">The two were not walked the same way.</exception>
        internal static TestDesignDiff Between(TestDesign before, TestDesign after)
        {
            Route route = new();
            return Between(before, after, route, route.Of(before), route.Of(after));
        }

        private static TestDesignDiff Between(TestDesign before, TestDesign after, Route route, int[] was, int[] now)
        {
            // A setting that moved would show up as states that are not there and
            // inputs that were not looked for, and none of that is the rule set deciding
            // differently. Two test designs walked differently are not comparable, and saying so is
            // the only thing to do about it.
            if (before.Settings != after.Settings)
            {
                throw new InvalidOperationException(
                    $"These were walked differently — {before.Settings} against {after.Settings} — so what is "
                    + "different between them is not the rule set's doing alone.");
            }

            Dictionary<int, int> arrived = [];
            for (int at = 0; at < now.Length; at++)
            {
                arrived[now[at]] = at;
            }

            Where led = new(route, was, Named(before));
            Where leads = new(route, now, Named(after));

            List<StateChange> changed = [];
            List<TestDesignState> gone = [];
            int common = 0;

            for (int at = 0; at < was.Length; at++)
            {
                if (!arrived.TryGetValue(was[at], out int then))
                {
                    gone.Add(before.States[at]);
                    continue;
                }

                common++;
                if (Compare(before.States[at], after.States[then], led, leads) is StateChange change)
                {
                    changed.Add(change.At(route.Text(was[at])));
                }
            }

            HashSet<int> walked = [.. was];
            List<TestDesignState> appeared = [.. Enumerable.Range(0, now.Length)
                .Where(at => !walked.Contains(now[at]))
                .Select(at => after.States[at])];

            return new TestDesignDiff(before, after, common, changed, gone, appeared);
        }

        // A choice names a state, and a state has two names: the number it has in the test design a
        // person read it off, and how the walk arrived there. Only the second one still means
        // this state in the next version, so the number is spelled out here rather than handed
        // to a walk that would number a different state the same.
        private static IReadOnlyList<TestDesignEdit> Carried(TestDesign before, Route route, int[] routes)
        {
            Dictionary<string, int> named = Named(before);

            return
            [
                .. before.Edits.Select(result =>
                {
                    string state = result.Name ?? result.Edit.State;
                    if (named.TryGetValue(state, out int at))
                    {
                        state = route.Text(routes[at]);
                    }

                    return new TestDesignEdit(state, result.Edit.Input, result.Edit.Arguments);
                }),
            ];
        }

        private static Dictionary<string, int> Named(TestDesign design)
        {
            Dictionary<string, int> named = new(StringComparer.Ordinal);
            for (int at = 0; at < design.States.Count; at++)
            {
                named[design.States[at].Name] = at;
            }

            return named;
        }

        private static StateChange? Compare(TestDesignState before, TestDesignState after, Where led, Where leads)
        {
            Dictionary<string, Move> still = new(StringComparer.Ordinal);
            foreach (Move move in after.Moves)
            {
                still[move.Text] = move;
            }

            List<Move> lost = [];
            List<MoveChange> moved = [];

            foreach (Move move in before.Moves)
            {
                if (!still.TryGetValue(move.Text, out Move? other) || !Same(move, other))
                {
                    lost.Add(move);
                    continue;
                }

                if (!led.Of(move).SequenceEqual(leads.Of(other)))
                {
                    moved.Add(new MoveChange(move, other, led.Text(move), leads.Text(other)));
                }
            }

            HashSet<string> kept = [.. before.Moves.Except(lost).Select(move => move.Text)];
            List<Move> gained = [.. after.Moves.Where(move => !kept.Contains(move.Text))];

            bool ending = before.IsTerminal != after.IsTerminal
                || !string.Equals(before.Result, after.Result, StringComparison.Ordinal);
            bool truncation = before.Truncated != after.Truncated;

            return lost.Count == 0 && gained.Count == 0 && moved.Count == 0 && !ending && !truncation
                ? null
                : new StateChange(before, after, lost, gained, moved, ending, truncation);
        }

        private static bool Same(Move move, Move other) =>
            string.Equals(move.Text, other.Text, StringComparison.Ordinal)
            && string.Equals(move.Actor, other.Actor, StringComparison.Ordinal);

        // Where a move leads, written the way both test designs can be asked it: the number a
        // state has is a name inside one of them, and the two are numbering different walks. What was
        // drawn is part of the line, because a draw is observable and two branches of one
        // input are told apart by nothing else.
        private sealed class Where(Route route, int[] routes, Dictionary<string, int> named)
        {
            // Not a route, and not the opening either: what the walk stopped in front of.
            private const int Nowhere = -1;

            // Held against each other by the number of the route, which the two test designs
            // share, and written out only where they turn out to differ: spelling a route out
            // costs the depth of the walk, and most states have nothing to report.
            public IReadOnlyList<(string Drew, double Probability, int To)> Of(Move move) =>
            [
                .. move.Landings.Select(landing => (
                    Route.Drew(landing.Draw),
                    landing.Probability,
                    landing.To is not null && named.TryGetValue(landing.To, out int at) ? routes[at] : Nowhere)),
            ];

            public IReadOnlyList<string> Text(Move move) =>
            [
                .. Of(move).Select(landing =>
                {
                    string chance = landing.Probability.ToString(CultureInfo.InvariantCulture);
                    string where = landing.To == Nowhere ? "(not reached)" : route.Text(landing.To);
                    return landing.Drew.Length == 0 ? $"{chance} -> {where}" : $"{landing.Drew} {chance} -> {where}";
                }),
            ];
        }
    }

    /// <summary>One state both test designs have, and what they say differently about it.</summary>
    /// <remarks>
    /// Three things are observable about a state, so three things can differ: which inputs are
    /// legal, whether it is final and with what result, and where each legal input leads. What
    /// is in the state is not among them. A field added to every state is not something a test
    /// can observe, and a diff that reported it would be reporting on the writing rather than
    /// on the rules.
    /// </remarks>
    public sealed class StateChange
    {
        internal StateChange(
            TestDesignState before,
            TestDesignState after,
            IReadOnlyList<Move> lost,
            IReadOnlyList<Move> gained,
            IReadOnlyList<MoveChange> moved,
            bool ending,
            bool truncation)
        {
            Name = before.Name;
            Before = before;
            After = after;
            Lost = lost;
            Gained = gained;
            Moved = moved;
            EndingMoved = ending;
            TruncationMoved = truncation;
        }

        /// <summary>Gets how the walk arrives here, which is what this state is called in both test designs.</summary>
        /// <remarks>
        /// Written out in full, because the number a state has is a name inside one test design
        /// and these are two. <see cref="Before"/> and <see cref="After"/> each have the number it
        /// has in theirs.
        /// </remarks>
        public string Name { get; private set; }

        /// <summary>Gets what the previous test design says about it.</summary>
        public TestDesignState Before { get; }

        /// <summary>Gets what the new test design says about it.</summary>
        public TestDesignState After { get; }

        /// <summary>Gets the inputs that were legal here and are not any more.</summary>
        public IReadOnlyList<Move> Lost { get; }

        /// <summary>Gets the inputs that are legal here now and were not.</summary>
        public IReadOnlyList<Move> Gained { get; }

        /// <summary>Gets the inputs that are still legal here and lead somewhere else.</summary>
        public IReadOnlyList<MoveChange> Moved { get; }

        /// <summary>Gets a value indicating whether the two disagree about the state being final, or about its result.</summary>
        public bool EndingMoved { get; }

        /// <summary>Gets a value indicating whether the search for legal inputs stopped at the limit in one and not the other.</summary>
        /// <remarks>
        /// Which makes the rest of this state's line worth less than it looks: a list that
        /// stopped at the limit is not all of them, and inputs said to be lost or gained may be
        /// on the side of the limit that was not looked at.
        /// </remarks>
        public bool TruncationMoved { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            List<string> what = [];
            if (Lost.Count > 0)
            {
                what.Add("lost " + string.Join(' ', Lost));
            }

            if (Gained.Count > 0)
            {
                what.Add("gained " + string.Join(' ', Gained));
            }

            if (Moved.Count > 0)
            {
                what.Add(string.Join(", ", Moved));
            }

            if (EndingMoved)
            {
                what.Add($"{Ending(After)}, was {Ending(Before)}");
            }

            if (TruncationMoved)
            {
                what.Add(After.Truncated ? "stopped at the limit" : "no longer stopped at the limit");
            }

            // Said the way the test design being applied says it, because that is the one the
            // person has in front of them. The whole name is in Name, and past a few steps in
            // it is longer than anything it would help anybody read.
            return $"{Before}: {string.Join("; ", what)}";
        }

        // Writing the name out costs the depth of the walk, so it is done for the states
        // that differ rather than for every state that was looked at.
        internal StateChange At(string name)
        {
            Name = name;
            return this;
        }

        private static string Ending(TestDesignState state) =>
            state.IsTerminal ? "final " + (state.Result ?? "(no result)") : "not final";
    }

    /// <summary>One input that is still legal in a state, and leads somewhere else than it did.</summary>
    public sealed class MoveChange
    {
        internal MoveChange(Move before, Move after, IReadOnlyList<string> led, IReadOnlyList<string> leads)
        {
            Before = before;
            After = after;
            Led = led;
            Leads = leads;
        }

        /// <summary>Gets the input as the previous test design has it.</summary>
        public Move Before { get; }

        /// <summary>Gets the input as the new test design has it.</summary>
        public Move After { get; }

        /// <summary>Gets where it led, one line per branch, naming each state by how the walk arrives there.</summary>
        public IReadOnlyList<string> Led { get; }

        /// <summary>Gets where it leads now, one line per branch.</summary>
        public IReadOnlyList<string> Leads { get; }

        /// <inheritdoc />
        public override string ToString() =>
            $"{Before} leads to {string.Join(" ", Leads)}, was {string.Join(" ", Led)}";
    }
}
