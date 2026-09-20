// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Verify
{
    /// <summary>A rule set split into parts is the same rule set, so its test design is the same.</summary>
    /// <remarks>
    /// A composite and the one document it stands for are two ways of writing one set of
    /// rules. Nothing about which one was chosen belongs in a test design, so these hold the
    /// two designs against each other. Only the names differ: a composite offers a
    /// component's input under the alias it gave it, so `raise` in the merged document is
    /// `req.raise` here.
    /// </remarks>
    public class Splitting(Xunit.Abstractions.ITestOutputHelper said)
    {
        /// <summary>Each process, written as a composite and written as one rule set.</summary>
        public static TheoryData<string, string, string[]> BothWays =>
            new()
            {
                { "process", "process-merged", ["request", "shift"] },
                { "seating", "seating-merged", ["seats"] },
            };

        [Theory]
        [MemberData(nameof(BothWays))]
        public void SplittingARuleSetDoesNotChangeTheTestDesign(string composite, string merged, string[] components)
        {
            TestDesign split = Derive(composite, components);
            TestDesign whole = Derive(merged, []);

            said.WriteLine($"{composite} against {merged} at budget 300: "
                + $"{split.States.Count} states and {Fixture.Rejoins(split)} rejoins against "
                + $"{whole.States.Count} and {Fixture.Rejoins(whole)}, "
                + $"endings {string.Join('/', Fixture.Results(split))} against "
                + $"{string.Join('/', Fixture.Results(whole))}");

            Assert.Equal(whole.States.Count, split.States.Count);
            Assert.Equal(whole.Unreached, split.Unreached);
            Assert.Equal(Sketch(whole), Sketch(split));

            // Which positions are the same position has to agree too, or the two documents
            // would be designed over different state spaces while saying the same things
            // about the states they happened to share.
            Assert.Equal(Fixture.Rejoins(whole), Fixture.Rejoins(split));

            Assert.Equal(
                whole.States.Where(state => state.IsTerminal).Select(state => state.Result),
                split.States.Where(state => state.IsTerminal).Select(state => state.Result));
        }

        // What puts that at risk is how a composite has to read a component: as a record, one
        // key at a time. Read whole it would keep `note`, which nothing observes, and the two
        // documents would stop agreeing about which positions are the same position — nine
        // states and no rejoining on one side, seven and two on the other.
        [Fact]
        public void AFieldNothingObservesCollapsesOnBothSides()
        {
            said.WriteLine("seating-merged: "
                + $"{string.Join(", ", ObservationScan.Of(Fixture.RuleSet("seating-merged")).Collapsed)} dropped");
            said.WriteLine("seating holding seats: "
                + $"{string.Join(", ", ObservationScan.Of(Fixture.RuleSet("seating"), Held(["seats"])).Collapsed)} dropped");

            Assert.Contains("note", ObservationScan.Of(Fixture.RuleSet("seating-merged")).Collapsed);
            Assert.Contains("s.note", ObservationScan.Of(Fixture.RuleSet("seating"), Held(["seats"])).Collapsed);
        }

        // The guard neither half could write is enforced in both: an assignment only ever
        // happens as the consequence of a grant, so nothing is assignable from the opening.
        [Fact]
        public void TheGuardNeitherHalfCouldWriteHoldsInBoth()
        {
            Assert.DoesNotContain(
                "assign",
                Derive("process", ["request", "shift"]).States[0].Moves.Select(move => move.Input));

            Assert.DoesNotContain("assign", Derive("process-merged", []).States[0].Moves.Select(move => move.Input));
        }

        [Fact]
        public void WhatIsObservedIsTheSameFieldsUnderTheAliases()
        {
            ObservationScan split = ObservationScan.Of(Fixture.RuleSet("process"), Held(["request", "shift"]));

            said.WriteLine($"process holding request and shift: compared on {string.Join(", ", split.Observed)}");

            Assert.Equal(["req.stage", "req.shift", "roster.mon", "roster.tue"], split.Observed);
            Assert.Empty(split.Collapsed);
        }

        // Held on its own, `request` never reads the shift it was raised for — nothing
        // observable about it depends on which one. Inside the process it does, because the
        // guard neither half could write reads it. The composite's answer is not the
        // component's answer copied across.
        [Fact]
        public void AComponentObservesMoreInsideACompositeThanAlone()
        {
            Assert.Contains("shift", ObservationScan.Of(Fixture.RuleSet("request")).Collapsed);
            Assert.Contains(
                "req.shift",
                ObservationScan.Of(Fixture.RuleSet("process"), Held(["request", "shift"])).Observed);
        }

        [Fact]
        public void ARuleSetThatHoldsOneNotSuppliedIsRefused()
        {
            InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
                () => ObservationScan.Of(Fixture.RuleSet("process")));

            Assert.Contains("holds 'request'", refusal.Message, StringComparison.Ordinal);
        }

        private static IReadOnlyDictionary<string, string> Held(string[] components) =>
            components.ToDictionary(static name => name, Fixture.RuleSet, StringComparer.Ordinal);

        private static TestDesign Derive(string ruleSet, string[] components) =>
            TestDesign.Derive(
                Fixture.Runtime, Fixture.RuleSet(ruleSet), Held(components), new WalkSettings(Budget: 300));

        // Everything observable about every state, with the alias a composite puts in front
        // of a component's input taken off, because that is the one thing the two do differ on.
        private static string Sketch(TestDesign design) =>
            string.Join('\n', design.States.Select(state =>
                $"{state.Name} {state.IsTerminal} {state.Result} {state.Evaluated} "
                + string.Join(
                    ' ',
                    state.Moves.Select(move =>
                        $"{Bare(move.Text)}->{string.Join(',', move.Landings.Select(landing => landing.To))}"))));

        private static string Bare(string move)
        {
            int alias = move.IndexOf('.', StringComparison.Ordinal);
            int arguments = move.IndexOf('(', StringComparison.Ordinal);
            return alias >= 0 && (arguments < 0 || alias < arguments) ? move[(alias + 1)..] : move;
        }
    }
}
