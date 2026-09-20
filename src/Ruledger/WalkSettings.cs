// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger
{
    /// <summary>How far a walk goes, and how hard it looks on the way.</summary>
    /// <remarks>
    /// <para>
    /// None of this is derived from the rule set, so all of it travels with the design it
    /// produced. Two designs are only comparable when they were walked the same way — the
    /// same states in the same order is what makes a difference between them a difference
    /// the rules made.
    /// </para>
    /// <para>
    /// What is not here is a seed, and there is nowhere for one to go: the walk takes the
    /// legal inputs in the order the runtime returns them and never chooses among them.
    /// </para>
    /// </remarks>
    /// <param name="Budget">How many states may be visited before the walk stops.</param>
    /// <param name="ValidationLimit">How many candidates <c>GetValidInputs</c> may try in one state.</param>
    /// <param name="OutcomeLimit">How many outcomes <c>GetOutcomes</c> may return for one input.</param>
    public sealed record WalkSettings(
        int Budget = 3_000,
        int ValidationLimit = 10_000,
        int OutcomeLimit = 64);
}
