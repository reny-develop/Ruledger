// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Verify
{
    /// <summary>What a change to the rules costs, held against the size of the change.</summary>
    /// <remarks>
    /// Applying the previous test design to the new rule set and diffing the two are one
    /// operation described from two sides. Each pair in <c>verify/ruleset</c> is one line
    /// apart, and what the diff says about it is the measurement: a change that moves the
    /// endings moves the endings, and a change that stops the game stops it.
    /// </remarks>
    public class Differing(Xunit.Abstractions.ITestOutputHelper said)
    {
        // Swapping which count wins leaves the state space exactly where it was: three
        // thousand states in both, not one of them gained or lost an input or a landing, and
        // 844 endings saying the other name.
        [Fact]
        public void ChangingWhoWinsChangesTheEndingsAndNoStateAtAll()
        {
            TestDesignDiff diff = Fixture.Apply(Fixture.Walk("reversi", 3000), "reversi-swapped");

            said.WriteLine($"reversi -> reversi-swapped at 3000 states: {diff.Common} states in both, "
                + $"{diff.Changed.Count} decided differently, {diff.Gone.Count} gone, {diff.Appeared.Count} new");

            Assert.Equal(3000, diff.Common);
            Assert.Empty(diff.Gone);
            Assert.Empty(diff.Appeared);
            Assert.Equal(844, diff.Changed.Count);
            Assert.All(diff.Changed, change =>
            {
                Assert.True(change.EndingMoved);
                Assert.Empty(change.Lost);
                Assert.Empty(change.Gained);
                Assert.Empty(change.Moved);
            });
        }

        // And the other end of the same rule set: requiring two discs to be flipped instead
        // of one leaves nothing in common but the opening position, where every move reversi
        // starts with has become illegal and passing is all there is.
        [Fact]
        public void AGuardThatStopsTheGameLeavesOnlyTheOpeningInCommon()
        {
            TestDesignDiff diff = Fixture.Apply(Fixture.Walk("reversi", 3000), "reversi-strict");

            said.WriteLine($"reversi -> reversi-strict at 3000 states: {diff.Common} states in both, "
                + $"{diff.Changed.Count} decided differently, {diff.Gone.Count} gone, {diff.Appeared.Count} new");

            Assert.Equal(1, diff.Common);
            Assert.Equal(2999, diff.Gone.Count);
            Assert.Equal(2, diff.Appeared.Count);

            StateChange opening = Assert.Single(diff.Changed);
            Assert.Equal("#0", opening.Name);
            Assert.Equal(
                ["place(at: c4)", "place(at: d3)", "place(at: e6)", "place(at: f5)"],
                opening.Lost.Select(move => move.Text).Order());
            Assert.Equal("pass", Assert.Single(opening.Gained).Text);
        }

        // A field added to every state, read in `terminal`, with a cap nothing can reach.
        // The rules decide exactly what they decided before, and every state in the old
        // design is a state whose contents the new walk never produces — which is the change
        // a design has no defence against if it holds states by what is in them.
        [Fact]
        public void AFieldAddedToEveryStateMovesNothingThatCanBeObserved()
        {
            TestDesignDiff diff = Fixture.Apply(Fixture.Walk("reversi", 3000), "reversi-counted");

            HashSet<string> was = [.. diff.Before.States.Select(Contents)];

            said.WriteLine($"reversi -> reversi-counted at 3000 states: {diff.Common} states in both, "
                + $"{diff.Changed.Count} decided differently, {diff.Gone.Count} gone, {diff.Appeared.Count} new; "
                + $"not one of the new walk's states has the contents of an old one; "
                + $"compared on {string.Join(", ", diff.After.Observed)}, was "
                + $"{string.Join(", ", diff.Before.Observed)}");

            Assert.DoesNotContain("plies", diff.Before.Observed);
            Assert.Contains("plies", diff.After.Observed);
            Assert.False(diff.ComparedTheSameWay);
            Assert.DoesNotContain(diff.After.States.Select(Contents), was.Contains);

            // What is left is the walk, not the rules. No input became legal or illegal
            // anywhere and no ending moved; the two that differ are both a landing arriving
            // somewhere else, deep where the walk ran out, because two positions that differ
            // only in how many moves it took to reach them are now two positions.
            Assert.Equal(2999, diff.Common);
            Assert.Equal(2, diff.Changed.Count);
            Assert.All(diff.Changed, change =>
            {
                Assert.False(change.EndingMoved);
                Assert.Empty(change.Lost);
                Assert.Empty(change.Gained);
                Assert.NotEmpty(change.Moved);
            });
        }

        // The smallest diff worth reading: what the two endings are called, and nothing else.
        [Fact]
        public void RenamingAnEndingMovesTheEndingsAndNothingElse()
        {
            TestDesignDiff diff = Fixture.Apply(Fixture.Walk("approval", 3000), "approval-results");

            said.WriteLine($"approval -> approval-results: {diff.Common} states in both, "
                + $"{diff.Changed.Count} decided differently — "
                + $"{string.Join('/', diff.Changed.Select(change => change.Before.Result))} became "
                + $"{string.Join('/', diff.Changed.Select(change => change.After.Result))}");

            Assert.Equal(4, diff.Common);
            Assert.Empty(diff.Gone);
            Assert.Empty(diff.Appeared);
            Assert.Equal(2, diff.Changed.Count);
            Assert.All(diff.Changed, change => Assert.True(change.EndingMoved));
            Assert.Equal(["approved", "rejected"], diff.Changed.Select(change => change.Before.Result).Order());
            Assert.Equal(["declined", "passed"], diff.Changed.Select(change => change.After.Result).Order());
        }

        // A test design states what its own rule set decides, so running it against that rule
        // set can only pass. It is the control, and it is also the reason a diff needs two
        // versions to say anything at all.
        [Theory]
        [InlineData("approval")]
        [InlineData("roster")]
        [InlineData("reversi")]
        public void ADesignAppliedToTheVersionItCameFromFindsNothing(string ruleSet)
        {
            TestDesignDiff diff = Fixture.Apply(Fixture.Walk(ruleSet, 3000), ruleSet);

            said.WriteLine($"{ruleSet} against itself: {diff.Common} states in both, nothing moved");

            Assert.True(diff.IsEmpty);
            Assert.Equal(diff.Before.States.Count, diff.Common);
        }

        // Byte for byte, twice over. Everything a diff says rests on this: two walks that
        // came out in a different order would report a difference the rules did not make.
        [Theory]
        [InlineData("reversi")]
        [InlineData("roster")]
        [InlineData("blackjack")]
        public void TheSameWalkTwiceIsTheSameBytes(string ruleSet)
        {
            string first = Fixture.Walk(ruleSet, 300).ToJson();
            string second = Fixture.Walk(ruleSet, 300).ToJson();

            said.WriteLine($"{ruleSet} at 300 states, walked twice: {first.Length} bytes both times, identical");

            Assert.Equal(first, second);
        }

        // What is in a state, without the rule set stamped on it: two versions say their own
        // name in every state document, and that is the one thing about them that is
        // different on purpose.
        private static string Contents(TestDesignState state)
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(state.State);
            return document.RootElement.GetProperty("data").GetRawText();
        }
    }
}
