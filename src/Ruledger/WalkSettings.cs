// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger
{
    /// <summary>How far a walk goes, and how hard it looks on the way.</summary>
    /// <remarks>
    /// <para>
    /// Each of the three is a count of the thing it limits, and is named after it, because a
    /// person reading a test design has no way to find out what a word like "budget" was a
    /// budget of.
    /// </para>
    /// <para>
    /// None of this is derived from the rule set, so all of it travels with the test design
    /// it produced. Two of them are only comparable when they were walked the same way — the
    /// same states in the same order is what makes a difference between them a difference
    /// the rules made.
    /// </para>
    /// <para>
    /// What is not here is a seed, and there is nowhere for one to go: the walk takes the
    /// legal inputs in the order the runtime returns them and never chooses among them.
    /// </para>
    /// </remarks>
    /// <param name="States">How many states may be visited before the walk stops.</param>
    /// <param name="Candidates">How many candidate inputs may be tried in one state.</param>
    /// <param name="Outcomes">How many outcomes may be followed for one input.</param>
    public sealed record WalkSettings(
        int States = 3_000,
        int Candidates = 10_000,
        int Outcomes = 64);
}
