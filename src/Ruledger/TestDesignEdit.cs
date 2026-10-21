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
    /// test design that can be wrong.
    /// </para>
    /// <para>
    /// It names a state and an input, and neither of those is anything about the rule set's
    /// structure. Renaming a definition or reordering the arguments of one moves nothing here,
    /// and neither does adding a field to the state: a state is named after how the walk
    /// arrived, and adding a field leaves every route through it spelled the same.
    /// </para>
    /// </remarks>
    public sealed class TestDesignEdit
    {
        /// <summary>Initializes a new instance of the <see cref="TestDesignEdit"/> class.</summary>
        /// <param name="state">
        /// The name of the state to choose from, either written out as how the walk arrives
        /// there — <c>#0 + submit</c> — or as the number that state has in the test design
        /// being read, such as <c>#4</c>. The number names a state only inside the one it was
        /// read from, which is why a test design writes its choices out the long way: so that
        /// they survive the rule set changing.
        /// </param>
        /// <param name="input">The name of the input to take first.</param>
        /// <param name="arguments">What to call it with, by parameter name. An input without parameters takes none.</param>
        public TestDesignEdit(string state, string input, IReadOnlyDictionary<string, string>? arguments = null)
            : this(state, input, null, arguments)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TestDesignEdit"/> class, for an input at an instance.</summary>
        /// <param name="state">The name of the state to choose from, either way of writing it.</param>
        /// <param name="input">The name of the input to take first.</param>
        /// <param name="at">
        /// Which instance it is at, one key per collection on the way down, by alias, as an
        /// input document's <c>at</c> has it; null or empty for the rule set's own document. A
        /// key of a collection written as a schema that the walk could not name is how a person
        /// says which instance to look at: the walk goes to every key a choice names.
        /// </param>
        /// <param name="arguments">What to call it with, by parameter name. An input without parameters takes none.</param>
        public TestDesignEdit(
            string state,
            string input,
            IReadOnlyList<KeyValuePair<string, string>>? at,
            IReadOnlyDictionary<string, string>? arguments = null)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(input);

            State = state;
            Input = input;
            At = at ?? [];
            Arguments = arguments ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>Gets the name of the state this chooses from, either way of writing it.</summary>
        public string State { get; }

        /// <summary>Gets the name of the input to take first.</summary>
        public string Input { get; }

        /// <summary>Gets which instance it is at, one key per collection on the way down, or none.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> At { get; }

        /// <summary>Gets what to call it with, by parameter name.</summary>
        public IReadOnlyDictionary<string, string> Arguments { get; }

        /// <inheritdoc />
        public override string ToString() =>
            $"{State}: {Move.Write(Input, Arguments, null, [.. At.Select(static key => KeyValuePair.Create(key.Key, (string?)key.Value))])}";
    }

    /// <summary>What became of an edit when the test design was derived again.</summary>
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

        /// <summary>The walk stopped before it reached a state of that name.</summary>
        NotReached,
    }

    /// <summary>One edit and what became of it.</summary>
    /// <param name="Edit">
    /// The choice a person made. Where the state was walked, a value for a parameter left open
    /// is in the form the move it names is written in, so that <c>3.0</c> for a whole number
    /// comes back as <c>3</c>: that is the form it was compared in.
    /// </param>
    /// <param name="Outcome">Whether it was taken, and if not, why not.</param>
    /// <param name="Name">
    /// The name of the state it was found in, spelled out as how the walk arrived there, or
    /// null where no state of that name was walked. A choice can be written against the number
    /// a state has in the test design being read, and that number is a name only inside it;
    /// spelling it out is what carries the choice into the next version.
    /// </param>
    public sealed record EditResult(TestDesignEdit Edit, EditOutcome Outcome, string? Name = null);
}
