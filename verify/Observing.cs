// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Verify
{
    /// <summary>What a test design is: the three observables, per state, and nothing else.</summary>
    /// <remarks>
    /// Three things are observable about a state — which inputs are legal, whether it is
    /// final and with what result, and where each legal input leads — and the claim being
    /// measured is that the output is those three laid out per state. What can be counted is
    /// bounded the same way, which is the second measurement here: everything countable about
    /// a walk is a count of states, of legal inputs, of endings or of results.
    /// </remarks>
    public class Observing(Xunit.Abstractions.ITestOutputHelper said)
    {
        public static TheoryData<string> Walked => ["reversi", "roster", "blackjack", "approval"];

        [Theory]
        [MemberData(nameof(Walked))]
        public void TheOutputIsTheThreeObservablesLaidOutPerState(string ruleSet)
        {
            TestDesign design = Fixture.Walk(ruleSet, 300);

            int pairs = design.States.Sum(state => state.Moves.Count);
            int endings = design.States.Count(state => state.IsTerminal);

            said.WriteLine(
                $"{design.RuleSet} at 300 states: {design.States.Count} states, {pairs} legal inputs over them, "
                + $"{endings} final, results {string.Join('/', Fixture.Results(design))}, "
                + $"{design.Unreached} landings the walk had no states left for");

            // Every state says all three, and a state that says nothing about one of them
            // would be a state a test could not be read off.
            Assert.All(design.States, state =>
            {
                Assert.NotNull(state.State);
                Assert.NotNull(state.Moves);
                Assert.All(state.Moves, move =>
                {
                    Assert.NotEmpty(move.Input);
                    Assert.Equal(state.IsTerminal, move.Landings.Count is 0);
                });
            });

            Assert.NotEmpty(design.States);
        }

        // What can be counted is bounded by what can be observed. There is no fourth quantity
        // here and no number standing in for how much of the rule set was covered: where the
        // walk stopped is a count of places, and it is the only number of its kind.
        [Fact]
        public void WhatCanBeCountedIsWhatCanBeObserved()
        {
            TestDesign design = Fixture.Walk("reversi", 3000);

            int pairs = design.States.Sum(state => state.Moves.Count);
            int endings = design.States.Count(state => state.IsTerminal);
            IReadOnlyList<string> results = Fixture.Results(design);

            said.WriteLine(
                $"reversi at 3000 states: {pairs} state-and-input pairs, {endings} endings, "
                + $"{results.Count} results ({string.Join('/', results)})");

            Assert.Equal(3000, design.States.Count);
            Assert.Equal(4342, pairs);
            Assert.Equal(849, endings);
            Assert.Equal(["black", "draw", "white"], results);
        }

        // Past the end the walk stops, and what the rules still offer there is written down
        // with nowhere to go rather than left out. Stopping is the walk's decision; leaving
        // the moves out would make it look like the rule set's.
        [Fact]
        public void AFinalStateKeepsItsLegalInputsAndLeadsNowhere()
        {
            TestDesignState ending = Fixture.Walk("reversi", 3000).States.First(state => state.IsTerminal);

            said.WriteLine($"reversi {ending}: final {ending.Result}, {ending.Moves.Count} legal inputs, none followed");

            Assert.NotNull(ending.Result);
            Assert.All(ending.Moves, move => Assert.Empty(move.Landings));
        }

        // What the walk stopped in front of is reported as that, and never as nothing being
        // there. Every landing the walk did not take is counted, and the count is the sum of
        // them rather than a figure arrived at some other way.
        [Fact]
        public void WhereTheWalkStoppedIsSaidSo()
        {
            TestDesign design = Fixture.Walk("reversi", 300);

            said.WriteLine($"reversi at 300 states: {design.States.Count} states, "
                + $"{design.Unreached} landings the walk had no states left for");

            Assert.Equal(300, design.States.Count);
            Assert.Equal(
                design.Unreached,
                design.States.Sum(state => state.Moves.Sum(move => move.Landings.Count(landing => landing.To is null))));
            Assert.True(design.Unreached > 0);
        }

        // Where the next state is nobody's to decide, one input lands in more than one place,
        // and how likely each of them is comes from the runtime rather than from a sample.
        [Fact]
        public void WhereTheRulesDrawOneInputLandsInMoreThanOnePlace()
        {
            TestDesign design = Fixture.Walk("blackjack", 300);

            Move drawn = design.States.SelectMany(state => state.Moves).First(move => move.Landings.Count > 1);

            said.WriteLine($"blackjack {drawn}: {drawn.Landings.Count} branches, "
                + $"probabilities summing to {drawn.Landings.Sum(landing => landing.Probability):0.######}");

            Assert.All(drawn.Landings, landing => Assert.NotNull(landing.Draw));
            Assert.Equal(1.0, drawn.Landings.Sum(landing => landing.Probability), 6);
        }
    }
}
