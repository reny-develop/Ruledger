// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Tests
{
    public class TestDesignTests
    {
        // Same rule set, same settings, same states in the same order. Everything a diff
        // between two designs says rests on this.
        [Theory]
        [InlineData("reversi")]
        [InlineData("roster")]
        [InlineData("blackjack")]
        public void TheSameWalkTwiceIsTheSameWalk(string ruleSet)
        {
            TestDesign first = Derive(ruleSet, 300);
            TestDesign second = Derive(ruleSet, 300);

            Assert.Equal(Sketch(first), Sketch(second));
        }

        [Fact]
        public void EverythingObservableAboutAStateIsWrittenDown()
        {
            TestDesignState opening = Derive("reversi", 300).States[0];

            Assert.Equal(["place(at: e6)", "place(at: f5)", "place(at: c4)", "place(at: d3)"],
                opening.Moves.Select(move => move.Text));
            Assert.Equal(65, opening.Evaluated);
            Assert.False(opening.Truncated);
            Assert.False(opening.IsTerminal);
            Assert.Null(opening.Result);
        }

        // A state is named after how the walk arrived, and the first thing the opening leads
        // to is #1. An edit a person makes is attached to that name.
        [Fact]
        public void AStateIsNamedAfterHowTheWalkGotThere()
        {
            TestDesign design = Derive("reversi", 300);

            Assert.Equal("#0", design.States[0].Name);
            Assert.Null(design.States[0].From);
            Assert.Equal("#0", design.States[1].From);
            Assert.Equal(design.States[0].Moves[0].Text, design.States[1].By);
            Assert.Equal("#1 = #0 + place(at: e6)", design.States[1].ToString());
        }

        // Walking one route to the end before coming back is what makes a result observable
        // at all: spreading out by depth collects openings and finishes no game.
        [Fact]
        public void WalkingToTheEndObservesTheResults()
        {
            TestDesign design = Derive("reversi", 3000);

            Assert.Equal(["black", "draw", "white"],
                design.States.Where(state => state.IsTerminal).Select(state => state.Result).Distinct().Order());
        }

        [Fact]
        public void AShortRuleSetIsWalkedOut()
        {
            TestDesign design = Derive("approval", 3000);

            Assert.Equal(4, design.States.Count);
            Assert.Equal(0, design.Unreached);
            Assert.Equal(["approved", "rejected"],
                design.States.Where(state => state.IsTerminal).Select(state => state.Result).Distinct().Order());
        }

        // What the budget stopped in front of is reported as that, and never as nothing being
        // there. Every landing the walk did not take is counted.
        [Fact]
        public void WhatTheBudgetDidNotReachIsSaidSo()
        {
            TestDesign design = Derive("reversi", 300);

            Assert.Equal(300, design.States.Count);
            Assert.Equal(
                design.Unreached,
                design.States.Sum(state => state.Moves.Sum(move => move.Landings.Count(landing => landing.To is null))));
            Assert.True(design.Unreached > 0);
        }

        // Where the next state is nobody's to decide, one input lands in more than one place,
        // and how likely each of them is comes from the runtime rather than from a sample.
        [Fact]
        public void AnInputThatDrawsLandsInMoreThanOnePlace()
        {
            TestDesign design = Derive("blackjack", 300);

            Move drawn = design.States
                .SelectMany(state => state.Moves)
                .First(move => move.Landings.Count > 1);

            Assert.All(drawn.Landings, landing => Assert.NotNull(landing.Draw));
            Assert.Equal(1.0, drawn.Landings.Sum(landing => landing.Probability), 6);
        }

        [Fact]
        public void AnInputThatDrawsNothingLandsInOne()
        {
            TestDesign design = Derive("reversi", 300);

            Assert.All(
                design.States.Where(state => !state.IsTerminal).SelectMany(state => state.Moves),
                move =>
                {
                    Landing only = Assert.Single(move.Landings);
                    Assert.Null(only.Draw);
                    Assert.Equal(1.0, only.Probability);
                });
        }

        // Past the end the walk stops, and what the rules still offer there is written down
        // with nowhere to go rather than left out. Stopping is the walk's decision; leaving
        // the moves out would make it look like the rule set's.
        [Fact]
        public void AFinalStateKeepsItsMovesAndLeadsNowhere()
        {
            TestDesignState ending = Derive("reversi", 3000).States.First(state => state.IsTerminal);

            Assert.NotNull(ending.Result);
            Assert.All(ending.Moves, move => Assert.Empty(move.Landings));
        }

        // The measurement collapsing exists for. roster-trail keeps every entry of its audit
        // trail, so no two of its states are ever equal as documents: comparing them whole,
        // the walk never once comes back to a position it has been in, and one of the two
        // results this rule set has is never reached inside the budget.
        [Fact]
        public void WithoutCollapsingAWalkNeverComesBack()
        {
            string text = Vocabulary.Read("roster-trail");
            WalkSettings settings = new(Budget: 3000);

            TestDesign collapsed = TestDesign.Derive(Vocabulary.Runtime, text, null, settings, ObservationScan.Of(text));
            TestDesign whole = TestDesign.Derive(Vocabulary.Runtime, text, null, settings, ObservationScan.Whole(text));

            Assert.Equal(0, Rejoins(whole));
            Assert.True(Rejoins(collapsed) > 2000);
            Assert.Contains("stuck", Results(collapsed));
            Assert.DoesNotContain("stuck", Results(whole));
        }

        // It still walks, though — the audit trail costs rejoining and not the walk itself.
        [Fact]
        public void WithoutCollapsingAWalkStillReachesAnEnding()
        {
            string text = Vocabulary.Read("roster-trail");
            TestDesign whole = TestDesign.Derive(
                Vocabulary.Runtime, text, null, new WalkSettings(Budget: 3000), ObservationScan.Whole(text));

            Assert.True(whole.States.Count(state => state.IsTerminal) > 900);
        }

        private static TestDesign Derive(string ruleSet, int budget) =>
            TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read(ruleSet), new WalkSettings(Budget: budget));

        private static int Rejoins(TestDesign design) =>
            design.States.Sum(state => state.Moves.Sum(move => move.Landings.Count(landing => landing.To is not null)))
                - (design.States.Count - 1);

        private static IEnumerable<string?> Results(TestDesign design) =>
            design.States.Where(state => state.IsTerminal).Select(state => state.Result).Distinct();

        private static string Sketch(TestDesign design) =>
            string.Join('\n', design.States.Select(state =>
                $"{state} {state.Result} {state.Evaluated} " +
                string.Join(' ', state.Moves.Select(move => $"{move.Text}->{string.Join(',', move.Landings)}"))));
    }
}
