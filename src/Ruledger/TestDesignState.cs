// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;
using Rulealize;
using Rulealize.Abstraction.Value;

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
            IReadOnlyList<Move> moves,
            IReadOnlyList<Refusal>? refused = null)
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
            Refused = refused ?? [];
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

        /// <summary>Gets the values for a parameter left open that were tried here and refused, in the order they were tried.</summary>
        /// <remarks>
        /// Only for values the walk tried: every value of a schema it could name, and the values a
        /// person wrote. What the rules refused a value with is part of the answer, so the codes
        /// are carried; a value refused by something other than a clause has none.
        /// </remarks>
        public IReadOnlyList<Refusal> Refused { get; }

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
            IReadOnlyList<Landing> landings,
            double followed = 1,
            IReadOnlyList<string>? open = null)
        {
            Input = input;
            Arguments = arguments;
            Text = text;
            Actor = actor;
            Document = document;
            Landings = landings;
            Followed = followed;
            Open = open ?? [];
        }

        /// <summary>Gets the name of the input.</summary>
        public string Input { get; }

        /// <summary>Gets what it was called with, by parameter name, each in the text form the runtime writes.</summary>
        public IReadOnlyDictionary<string, string> Arguments { get; }

        /// <summary>Gets the input and its arguments, written as <c>place(at: c4)</c>.</summary>
        public string Text { get; }

        /// <summary>Gets whose move this is, or null where the rule set does not say.</summary>
        public string? Actor { get; }

        /// <summary>Gets the parameters this move is still waiting for, in declared order, or none.</summary>
        /// <remarks>
        /// A move offered with a parameter whose values nobody could enumerate, and for which
        /// no person wrote one. It is legal and it is not followed: where it would lead turns
        /// on a value the walk does not have, and it does not make one up.
        /// </remarks>
        public IReadOnlyList<string> Open { get; }

        /// <summary>Gets the input as a <c>rulealize/input/v1</c> document, ready to apply, or the empty string for a move still waiting for a value.</summary>
        public string Document { get; }

        /// <summary>Gets where applying it can land, which is more than one place where the rules draw.</summary>
        public IReadOnlyList<Landing> Landings { get; }

        /// <summary>Gets how much of the draw <see cref="Landings"/> is: one when all of it, less when the limit cut it short.</summary>
        /// <remarks>
        /// <para>
        /// The one thing about a draw that cutting it short does not leave visible. Truncating
        /// a list of legal inputs leaves a list of legal inputs, and the state says
        /// <c>truncated</c> beside it; truncating a draw leaves a distribution that no longer
        /// sums to one, and adding the branches up is not a way to find that out — floating
        /// point loses a little of the total whether the search ran to the end or not.
        /// </para>
        /// <para>
        /// The runtime answers it, so this is that answer carried rather than one worked out
        /// here — and carried only where the runtime also says the search stopped at the
        /// limit. A draw followed to its end comes to one but for what floating point loses,
        /// and less than one here means the limit and never that. A move the rules settle has
        /// one branch and all of it.
        /// </para>
        /// </remarks>
        public double Followed { get; }

        /// <inheritdoc />
        public override string ToString() => Text;

        /// <summary>Writes a move the way <see cref="Text"/> has it: <c>place(at: c4)</c>, and <c>setName(to: ?)</c> for one still waiting.</summary>
        internal static string Write(string input, IReadOnlyDictionary<string, string> arguments, IReadOnlyList<string>? open = null)
        {
            string[] parts =
            [
                .. arguments.Select(static argument => $"{argument.Key}: {argument.Value}"),
                .. (open ?? []).Select(static name => $"{name}: ?"),
            ];

            return parts.Length == 0 ? input : $"{input}({string.Join(", ", parts)})";
        }
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

        /// <summary>Gets what was drawn, written out for a person, or the empty string where nothing was.</summary>
        /// <remarks>
        /// The draws of <see cref="Draw"/> and nothing else of it, which is what tells one
        /// branch of an input from another. Worked out when it is asked for rather than when
        /// the branch is made, because the only thing that asks is a line being printed about
        /// a move that changed, and most moves do not.
        /// </remarks>
        public string Drew => Route.Drew(Draw);

        /// <summary>Gets the name of the state this lands in, or null when the walk stopped before it.</summary>
        /// <remarks>
        /// Null is not "there is nothing there". It is the one honest thing to say about a
        /// state the walk never reached, and it is counted in <see cref="TestDesign.Unreached"/>.
        /// </remarks>
        public string? To { get; internal set; }

        /// <inheritdoc />
        public override string ToString() => To ?? "(not reached)";
    }

    /// <summary>A value for a parameter left open that was tried in a state and refused.</summary>
    public sealed class Refusal
    {
        internal Refusal(string input, IReadOnlyDictionary<string, string> arguments, IReadOnlyList<string> codes)
        {
            Input = input;
            Arguments = arguments;
            Codes = codes;
        }

        /// <summary>Gets the name of the input.</summary>
        public string Input { get; }

        /// <summary>Gets what it was tried with, by parameter name, in the text form the runtime writes.</summary>
        public IReadOnlyDictionary<string, string> Arguments { get; }

        /// <summary>Gets the codes the rules refused it with, in the order the clauses are written.</summary>
        public IReadOnlyList<string> Codes { get; }

        /// <summary>Gets the input and its arguments, written as <see cref="Move.Text"/> would write them.</summary>
        public string Text => Move.Write(Input, Arguments);

        /// <inheritdoc />
        public override string ToString() =>
            Codes.Count == 0 ? $"{Text} refused" : $"{Text} refused: {string.Join(", ", Codes)}";
    }

    /// <summary>What a parameter the rule set leaves open admits, as the runtime answered it.</summary>
    /// <param name="Input">The input's name.</param>
    /// <param name="Parameter">The parameter's name.</param>
    /// <param name="Schema">
    /// The op of the schema admitting its value and the bounds that schema declares, as one JSON
    /// object: <c>{"op":"type.int","min":1,"max":6}</c>. Carried as the runtime gave them;
    /// nothing here reads the keys.
    /// </param>
    public sealed record Admission(string Input, string Parameter, string Schema)
    {
        /// <inheritdoc />
        public override string ToString() => $"{Input}({Parameter}) admits {Schema}";

        internal static Admission Of(string input, OpenParameter open)
        {
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("op", open.Op);
                foreach (KeyValuePair<string, RuleValue> field in open.Description.Fields)
                {
                    writer.WritePropertyName(field.Key);
                    Write(writer, field.Value);
                }

                writer.WriteEndObject();
            }

            return new Admission(input, open.Name, Encoding.UTF8.GetString(buffer.ToArray()));
        }

        private static void Write(Utf8JsonWriter writer, RuleValue value)
        {
            switch (value)
            {
                case TextValue text:
                    writer.WriteStringValue(text.Value);
                    break;

                case NumberValue number:
                    writer.WriteRawValue(RuleValue.FormatNumber(number.Value));
                    break;

                case BooleanValue flag:
                    writer.WriteBooleanValue(flag.Value);
                    break;

                case SequenceValue sequence:
                    writer.WriteStartArray();
                    foreach (RuleValue each in sequence)
                    {
                        Write(writer, each);
                    }

                    writer.WriteEndArray();
                    break;

                case RecordValue record:
                    writer.WriteStartObject();
                    foreach (KeyValuePair<string, RuleValue> field in record.Fields)
                    {
                        writer.WritePropertyName(field.Key);
                        Write(writer, field.Value);
                    }

                    writer.WriteEndObject();
                    break;

                default:
                    writer.WriteNullValue();
                    break;
            }
        }
    }
}
