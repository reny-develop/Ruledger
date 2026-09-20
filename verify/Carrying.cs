// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Verify
{
    /// <summary>What becomes of a person's choices when the rules change under them.</summary>
    /// <remarks>
    /// <para>
    /// The choices are the twenty in <c>verify/choice</c>, fixed and committed. They are held
    /// by how the walk arrives at a state rather than by what is in it, and these measure
    /// what that buys: a field added to every state costs nothing, a definition renamed and
    /// its parameters reordered cost nothing, and fewer states to visit cost only what falls
    /// behind them.
    /// </para>
    /// <para>
    /// Where they are lost, which of the two ways is said of every one of them. How many fall
    /// is a fact about these twenty and not about the change: they are made by always taking
    /// the last input a state offers, so one of them sits at the opening, and a choice made
    /// at the opening moves everything below it.
    /// </para>
    /// </remarks>
    public class Carrying(Xunit.Abstractions.ITestOutputHelper said)
    {
        /// <summary>Each rule set, and the twin of it that decides exactly what it decides.</summary>
        /// <remarks>
        /// Every definition renamed and every definition of more than one parameter taking
        /// them the other way round. chess and shogi are walked less far than the other two
        /// because they cost more per state, and the claim does not need the distance.
        /// </remarks>
        public static TheoryData<string, string, int> WrittenAnotherWay =>
            new()
            {
                { "reversi", "reversi-renamed", 3000 },
                { "roster", "roster-renamed", 3000 },
                { "chess", "chess-renamed", 300 },
                { "shogi", "shogi-renamed", 60 },
            };

        // The same twenty against the version they were made on: every one taken. Without
        // this the measurements below only say that something was lost, and not that it was
        // the change that lost it.
        [Theory]
        [InlineData("reversi")]
        [InlineData("roster")]
        public void ChoicesAppliedToTheVersionTheyWereMadeOnAreAllTaken(string ruleSet)
        {
            TestDesign design = Chosen(ruleSet, 3000);

            said.WriteLine($"{ruleSet}, the committed twenty: "
                + $"{Carried(design)} of {design.Edits.Count} carried");

            Assert.Equal(20, design.Edits.Count);
            Assert.All(design.Edits, edit => Assert.Equal(EditOutcome.Carried, edit.Outcome));
        }

        // The change a test design has no defence against if it holds choices by what is in a
        // state. Counting the moves made puts a field in every state and reads it in
        // `terminal`, so every state is a state the old design never saw — and the rules
        // decide exactly what they decided before.
        [Theory]
        [InlineData("reversi-swapped")]
        [InlineData("reversi-counted")]
        public void ChoicesSurviveAChangeThatMovesNoObservation(string after)
        {
            TestDesignDiff diff = Fixture.Apply(Chosen("reversi", 3000), after);

            said.WriteLine($"reversi -> {after}: {Carried(diff.After)} of {diff.After.Edits.Count} choices carried");

            Assert.Equal(20, diff.After.Edits.Count);
            Assert.All(diff.After.Edits, edit => Assert.Equal(EditOutcome.Carried, edit.Outcome));
        }

        // How a rule set is written is not something a test design can observe. Every
        // definition renamed, every definition of more than one parameter taking them the
        // other way round, and the design that comes out is the same design.
        [Theory]
        [MemberData(nameof(WrittenAnotherWay))]
        public void HowTheRuleSetIsWrittenIsNotInTheTestDesign(string ruleSet, string renamed, int states)
        {
            TestDesignDiff diff = Fixture.Apply(Fixture.Walk(ruleSet, states), renamed);

            said.WriteLine($"{ruleSet} -> {renamed} at {states} states: {diff.Common} states in both, "
                + $"{diff.Changed.Count} decided differently, {diff.Gone.Count} gone, {diff.Appeared.Count} new");

            Assert.True(diff.IsEmpty);
            Assert.True(diff.ComparedTheSameWay);
        }

        // And a choice names an input in a state, neither of which is anything about how the
        // rule set is written either. The two that have committed choices carry all twenty
        // across the rename; chess and shogi have none, and what they measure is the line
        // above.
        [Theory]
        [InlineData("reversi", "reversi-renamed")]
        [InlineData("roster", "roster-renamed")]
        public void ChoicesDoNotPointAtHowTheRuleSetIsWritten(string ruleSet, string renamed)
        {
            TestDesignDiff diff = Fixture.Apply(Chosen(ruleSet, 3000), renamed);

            said.WriteLine($"{ruleSet} -> {renamed} at 3000 states: "
                + $"{Carried(diff.After)} of {diff.After.Edits.Count} choices carried");

            Assert.Equal(20, diff.After.Edits.Count);
            Assert.All(diff.After.Edits, edit => Assert.Equal(EditOutcome.Carried, edit.Outcome));
        }

        // Where they are lost, which of the two ways is said of every one of them. The input
        // chosen in the opening is illegal in both of these versions, and everything chosen
        // below it went with the states that are not walked any more — the losses are mostly
        // that: one choice falls, the walk goes elsewhere, and the ground the rest stood on
        // is gone.
        [Theory]
        [InlineData("reversi", "reversi-strict")]
        [InlineData("roster", "roster-seniors")]
        public void AChoiceThatCouldNotBeCarriedIsReportedWithWhichWayItWentMissing(string ruleSet, string after)
        {
            TestDesignDiff diff = Fixture.Apply(Chosen(ruleSet, 3000), after);

            int legal = diff.After.Edits.Count(edit => edit.Outcome == EditOutcome.NotLegal);
            int reached = diff.After.Edits.Count(edit => edit.Outcome == EditOutcome.NotReached);

            said.WriteLine($"{ruleSet} -> {after}: {Carried(diff.After)} carried, "
                + $"{legal} not legal there any more, {reached} not reached before the walk stopped");

            Assert.Equal(20, diff.After.Edits.Count);
            Assert.Equal(0, Carried(diff.After));
            Assert.Equal(1, legal);
            Assert.Equal(19, reached);
        }

        // Fewer states to visit is the other way a design gets re-derived, and what a smaller
        // walk costs is only the choices it stops in front of. These twenty are made down one
        // line of play, twenty deep, so a hundred states still reaches every one of
        // them; at twelve it does not, and the eight it stops in front of are reported as
        // that rather than dropped.
        [Theory]
        [InlineData(100, 20, 0)]
        [InlineData(12, 12, 8)]
        public void FewerStatesCostOnlyTheChoicesTheWalkStopsInFrontOf(int states, int carried, int missed)
        {
            TestDesign shorter = Chosen("reversi", states);

            int reached = shorter.Edits.Count(edit => edit.Outcome == EditOutcome.NotReached);

            said.WriteLine($"reversi, the committed twenty, 3000 states lowered to {states}: "
                + $"{Carried(shorter)} carried, {reached} not reached before the walk stopped");

            Assert.Equal(20, shorter.Edits.Count);
            Assert.Equal(carried, Carried(shorter));
            Assert.Equal(missed, reached);
            Assert.Equal(0, shorter.Edits.Count(edit => edit.Outcome == EditOutcome.NotLegal));
        }

        // The control for the settings, which is the one thing about a walk that is not
        // derived from the rule set. The thesis argues that a diff and a carry hold under
        // other settings because both versions are walked the same way; this measures one
        // other setting rather than leaving it argued. Same change, same shape of answer.
        [Fact]
        public void ADifferentSettingGivesTheSameKindOfAnswer()
        {
            TestDesign design = TestDesign.Derive(
                Fixture.Runtime,
                Fixture.RuleSet("reversi"),
                null,
                new WalkSettings(States: 500, Candidates: 200, Outcomes: 8),
                Fixture.Choices("reversi"));

            TestDesignDiff diff = Fixture.Apply(design, "reversi-swapped");

            said.WriteLine("reversi -> reversi-swapped at 500 states, 200 candidates, 8 outcomes: "
                + $"{diff.Common} states in both, {diff.Changed.Count} decided differently, "
                + $"{diff.Gone.Count} gone, {diff.Appeared.Count} new, "
                + $"{Carried(diff.After)} of {diff.After.Edits.Count} choices carried");

            Assert.Equal(500, diff.Common);
            Assert.Empty(diff.Gone);
            Assert.Empty(diff.Appeared);
            Assert.All(diff.Changed, change =>
            {
                Assert.True(change.EndingMoved);
                Assert.Empty(change.Lost);
                Assert.Empty(change.Gained);
                Assert.Empty(change.Moved);
            });

            Assert.Equal(diff.After.Edits.Count, Carried(diff.After));
        }

        private static int Carried(TestDesign design) =>
            design.Edits.Count(edit => edit.Outcome == EditOutcome.Carried);

        private static TestDesign Chosen(string ruleSet, int states) =>
            Fixture.Walk(ruleSet, states, Fixture.Choices(ruleSet));
    }
}
