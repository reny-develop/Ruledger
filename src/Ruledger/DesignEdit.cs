// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger
{
    /// <summary>A choice a person made: from this state, take this input first.</summary>
    /// <remarks>
    /// <para>
    /// The whole of what a person writes. The machine chooses every state and every input;
    /// where the choice is not the one somebody wanted to look at, they replace the choice and
    /// the machine works out the rest — which inputs are legal from there, whether it ends,
    /// where each one goes. Writing an observation by hand would make it the one thing in a
    /// design that can be wrong.
    /// </para>
    /// <para>
    /// It names a state and an input, and neither of those is anything about the rule set's
    /// structure. Renaming a definition or reordering the arguments of one moves nothing here,
    /// and neither does adding a field to the state: a state is named after how the walk
    /// arrived, and adding a field leaves every route through it spelled the same.
    /// </para>
    /// </remarks>
    public sealed class DesignEdit
    {
        /// <summary>Initializes a new instance of the <see cref="DesignEdit"/> class.</summary>
        /// <param name="state">The name of the state to choose from, such as <c>#0</c>.</param>
        /// <param name="input">The name of the input to take first.</param>
        /// <param name="arguments">What to call it with, by parameter name. An input without parameters takes none.</param>
        public DesignEdit(string state, string input, IReadOnlyDictionary<string, string>? arguments = null)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(input);

            State = state;
            Input = input;
            Arguments = arguments ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>Gets the name of the state this chooses from.</summary>
        public string State { get; }

        /// <summary>Gets the name of the input to take first.</summary>
        public string Input { get; }

        /// <summary>Gets what to call it with, by parameter name.</summary>
        public IReadOnlyDictionary<string, string> Arguments { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Arguments.Count == 0
                ? $"{State}: {Input}"
                : $"{State}: {Input}({string.Join(", ", Arguments.Select(static argument => $"{argument.Key}: {argument.Value}"))})";
    }

    /// <summary>What became of an edit when the design was derived again.</summary>
    /// <remarks>
    /// A choice that could not be carried is reported and never quietly dropped back to what
    /// the machine would have picked. There are two ways to lose one, and they are different
    /// enough to be worth telling apart: the state is still there and the input is not legal
    /// in it any more, or the walk did not get that far this time.
    /// </remarks>
    public enum EditOutcome
    {
        /// <summary>The choice was taken.</summary>
        Carried,

        /// <summary>The state was reached and the input is not legal in it.</summary>
        NotLegal,

        /// <summary>The walk did not reach a state of that name inside the budget.</summary>
        NotReached,
    }

    /// <summary>One edit and what became of it.</summary>
    /// <param name="Edit">The choice a person made.</param>
    /// <param name="Outcome">Whether it was taken, and if not, why not.</param>
    public sealed record EditResult(DesignEdit Edit, EditOutcome Outcome);
}
