// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Ruledger
{
    /// <summary>The <c>ruledger/test-design/v5</c> document: a test design, written down.</summary>
    /// <remarks>
    /// <para>
    /// A fourth document beside the three Rulealize already reads, and made of them: a state
    /// travels as a <c>rulealize/state/v1</c>, an input as the name and arguments an
    /// <c>rulealize/input/v1</c> carries, a draw as what a <c>rulealize/outcome/v1</c> carries.
    /// Nothing is transcribed into a shape of its own, so a person stepping through this by
    /// hand and Ruledger applying it again are reading the same thing.
    /// </para>
    /// <para>
    /// A state is one object and they are in the order the walk arrived at them, which is what
    /// a line-oriented rendering needs and the only thing it needs. That rendering is a view of
    /// this; it is not a second copy to be kept in step.
    /// </para>
    /// <para>
    /// Where the rules draw nothing, applying an input settles the next state, so a move says
    /// <c>to</c>. Where they draw, it says <c>lands</c>, one entry per branch with what was
    /// drawn and how likely it was — the same distinction Rulealize makes by leaving an outcome
    /// document out when there is nothing in it — and <c>followed</c> beside it where the limit
    /// cut the draw short of all of it. <c>"to": null</c> is a landing the walk had no states
    /// left to visit; a move with neither is a move in a state the rules call final, offered
    /// and not followed.
    /// </para>
    /// <para>
    /// v2 adds one shape to v1: a move that says <c>open</c> is legal and waiting for a value
    /// nobody could enumerate, so it too has neither <c>to</c> nor <c>lands</c>. A v1 reader would
    /// take it for a move in a final state, which is why the version moved. Everything v1 wrote
    /// is still v2, so a v1 document is read as one.
    /// </para>
    /// <para>
    /// v3 adds <c>start</c>, the state the walk began in where it was given one. A v2 reader
    /// passes over a key it does not know, and would have read a walk of one case as a walk
    /// from the rule set's own initial state and replayed it from there, which is why the
    /// version moved again. A document without it began where the rule set begins, so
    /// everything v2 wrote is still v3.
    /// </para>
    /// <para>
    /// v4 adds <c>projections</c>: the names a rule set declares, once, and what each says
    /// about every state. Observing them moves which fields two positions are compared on, so
    /// a v4 walk is not the walk a v3 document recorded, and a v3 document is read as one that
    /// said nothing about projections rather than as one whose rule set declares none.
    /// </para>
    /// <para>
    /// v5 adds what a rule set that keeps instances as documents of their own needs: a state
    /// whose position is several documents writes <c>documents</c> in place of <c>state</c>, a
    /// move, a refusal and an edit at an instance say <c>at</c>, a move waiting for a key says
    /// so, and <c>keys</c> says once what each collection written as a schema admits. A v4
    /// reader would take a state with no <c>state</c> for a malformed one and every move at an
    /// instance for the same move at the rule set's own document. A rule set that keeps none
    /// writes what v4 wrote, so a v4 document is read as a v5 one.
    /// </para>
    /// </remarks>
    internal static class TestDesignDocument
    {
        private const string Schema = "ruledger/test-design/v5";

        // What earlier Ruledgers wrote, read as they stand: every v4 document is a v5 document
        // whose rule set keeps no instances, every v3 document is a v4 document
        // that recorded no projections, every v2 document is a v3 document that began where
        // the rule set begins, and every v1 document is a v2 document that happens to have no
        // move waiting for a value.
        private static readonly string[] Earlier =
            ["ruledger/test-design/v4", "ruledger/test-design/v3", "ruledger/test-design/v2", "ruledger/test-design/v1"];

        // The version that first recorded projections. A document older than it said nothing about them.
        private static readonly string[] Unprojected = ["ruledger/test-design/v3", "ruledger/test-design/v2", "ruledger/test-design/v1"];

        public static string Write(TestDesign design)
        {
            using MemoryStream buffer = new();

            // Written for somebody to read. The escaping JSON defaults to is there for a
            // document that will be pasted into a web page, and it would spell the name of
            // every state `#0 + submit` and every card `♠` on the way to a file.
            JsonWriterOptions options = new()
            {
                Indented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            using (Utf8JsonWriter writer = new(buffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("$schema", Schema);
                writer.WriteString("ruleSet", design.RuleSet);

                writer.WriteStartObject("settings");
                writer.WriteNumber("states", design.Settings.States);
                writer.WriteNumber("candidates", design.Settings.Candidates);
                writer.WriteNumber("outcomes", design.Settings.Outcomes);
                writer.WriteEndObject();

                // Beside the settings and not among them: those say how far the walk went, and
                // this is where it went from. Left out where the rule set's own initial state is.
                if (design.Start is not null)
                {
                    writer.WritePropertyName("start");
                    Embed(writer, design.Start);
                }

                WriteNames(writer, "observed", design.Observed);
                WriteNames(writer, "collapsed", design.Collapsed);
                WriteAdmits(writer, design.Admits);
                WriteKeys(writer, design.Keys);

                // Left out where the rule set declares none, as `admits` is where nothing is
                // open; the schema is what says whether a document could have said any.
                if (design.Projections is { Count: > 0 } projections)
                {
                    WriteNames(writer, "projections", projections);
                }

                writer.WriteNumber("unreached", design.Unreached);

                WriteEdits(writer, design.Edits);

                writer.WriteStartArray("states");
                foreach (TestDesignState state in design.States)
                {
                    WriteState(writer, state);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }

        public static TestDesign Read(string testDesign)
        {
            ArgumentNullException.ThrowIfNull(testDesign);

            using JsonDocument document = JsonDocument.Parse(testDesign);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("$schema", out JsonElement schema)
                || schema.GetString() is not string named
                || (named != Schema && !Earlier.Contains(named)))
            {
                throw new InvalidOperationException($"This is not a '{Schema}' document.");
            }

            JsonElement settings = root.GetProperty("settings");

            return new TestDesign(
                root.GetProperty("ruleSet").GetString() ?? string.Empty,
                new WalkSettings(
                    settings.GetProperty("states").GetInt32(),
                    settings.GetProperty("candidates").GetInt32(),
                    settings.GetProperty("outcomes").GetInt32()),
                root.TryGetProperty("start", out JsonElement start) && start.ValueKind == JsonValueKind.Object ? start.GetRawText() : null,
                ReadNames(root, "observed"),
                ReadNames(root, "collapsed"),
                [.. root.GetProperty("states").EnumerateArray().Select(ReadState)],
                [.. root.GetProperty("edits").EnumerateArray().Select(ReadEdit)],
                root.GetProperty("unreached").GetInt32(),
                ReadAdmits(root),
                Unprojected.Contains(named) ? null
                    : root.TryGetProperty("projections", out _) ? ReadNames(root, "projections")
                    : [],
                ReadKeys(root));
        }

        // By collection, in the order the rule set declares them. Left out where there is none.
        private static void WriteKeys(Utf8JsonWriter writer, IReadOnlyList<KeyAdmission> keys)
        {
            if (keys.Count == 0)
            {
                return;
            }

            writer.WriteStartObject("keys");
            foreach (KeyAdmission each in keys)
            {
                writer.WritePropertyName(each.Collection);
                Embed(writer, each.Schema);
            }

            writer.WriteEndObject();
        }

        private static IReadOnlyList<KeyAdmission> ReadKeys(JsonElement root) =>
            root.TryGetProperty("keys", out JsonElement keys) && keys.ValueKind == JsonValueKind.Object
                ? [.. keys.EnumerateObject().Select(static each => new KeyAdmission(each.Name, JsonSerializer.Serialize(each.Value)))]
                : [];

        // An `at` as an input document has it: by alias, outermost first. A key a move is
        // waiting for is null. Left out where there is none.
        private static void WriteAt(Utf8JsonWriter writer, IEnumerable<KeyValuePair<string, string?>> at)
        {
            bool started = false;
            foreach ((string alias, string? key) in at)
            {
                if (!started)
                {
                    writer.WriteStartObject("at");
                    started = true;
                }

                if (key is null)
                {
                    writer.WriteNull(alias);
                }
                else
                {
                    writer.WriteString(alias, key);
                }
            }

            if (started)
            {
                writer.WriteEndObject();
            }
        }

        private static IReadOnlyList<KeyValuePair<string, string?>> ReadAt(JsonElement node) =>
            node.TryGetProperty("at", out JsonElement at) && at.ValueKind == JsonValueKind.Object
                ? [.. at.EnumerateObject().Select(static each => KeyValuePair.Create(
                    each.Name,
                    each.Value.ValueKind == JsonValueKind.String ? each.Value.GetString() : null))]
                : [];

        private static void WriteNames(Utf8JsonWriter writer, string name, IReadOnlyList<string> names)
        {
            writer.WriteStartArray(name);
            foreach (string field in names)
            {
                writer.WriteStringValue(field);
            }

            writer.WriteEndArray();
        }

        // By input, then by parameter, in the order the walk met them. Left out where nothing
        // is open, so a rule set without an open parameter writes what it always wrote.
        private static void WriteAdmits(Utf8JsonWriter writer, IReadOnlyList<Admission> admits)
        {
            if (admits.Count == 0)
            {
                return;
            }

            writer.WriteStartObject("admits");
            foreach (IGrouping<string, Admission> input in admits.GroupBy(static each => each.Input))
            {
                writer.WriteStartObject(input.Key);
                foreach (Admission each in input)
                {
                    writer.WritePropertyName(each.Parameter);
                    Embed(writer, each.Schema);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static IReadOnlyList<Admission> ReadAdmits(JsonElement root)
        {
            if (!root.TryGetProperty("admits", out JsonElement admits) || admits.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return
            [
                .. admits.EnumerateObject().SelectMany(static input => input.Value.EnumerateObject().Select(parameter =>
                    new Admission(input.Name, parameter.Name, JsonSerializer.Serialize(parameter.Value)))),
            ];
        }

        private static void WriteEdits(Utf8JsonWriter writer, IReadOnlyList<EditResult> edits)
        {
            writer.WriteStartArray("edits");
            foreach (EditResult result in edits)
            {
                writer.WriteStartObject();

                // Spelled out as how the walk arrives there, which is the name that still means
                // this state in the next version. A choice written against the number a state
                // has here comes back out written the long way; one the walk never found is
                // left as it was written, because there is nothing truer to say about it.
                writer.WriteString("state", result.Name ?? result.Edit.State);
                writer.WriteString("input", result.Edit.Input);
                WriteAt(writer, result.Edit.At.Select(static key => KeyValuePair.Create(key.Key, (string?)key.Value)));
                WriteArguments(writer, result.Edit.Arguments);

                // Carried or not, and never anything else: a choice that could not be taken is
                // said so here rather than disappearing into the machine's own pick.
                writer.WriteString("carried", result.Outcome switch
                {
                    EditOutcome.Carried => "yes",
                    EditOutcome.NotLegal => "not legal here",
                    _ => "not reached",
                });

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteState(Utf8JsonWriter writer, TestDesignState state)
        {
            writer.WriteStartObject();
            writer.WriteString("name", state.Name);

            if (state.From is not null)
            {
                writer.WriteString("from", state.From);
                writer.WriteString("by", state.By);
            }

            // A position of several documents is written as all of them, by address, and
            // the rule set's own is the first of them rather than written twice.
            if (state.KeepsInstances)
            {
                writer.WriteStartArray("documents");
                foreach ((string at, string document) in state.Documents)
                {
                    writer.WriteStartObject();
                    writer.WriteString("at", at);
                    writer.WritePropertyName("state");
                    Embed(writer, document);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
            else
            {
                writer.WritePropertyName("state");
                Embed(writer, state.State);
            }

            writer.WriteNumber("evaluated", state.Evaluated);
            if (state.Truncated)
            {
                writer.WriteBoolean("truncated", true);
            }

            if (state.IsTerminal)
            {
                writer.WriteStartObject("terminal");
                if (state.Result is null)
                {
                    writer.WriteNull("result");
                }
                else
                {
                    writer.WriteString("result", state.Result);
                }

                writer.WriteEndObject();
            }

            // By name, each the text the runtime answered or, where it faulted, an object
            // saying so: a string and an object cannot be taken for one another.
            if (state.Projections.Count > 0)
            {
                writer.WriteStartObject("projections");
                foreach (Projection projection in state.Projections)
                {
                    if (projection.Fault is null)
                    {
                        writer.WriteString(projection.Name, projection.Value);
                    }
                    else
                    {
                        writer.WriteStartObject(projection.Name);
                        writer.WriteString("fault", projection.Fault);
                        writer.WriteEndObject();
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteStartArray("moves");
            foreach (Move move in state.Moves)
            {
                WriteMove(writer, move);
            }

            writer.WriteEndArray();

            // Beside the moves rather than among them, because `moves` is what is legal and
            // these are not. Left out where nothing was refused.
            if (state.Refused.Count > 0)
            {
                writer.WriteStartArray("refused");
                foreach (Refusal refusal in state.Refused)
                {
                    writer.WriteStartObject();
                    writer.WriteString("input", refusal.Input);
                    WriteAt(writer, refusal.At);
                    WriteArguments(writer, refusal.Arguments);
                    WriteNames(writer, "codes", refusal.Codes);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        private static void WriteMove(Utf8JsonWriter writer, Move move)
        {
            writer.WriteStartObject();

            // A move waiting for a key is no input yet: which instance is what it waits for,
            // and what is offered there is not known until somebody says.
            if (!move.At.Any(static key => key.Value is null))
            {
                writer.WriteString("input", move.Input);
            }

            WriteAt(writer, move.At);
            WriteArguments(writer, move.Arguments);

            if (move.Actor is not null)
            {
                writer.WriteString("actor", move.Actor);
            }

            if (move.Open.Count > 0)
            {
                WriteNames(writer, "open", move.Open);
            }

            if (move.Landings.Count == 1 && move.Landings[0].As is string followed)
            {
                writer.WriteString("as", followed);
            }
            else if (move.Landings.Count == 1 && move.Landings[0].Draw is null)
            {
                WriteTarget(writer, move.Landings[0].To);
            }
            else if (move.Landings.Count > 0)
            {
                // Written only where the limit cut the draw short, the way a state says
                // `truncated` only where it stopped looking. A move without it is a move whose
                // branches are all of them, and that is the ordinary case.
                if (move.Followed < 1)
                {
                    writer.WriteNumber("followed", move.Followed);
                }

                writer.WriteStartArray("lands");
                foreach (Landing landing in move.Landings)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("probability", landing.Probability);
                    if (landing.Draw is not null)
                    {
                        writer.WritePropertyName("drew");
                        Embed(writer, landing.Draw);
                    }

                    WriteTarget(writer, landing.To);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        // Read and written again rather than pasted in, so a document Rulealize indented one
        // way does not land here indented that way inside a document indented another.
        private static void Embed(Utf8JsonWriter writer, string document)
        {
            using JsonDocument parsed = JsonDocument.Parse(document);
            parsed.RootElement.WriteTo(writer);
        }

        private static void WriteTarget(Utf8JsonWriter writer, string? to)
        {
            if (to is null)
            {
                writer.WriteNull("to");
            }
            else
            {
                writer.WriteString("to", to);
            }
        }

        private static void WriteArguments(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> arguments)
        {
            if (arguments.Count == 0)
            {
                return;
            }

            writer.WriteStartObject("args");
            foreach ((string name, string value) in arguments)
            {
                writer.WriteString(name, value);
            }

            writer.WriteEndObject();
        }

        private static IReadOnlyList<string> ReadNames(JsonElement root, string name) =>
            [.. root.GetProperty(name).EnumerateArray().Select(static field => field.GetString() ?? string.Empty)];

        private static EditResult ReadEdit(JsonElement edit)
        {
            string state = edit.GetProperty("state").GetString() ?? string.Empty;
            EditOutcome outcome = edit.TryGetProperty("carried", out JsonElement carried)
                ? carried.GetString() switch
                {
                    "yes" => EditOutcome.Carried,
                    "not legal here" => EditOutcome.NotLegal,
                    _ => EditOutcome.NotReached,
                }
                : EditOutcome.Carried;

            return new EditResult(
                new TestDesignEdit(
                    state,
                    edit.GetProperty("input").GetString() ?? string.Empty,
                    [.. ReadAt(edit).Select(static key => KeyValuePair.Create(key.Key, key.Value ?? string.Empty))],
                    ReadArguments(edit)),
                outcome,
                outcome == EditOutcome.NotReached ? null : state);
        }

        private static TestDesignState ReadState(JsonElement state)
        {
            List<KeyValuePair<string, string>>? documents = state.TryGetProperty("documents", out JsonElement kept)
                ? [.. kept.EnumerateArray().Select(static each => KeyValuePair.Create(
                    each.GetProperty("at").GetString() ?? string.Empty,
                    each.GetProperty("state").GetRawText()))]
                : null;

            return new(
                state.GetProperty("name").GetString() ?? string.Empty,
                state.TryGetProperty("from", out JsonElement from) ? from.GetString() : null,
                state.TryGetProperty("by", out JsonElement by) ? by.GetString() : null,
                documents is null ? state.GetProperty("state").GetRawText() : documents.First(static each => each.Key == "/").Value,
                state.TryGetProperty("terminal", out JsonElement terminal),
                terminal.ValueKind == JsonValueKind.Object && terminal.TryGetProperty("result", out JsonElement result)
                    ? result.GetString()
                    : null,
                state.GetProperty("evaluated").GetInt32(),
                state.TryGetProperty("truncated", out JsonElement truncated) && truncated.GetBoolean(),
                [.. state.GetProperty("moves").EnumerateArray().Select(ReadMove)],
                state.TryGetProperty("refused", out JsonElement refused)
                    ? [.. refused.EnumerateArray().Select(static each => new Refusal(
                        each.GetProperty("input").GetString() ?? string.Empty,
                        ReadArguments(each),
                        ReadNames(each, "codes"),
                        ReadAt(each)))]
                    : null,
                state.TryGetProperty("projections", out JsonElement projections) && projections.ValueKind == JsonValueKind.Object
                    ? [.. projections.EnumerateObject().Select(static each => each.Value.ValueKind == JsonValueKind.Object
                        ? new Projection(each.Name, null, each.Value.GetProperty("fault").GetString() ?? string.Empty)
                        : new Projection(each.Name, each.Value.GetString(), null))]
                    : null,
                documents);
        }

        private static Move ReadMove(JsonElement move)
        {
            List<Landing> landings = [];
            if (move.TryGetProperty("lands", out JsonElement lands))
            {
                landings.AddRange(lands.EnumerateArray().Select(static land => new Landing(
                    land.GetProperty("probability").GetDouble(),
                    land.TryGetProperty("drew", out JsonElement drew) ? drew.GetRawText() : null)
                {
                    To = Target(land),
                }));
            }
            else if (move.TryGetProperty("to", out _))
            {
                landings.Add(new Landing(1.0, null) { To = Target(move) });
            }
            else if (move.TryGetProperty("as", out JsonElement taken))
            {
                landings.Add(new Landing(1.0, null) { As = taken.GetString() });
            }

            IReadOnlyList<KeyValuePair<string, string?>> at = ReadAt(move);
            string input = move.TryGetProperty("input", out JsonElement named)
                ? named.GetString() ?? string.Empty
                : string.Join('.', at.Select(static key => key.Key));
            IReadOnlyDictionary<string, string> arguments = ReadArguments(move);
            IReadOnlyList<string>? open = move.TryGetProperty("open", out _) ? ReadNames(move, "open") : null;

            return new Move(
                input,
                arguments,
                Move.Write(input, arguments, open, at),
                move.TryGetProperty("actor", out JsonElement actor) ? actor.GetString() : null,
                string.Empty,
                landings,
                move.TryGetProperty("followed", out JsonElement followed) ? followed.GetDouble() : 1,
                open,
                at);
        }

        private static string? Target(JsonElement node) =>
            node.TryGetProperty("to", out JsonElement to) && to.ValueKind == JsonValueKind.String
                ? to.GetString()
                : null;

        private static IReadOnlyDictionary<string, string> ReadArguments(JsonElement node)
        {
            Dictionary<string, string> arguments = new(StringComparer.Ordinal);
            if (node.TryGetProperty("args", out JsonElement args) && args.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty argument in args.EnumerateObject())
                {
                    arguments[argument.Name] = argument.Value.GetString() ?? string.Empty;
                }
            }

            return arguments;
        }
    }
}
