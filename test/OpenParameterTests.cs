// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Tests
{
    /// <summary>What a walk does with a parameter the rule set leaves open.</summary>
    /// <remarks>
    /// signup has one of each kind: text nobody can enumerate, a bounded whole number, and an
    /// enumeration. The first is followed only where a person wrote a value; the other two are
    /// tried in full, and a value <c>validate</c> refuses is not a legal move.
    /// </remarks>
    public class OpenParameterTests
    {
        [Fact]
        public void AMoveWaitingForTextNobodyWroteIsLegalAndNotFollowed()
        {
            TestDesign design = Derive();

            TestDesignState opening = Assert.Single(design.States);
            Move waiting = Assert.Single(opening.Moves);

            Assert.Equal("setName(to: ?)", waiting.Text);
            Assert.Equal(["to"], waiting.Open);
            Assert.Empty(waiting.Landings);
            Assert.Equal(0, design.Unreached);
        }

        [Fact]
        public void AValueAPersonWroteIsFollowed()
        {
            TestDesign design = Derive(new TestDesignEdit("#0", "setName", Arguments(("to", "alice"))));

            EditResult carried = Assert.Single(design.Edits);
            Assert.Equal(EditOutcome.Carried, carried.Outcome);

            TestDesignState named = design.States[1];
            Assert.Equal("setName(to: alice)", named.By);

            // Still waiting as well: a person wrote one value, and the rules take any other.
            Assert.Contains(design.States[0].Moves, move => move.Text == "setName(to: ?)");
            Assert.Contains(design.States[0].Moves, move => move.Text == "setName(to: alice)" && move.Landings.Single().To == "#1");
        }

        [Fact]
        public void EveryValueABoundedSchemaAdmitsIsTriedAndWhatValidateRefusesIsNotLegal()
        {
            TestDesignState named = Derive(new TestDesignEdit("#0", "setName", Arguments(("to", "alice")))).States[1];

            // One is the party already, so `party.unchanged` refuses it.
            Assert.Equal(
                ["setParty(size: 2)", "setParty(size: 3)", "setParty(size: 4)", "setParty(size: 5)", "setParty(size: 6)"],
                named.Moves.Where(move => move.Input == "setParty").Select(move => move.Text));

            Assert.Equal(
                ["note(what: quiet)", "note(what: near the door)"],
                named.Moves.Where(move => move.Input == "note").Select(move => move.Text));

            Assert.All(named.Moves.Where(move => move.Input is "setParty" or "note"), move => Assert.Empty(move.Open));
        }

        [Fact]
        public void AWrittenValueTheRulesRefuseIsNotCarried()
        {
            TestDesign design = Derive(new TestDesignEdit("#0", "setName", Arguments(("to", "admin"))));

            Assert.Equal(EditOutcome.NotLegal, Assert.Single(design.Edits).Outcome);
            Assert.Single(design.States);
        }

        [Fact]
        public void WhatValidateReadsIsObserved()
        {
            // `name.unchanged` reads the name and `wants.tooManyForQuiet` the party, so two
            // positions that differ only there offer different values and are not one position.
            Assert.Equal(["stage", "name", "party", "seat", "wants"], ObservationScan.Of(Vocabulary.Read("signup")).Observed);
        }

        [Fact]
        public void AWaitingMoveSurvivesBeingWrittenAndReadBack()
        {
            TestDesign design = Derive();
            TestDesign read = TestDesign.FromJson(design.ToJson());

            Move waiting = Assert.Single(read.States[0].Moves);
            Assert.Equal("setName(to: ?)", waiting.Text);
            Assert.Equal(["to"], waiting.Open);
            Assert.Contains("\"open\": [", design.ToJson(), StringComparison.Ordinal);
        }

        private static TestDesign Derive(params TestDesignEdit[] edits) =>
            TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read("signup"), null, new WalkSettings(States: 300), edits);

        private static Dictionary<string, string> Arguments(params (string Name, string Value)[] arguments) =>
            arguments.ToDictionary(static a => a.Name, static a => a.Value, StringComparer.Ordinal);
    }
}
