// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rulealize;
using Rulealize.Abstraction;
using Rulealize.Abstraction.Store;
using Rulealize.Abstraction.Value;

namespace Ruledger
{
    /// <summary>A test design: the states a walk visited, and what is observable at each.</summary>
    /// <remarks>
    /// <para>
    /// It is derived, and it is tautological. Applied to the rule set it came from it always
    /// passes, because it says what that rule set decides and never what it should decide.
    /// Reading it against what was meant is the part that is not here.
    /// </para>
    /// <para>
    /// The walk takes every legal input from each state and follows one route to the end
    /// before coming back, because the alternative never finishes a game: spreading out by
    /// depth collects openings and reaches no ending at all, and an ending is where a result
    /// is observed. Nothing is chosen at random and there is no seed — the same rule set
    /// walked with the same settings visits the same states in the same order, which is what
    /// makes a difference between two designs a difference the rules made.
    /// </para>
    /// </remarks>
    public sealed class TestDesign
    {
        internal TestDesign(
            string ruleSet,
            WalkSettings settings,
            string? start,
            IReadOnlyList<string> observed,
            IReadOnlyList<string> collapsed,
            IReadOnlyList<TestDesignState> states,
            IReadOnlyList<EditResult> edits,
            int unreached,
            IReadOnlyList<Admission>? admits = null,
            IReadOnlyList<string>? projections = null,
            IReadOnlyList<KeyAdmission>? keys = null)
        {
            RuleSet = ruleSet;
            Settings = settings;
            Start = start;
            Observed = observed;
            Collapsed = collapsed;
            States = states;
            Edits = edits;
            Unreached = unreached;
            Admits = admits ?? [];
            Projections = projections;
            Keys = keys ?? [];
        }

        /// <summary>Gets what the rule set calls itself, as <c>id@version</c>.</summary>
        public string RuleSet { get; }

        /// <summary>Gets how the walk was told to go, which is recorded because none of it is derived.</summary>
        public WalkSettings Settings { get; }

        /// <summary>Gets the state the walk began in, as a <c>rulealize/state/v1</c> document, where it was given one.</summary>
        /// <remarks>
        /// For a rule set that keeps instances of another as documents of their own, the
        /// position is several documents, and this is them as a JSON array of
        /// <c>{ "at": "claims/1", "state": … }</c>, the rule set's own at <c>/</c> first and the
        /// instances in the order they were created.
        /// </remarks>
        /// <remarks>
        /// <para>
        /// Null where the walk began where the rule set itself begins. A rule set's initial
        /// state is often only the shape of a case — who may sign, what may be requested —
        /// with nothing in it yet, and the case is a state document of its own. Which case was
        /// walked is not derived from the rule set, so it travels in the test design like the
        /// settings do, and the document itself travels rather than where it was read from:
        /// whatever replays this begins where the walk began, even after the file has moved on.
        /// </para>
        /// <para>
        /// A choice is held by how the walk arrived at a state, and #0 is wherever the walk
        /// began, so two test designs that began in different states name different states the
        /// same. They are not compared with each other.
        /// </para>
        /// </remarks>
        public string? Start { get; }

        /// <summary>Gets the state a position was compared on, in the order the schema declares it.</summary>
        public IReadOnlyList<string> Observed { get; }

        /// <summary>Gets the state that was dropped before comparing, because nothing observable reads it.</summary>
        public IReadOnlyList<string> Collapsed { get; }

        /// <summary>Gets the states, in the order they were first arrived at.</summary>
        public IReadOnlyList<TestDesignState> States { get; }

        /// <summary>Gets the choices a person made, and what became of each.</summary>
        /// <remarks>
        /// Every one of them, carried or not. A choice that could not be taken is reported and
        /// never quietly dropped back to what the machine would have picked.
        /// </remarks>
        public IReadOnlyList<EditResult> Edits { get; }

        /// <summary>Gets what each parameter the rule set leaves open admits, in the order the walk first met it.</summary>
        /// <remarks>
        /// <para>
        /// A move waiting for a value is one move whatever its schema allows, so a bound widened
        /// or a choice added leaves the list of legal moves where it was. Where the walk tried
        /// every value, the moves beside it show the change; where it could not, as with text,
        /// nothing else in the test design would. This is what the runtime answered about the
        /// parameter, carried rather than interpreted, so that what a value may be is observed
        /// for every open parameter and not only for the ones whose values can be counted.
        /// </para>
        /// <para>
        /// Once per parameter and not per state: it is read off the schema the parameter is open
        /// to, which the position does not change.
        /// </para>
        /// </remarks>
        public IReadOnlyList<Admission> Admits { get; }

        /// <summary>Gets the names of the projections the rule set declares, in the order it declares them, or null where the test design did not record them.</summary>
        /// <remarks>
        /// <para>
        /// Once and not per state, for the reason <see cref="Admits"/> is: which projections
        /// there are is read off the rule set, which the position does not change, and a
        /// projection added or taken away is one thing that happened and not one per state.
        /// </para>
        /// <para>
        /// Null for a test design written before projections were observed, which said nothing
        /// about them; that is not the same as a rule set that declares none.
        /// </para>
        /// </remarks>
        public IReadOnlyList<string>? Projections { get; }

        /// <summary>Gets what the keys of each collection written as a schema admit, in the order the rule set declares the collections.</summary>
        /// <remarks>
        /// Once, for the reason <see cref="Admits"/> is. Such a collection has an instance at
        /// every key its schema admits; the walk goes to every one where the schema names them,
        /// and otherwise only to the keys a person wrote in a choice's <c>at</c>, so a bound that
        /// moved would show nowhere else. Empty for a rule set that keeps no such collection.
        /// </remarks>
        public IReadOnlyList<KeyAdmission> Keys { get; }

        /// <summary>Gets how many landings there were no states left to visit.</summary>
        /// <remarks>
        /// Not "how much is missing", which nobody can say. It is how many places the walk was
        /// standing in front of when it ran out, and it is reported rather than rounded off, so
        /// that where a walk stopped for want of states is never read as what is not there.
        /// </remarks>
        public int Unreached { get; }

        /// <summary>Walks a rule set and writes down what is observable along the way.</summary>
        /// <param name="runtime">A runtime with the vocabularies the rule set draws on loaded.</param>
        /// <param name="ruleSet">The rule set, as text.</param>
        /// <param name="settings">How far to go. The default is three thousand states.</param>
        /// <returns>The test design.</returns>
        /// <exception cref="RuleSetBuildException">The text is not a rule set this runtime can compile.</exception>
        public static TestDesign Derive(RuleRuntime runtime, string ruleSet, WalkSettings? settings = null) =>
            Derive(runtime, ruleSet, null, settings);

        /// <summary>Walks a rule set that holds others, and writes down what is observable along the way.</summary>
        /// <param name="runtime">A runtime with the vocabularies the rule set draws on loaded.</param>
        /// <param name="ruleSet">The rule set, as text.</param>
        /// <param name="components">The document of every rule set reachable through <c>uses</c>, by identifier.</param>
        /// <param name="settings">How far to go. The default is three thousand states.</param>
        /// <param name="edits">Choices a person made, which the walk takes first where it can.</param>
        /// <returns>The test design.</returns>
        /// <remarks>
        /// Splitting a rule set into a composite and the parts it holds is a way of writing it,
        /// not a different set of rules, so the test design does not turn on which way it was
        /// written: the composite is walked, exactly as the one document it stands for would
        /// be. Only the names differ, because a composite offers a component's input under the
        /// alias it gave it.
        /// </remarks>
        /// <exception cref="RuleSetBuildException">The text is not a rule set this runtime can compile.</exception>
        /// <exception cref="InvalidOperationException">A rule set named in <c>uses</c> was not supplied.</exception>
        public static TestDesign Derive(
            RuleRuntime runtime,
            string ruleSet,
            IReadOnlyDictionary<string, string>? components,
            WalkSettings? settings = null,
            IReadOnlyList<TestDesignEdit>? edits = null)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(ruleSet);

            return Derive(runtime, ruleSet, components, settings, ObservationScan.Of(ruleSet, components), edits);
        }

        /// <summary>Walks a rule set from a state of a case, and writes down what is observable along the way.</summary>
        /// <param name="runtime">A runtime with the vocabularies the rule set draws on loaded.</param>
        /// <param name="ruleSet">The rule set, as text.</param>
        /// <param name="components">The document of every rule set reachable through <c>uses</c>, by identifier, or null where it holds none.</param>
        /// <param name="start">The <c>rulealize/state/v1</c> document to begin in, in place of the rule set's own initial state.</param>
        /// <param name="settings">How far to go, or null for three thousand states.</param>
        /// <param name="edits">Choices a person made, which the walk takes first where it can, or null for none.</param>
        /// <returns>The test design, with <see cref="Start"/> set to the document it began in.</returns>
        /// <remarks>
        /// <para>
        /// For a rule set whose initial state is the shape of a case and not one: a list of who
        /// may sign that is empty until somebody says who. The state is read against the rule
        /// set's schema, a composite's components included, exactly as any other state the
        /// runtime is handed; a component's state may leave out the <c>ruleSet</c> it belongs
        /// to, and is then the same state as one that names it.
        /// </para>
        /// <para>
        /// The choices are carried one by one, as they are when the rule set changes: each is
        /// held by how the walk arrived at a state, and each that does not name a legal move
        /// from where it now leads is reported as such. What a choice was made against is not
        /// asked, because a person who handed in both the start and the choices has said which
        /// they meant.
        /// </para>
        /// </remarks>
        /// <exception cref="RuleSetBuildException">The text is not a rule set this runtime can compile.</exception>
        /// <exception cref="RuleDocumentException">The start is not a state of this rule set.</exception>
        /// <exception cref="InvalidOperationException">A rule set named in <c>uses</c> was not supplied.</exception>
        public static TestDesign Derive(
            RuleRuntime runtime,
            string ruleSet,
            IReadOnlyDictionary<string, string>? components,
            string start,
            WalkSettings? settings,
            IReadOnlyList<TestDesignEdit>? edits)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(ruleSet);
            ArgumentNullException.ThrowIfNull(start);

            return Derive(runtime, ruleSet, components, settings, ObservationScan.Of(ruleSet, components), edits, start);
        }

        /// <summary>Walks a rule set that keeps instances of another as documents of their own, and writes down what is observable along the way.</summary>
        /// <param name="runtime">A runtime with the vocabularies the rule set draws on loaded, its store plugins among them.</param>
        /// <param name="ruleSet">The rule set, as text.</param>
        /// <param name="components">The document of every rule set reachable through <c>uses</c>, by identifier, or null where it holds none.</param>
        /// <param name="starts">The state documents a <c>many</c> names as its <c>start</c>, by name, or null where none does.</param>
        /// <param name="start">
        /// The position to begin in, or null for the rule set's own initial state. For a rule set
        /// that keeps instances, a JSON array of <c>{ "at": …, "state": … }</c> as
        /// <see cref="TestDesignState.Documents"/> has them; otherwise a <c>rulealize/state/v1</c>
        /// document.
        /// </param>
        /// <param name="settings">How far to go, or null for three thousand states.</param>
        /// <param name="edits">Choices a person made, which the walk takes first where it can, or null for none.</param>
        /// <returns>The test design.</returns>
        /// <remarks>
        /// <para>
        /// A rule set whose <c>uses</c> holds something <c>many</c> keeps each instance as its own
        /// document, in a store its <c>requires</c> names. The walk does not open that store:
        /// evaluation is pure, so where the documents are kept changes no answer, and it gives the
        /// runtime a <see cref="MemoryStateStore"/> in its place with
        /// <see cref="RuleRuntime.UseStore(MemoryStateStore)"/>, which the runtime keeps for the
        /// contexts created from it afterwards. The store plugins are still loaded, because the
        /// rule set is built against them.
        /// </para>
        /// <para>
        /// Moves are asked of the rule set's own document and of every instance in the position,
        /// and written with the <c>at</c> they were found at. A collection whose keys are written
        /// as a schema has an instance at every key it admits: where the schema names them all,
        /// every one is walked, and otherwise only the keys a person wrote in a choice's
        /// <c>at</c>, with a move in every state waiting for the rest. The machine does not make
        /// up a key, as it does not make up a value.
        /// </para>
        /// </remarks>
        /// <exception cref="RuleSetBuildException">The text is not a rule set this runtime can compile.</exception>
        /// <exception cref="RuleDocumentException">The start is not a position of this rule set.</exception>
        /// <exception cref="InvalidOperationException">A rule set named in <c>uses</c> was not supplied.</exception>
        public static TestDesign Derive(
            RuleRuntime runtime,
            string ruleSet,
            IReadOnlyDictionary<string, string>? components,
            IReadOnlyDictionary<string, string>? starts,
            string? start,
            WalkSettings? settings,
            IReadOnlyList<TestDesignEdit>? edits)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(ruleSet);

            return Derive(runtime, ruleSet, components, settings, ObservationScan.Of(ruleSet, components), edits, start, starts);
        }

        /// <summary>Reads a test design back.</summary>
        /// <param name="testDesign">A <c>ruledger/test-design/v5</c> document, or a v4, v3, v2 or v1 one.</param>
        /// <returns>What it says.</returns>
        /// <exception cref="JsonException">The text is not JSON.</exception>
        /// <exception cref="InvalidOperationException">The text is not a test design.</exception>
        public static TestDesign FromJson(string testDesign) => TestDesignDocument.Read(testDesign);

        /// <summary>Writes this test design as a <c>ruledger/test-design/v5</c> document.</summary>
        /// <returns>The document.</returns>
        /// <remarks>
        /// This is the test design — what a person reads against what they meant, what they put
        /// their choices into, and what gets committed. A line-oriented rendering of it is a
        /// view of this, and not a second copy of it.
        /// </remarks>
        public string ToJson() => TestDesignDocument.Write(this);

        internal static TestDesign Derive(
            RuleRuntime runtime,
            string ruleSet,
            IReadOnlyDictionary<string, string>? components,
            WalkSettings? settings,
            ObservationScan observation,
            IReadOnlyList<TestDesignEdit>? edits = null,
            string? start = null,
            IReadOnlyDictionary<string, string>? starts = null)
        {
            // Only a rule set that keeps instances is given a store: one that keeps none is one
            // document, and the runtime is left as the host gave it.
            MemoryStateStore? store = null;
            if (observation.Kept.Count > 0)
            {
                store = new MemoryStateStore();
                runtime.UseStore(store);
            }

            Walker walker = new(
                runtime.CreateContext(ruleSet, components, starts),
                observation,
                settings ?? new WalkSettings(),
                start,
                edits ?? [],
                store);

            return walker.Walk();
        }

        private sealed class Walker(
            RuleContext rules,
            ObservationScan observation,
            WalkSettings settings,
            string? start,
            IReadOnlyList<TestDesignEdit> edits,
            MemoryStateStore? store)
        {
            private readonly Dictionary<string, string> seen = new(StringComparer.Ordinal);

            // For a rule set that keeps instances, each step followed, by what it reads, and the
            // state it was followed from. See Stepping.
            private readonly Dictionary<string, string> stepped = new(StringComparer.Ordinal);
            private readonly List<Entry> order = [];
            private readonly Stack<Entry> descending = new();
            private readonly Dictionary<TestDesignEdit, EditResult> outcomes = [];

            // How the walk got where it is standing, which is the name of the state it is
            // standing in. It grows and shrinks with the descent, so it costs the depth of the
            // walk and not its size.
            private readonly StringBuilder here = new("#0");
            private int unreached;
            private readonly List<Admission> admits = [];

            public TestDesign Walk()
            {
                // A start somebody wrote is put to the runtime before anything else reads it, so
                // that one which is not a state of this rule set is refused in the runtime's
                // words, naming the field that is wrong, rather than failing somewhere in here.
                Position opening = store is null
                    ? new Position(start ?? rules.InitialState, null)
                    : Position.Of(start, rules.InitialState);

                if (start is not null)
                {
                    rules.GetTerminalStatus(opening.Root);
                }

                Arrive(opening, null, null);

                // The recursion is written out because its depth is the depth of the walk, and
                // only the count of states it may visit keeps that bounded: a rule set holding a
                // history never returns to a state it has been in, so one route can be as long
                // as that count.
                while (this.descending.Count > 0)
                {
                    Entry entry = this.descending.Peek();
                    if (entry.Next >= entry.Ahead.Count)
                    {
                        this.here.Length = this.descending.Pop().Back;
                        continue;
                    }

                    Step step = entry.Ahead[entry.Next++];
                    step.Landing.To = Arrive(step.Position, entry.Name, step);
                }

                return new TestDesign(
                    rules.RuleSet,
                    settings,
                    start,
                    observation.Observed,
                    observation.Collapsed,
                    [.. this.order.Select(static entry => entry.Close())],
                    [.. edits.Select(edit =>
                        this.outcomes.TryGetValue(edit, out EditResult? result)
                            ? result
                            : new EditResult(edit, EditOutcome.NotReached))],
                    this.unreached,
                    this.admits,
                    rules.Projections,
                    [.. observation.Kept
                        .Where(static collection => collection.Key is not null)
                        .Select(static collection => new KeyAdmission(collection.Name, JsonNode.Parse(collection.Key!)!.ToJsonString(Compact)))]);
            }

            // A composite keeps each component's state under its alias, and keeps it as a state
            // document rather than as bare fields, so `req.stage` is one step through the alias
            // and one through what the component wrote. Told by the alias and not by the shape:
            // the runtime names the component in what it writes, and a person writing a start
            // need not, so a document that leaves `ruleSet` out is still the same state.
            private JsonElement? Locate(JsonElement data, string field)
            {
                JsonElement node = data;
                string[] segments = field.Split('.');
                for (int at = 0; at < segments.Length; at++)
                {
                    if (at > 0
                        && observation.Held.Contains(string.Join('.', segments, 0, at))
                        && node.ValueKind == JsonValueKind.Object
                        && node.TryGetProperty("data", out JsonElement held))
                    {
                        node = held;
                    }

                    if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(segments[at], out node))
                    {
                        return null;
                    }
                }

                return node;
            }

            private static bool Matches(TestDesignEdit edit, Move move) =>
                string.Equals(edit.Input, move.Input, StringComparison.Ordinal)
                && SameAt(edit.At, move.At)
                && edit.Arguments.Count == move.Arguments.Count
                && edit.Arguments.All(argument =>
                    move.Arguments.TryGetValue(argument.Key, out string? value)
                    && string.Equals(value, argument.Value, StringComparison.Ordinal));

            private static bool SameAt(IReadOnlyList<KeyValuePair<string, string>> written, IReadOnlyList<KeyValuePair<string, string?>> found) =>
                written.Count == found.Count
                && written.Zip(found).All(static pair =>
                    string.Equals(pair.First.Key, pair.Second.Key, StringComparison.Ordinal)
                    && string.Equals(pair.First.Value, pair.Second.Value, StringComparison.Ordinal));

            private string? Arrive(Position position, string? from, Step? step)
            {
                string key = Key(position);
                if (this.seen.TryGetValue(key, out string? seenAs))
                {
                    return seenAs;
                }

                if (this.order.Count >= settings.States)
                {
                    this.unreached++;
                    return null;
                }

                // Numbered on arrival rather than on departure, so that a state's name says
                // where the walk found it and the first thing #0 leads to is #1. The number is
                // the short way to write the name; how the walk arrived is the whole of it, and
                // it is the only one of the two that means the same thing in the next version.
                string name = "#" + this.order.Count;
                this.seen[key] = name;

                int back = this.here.Length;
                if (step is not null)
                {
                    this.here.Append(" + ").Append(Route.Step(step.By, step.Landing.Draw));
                }

                Entry entry = new(name, from, step?.By, position) { Back = back };
                this.order.Add(entry);

                TerminalStatus terminal = rules.GetTerminalStatus(position.Root);
                entry.IsTerminal = terminal.IsTerminal;
                entry.Result = terminal.Result;

                foreach (string projection in rules.Projections)
                {
                    entry.Projections.Add(Project(projection, position));
                }

                List<ValidInput> offered = [];
                List<List<Step>> onward = [];
                foreach (ValidInputSet legal in Offered(position, name, entry, onward))
                {
                    entry.Evaluated += legal.Evaluated;
                    entry.Truncated |= legal.Truncated;
                    offered.AddRange(legal);

                    foreach (ValidInput input in legal)
                    {
                        if (input.IsComplete)
                        {
                            Follow(entry, onward, position, terminal, input, Settled(input), Written(input), input.ToInputDocument(rules.RuleSet));
                            continue;
                        }

                        Fill(entry, onward, name, position, terminal, input);
                    }
                }

                entry.Ahead.AddRange(Ordered(name, entry.Moves, onward, offered));
                this.descending.Push(entry);
                return name;
            }

            // Where moves are asked. A rule set that keeps no instances is one document and is
            // asked once. One that keeps them is asked at its own document and at every instance
            // in the position, in the order they were created, because the runtime finds moves
            // one place at a time; then at every key a collection written as a schema names, or
            // a person wrote, that nothing has been written at yet. Where the schema does not
            // name its keys, a move waiting for one is written down, so that a position with
            // nothing else on offer is not taken for a final one.
            private IEnumerable<ValidInputSet> Offered(Position position, string name, Entry entry, List<List<Step>> onward)
            {
                if (store is null)
                {
                    yield return rules.GetValidInputs(position.Root, settings.Candidates);
                    yield break;
                }

                List<StateAddress> addresses = [StateAddress.Root, .. position.Documents!.Skip(1).Select(static document => document.Key)];
                for (int at = 0; at < addresses.Count; at++)
                {
                    StateAddress holder = addresses[at];
                    string[] aliases = [.. holder.Segments.Where((_, index) => index % 2 == 0)];

                    foreach (ObservationScan.Collection collection in observation.Kept.Where(each =>
                        each.Key is not null
                        && each.Path.Count == aliases.Length + 1
                        && each.Path.Take(aliases.Length).SequenceEqual(aliases, StringComparer.Ordinal)))
                    {
                        string alias = collection.Path[^1];
                        IReadOnlyList<string>? every = collection.Keys();

                        foreach (string key in every ?? Chosen(holder, alias, name))
                        {
                            StateAddress instance = holder.Child(alias, key);
                            if (!addresses.Contains(instance))
                            {
                                addresses.Add(instance);
                            }
                        }

                        if (every is null)
                        {
                            KeyValuePair<string, string?>[] waiting = [.. At(holder), KeyValuePair.Create(alias, (string?)null)];
                            entry.Moves.Add(new Move(
                                collection.Name,
                                new Dictionary<string, string>(StringComparer.Ordinal),
                                Move.Write(collection.Name, new Dictionary<string, string>(), null, waiting),
                                null,
                                string.Empty,
                                [],
                                open: ["at." + alias],
                                at: waiting));
                            onward.Add([]);
                        }
                    }
                }

                foreach (StateAddress address in addresses)
                {
                    store.Load(position.Documents!);
                    yield return rules.GetValidInputsAsync(address, settings.Candidates).GetAwaiter().GetResult();
                }
            }

            // The keys a person wrote for an instance of a collection the walk could not list,
            // in the order the choices are written, from a choice made in this state.
            private IEnumerable<string> Chosen(StateAddress holder, string alias, string name)
            {
                int depth = holder.Segments.Length / 2;
                HashSet<string> named = new(StringComparer.Ordinal);

                foreach (TestDesignEdit edit in edits.Where(edit => Names(edit, name)))
                {
                    if (edit.At.Count > depth
                        && edit.At[depth].Key == alias
                        && edit.At.Take(depth).Select(static key => key.Value).SequenceEqual(holder.Segments.Where((_, index) => index % 2 == 1), StringComparer.Ordinal)
                        && named.Add(edit.At[depth].Value))
                    {
                        yield return edit.At[depth].Value;
                    }
                }
            }

            // Asked of every state and not only of the ones a host happens to show, and written
            // as the runtime answered: a fault is the answer where there is one, because a
            // projection moves nothing and the walk does not need it to go on.
            private Projection Project(string projection, Position position)
            {
                string answer;
                try
                {
                    if (store is null)
                    {
                        answer = rules.Project(projection, position.Root);
                    }
                    else
                    {
                        store.Load(position.Documents!);
                        answer = rules.ProjectAsync(projection).GetAwaiter().GetResult();
                    }
                }
                catch (RuleEvaluationException fault)
                {
                    return new Projection(projection, null, fault.Message);
                }

                JsonNode? value = JsonNode.Parse(answer);
                return new Projection(
                    projection,
                    value is JsonObject or JsonArray ? value.ToJsonString(Compact) : Text(value),
                    null);
            }

            // A move offered with a parameter left open is waiting for a value from outside,
            // and there are two honest things to do with it. Where the schema it is open to
            // admits few enough values to name them all — an enumeration, a boolean, a whole
            // number between two bounds — every one of them is tried, exactly as a domain
            // would have been, and the ones the rules refuse are not legal moves. Where it
            // does not, as with text, the walk follows only the values a person wrote as
            // choices, and writes the move down as legal and waiting for the rest. Guessing a
            // value would be the machine supplying judgement, which is the one thing it
            // refuses to do.
            private void Fill(Entry entry, List<List<Step>> onward, string name, Position position, TerminalStatus terminal, ValidInput input)
            {
                foreach (OpenParameter open in input.Open)
                {
                    if (!this.admits.Any(each => each.Input == input.Input && each.Parameter == open.Name))
                    {
                        this.admits.Add(Admission.Of(input.Input, open));
                    }
                }

                IReadOnlyList<Dictionary<string, JsonNode?>>? every = Every(input.Open, entry);

                if (every is null)
                {
                    string[] waiting = [.. input.Open.Select(static open => open.Name)];
                    entry.Moves.Add(new Move(
                        input.Input,
                        Settled(input),
                        Move.Write(input.Input, Settled(input), waiting, At(input.At)),
                        input.Actor,
                        string.Empty,
                        [],
                        open: waiting,
                        at: At(input.At)));
                    onward.Add([]);
                }

                foreach (Dictionary<string, JsonNode?> values in every ?? Written(input, name))
                {
                    Dictionary<string, string> arguments = Settled(input);
                    foreach (OpenParameter open in input.Open)
                    {
                        arguments[open.Name] = Text(values[open.Name]);
                    }

                    string document = input.ToInputDocument(rules.RuleSet, values!);
                    entry.Evaluated++;

                    // Applying it is how a value is found to be one the rules admit: what
                    // `validate` refuses is not a legal move, however the guard answered. It
                    // is written down as refused, with the codes the rules gave, because a
                    // value that was tried and turned away is as much an answer as one that
                    // was taken — and the only one a later version can take back without
                    // the list of legal moves showing it.
                    try
                    {
                        if (store is null)
                        {
                            rules.GetOutcomes(document, position.Root, 1);
                        }
                        else
                        {
                            Apply(position, document);
                        }
                    }
                    catch (InputRejectedException rejected)
                    {
                        entry.Refused.Add(new Refusal(input.Input, arguments, [.. rejected.Rejections.Select(static each => each.Code)], At(input.At)));
                        continue;
                    }
                    catch (IllegalInputException)
                    {
                        entry.Refused.Add(new Refusal(input.Input, arguments, [], At(input.At)));
                        continue;
                    }

                    Follow(entry, onward, position, terminal, input, arguments, Move.Write(input.Input, arguments, null, At(input.At)), document);
                }
            }

            private void Follow(
                Entry entry,
                List<List<Step>> onward,
                Position position,
                TerminalStatus terminal,
                ValidInput input,
                Dictionary<string, string> arguments,
                string text,
                string document)
            {
                List<Landing> landings = [];
                List<Step> ahead = [];
                double followed = 1;

                // Past the end there is nothing to reach, so the moves a rule set still
                // offers are written down and none of them is followed.
                if (!terminal.IsTerminal && store is not null)
                {
                    // Nothing a rule set that keeps instances does draws yet, so applying the
                    // input settles where it goes.
                    Landing landing = new(1, null);
                    landings.Add(landing);

                    string stepping = Stepping(position, text, input.At);
                    if (this.stepped.TryGetValue(stepping, out string? followedAt))
                    {
                        landing.As = followedAt;
                    }
                    else
                    {
                        this.stepped[stepping] = entry.Name;
                        ahead.Add(new Step(landing, Apply(position, document), text));
                    }
                }
                else if (!terminal.IsTerminal)
                {
                    OutcomeSet outcomes = rules.GetOutcomes(document, position.Root, settings.Outcomes);

                    // How much of the draw these branches are. Kept because it is the one
                    // thing the branches themselves do not say: a distribution the limit
                    // cut short is one that does not sum to one, and what floating point
                    // loses on the way makes adding them up no way to tell.
                    //
                    // Which is also why the runtime's answer is taken only where the
                    // runtime says the search stopped. Thirteen branches of a thirteen
                    // card draw come to 0.9999999999999996, and a walk that wrote that
                    // down would be saying a draw was cut short that was not.
                    followed = outcomes.Truncated ? outcomes.Coverage : 1;

                    foreach (Outcome outcome in outcomes)
                    {
                        Landing landing = new(
                            outcome.Probability,
                            outcome.Draws.IsEmpty ? null : outcome.ToOutcomeDocument(rules.RuleSet));

                        landings.Add(landing);
                        ahead.Add(new Step(landing, new Position(outcome.Result.State, null), text));
                    }
                }

                entry.Moves.Add(new Move(
                    input.Input,
                    arguments,
                    text,
                    input.Actor,
                    document,
                    landings,
                    followed,
                    at: At(input.At)));

                onward.Add(ahead);
            }

            // Where an input leads from a position kept in the store: the position put back,
            // the input applied and committed, and what the store then holds read out. Keys are
            // the store's, and a memory store's follow from the documents, so the same position
            // and input give the same key whichever route the walk took there.
            private Position Apply(Position position, string document)
            {
                store!.Load(position.Documents!);
                rules.ApplyAsync(document).GetAwaiter().GetResult();
                return Position.Read(store.Documents, rules.InitialState);
            }

            // What a step reads: its text, the rule set's own document as it is compared, and each
            // instance on the way to where it is, whole.
            //
            // A rule set that keeps instances can always create another, so a walk that follows
            // every move from every position goes on creating them and reaches nothing else. It
            // need not: Rulealize refuses a guard that reads a document its transition does not
            // write, so whether a step is legal and what it writes turn on these documents and no
            // others, and a step already followed from the same ones decides nothing new. It is
            // still written down as legal, with `as` naming where it was followed.
            //
            // That settles what the step decides, not what follows it, and verify/Keeping.cs is
            // the measurement that nothing is lost: every step a few claims can take, walked to the
            // end without this, is among the steps this walk takes.
            private string Stepping(Position position, string text, StateAddress at)
            {
                StringBuilder key = new(text);
                key.Append('|').Append(Key(position.Root));

                StateAddress on = StateAddress.Root;
                for (int segment = 0; segment + 1 < at.Segments.Length; segment += 2)
                {
                    on = on.Child(at.Segments[segment], at.Segments[segment + 1]);
                    string? state = position.Documents!.FirstOrDefault(document => document.Key.Equals(on)).Value;
                    key.Append('|').Append(on).Append('=');
                    if (state is not null)
                    {
                        using JsonDocument document = JsonDocument.Parse(state);
                        Canonical.Write(document.RootElement.GetProperty("data"), key);
                    }
                }

                return key.ToString();
            }

            // Every value each open parameter's schema admits, and every way of putting them
            // together, or null where one of them cannot be named in full.
            //
            // The schema is read by its op, which puts three names from TypeSchema in here
            // beside the ones ObservationScan knows. The runtime hands the op over for exactly
            // this — the bounds come as a record whose keys mean what that vocabulary says
            // they mean — and the alternative, reading `min` and `max` without it, would count
            // the whole numbers between the bounds of a field that holds fractions.
            private IReadOnlyList<Dictionary<string, JsonNode?>>? Every(OpenParameterList open, Entry entry)
            {
                List<Dictionary<string, JsonNode?>> every = [new(StringComparer.Ordinal)];

                foreach (OpenParameter parameter in open)
                {
                    if (Values(parameter) is not { } values)
                    {
                        return null;
                    }

                    every = [.. every.SelectMany(so => values.Select(value =>
                        new Dictionary<string, JsonNode?>(so, StringComparer.Ordinal) { [parameter.Name] = value?.DeepClone() }))];

                    // More than the walk may try in one state is cut where the guard's own
                    // search would have been cut, and marked the same way.
                    if (every.Count > settings.Candidates)
                    {
                        every.RemoveRange(settings.Candidates, every.Count - settings.Candidates);
                        entry.Truncated = true;
                    }
                }

                return every;
            }

            private IReadOnlyList<JsonNode?>? Values(OpenParameter parameter)
            {
                RecordValue bounds = parameter.Description;

                switch (parameter.Op)
                {
                    case "type.enum" when bounds["values"] is SequenceValue values:
                        return [.. values.Select(Json)];

                    case "type.bool":
                        return [JsonValue.Create(false), JsonValue.Create(true)];

                    case "type.int" when bounds["min"] is NumberValue min && bounds["max"] is NumberValue max
                        && max.Value - min.Value < settings.Candidates:
                        List<JsonNode?> range = [];
                        for (decimal at = decimal.Ceiling(min.Value); at <= max.Value; at++)
                        {
                            range.Add(JsonValue.Create(at));
                        }

                        return range;

                    default:
                        return null;
                }
            }

            // The values a person wrote for a move nobody could enumerate, one choice each.
            // A choice that names a value the rules then refuse is not carried, and says so.
            // Two choices that name one value however it was written are one move.
            private IEnumerable<Dictionary<string, JsonNode?>> Written(ValidInput input, string name)
            {
                Dictionary<string, string> settled = Settled(input);
                HashSet<string> named = new(StringComparer.Ordinal);
                IReadOnlyList<KeyValuePair<string, string?>> at = At(input.At);

                foreach (TestDesignEdit edit in edits.Where(edit => edit.Input == input.Input && SameAt(edit.At, at) && Names(edit, name)))
                {
                    if (!input.Open.All(open => edit.Arguments.ContainsKey(open.Name))
                        || !settled.All(argument => edit.Arguments.TryGetValue(argument.Key, out string? value) && value == argument.Value))
                    {
                        continue;
                    }

                    Dictionary<string, JsonNode?> values = input.Open.ToDictionary(
                        static open => open.Name,
                        open => Parse(open, edit.Arguments[open.Name]),
                        StringComparer.Ordinal);

                    if (named.Add(Move.Write(input.Input, values.ToDictionary(static each => each.Key, each => Text(each.Value), StringComparer.Ordinal))))
                    {
                        yield return values;
                    }
                }
            }

            // A move the walk found complete, written as the runtime writes it, with where it is
            // put after the name where it is at an instance.
            private static string Written(ValidInput input) =>
                input.At.IsRoot ? input.ToString() : Move.Write(input.Input, Settled(input), null, At(input.At));

            // An address as a move's `at` has it: each collection on the way down and its key.
            private static IReadOnlyList<KeyValuePair<string, string?>> At(StateAddress address) =>
            [
                .. Enumerable.Range(0, address.Segments.Length / 2)
                    .Select(index => KeyValuePair.Create(address.Segments[2 * index], (string?)address.Segments[(2 * index) + 1])),
            ];

            // What a person wrote is text, and "2" is not 2: the schema's op says which it is.
            private static JsonNode? Parse(OpenParameter open, string text) => open.Op switch
            {
                "type.int" or "type.decimal" when decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number) =>
                    JsonValue.Create(number),
                "type.bool" when bool.TryParse(text, out bool flag) => JsonValue.Create(flag),
                _ => JsonValue.Create(text),
            };

            private static JsonNode? Json(RuleValue value) => value switch
            {
                TextValue text => JsonValue.Create(text.Value),
                NumberValue number => JsonValue.Create(number.Value),
                BooleanValue flag => JsonValue.Create(flag.Value),
                _ => null,
            };

            // One line, and a card written `♠` rather than escaped, since a person reads it.
            private static readonly JsonSerializerOptions Compact = new()
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            // In the text form the runtime writes an argument in, so that a value somebody
            // filled in and one a domain produced read the same.
            private static string Text(JsonNode? value) => value switch
            {
                null => "null",
                JsonValue text when text.GetValueKind() == JsonValueKind.String => text.GetValue<string>(),
                JsonValue number when number.GetValueKind() == JsonValueKind.Number =>
                    RuleValue.FormatNumber(decimal.Parse(number.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture)),
                _ => value.ToJsonString(),
            };

            // What a person wrote for a parameter left open, in the form the walk writes that
            // argument in, so that a choice of 3.0 names the move written 3: the walk goes down
            // a written value by what it is, and whether it was carried is asked of the same
            // thing. The schema's op is what says "3.0" is a number at all, so an argument the
            // rule set settled itself is left as it was written.
            private static TestDesignEdit Spelled(TestDesignEdit edit, IReadOnlyList<ValidInput> legal)
            {
                Dictionary<string, string> arguments = new(StringComparer.Ordinal);
                foreach ((string parameter, string text) in edit.Arguments)
                {
                    OpenParameter? open = legal
                        .Where(input => input.Input == edit.Input)
                        .SelectMany(static input => input.Open)
                        .FirstOrDefault(open => open.Name == parameter);

                    arguments[parameter] = open is null ? text : Text(Parse(open, text));
                }

                return new TestDesignEdit(edit.State, edit.Input, edit.At, arguments);
            }

            private static Dictionary<string, string> Settled(ValidInput input) =>
                input.Arguments.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);

            // The one place a person's writing reaches. Which inputs are legal here is an
            // observation and keeps the order the runtime offered them in; which one the walk
            // goes down first is a choice, and that is what an edit replaces.
            private IEnumerable<Step> Ordered(string name, List<Move> moves, List<List<Step>> onward, IReadOnlyList<ValidInput> legal)
            {
                List<int> taken = [];
                foreach (TestDesignEdit written in edits.Where(edit => Names(edit, name)))
                {
                    TestDesignEdit edit = Spelled(written, legal);
                    int found = moves.FindIndex(move => Matches(edit, move));

                    // Written back with the name spelled out, whichever way it was written
                    // here: the number is only a name inside the test design it was read from.
                    // The values go back as the move is written for the same reason.
                    this.outcomes[written] = new EditResult(
                        edit,
                        found < 0 ? EditOutcome.NotLegal : EditOutcome.Carried,
                        this.here.ToString());

                    if (found >= 0 && !taken.Contains(found))
                    {
                        taken.Add(found);
                    }
                }

                return taken
                    .Concat(Enumerable.Range(0, onward.Count).Where(index => !taken.Contains(index)))
                    .SelectMany(index => onward[index]);
            }

            // A state has two names and this answers to either. The number is the one a person
            // reads off the test design in front of them; how the walk arrived is the one that still
            // means the same place after the rule set changes, and it is what a choice read out
            // of an earlier test design is written with.
            private bool Names(TestDesignEdit edit, string name) =>
                string.Equals(edit.State, name, StringComparison.Ordinal)
                || this.here.Equals(edit.State.AsSpan());

            // Two states are the same state when they agree on everything an observation can
            // depend on. Anything else a rule set keeps — an audit trail, a move history — is
            // dropped here, or a walk through a rule set that keeps one would never return to a
            // position it had already been in.
            //
            // An instance kept as a document of its own is compared whole, and where it is and
            // in what order it was created are part of the position. Keeping too much costs
            // only how often the walk rejoins.
            private string Key(Position position)
            {
                string own = Key(position.Root);
                if (position.Documents is null)
                {
                    return own;
                }

                StringBuilder key = new(own);
                foreach ((StateAddress address, string state) in position.Documents.Skip(1))
                {
                    using JsonDocument document = JsonDocument.Parse(state);
                    key.Append('|').Append(address).Append('=');
                    Canonical.Write(
                        document.RootElement.TryGetProperty("data", out JsonElement data) ? data : document.RootElement,
                        key);
                }

                return key.ToString();
            }

            private string Key(string state)
            {
                using JsonDocument document = JsonDocument.Parse(state);
                if (!document.RootElement.TryGetProperty("data", out JsonElement data))
                {
                    return state;
                }

                StringBuilder key = new();
                foreach (string field in observation.Observed)
                {
                    key.Append(field).Append('=');
                    if (Locate(data, field) is JsonElement value)
                    {
                        Canonical.Write(value, key);
                    }

                    key.Append(';');
                }

                return key.ToString();
            }

            private sealed record Step(Landing Landing, Position Position, string By);

            private sealed class Entry(string name, string? from, string? by, Position position)
            {
                public string Name => name;

                public bool IsTerminal { get; set; }

                public string? Result { get; set; }

                public int Evaluated { get; set; }

                public bool Truncated { get; set; }

                public int Next { get; set; }

                /// <summary>How much of the walk's route belongs to the state this was reached from.</summary>
                public int Back { get; init; }

                public List<Move> Moves { get; } = [];

                public List<Step> Ahead { get; } = [];

                public List<Refusal> Refused { get; } = [];

                public List<Projection> Projections { get; } = [];

                public TestDesignState Close() =>
                    new(name, from, by, position.Root, IsTerminal, Result, Evaluated, Truncated, Moves, Refused, Projections,
                        position.Documents?.Select(static document => KeyValuePair.Create(document.Key.ToString(), document.Value)).ToList());
            }
        }

        /// <summary>A position: the rule set's own document, and every instance it keeps where it keeps any.</summary>
        /// <param name="Root">The rule set's own document.</param>
        /// <param name="Documents">
        /// Every document of the position, the rule set's own first and the instances in the
        /// order they were created, or null for a rule set that keeps none.
        /// </param>
        internal sealed record Position(string Root, IReadOnlyList<KeyValuePair<StateAddress, string>>? Documents)
        {
            /// <summary>Reads what a store holds, putting the rule set's own document first where nothing has written it yet.</summary>
            public static Position Read(IReadOnlyList<KeyValuePair<StateAddress, string>> kept, string initial)
            {
                string root = kept.FirstOrDefault(static document => document.Key.IsRoot).Value ?? initial;
                return new Position(root, [KeyValuePair.Create(StateAddress.Root, root), .. kept.Where(static document => !document.Key.IsRoot)]);
            }

            /// <summary>Reads a start written as a JSON array of <c>{ "at": …, "state": … }</c>, or begins in the initial state.</summary>
            /// <exception cref="RuleDocumentException">The start is not a list of documents by address.</exception>
            public static Position Of(string? start, string initial)
            {
                if (start is null)
                {
                    return Read([], initial);
                }

                using JsonDocument written = JsonDocument.Parse(start);
                if (written.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new RuleDocumentException(
                        "This rule set keeps instances as documents of their own, so a position is several documents: "
                        + "a start is a JSON array of { \"at\": ..., \"state\": ... }, the rule set's own at \"/\".");
                }

                return Read(
                    [.. written.RootElement.EnumerateArray().Select(static document => KeyValuePair.Create(
                        Address(document.GetProperty("at").GetString() ?? "/"),
                        document.GetProperty("state").GetRawText()))],
                    initial);
            }

            /// <summary>Reads an address as <see cref="StateAddress.ToString"/> writes it: <c>/</c>, or <c>months/2026-10/claims/1</c>.</summary>
            public static StateAddress Address(string written)
            {
                StateAddress address = StateAddress.Root;
                if (written == "/")
                {
                    return address;
                }

                string[] segments = written.Split('/');
                for (int at = 0; at + 1 < segments.Length; at += 2)
                {
                    address = address.Child(segments[at], segments[at + 1]);
                }

                return address;
            }
        }
    }
}
