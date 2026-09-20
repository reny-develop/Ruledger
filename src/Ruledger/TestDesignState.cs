// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger
{
    /// <summary>One state the walk visited, and everything that can be observed there.</summary>
    /// <remarks>
    /// Three things are observable about a state and all three are here: which inputs are
    /// legal, whether it is final and with what result, and where each legal input leads.
    /// Nothing else is written down, because nothing else can be checked.
    /// </remarks>
    public sealed class TestDesignState
    {
        internal TestDesignState(
            string name,
            string? from,
            string? by,
            string state,
            bool isTerminal,
            string? result,
            int evaluated,
            bool truncated,
            IReadOnlyList<Move> moves)
        {
            Name = name;
            From = from;
            By = by;
            State = state;
            IsTerminal = isTerminal;
            Result = result;
            Evaluated = evaluated;
            Truncated = truncated;
            Moves = moves;
        }

        /// <summary>Gets what this state is called, which is its position in the walk.</summary>
        /// <remarks>
        /// <c>#0</c> is where the rule set starts and the rest are numbered as they are first
        /// arrived at. Together with <see cref="From"/> and <see cref="By"/> the name says how
        /// the walk got here, and that is what an edit a person made is attached to: adding a
        /// field to the state changes every state and no route to one.
        /// </remarks>
        public string Name { get; }

        /// <summary>Gets the state this one was first reached from, or null for the opening position.</summary>
        /// <remarks>
        /// First reached, because a walk can arrive at the same state along more than one
        /// route and only takes the first: the rest are recorded as landing here.
        /// </remarks>
        public string? From { get; }

        /// <summary>Gets the input that first reached it, written as <c>place(at: c4)</c>, or null for the opening position.</summary>
        public string? By { get; }

        /// <summary>Gets the position itself, as a <c>rulealize/state/v1</c> document.</summary>
        /// <remarks>
        /// Carried so that a person running this by hand has somewhere to start, and so that
        /// what was walked can be applied again without walking to it.
        /// </remarks>
        public string State { get; }

        /// <summary>Gets a value indicating whether the rules call this state final.</summary>
        public bool IsTerminal { get; }

        /// <summary>Gets what the rules call the outcome, when the state is final.</summary>
        public string? Result { get; }

        /// <summary>Gets how many candidates had their guard evaluated to find <see cref="Moves"/>.</summary>
        /// <remarks>
        /// The denominator of "four of sixty-five". It is not fixed across a rule set: a
        /// parameter's domain may be read out of the state, in which case how many candidates
        /// there were is itself a thing about this position.
        /// </remarks>
        public int Evaluated { get; }

        /// <summary>Gets a value indicating whether the search for legal inputs stopped at the limit.</summary>
        /// <remarks>
        /// True means <see cref="Moves"/> is not all of them, and says so rather than letting
        /// a shortened list read as a complete one.
        /// </remarks>
        public bool Truncated { get; }

        /// <summary>Gets every input that is legal here, in the order the runtime offered them.</summary>
        /// <remarks>
        /// A final state still has its legal inputs listed and nothing beyond it walked. What
        /// the rules answer about a finished position is theirs to answer; stopping is the
        /// walk's decision, and it is not written into the observation.
        /// </remarks>
        public IReadOnlyList<Move> Moves { get; }

        /// <inheritdoc />
        public override string ToString() =>
            From is null ? Name : $"{Name} = {From} + {By}";
    }

    /// <summary>One input that is legal in a state, and where it goes.</summary>
    public sealed class Move
    {
        internal Move(
            string input,
            IReadOnlyDictionary<string, string> arguments,
            string text,
            string? actor,
            string document,
            IReadOnlyList<Landing> landings)
        {
            Input = input;
            Arguments = arguments;
            Text = text;
            Actor = actor;
            Document = document;
            Landings = landings;
        }

        /// <summary>Gets the name of the input.</summary>
        public string Input { get; }

        /// <summary>Gets what it was called with, by parameter name, each in the text form the runtime writes.</summary>
        public IReadOnlyDictionary<string, string> Arguments { get; }

        /// <summary>Gets the input and its arguments, written as <c>place(at: c4)</c>.</summary>
        public string Text { get; }

        /// <summary>Gets whose move this is, or null where the rule set does not say.</summary>
        public string? Actor { get; }

        /// <summary>Gets the input as a <c>rulealize/input/v1</c> document, ready to apply.</summary>
        public string Document { get; }

        /// <summary>Gets where applying it can land, which is more than one place where the rules draw.</summary>
        public IReadOnlyList<Landing> Landings { get; }

        /// <inheritdoc />
        public override string ToString() => Text;
    }

    /// <summary>One place applying an input can land.</summary>
    /// <remarks>
    /// An input that draws nothing has exactly one of these, of probability one. An input
    /// that draws has one per branch, and which of them happens is not the mover's to decide.
    /// </remarks>
    public sealed class Landing
    {
        internal Landing(double probability, string? draw)
        {
            Probability = probability;
            Draw = draw;
        }

        /// <summary>Gets how likely this branch is.</summary>
        public double Probability { get; }

        /// <summary>Gets what was drawn, as a <c>rulealize/outcome/v1</c> document, or null where nothing was.</summary>
        public string? Draw { get; }

        /// <summary>Gets the name of the state this lands in, or null when the budget stopped before it.</summary>
        /// <remarks>
        /// Null is not "there is nothing there". It is the one honest thing to say about a
        /// state the walk never reached, and it is counted in <see cref="TestDesign.Unreached"/>.
        /// </remarks>
        public string? To { get; internal set; }

        /// <inheritdoc />
        public override string ToString() => To ?? "(not reached)";
    }
}
