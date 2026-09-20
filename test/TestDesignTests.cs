// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Tests
{
    /// <summary>What a walk writes down about a state.</summary>
    /// <remarks>
    /// What the numbers come out as on the measured rule sets is in <c>verify/</c>. These ask
    /// the smaller question: that each of the three observables is written, that a state is
    /// named after how the walk arrived, and that a rule set short enough to be walked out
    /// is walked out.
    /// </remarks>
    public class TestDesignTests
    {
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

        [Fact]
        public void AShortRuleSetIsWalkedOut()
        {
            TestDesign design = Derive("approval", 3000);

            Assert.Equal(4, design.States.Count);
            Assert.Equal(0, design.Unreached);
            Assert.Equal(["approved", "rejected"],
                design.States.Where(state => state.IsTerminal).Select(state => state.Result).Distinct().Order());
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

        private static TestDesign Derive(string ruleSet, int budget) =>
            TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read(ruleSet), new WalkSettings(Budget: budget));
    }
}
