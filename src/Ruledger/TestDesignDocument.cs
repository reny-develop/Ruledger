// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Ruledger
{
    /// <summary>The <c>ruledger/test-design/v1</c> document: a test design, written down.</summary>
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
    /// document out when there is nothing in it. <c>"to": null</c> is a landing the walk had
    /// no states left to visit; a move with neither is a move in a state the rules call final,
    /// offered and not followed.
    /// </para>
    /// </remarks>
    internal static class TestDesignDocument
    {
        private const string Schema = "ruledger/test-design/v1";

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

                WriteNames(writer, "observed", design.Observed);
                WriteNames(writer, "collapsed", design.Collapsed);
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
                || schema.GetString() != Schema)
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
                ReadNames(root, "observed"),
                ReadNames(root, "collapsed"),
                [.. root.GetProperty("states").EnumerateArray().Select(ReadState)],
                [.. root.GetProperty("edits").EnumerateArray().Select(ReadEdit)],
                root.GetProperty("unreached").GetInt32());
        }

        private static void WriteNames(Utf8JsonWriter writer, string name, IReadOnlyList<string> names)
        {
            writer.WriteStartArray(name);
            foreach (string field in names)
            {
                writer.WriteStringValue(field);
            }

            writer.WriteEndArray();
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

            writer.WritePropertyName("state");
            Embed(writer, state.State);

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

            writer.WriteStartArray("moves");
            foreach (Move move in state.Moves)
            {
                WriteMove(writer, move);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteMove(Utf8JsonWriter writer, Move move)
        {
            writer.WriteStartObject();
            writer.WriteString("input", move.Input);
            WriteArguments(writer, move.Arguments);

            if (move.Actor is not null)
            {
                writer.WriteString("actor", move.Actor);
            }

            if (move.Landings.Count == 1 && move.Landings[0].Draw is null)
            {
                WriteTarget(writer, move.Landings[0].To);
            }
            else if (move.Landings.Count > 0)
            {
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
                new TestDesignEdit(state, edit.GetProperty("input").GetString() ?? string.Empty, ReadArguments(edit)),
                outcome,
                outcome == EditOutcome.NotReached ? null : state);
        }

        private static TestDesignState ReadState(JsonElement state) =>
            new(
                state.GetProperty("name").GetString() ?? string.Empty,
                state.TryGetProperty("from", out JsonElement from) ? from.GetString() : null,
                state.TryGetProperty("by", out JsonElement by) ? by.GetString() : null,
                state.GetProperty("state").GetRawText(),
                state.TryGetProperty("terminal", out JsonElement terminal),
                terminal.ValueKind == JsonValueKind.Object && terminal.TryGetProperty("result", out JsonElement result)
                    ? result.GetString()
                    : null,
                state.GetProperty("evaluated").GetInt32(),
                state.TryGetProperty("truncated", out JsonElement truncated) && truncated.GetBoolean(),
                [.. state.GetProperty("moves").EnumerateArray().Select(ReadMove)]);

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

            string input = move.GetProperty("input").GetString() ?? string.Empty;
            IReadOnlyDictionary<string, string> arguments = ReadArguments(move);

            return new Move(
                input,
                arguments,
                arguments.Count == 0
                    ? input
                    : $"{input}({string.Join(", ", arguments.Select(static a => $"{a.Key}: {a.Value}"))})",
                move.TryGetProperty("actor", out JsonElement actor) ? actor.GetString() : null,
                string.Empty,
                landings);
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
