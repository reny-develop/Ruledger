// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Tests
{
    /// <summary>The one place a person writes, and what becomes of what they wrote.</summary>
    public class DesignEditTests
    {
        // The machine picks every state and every input. Where the pick is not the one somebody
        // wanted to look at, they replace the pick — and everything after it is worked out
        // again rather than written down.
        [Fact]
        public void AChoiceDecidesWhichWayTheWalkGoesFirst()
        {
            Assert.Equal("approve", Derive().States[1].Moves[0].Input);
            Assert.Equal("#2", Derive().States[1].Moves[0].Landings[0].To);

            Design edited = Derive(Reject("timing"));

            // The reject is now what #1 leads to first, so it is #2 and approve is #3. Nothing
            // about what is legal in #1 moved: that is an observation, and it is still the four
            // the runtime offered, in the order it offered them.
            Assert.Equal("approve", edited.States[1].Moves[0].Input);
            Assert.Equal("#3", edited.States[1].Moves[0].Landings[0].To);
            Assert.Equal("#2 = #1 + reject(reason: timing)", edited.States[2].ToString());
        }

        [Fact]
        public void AChoiceThatWasTakenSaysSo()
        {
            EditResult carried = Assert.Single(Derive(Reject("cost")).Edits);

            Assert.Equal(EditOutcome.Carried, carried.Outcome);
        }

        // Two ways to lose one, and they are not the same thing. Saying which is the whole
        // point of reporting it at all.
        [Fact]
        public void AChoiceThatIsNotLegalWhereItWasMadeSaysThat()
        {
            EditResult lost = Assert.Single(Derive(new DesignEdit("#0", "approve")).Edits);

            Assert.Equal(EditOutcome.NotLegal, lost.Outcome);
        }

        [Fact]
        public void AChoiceInAStateTheWalkNeverReachedSaysThat()
        {
            EditResult lost = Assert.Single(Derive(new DesignEdit("#900", "approve")).Edits);

            Assert.Equal(EditOutcome.NotReached, lost.Outcome);
        }

        // Nothing is ever quietly dropped back to the machine's own pick: every choice a person
        // made is in the design that comes out, carried or not.
        [Fact]
        public void EveryChoiceIsInTheDesignThatComesOut()
        {
            Design design = Derive(Reject("cost"), new DesignEdit("#900", "approve"));

            Assert.Equal(2, design.Edits.Count);
            Assert.Equal(2, Design.FromJson(design.ToJson()).Edits.Count);
            Assert.Contains("\"carried\": \"not reached\"", design.ToJson(), StringComparison.Ordinal);
        }

        [Fact]
        public void ChoicesSurviveBeingWrittenDownAndReadBack()
        {
            Design design = Derive(Reject("timing"));

            EditResult read = Assert.Single(Design.FromJson(design.ToJson()).Edits);

            Assert.Equal("#1", read.Edit.State);
            Assert.Equal("reject", read.Edit.Input);
            Assert.Equal("timing", read.Edit.Arguments["reason"]);
            Assert.Equal(EditOutcome.Carried, read.Outcome);
        }

        private static DesignEdit Reject(string reason) =>
            new("#1", "reject", new Dictionary<string, string>(StringComparer.Ordinal) { ["reason"] = reason });

        private static Design Derive(params DesignEdit[] edits) =>
            Design.Derive(
                Vocabulary.Runtime,
                Vocabulary.Read("approval"),
                null,
                new WalkSettings(Budget: 300),
                edits);
    }
}
