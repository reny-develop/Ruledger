// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Verify
{
    /// <summary>Which state a position is compared on, and what happens without it.</summary>
    /// <remarks>
    /// The one thing the method needed that had to be built: working out mechanically, from
    /// the rule set, which state fields an observation can depend on. Two measurements — that
    /// it is worked out and not declared, and that a walk which skips it never comes back to
    /// a position it has been in.
    /// </remarks>
    public class Collapsing(Xunit.Abstractions.ITestOutputHelper said)
    {
        public static TheoryData<string> Scanned =>
            ["reversi", "roster", "chess", "shogi", "blackjack", "deploy"];

        [Theory]
        [MemberData(nameof(Scanned))]
        public void WhichStateAPositionIsComparedOnIsWorkedOut(string ruleSet)
        {
            ObservationScan scan = ObservationScan.Of(Fixture.RuleSet(ruleSet));

            said.WriteLine($"{ruleSet}: compared on {string.Join(", ", scan.Observed)}"
                + (scan.Collapsed.Count is 0 ? "" : $" — {string.Join(", ", scan.Collapsed)} dropped"));

            Assert.DoesNotContain(scan.Observed, scan.Collapsed.Contains);
            Assert.NotEmpty(scan.Observed);

            // An audit trail nobody reads is the case this exists for, and a board game has
            // nothing in it an observation does not reach.
            if (ruleSet is "roster" or "deploy")
            {
                Assert.Contains("log", scan.Collapsed);
            }

            if (ruleSet is "reversi" or "chess" or "shogi")
            {
                Assert.Empty(scan.Collapsed);
            }
        }

        // The answer comes from what a field is used for and not from what it is called.
        // Renaming the audit trail leaves it on the same side.
        [Fact]
        public void TheAnswerIsNotDrawnFromNames()
        {
            string renamed = Fixture.RuleSet("roster").Replace("\"log\"", "\"history\"", StringComparison.Ordinal);

            said.WriteLine("roster with `log` renamed `history`: "
                + $"{string.Join(", ", ObservationScan.Of(renamed).Collapsed)} dropped");

            Assert.Contains("history", ObservationScan.Of(renamed).Collapsed);
        }

        // The measurement collapsing exists for. roster-trail keeps every entry of its audit
        // trail, so no two of its states are ever equal as documents: comparing them whole,
        // the walk never once comes back to a position it has been in, and one of the two
        // results this rule set has is never reached before the walk stops.
        [Fact]
        public void WithoutCollapsingAWalkNeverComesBack()
        {
            string text = Fixture.RuleSet("roster-trail");
            WalkSettings settings = new(States: 3000);

            TestDesign collapsed = TestDesign.Derive(Fixture.Runtime, text, null, settings, ObservationScan.Of(text));
            TestDesign whole = TestDesign.Derive(Fixture.Runtime, text, null, settings, ObservationScan.Whole(text));

            said.WriteLine($"roster-trail at 3000 states: dropping what nothing observes, "
                + $"{Fixture.Rejoins(collapsed)} rejoins and results {string.Join('/', Fixture.Results(collapsed))}; "
                + $"comparing whole states, {Fixture.Rejoins(whole)} rejoins and results "
                + $"{string.Join('/', Fixture.Results(whole))}");

            Assert.Equal(0, Fixture.Rejoins(whole));
            Assert.True(Fixture.Rejoins(collapsed) > 2000);
            Assert.Contains("stuck", Fixture.Results(collapsed));
            Assert.DoesNotContain("stuck", Fixture.Results(whole));
        }

        // It still walks, though — the audit trail costs rejoining and not the walk itself.
        [Fact]
        public void WithoutCollapsingAWalkStillReachesAnEnding()
        {
            string text = Fixture.RuleSet("roster-trail");
            TestDesign whole = TestDesign.Derive(
                Fixture.Runtime, text, null, new WalkSettings(States: 3000), ObservationScan.Whole(text));

            said.WriteLine($"roster-trail compared whole at 3000 states: "
                + $"{whole.States.Count(state => state.IsTerminal)} endings reached anyway");

            Assert.True(whole.States.Count(state => state.IsTerminal) > 900);
        }
    }
}
