// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Ruledger
{
    /// <summary>Which of a rule set's state fields its observations depend on.</summary>
    /// <remarks>
    /// <para>
    /// Two states that agree on every observed field are the same state, whatever else
    /// differs between them. Comparing whole state documents instead would make an audit
    /// trail enough to tell two identical positions apart, and a rule set that keeps one is
    /// ordinary — so a walk would never rejoin, and no edit a person made would survive a
    /// change to the rules.
    /// </para>
    /// <para>
    /// The set is derived, never declared. A guard, a parameter domain and the two
    /// <c>terminal</c> expressions are the only places an observation can come from, so what
    /// those read — through definitions, and through the effects that write what they read —
    /// is the whole of it. The second half is why this is a fixed point rather than a walk:
    /// a field no guard mentions still reaches an observation if an effect writes an observed
    /// field in terms of it.
    /// </para>
    /// <para>
    /// This reads the document rather than asking the runtime, and it is the one place where
    /// Ruledger knows a vocabulary by name: <c>state.get</c>, <c>state.update</c>,
    /// <c>def.ref</c>, <c>def.call</c> and the <c>$</c> and <c>#</c> shorthands. Everything
    /// else is walked without being understood. The cost shows in <see cref="Observed"/>,
    /// which over-approximates: where a write target cannot be told from a value by shape
    /// alone, the field is kept rather than collapsed. Keeping too much weakens how far a
    /// walk rejoins and how much of an edit survives a change; it never hides something
    /// observable.
    /// </para>
    /// </remarks>
    public sealed class ObservationScan
    {
        private static readonly JsonDocumentOptions ReaderOptions = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private readonly List<string> declared = [];
        private readonly Dictionary<string, JsonElement> definitions = new(StringComparer.Ordinal);
        private readonly HashSet<string> reached = new(StringComparer.Ordinal);

        private ObservationScan(JsonElement root, bool whole = false)
        {
            ReadSchema(root);
            ReadDefinitions(root);

            if (whole)
            {
                this.reached.UnionWith(this.declared);
            }
            else
            {
                List<JsonElement> effects = [];
                foreach (JsonElement point in ObservationPoints(root, effects))
                {
                    Read(point, this.reached, []);
                }

                Settle(effects);
            }

            Observed = [.. this.declared.Where(this.reached.Contains)];
            Collapsed = [.. this.declared.Where(field => !this.reached.Contains(field))];
        }

        /// <summary>Gets the state fields an observation depends on, in the order the schema declares them.</summary>
        public IReadOnlyList<string> Observed { get; }

        /// <summary>Gets the state fields nothing observable depends on, in the order the schema declares them.</summary>
        /// <remarks>
        /// These are dropped before two states are compared. They are still carried and still
        /// written: what a rule set keeps in its state is its own business, and this decides
        /// only what counts when asking whether a state has been seen before.
        /// </remarks>
        public IReadOnlyList<string> Collapsed { get; }

        /// <summary>Scans a rule set document.</summary>
        /// <param name="ruleSet">The document, as text. Comments and trailing commas are accepted.</param>
        /// <returns>What the document's observations depend on.</returns>
        /// <exception cref="JsonException">The text is not JSON.</exception>
        /// <exception cref="NotSupportedException">The rule set holds other rule sets.</exception>
        public static ObservationScan Of(string ruleSet)
        {
            ArgumentNullException.ThrowIfNull(ruleSet);

            using JsonDocument document = JsonDocument.Parse(ruleSet, ReaderOptions);
            Refuse(document.RootElement);
            return new ObservationScan(document.RootElement);
        }

        // A rule set that holds others reads their state through their guards, which are in
        // documents this has not been given, so the answer would be a set with fields missing
        // from it and no sign that any were. Refusing says so; collapsing a field that turns
        // out to matter does not.
        //
        // There is a better reason to walk the parts anyway: a composite's reachable set is
        // the product of its components, and whatever a walk of one component found holds
        // inside every composite that holds it.
        internal static void Refuse(JsonElement root)
        {
            if (root.TryGetProperty("uses", out JsonElement uses) && uses.ValueKind == JsonValueKind.Array && uses.GetArrayLength() > 0)
            {
                throw new NotSupportedException(
                    "This rule set holds others, and a rule set that holds others is not walked: "
                    + "its reachable states are the product of its components, and what its guards "
                    + "read lives in documents this one does not carry. Walk the components.");
            }
        }

        // Every declared field observed, which is to say nothing collapsed. Not offered to a
        // caller: it exists so that the measurement can show what happens without collapsing.
        internal static ObservationScan Whole(string ruleSet)
        {
            ArgumentNullException.ThrowIfNull(ruleSet);

            using JsonDocument document = JsonDocument.Parse(ruleSet, ReaderOptions);
            return new ObservationScan(document.RootElement, whole: true);
        }

        private static string? Operation(JsonElement node) =>
            node.ValueKind == JsonValueKind.Object
                && node.TryGetProperty("op", out JsonElement op)
                && op.ValueKind == JsonValueKind.String
                ? op.GetString()
                : null;

        // A definition is either an expression or a parameter list and a body. Only the
        // second shape can carry parameters, so the first is told by the absence of `body`.
        private static JsonElement Body(JsonElement definition) =>
            definition.ValueKind == JsonValueKind.Object
                && definition.TryGetProperty("body", out JsonElement body)
                ? body
                : definition;

        // Everywhere an observation comes from, and — collected on the way past, because they
        // sit under the same inputs — every effect, which the fixed point needs.
        //
        // A legal input is its name, its arguments and whose it is, so all three of the keys
        // that build one are here. `actor` is not a fourth thing that can be observed; it is
        // part of the first, and leaving it out would collapse a field only it reads while
        // the design went on printing two different movers for the one state.
        private static IEnumerable<JsonElement> ObservationPoints(JsonElement root, List<JsonElement> effects)
        {
            if (root.TryGetProperty("inputs", out JsonElement inputs)
                && inputs.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty input in inputs.EnumerateObject())
                {
                    if (input.Value.TryGetProperty("when", out JsonElement guard))
                    {
                        yield return guard;
                    }

                    if (input.Value.TryGetProperty("actor", out JsonElement actor))
                    {
                        yield return actor;
                    }

                    if (input.Value.TryGetProperty("params", out JsonElement parameters)
                        && parameters.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty parameter in parameters.EnumerateObject())
                        {
                            if (parameter.Value.TryGetProperty("domain", out JsonElement domain))
                            {
                                yield return domain;
                            }
                        }
                    }

                    if (input.Value.TryGetProperty("effects", out JsonElement written)
                        && written.ValueKind == JsonValueKind.Array)
                    {
                        effects.AddRange(written.EnumerateArray());
                    }
                }
            }

            if (root.TryGetProperty("terminal", out JsonElement terminal))
            {
                if (terminal.TryGetProperty("when", out JsonElement when))
                {
                    yield return when;
                }

                if (terminal.TryGetProperty("result", out JsonElement result))
                {
                    yield return result;
                }
            }
        }

        private void ReadSchema(JsonElement root)
        {
            if (!root.TryGetProperty("state", out JsonElement state)
                || !state.TryGetProperty("schema", out JsonElement schema)
                || schema.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty field in schema.EnumerateObject())
            {
                this.declared.Add(field.Name);
            }
        }

        private void ReadDefinitions(JsonElement root)
        {
            if (!root.TryGetProperty("definitions", out JsonElement section)
                || section.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty definition in section.EnumerateObject())
            {
                this.definitions[definition.Name] = Body(definition.Value);
            }
        }

        private void Settle(List<JsonElement> effects)
        {
            bool moved = true;
            while (moved)
            {
                moved = false;
                foreach (JsonElement effect in effects)
                {
                    if (!Writes(effect).Any(this.reached.Contains))
                    {
                        continue;
                    }

                    HashSet<string> reads = new(StringComparer.Ordinal);
                    Read(effect, reads, []);
                    foreach (string field in reads)
                    {
                        moved |= this.reached.Add(field);
                    }
                }
            }
        }

        // A write target has to denote a state field, so it is either a path written out as a
        // string or a `state.get` node — never a computed expression, and never nested. What
        // this cannot tell apart is a direct child that reads rather than writes, such as
        // `"value": "$turn"`; that field is then counted as written, which can only keep more
        // fields observed than a perfect answer would.
        private IEnumerable<string> Writes(JsonElement effect)
        {
            if (effect.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            foreach (JsonProperty property in effect.EnumerateObject())
            {
                if (property.NameEquals("op"))
                {
                    continue;
                }

                string? field = null;
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    string text = property.Value.GetString() ?? string.Empty;
                    field = text.StartsWith('$') ? Field(text[1..])
                        : text.Length == 0 || text[0] is '#' or '@' ? null
                        : Field(text);
                }
                else if (Operation(property.Value) == "state.get"
                    && property.Value.TryGetProperty("path", out JsonElement path)
                    && path.ValueKind == JsonValueKind.String)
                {
                    field = Field(path.GetString() ?? string.Empty);
                }

                if (field is not null)
                {
                    yield return field;
                }
            }
        }

        private void Read(JsonElement node, HashSet<string> into, HashSet<string> active)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.String:
                    string text = node.GetString() ?? string.Empty;
                    if (text.StartsWith('$'))
                    {
                        Add(into, text[1..]);
                    }
                    else if (text.StartsWith('#'))
                    {
                        Follow(text[1..], into, active);
                    }

                    return;

                case JsonValueKind.Array:
                    foreach (JsonElement item in node.EnumerateArray())
                    {
                        Read(item, into, active);
                    }

                    return;

                case JsonValueKind.Object:
                    ReadObject(node, into, active);
                    return;

                default:
                    return;
            }
        }

        private void ReadObject(JsonElement node, HashSet<string> into, HashSet<string> active)
        {
            switch (Operation(node))
            {
                case "state.get":
                    if (node.TryGetProperty("path", out JsonElement path)
                        && path.ValueKind == JsonValueKind.String)
                    {
                        Add(into, path.GetString() ?? string.Empty);
                    }

                    return;

                // Writes a field in terms of itself, so it reads the field it names as well as
                // whatever the new value is built from.
                case "state.update":
                    if (node.TryGetProperty("path", out JsonElement updated)
                        && updated.ValueKind == JsonValueKind.String)
                    {
                        Add(into, updated.GetString() ?? string.Empty);
                    }

                    if (node.TryGetProperty("value", out JsonElement replacement))
                    {
                        Read(replacement, into, active);
                    }

                    return;

                case "def.ref":
                    if (node.TryGetProperty("name", out JsonElement name)
                        && name.ValueKind == JsonValueKind.String)
                    {
                        Follow(name.GetString() ?? string.Empty, into, active);
                    }

                    return;

                case "def.call":
                    if (node.TryGetProperty("def", out JsonElement called)
                        && called.ValueKind == JsonValueKind.String)
                    {
                        Follow(called.GetString() ?? string.Empty, into, active);
                    }

                    if (node.TryGetProperty("args", out JsonElement arguments))
                    {
                        Read(arguments, into, active);
                    }

                    return;

                default:
                    foreach (JsonProperty property in node.EnumerateObject())
                    {
                        if (!property.NameEquals("op"))
                        {
                            Read(property.Value, into, active);
                        }
                    }

                    return;
            }
        }

        private void Follow(string definition, HashSet<string> into, HashSet<string> active)
        {
            if (!this.definitions.TryGetValue(definition, out JsonElement body) || !active.Add(definition))
            {
                return;
            }

            Read(body, into, active);
            active.Remove(definition);
        }

        private void Add(HashSet<string> into, string path)
        {
            if (Field(path) is string field)
            {
                into.Add(field);
            }
        }

        // A path names a field and then reaches inside it. Identity is per field, because that
        // is the granularity at which a change to the state's shape shows up.
        private string? Field(string path)
        {
            int end = path.IndexOfAny(['.', '[']);
            string head = end < 0 ? path : path[..end];
            return this.declared.Contains(head) ? head : null;
        }
    }
}
