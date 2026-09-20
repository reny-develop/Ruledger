// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Ruledger.Tests
{
    /// <summary>Last version's test design against this version's rules, which is the diff.</summary>
    /// <remarks>
    /// Applying a test design and diffing two of them are one operation described from two sides,
    /// so these are about both at once. Each pair of rule sets in <c>verify/ruleset</c> is one
    /// line changed and nothing else, and what the diff says about it is the measurement: a
    /// change that moves the endings moves the endings, a change that stops the game stops it,
    /// and a change to what is in every state moves nothing that can be observed.
    /// </remarks>
    public class TestDesignDiffTests
    {
        // Twenty choices, stacked the way a person makes them: one at a time, each read off
        // the test design the ones before it produced. Twenty taken off an unedited one and
        // applied together is a different thing, because the first one moves the order the
        // walk goes in and everything below it is worked out again.
        private static readonly Dictionary<string, TestDesign> Stacked = new(StringComparer.Ordinal);

        // A test design states what its own rule set decides, so running it against that rule set
        // can only pass. It is the control, and it is also the reason a diff needs two
        // versions to say anything at all.
        [Theory]
        [InlineData("approval")]
        [InlineData("roster")]
        public void ADesignAppliedToTheVersionItCameFromFindsNothing(string ruleSet)
        {
            TestDesignDiff diff = Apply(Derive(ruleSet), ruleSet);

            Assert.True(diff.IsEmpty);
            Assert.Equal(diff.Before.States.Count, diff.Common);
            Assert.Empty(diff.Changed);
        }

        // A composite is walked as the one rule set it stands for, so it is applied as one
        // too: the documents it holds are handed over here exactly as they are to a walk.
        [Fact]
        public void ACompositeIsAppliedAsTheOneRuleSetItStandsFor()
        {
            Dictionary<string, string> components = new(StringComparer.Ordinal)
            {
                ["seats"] = Vocabulary.Read("seats"),
            };

            TestDesign design = TestDesign.Derive(
                Vocabulary.Runtime, Vocabulary.Read("seating"), components, new WalkSettings(Budget: 3000));

            TestDesignDiff diff = TestDesignDiff.Of(
                Vocabulary.Runtime, design.ToJson(), Vocabulary.Read("seating"), components);

            Assert.True(diff.IsEmpty);
            Assert.Equal(design.States.Count, diff.Common);
        }

        // Where the rules draw, one input arrives in more than one state, and what was drawn
        // is the only thing telling those states apart. A name that left it out would be the
        // name of all of them, and two of them would be held against each other as one.
        [Fact]
        public void AStateArrivedAtByADrawIsNamedAfterWhatWasDrawn()
        {
            TestDesign design = TestDesign.Derive(
                Vocabulary.Runtime, Vocabulary.Read("blackjack"), new WalkSettings(Budget: 300));

            Route route = new();
            int[] routes = route.Of(design);

            Assert.Equal(design.States.Count, routes.Distinct().Count());

            List<string> names = [.. design.States.Select(state => state.Name)];
            Landing drawn = design.States
                .SelectMany(state => state.Moves)
                .Where(move => move.Landings.Count > 1)
                .SelectMany(move => move.Landings)
                .First(landing => landing.To is not null && names.IndexOf(landing.To) > 0);

            Assert.Contains(
                Route.Drew(drawn.Draw),
                route.Text(routes[names.IndexOf(drawn.To!)]),
                StringComparison.Ordinal);

            Assert.True(TestDesignDiff.Of(Vocabulary.Runtime, design.ToJson(), Vocabulary.Read("blackjack")).IsEmpty);
        }

        // Two walks told to go different distances visit different states, and none of that is
        // the rule set deciding differently. There is nothing to report about such a pair, so
        // it is refused rather than reported. Applying a test design cannot land here — the
        // new walk is given the settings the test design recorded, which is what they travel with it
        // for — and holding two designs against each other can.
        [Fact]
        public void TwoDesignsWalkedDifferentWaysAreNotCompared()
        {
            TestDesign shorter = TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read("approval"), new WalkSettings(Budget: 2));

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => TestDesignDiff.Between(shorter, Derive("approval")));

            Assert.Contains("walked differently", refused.Message, StringComparison.Ordinal);
        }

        // The smallest diff worth reading: what the two endings are called, and nothing else.
        // Four states, two of them final, both of them moved.
        [Fact]
        public void RenamingAnEndingMovesTheEndingsAndNothingElse()
        {
            TestDesignDiff diff = Apply(Derive("approval"), "approval-results");

            Assert.Equal(4, diff.Common);
            Assert.Empty(diff.Gone);
            Assert.Empty(diff.Appeared);
            Assert.Equal(2, diff.Changed.Count);
            Assert.All(diff.Changed, change =>
            {
                Assert.True(change.EndingMoved);
                Assert.Empty(change.Lost);
                Assert.Empty(change.Gained);
                Assert.Empty(change.Moved);
            });

            Assert.Equal(["approved", "rejected"], diff.Changed.Select(change => change.Before.Result).Order());
            Assert.Equal(["declined", "passed"], diff.Changed.Select(change => change.After.Result).Order());
        }

        // Swapping which count wins leaves the state space exactly where it was: three
        // thousand states in both, not one of them gained or lost an input or a landing, and
        // 844 endings say the other name. The diff is the size of the change.
        [Fact]
        public void SwappingTheWinnerMovesTheEndingsAndNoStateAtAll()
        {
            TestDesignDiff diff = Apply(Derive("reversi"), "reversi-swapped");

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

        // And the other end of the same rule set: requiring two discs to be flipped instead of
        // one leaves nothing in common but the opening position, where every move reversi
        // starts with has become illegal and passing is all there is.
        [Fact]
        public void AGuardThatStopsTheGameLeavesOnlyTheOpeningInCommon()
        {
            TestDesignDiff diff = Apply(Derive("reversi"), "reversi-strict");

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

        // The change a test design has no defence against if it holds states by what is in them.
        // Counting the moves made puts a field in every state and reads it in `terminal`, so
        // every state is a state the old test design never saw — and the rules decide exactly what
        // they decided before, because nothing can reach the count this ends at.
        [Fact]
        public void AFieldAddedToEveryStateMovesNothingThatCanBeObserved()
        {
            TestDesignDiff diff = Apply(Derive("reversi"), "reversi-counted");

            Assert.DoesNotContain("plies", diff.Before.Observed);
            Assert.Contains("plies", diff.After.Observed);
            Assert.False(diff.ComparedTheSameWay);

            HashSet<string> was = [.. diff.Before.States.Select(Contents)];
            Assert.DoesNotContain(diff.After.States.Select(Contents), was.Contains);

            // What is left is the walk, not the rules. 2,999 of the 3,000 states are the same
            // states, no input became legal or illegal anywhere and no ending moved; the two
            // that differ are both a landing arriving somewhere else, deep where the walk ran
            // out. Two positions that differ only in how many moves it took to get to them
            // are now two positions, so a route that used to rejoin one of them does not —
            // which is the whole of what the false above warns about.
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

        // Which is the whole reason a choice is held by how the walk arrives and not by what
        // is in the state. Twenty choices, a field added to every state, and not one of them
        // lost — where the key is the state, every one of the twenty would have gone.
        [Theory]
        [InlineData("reversi-swapped")]
        [InlineData("reversi-counted")]
        public void ChoicesSurviveAChangeThatMovesNoObservation(string after)
        {
            TestDesignDiff diff = Apply(Chosen("reversi"), after);

            Assert.Equal(20, diff.After.Edits.Count);
            Assert.All(diff.After.Edits, edit => Assert.Equal(EditOutcome.Carried, edit.Outcome));
        }

        // Where they are lost, which of the two ways is said of every one of them. The input
        // chosen in the opening is illegal in both of these versions, and everything chosen
        // below it went with the states that are not walked any more — the losses are mostly
        // that: one choice falls, the walk goes elsewhere, and the ground the rest stood on
        // is gone. How many fall this way is a fact about these twenty choices and not about
        // the change: they are taken here by always choosing the last input a state offers,
        // and a choice made at the opening moves everything.
        [Theory]
        [InlineData("reversi", "reversi-strict")]
        [InlineData("roster", "roster-seniors")]
        public void AChoiceThatCouldNotBeCarriedIsReportedWithWhichWayItWentMissing(string ruleSet, string after)
        {
            TestDesignDiff diff = Apply(Chosen(ruleSet), after);

            Assert.Equal(20, diff.After.Edits.Count);
            Assert.Equal(0, diff.After.Edits.Count(edit => edit.Outcome == EditOutcome.Carried));
            Assert.Equal(1, diff.After.Edits.Count(edit => edit.Outcome == EditOutcome.NotLegal));
            Assert.Equal(19, diff.After.Edits.Count(edit => edit.Outcome == EditOutcome.NotReached));
        }

        // The same twenty against the version they were made on: every one of them taken.
        // Without this the line above only says that something was lost, and not that it was
        // the change that lost it.
        [Theory]
        [InlineData("reversi")]
        [InlineData("roster")]
        public void ChoicesAppliedToTheVersionTheyWereMadeOnAreAllTaken(string ruleSet)
        {
            TestDesignDiff diff = Apply(Chosen(ruleSet), ruleSet);

            Assert.True(diff.IsEmpty);
            Assert.Equal(20, diff.After.Edits.Count);
            Assert.All(diff.After.Edits, edit => Assert.Equal(EditOutcome.Carried, edit.Outcome));
        }

        // Only the assignments a roster has made, without the rule set stamped on them: two
        // versions of the same rule set say so in every state document, and that is the one
        // thing about them that is different on purpose.
        private static string Contents(TestDesignState state)
        {
            using JsonDocument document = JsonDocument.Parse(state.State);
            return document.RootElement.GetProperty("data").GetRawText();
        }

        private static TestDesign Derive(string ruleSet) =>
            TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read(ruleSet), new WalkSettings(Budget: 3000));

        private static TestDesignDiff Apply(TestDesign design, string ruleSet) =>
            TestDesignDiff.Of(Vocabulary.Runtime, design.ToJson(), Vocabulary.Read(ruleSet));

        private static TestDesign Chosen(string ruleSet)
        {
            if (Stacked.TryGetValue(ruleSet, out TestDesign? stacked))
            {
                return stacked;
            }

            string text = Vocabulary.Read(ruleSet);
            WalkSettings settings = new(Budget: 3000);
            List<TestDesignEdit> edits = [];
            TestDesign design = TestDesign.Derive(Vocabulary.Runtime, text, settings);

            for (int at = 0; edits.Count < 20 && at < design.States.Count; at++)
            {
                TestDesignState state = design.States[at];
                if (state.Moves.Count < 2)
                {
                    continue;
                }

                Move last = state.Moves[^1];
                edits.Add(new TestDesignEdit(state.Name, last.Input, last.Arguments));
                design = TestDesign.Derive(Vocabulary.Runtime, text, null, settings, edits);
            }

            Stacked[ruleSet] = design;
            return design;
        }
    }
}
