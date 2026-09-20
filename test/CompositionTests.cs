// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Tests
{
    /// <summary>A rule set split into parts is the same rule set, so it is designed the same.</summary>
    /// <remarks>
    /// A composite and the one document it stands for are two ways of writing one set of rules.
    /// Nothing about which one was chosen belongs in a test design, so these hold the two
    /// designs against each other. Only the names differ: a composite offers a component's
    /// input under the alias it gave it, so `raise` in the merged document is `req.raise` here.
    /// </remarks>
    public class CompositionTests
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
        public void SplittingARuleSetDoesNotChangeTheStatesItIsDesignedOver(
            string composite, string merged, string[] components)
        {
            Design split = Derive(composite, components);
            Design whole = Derive(merged, []);

            Assert.Equal(whole.States.Count, split.States.Count);
            Assert.Equal(whole.Unreached, split.Unreached);
        }

        [Theory]
        [MemberData(nameof(BothWays))]
        public void SplittingARuleSetDoesNotChangeWhatIsLegalOrWhereItGoes(
            string composite, string merged, string[] components)
        {
            Assert.Equal(Sketch(Derive(merged, [])), Sketch(Derive(composite, components)));
        }

        [Theory]
        [MemberData(nameof(BothWays))]
        public void SplittingARuleSetDoesNotChangeTheEndings(
            string composite, string merged, string[] components)
        {
            Design split = Derive(composite, components);
            Design whole = Derive(merged, []);

            Assert.Equal(
                whole.States.Where(state => state.IsTerminal).Select(state => state.Result),
                split.States.Where(state => state.IsTerminal).Select(state => state.Result));
        }

        // What puts that at risk is how a composite has to read a component: as a record, one
        // key at a time. Read whole it would keep `note`, which nothing observes, and the two
        // documents would stop agreeing about which positions are the same position — nine
        // states and no rejoining on one side, seven and two on the other.
        [Theory]
        [MemberData(nameof(BothWays))]
        public void SplittingARuleSetDoesNotChangeWhichPositionsAreTheSamePosition(
            string composite, string merged, string[] components)
        {
            Assert.Equal(Rejoins(Derive(merged, [])), Rejoins(Derive(composite, components)));
        }

        [Fact]
        public void AFieldNothingObservesCollapsesOnBothSides()
        {
            Assert.Contains("note", ObservationScan.Of(Vocabulary.Read("seating-merged")).Collapsed);
            Assert.Contains("s.note", ObservationScan.Of(Vocabulary.Read("seating"), Held(["seats"])).Collapsed);
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
            ObservationScan split = ObservationScan.Of(Vocabulary.Read("process"), Held(["request", "shift"]));

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
            Assert.Contains("shift", ObservationScan.Of(Vocabulary.Read("request")).Collapsed);
            Assert.Contains("req.shift", ObservationScan.Of(Vocabulary.Read("process"), Held(["request", "shift"])).Observed);
        }

        [Fact]
        public void ARuleSetThatHoldsOneNotSuppliedIsRefused()
        {
            InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
                () => ObservationScan.Of(Vocabulary.Read("process")));

            Assert.Contains("holds 'request'", refusal.Message, StringComparison.Ordinal);
        }

        private static IReadOnlyDictionary<string, string> Held(string[] components) =>
            components.ToDictionary(static name => name, Vocabulary.Read, StringComparer.Ordinal);

        private static Design Derive(string ruleSet, string[] components) =>
            Design.Derive(Vocabulary.Runtime, Vocabulary.Read(ruleSet), Held(components), new WalkSettings(Budget: 300));

        private static int Rejoins(Design design) =>
            design.States.Sum(state => state.Moves.Sum(move => move.Landings.Count(landing => landing.To is not null)))
                - (design.States.Count - 1);

        // Everything observable about every state, with the alias a composite puts in front of
        // a component's input taken off, because that is the one thing the two do differ on.
        private static string Sketch(Design design) =>
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
