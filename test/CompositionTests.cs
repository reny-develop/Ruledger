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
        private static IReadOnlyDictionary<string, string> Halves =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["request"] = Vocabulary.Read("request"),
                ["shift"] = Vocabulary.Read("shift"),
            };

        [Fact]
        public void SplittingARuleSetDoesNotChangeTheStatesItIsDesignedOver()
        {
            Design split = Composite();
            Design whole = Merged();

            Assert.Equal(whole.States.Count, split.States.Count);
            Assert.Equal(whole.Unreached, split.Unreached);
        }

        [Fact]
        public void SplittingARuleSetDoesNotChangeWhatIsLegalOrWhereItGoes()
        {
            Assert.Equal(Sketch(Merged()), Sketch(Composite()));
        }

        [Fact]
        public void SplittingARuleSetDoesNotChangeTheEndings()
        {
            Design split = Composite();
            Design whole = Merged();

            Assert.Equal(
                whole.States.Where(state => state.IsTerminal).Select(state => state.Result),
                split.States.Where(state => state.IsTerminal).Select(state => state.Result));
        }

        // The guard neither half could write is enforced in both: an assignment only ever
        // happens as the consequence of a grant, so nothing is assignable from the opening.
        [Fact]
        public void TheGuardNeitherHalfCouldWriteHoldsInBoth()
        {
            Assert.DoesNotContain("assign", Composite().States[0].Moves.Select(move => move.Input));
            Assert.DoesNotContain("assign", Merged().States[0].Moves.Select(move => move.Input));
        }

        // What an observation depends on is the component's own answer with the alias in
        // front, so the two documents collapse the same fields as each other.
        [Fact]
        public void WhatIsObservedIsTheSameFieldsUnderTheAliases()
        {
            ObservationScan split = ObservationScan.Of(Vocabulary.Read("process"), Halves);

            Assert.Equal(["req.stage", "req.shift", "roster.mon", "roster.tue"], split.Observed);
            Assert.Empty(split.Collapsed);
            Assert.Equal(
                ObservationScan.Of(Vocabulary.Read("process-merged")).Observed.Count,
                split.Observed.Count);
        }

        // Held on its own, `request` never reads the shift it was raised for — nothing
        // observable about it depends on which one. Inside the process it does, because the
        // guard neither half could write reads it. The composite's answer is not the
        // component's answer copied across.
        [Fact]
        public void AComponentObservesMoreInsideACompositeThanAlone()
        {
            Assert.Contains("shift", ObservationScan.Of(Vocabulary.Read("request")).Collapsed);
            Assert.Contains("req.shift", ObservationScan.Of(Vocabulary.Read("process"), Halves).Observed);
        }

        [Fact]
        public void ARuleSetThatHoldsOneNotSuppliedIsRefused()
        {
            InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
                () => ObservationScan.Of(Vocabulary.Read("process")));

            Assert.Contains("holds 'request'", refusal.Message, StringComparison.Ordinal);
        }

        private static Design Composite() =>
            Design.Derive(Vocabulary.Runtime, Vocabulary.Read("process"), Halves, new WalkSettings(Budget: 300));

        private static Design Merged() =>
            Design.Derive(Vocabulary.Runtime, Vocabulary.Read("process-merged"), new WalkSettings(Budget: 300));

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
