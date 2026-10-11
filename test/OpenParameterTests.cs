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

        [Fact]
        public void WhatEachOpenParameterAdmitsIsRecordedOnceAsTheRuntimeAnsweredIt()
        {
            TestDesign design = Derive(new TestDesignEdit("#0", "setName", Arguments(("to", "alice"))));

            Assert.Equal(
                [
                    "setName(to) admits {\"op\":\"type.string\",\"maxLength\":12}",
                    "setParty(size) admits {\"op\":\"type.int\",\"min\":1,\"max\":6}",
                    "note(what) admits {\"op\":\"type.enum\",\"values\":[\"quiet\",\"near the door\"]}",
                ],
                design.Admits.Select(each => each.ToString()));

            Assert.Equal(design.Admits, TestDesign.FromJson(design.ToJson()).Admits);
        }

        [Fact]
        public void ALimitOnTextThatMovedIsADifferenceThoughNoMoveDid()
        {
            // The name is never enumerated, so no move shows twelve becoming twenty. What the
            // parameter admits does.
            string design = Derive().ToJson();
            string longer = Vocabulary.Read("signup").Replace("\"maxLength\": 12", "\"maxLength\": 20", StringComparison.Ordinal);

            TestDesignDiff diff = TestDesignDiff.Of(Vocabulary.Runtime, design, longer);

            Assert.Empty(diff.Changed);
            (Admission before, Admission after) = Assert.Single(diff.AdmitsMoved);
            Assert.Equal("{\"op\":\"type.string\",\"maxLength\":12}", before.Schema);
            Assert.Equal("{\"op\":\"type.string\",\"maxLength\":20}", after.Schema);
            Assert.False(diff.IsEmpty);
        }

        [Fact]
        public void AValueTriedAndRefusedIsWrittenDownWithWhatRefusedIt()
        {
            TestDesign design = Derive(new TestDesignEdit("#0", "setName", Arguments(("to", "alice"))));

            Assert.Equal(["setParty(size: 1) refused: party.unchanged"], design.States[1].Refused.Select(each => each.ToString()));
            Assert.Contains(
                design.States.SelectMany(state => state.Refused),
                each => each.ToString() == "note(what: quiet) refused: wants.tooManyForQuiet");

            TestDesign read = TestDesign.FromJson(design.ToJson());
            Assert.Equal(
                design.States.SelectMany(state => state.Refused).Select(each => each.ToString()),
                read.States.SelectMany(state => state.Refused).Select(each => each.ToString()));
        }

        [Fact]
        public void AValueTheSchemaRefusesIsWrittenDownUnderTheParametersInvalidOrWithNoCode()
        {
            // Thirteen letters where the field holds twelve: the schema refuses it before any clause
            // is asked. Where the parameter names that refusal with `invalid` it is a code like a
            // clause's; where it does not, there is no code to write down.
            string named = Vocabulary.Read("signup").Replace(
                "\"to\": { \"open\": { \"field\": \"name\" } }",
                "\"to\": { \"open\": { \"field\": \"name\" }, \"invalid\": \"name.malformed\" }",
                StringComparison.Ordinal);
            TestDesignEdit tooLong = new("#0", "setName", Arguments(("to", "Christabellas")));

            TestDesign withCode = TestDesign.Derive(Vocabulary.Runtime, named, null, new WalkSettings(States: 300), [tooLong]);
            TestDesign withNone = Derive(tooLong);

            Assert.Equal(["setName(to: Christabellas) refused: name.malformed"], withCode.States[0].Refused.Select(each => each.ToString()));
            Assert.Equal(["setName(to: Christabellas) refused"], withNone.States[0].Refused.Select(each => each.ToString()));
        }

        [Fact]
        public void ARefusalWhoseCodeMovedIsADifference()
        {
            string design = Derive(new TestDesignEdit("#0", "setName", Arguments(("to", "alice")))).ToJson();
            string renamed = Vocabulary.Read("signup").Replace("\"party.unchanged\"", "\"party.same\"", StringComparison.Ordinal);

            TestDesignDiff diff = TestDesignDiff.Of(Vocabulary.Runtime, design, renamed);

            StateChange first = diff.Changed[0];
            Assert.Equal(["setParty(size: 1) refused: party.same"], first.Refusing.Select(each => each.ToString()));
            Assert.Equal(["setParty(size: 1) refused: party.unchanged"], first.NotRefusing.Select(each => each.ToString()));
            Assert.Empty(first.Gained);
        }

        [Theory]
        [InlineData("3.0")]
        [InlineData("03")]
        public void AWholeNumberWrittenAnotherWayIsCarriedAndComesBackAsTheMoveIsWritten(string size)
        {
            TestDesign design = Derive(
                new TestDesignEdit("#0", "setName", Arguments(("to", "alice"))),
                new TestDesignEdit("#1", "setParty", Arguments(("size", size))));

            EditResult party = design.Edits[1];
            Assert.Equal(EditOutcome.Carried, party.Outcome);
            Assert.Equal("3", party.Edit.Arguments["size"]);

            // Taken first, which is what the choice was for.
            Assert.Equal("setParty(size: 3)", design.States[2].By);
            Assert.Contains("\"size\": \"3\"", design.ToJson(), StringComparison.Ordinal);
        }

        [Fact]
        public void ANumberWrittenWithDecimalsIsCarriedWhereNothingIsEnumerated()
        {
            // Without an upper bound the whole numbers cannot be named in full, so this is the
            // value a person wrote being followed rather than one the walk found by trying them
            // all — the same road an amount takes.
            string unbounded = Vocabulary.Read("signup").Replace(
                "\"party\": { \"op\": \"type.int\", \"min\": 1, \"max\": 6 }",
                "\"party\": { \"op\": \"type.int\", \"min\": 1 }",
                StringComparison.Ordinal);

            TestDesign design = TestDesign.Derive(Vocabulary.Runtime, unbounded, null, new WalkSettings(States: 300),
            [
                new TestDesignEdit("#0", "setName", Arguments(("to", "alice"))),
                new TestDesignEdit("#1", "setParty", Arguments(("size", "3.00"))),
                new TestDesignEdit("#1", "setParty", Arguments(("size", "3"))),
            ]);

            Assert.All(design.Edits, result => Assert.Equal(EditOutcome.Carried, result.Outcome));
            Assert.Equal("3", design.Edits[1].Edit.Arguments["size"]);

            // Two ways of writing one value are one move.
            Assert.Equal(
                ["setParty(size: ?)", "setParty(size: 3)"],
                design.States[1].Moves.Where(move => move.Input == "setParty").Select(move => move.Text));
        }

        [Fact]
        public void ABooleanWrittenInCapitalsIsCarried()
        {
            string flagged = Vocabulary.Read("signup")
                .Replace(
                    "\"wants\": { \"op\": \"type.enum\", \"values\": [\"quiet\", \"near the door\"], \"nullable\": true }",
                    "\"wants\": { \"op\": \"type.enum\", \"values\": [\"quiet\", \"near the door\"], \"nullable\": true }, \"vip\": { \"op\": \"type.bool\" }",
                    StringComparison.Ordinal)
                .Replace("\"wants\": null }", "\"wants\": null, \"vip\": false }", StringComparison.Ordinal)
                .Replace(
                    "\"inputs\": {",
                    "\"inputs\": { \"setVip\": { \"params\": { \"to\": { \"open\": { \"field\": \"vip\" } } }, \"effects\": [ { \"op\": \"state.set\", \"path\": \"vip\", \"value\": \"@to\" } ] },",
                    StringComparison.Ordinal);

            TestDesign design = TestDesign.Derive(Vocabulary.Runtime, flagged, null, new WalkSettings(States: 300),
                [new TestDesignEdit("#0", "setVip", Arguments(("to", "True")))]);

            // Nothing reads `vip`, so where it lands is #0 again; whether the choice was taken
            // is the whole of what this is about.
            EditResult vip = Assert.Single(design.Edits);
            Assert.Equal(EditOutcome.Carried, vip.Outcome);
            Assert.Equal("true", vip.Edit.Arguments["to"]);
        }

        private static TestDesign Derive(params TestDesignEdit[] edits) =>
            TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read("signup"), null, new WalkSettings(States: 300), edits);

        private static Dictionary<string, string> Arguments(params (string Name, string Value)[] arguments) =>
            arguments.ToDictionary(static a => a.Name, static a => a.Value, StringComparer.Ordinal);
    }
}
