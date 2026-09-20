// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Ruledger.Tests
{
    /// <summary>The design as it is written down, read and committed.</summary>
    public class DesignDocumentTests
    {
        // Same rule set, same settings, the same bytes. Committing a design and running the
        // tool again rests on this: a diff that is not empty has to mean the rules moved.
        [Theory]
        [InlineData("approval")]
        [InlineData("reversi")]
        [InlineData("blackjack")]
        public void TheSameWalkTwiceWritesTheSameBytes(string ruleSet)
        {
            Assert.Equal(Derive(ruleSet).ToJson(), Derive(ruleSet).ToJson());
        }

        [Theory]
        [InlineData("approval")]
        [InlineData("reversi")]
        [InlineData("blackjack")]
        public void WhatIsWrittenReadsBackAsWhatItWas(string ruleSet)
        {
            string written = Derive(ruleSet).ToJson();

            Assert.Equal(written, Design.FromJson(written).ToJson());
        }

        [Fact]
        public void ADesignSaysWhichRuleSetAndHowFarItWasTold()
        {
            JsonElement design = Parse(Derive("approval").ToJson());

            Assert.Equal("ruledger/design/v1", design.GetProperty("$schema").GetString());
            Assert.Equal("approval@1.0.0", design.GetProperty("ruleSet").GetString());
            Assert.Equal(300, design.GetProperty("settings").GetProperty("budget").GetInt32());
            Assert.Equal(["stage"], design.GetProperty("observed").EnumerateArray().Select(field => field.GetString()));
            Assert.Equal(["reason"], design.GetProperty("collapsed").EnumerateArray().Select(field => field.GetString()));
        }

        // A state carries the position itself, exactly as the runtime writes one, so a person
        // stepping through this by hand has somewhere to start and Ruledger can apply an input
        // to it again without walking back to it.
        [Fact]
        public void AStateCarriesThePositionAsTheRuntimeWritesOne()
        {
            JsonElement opening = Parse(Derive("approval").ToJson()).GetProperty("states")[0];

            Assert.Equal("#0", opening.GetProperty("name").GetString());
            Assert.False(opening.TryGetProperty("from", out _));
            Assert.Equal("approval@1.0.0", opening.GetProperty("state").GetProperty("ruleSet").GetString());
            Assert.Equal("draft", opening.GetProperty("state").GetProperty("data").GetProperty("stage").GetString());
        }

        [Fact]
        public void AStateSaysHowTheWalkGotThere()
        {
            JsonElement second = Parse(Derive("approval").ToJson()).GetProperty("states")[1];

            Assert.Equal("#0", second.GetProperty("from").GetString());
            Assert.Equal("submit", second.GetProperty("by").GetString());
        }

        [Fact]
        public void AMoveSaysWhatItIsCalledWithAndWhereItGoes()
        {
            JsonElement rejecting = Parse(Derive("approval").ToJson())
                .GetProperty("states")[1]
                .GetProperty("moves")
                .EnumerateArray()
                .First(move => move.GetProperty("input").GetString() == "reject");

            Assert.Equal("scope", rejecting.GetProperty("args").GetProperty("reason").GetString());
            Assert.Equal(JsonValueKind.String, rejecting.GetProperty("to").ValueKind);
        }

        // Where the rules draw, one input has a landing per branch with what was drawn and how
        // likely it was. Where they draw nothing there is one place it goes, so it says so.
        [Fact]
        public void WhereTheRulesDrawAMoveLandsInMoreThanOnePlace()
        {
            JsonElement drawn = Parse(Derive("blackjack").ToJson())
                .GetProperty("states")
                .EnumerateArray()
                .SelectMany(state => state.GetProperty("moves").EnumerateArray())
                .First(move => move.TryGetProperty("lands", out _));

            JsonElement first = drawn.GetProperty("lands")[0];

            Assert.True(first.GetProperty("probability").GetDouble() < 1.0);
            Assert.Equal("blackjack@1.0.0", first.GetProperty("drew").GetProperty("ruleSet").GetString());
        }

        // The budget stopped in front of somewhere, and that is what is said, rather than the
        // move being left out as though it went nowhere.
        [Fact]
        public void ALandingTheBudgetStoppedBeforeIsWrittenAsNothingReached()
        {
            JsonElement design = Parse(Design
                .Derive(Vocabulary.Runtime, Vocabulary.Read("reversi"), new WalkSettings(Budget: 20))
                .ToJson());

            int stopped = design.GetProperty("states")
                .EnumerateArray()
                .SelectMany(state => state.GetProperty("moves").EnumerateArray())
                .Count(move => move.TryGetProperty("to", out JsonElement to) && to.ValueKind == JsonValueKind.Null);

            Assert.Equal(design.GetProperty("unreached").GetInt32(), stopped);
            Assert.True(stopped > 0);
        }

        // Past the end the moves the rules still offer are written down and none of them leads
        // anywhere: a move with no landing at all is a move in a state the rules call final.
        [Fact]
        public void AMoveInAFinalStateLeadsNowhere()
        {
            JsonElement ending = Parse(Derive("approval").ToJson())
                .GetProperty("states")
                .EnumerateArray()
                .First(state => state.TryGetProperty("terminal", out _));

            Assert.Equal("approved", ending.GetProperty("terminal").GetProperty("result").GetString());
            Assert.All(
                ending.GetProperty("moves").EnumerateArray(),
                move =>
                {
                    Assert.False(move.TryGetProperty("to", out _));
                    Assert.False(move.TryGetProperty("lands", out _));
                });
        }

        // One object per state, in the order the walk arrived, which is everything a
        // line-oriented rendering of this needs.
        [Fact]
        public void StatesAreOneObjectEachInTheOrderTheyWereReached()
        {
            JsonElement states = Parse(Derive("reversi").ToJson()).GetProperty("states");

            Assert.Equal(
                Enumerable.Range(0, states.GetArrayLength()).Select(index => "#" + index),
                states.EnumerateArray().Select(state => state.GetProperty("name").GetString()));
        }

        private static Design Derive(string ruleSet) =>
            Design.Derive(Vocabulary.Runtime, Vocabulary.Read(ruleSet), new WalkSettings(Budget: 300));

        private static JsonElement Parse(string design) => JsonDocument.Parse(design).RootElement.Clone();
    }
}
