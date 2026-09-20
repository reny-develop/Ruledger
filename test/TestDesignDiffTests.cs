// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Tests
{
    /// <summary>Last version's test design against this version's rules, which is the diff.</summary>
    /// <remarks>
    /// Applying a test design and diffing two of them are one operation described from two
    /// sides, so these are about both at once. What the numbers come out as on the measured
    /// pairs is in <c>verify/</c>; these ask what the operation does at its edges — a
    /// composite applied as the one rule set it stands for, a state named after what was
    /// drawn to reach it, and two designs that were not walked the same way.
    /// </remarks>
    public class TestDesignDiffTests
    {
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

        // Two walks told to go different distances visit different states, and none of that
        // is the rule set deciding differently. There is nothing to report about such a pair,
        // so it is refused rather than reported. Applying a test design cannot land here —
        // the new walk is given the settings the test design recorded, which is what they
        // travel with it for — and holding two designs against each other can.
        [Fact]
        public void TwoDesignsWalkedDifferentWaysAreNotCompared()
        {
            TestDesign shorter = TestDesign.Derive(
                Vocabulary.Runtime, Vocabulary.Read("approval"), new WalkSettings(Budget: 2));
            TestDesign whole = TestDesign.Derive(
                Vocabulary.Runtime, Vocabulary.Read("approval"), new WalkSettings(Budget: 3000));

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => TestDesignDiff.Between(shorter, whole));

            Assert.Contains("walked differently", refused.Message, StringComparison.Ordinal);
        }
    }
}
