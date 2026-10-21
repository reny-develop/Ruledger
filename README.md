# Ruledger

Ruledger implements
[Rule-Derived Test Design](https://github.com/reny-develop/rule-derived-test-design) on top of
[Rulealize](https://github.com/reny-develop/Rulealize).

It walks a rule set and writes down, for every state it visits, everything that can be observed
there: which inputs are legal, whether the state is terminal and with what `Result`, where
each legal input leads, and what each of the rule set's projections says about it. That
enumeration is the test design. It is mechanical, so it does not
forget. It is also tautological, so it does not know what is correct: it states what the
rule set decides, never what it should decide. Supplying that judgement is the developer's job,
and it is the only part of the work Ruledger refuses to guess at.

Change the rule set, run it again, and the diff is the blast radius — which states gained or
lost a legal input, which outcomes moved, where a projection says something else. Edits the developer made to the previous test
design are carried over; whatever could not be carried is reported instead of silently reset.
How far the walk goes is a number of states it may visit, and where it stopped for want of
them is reported as that, never as nothing being there.

The walk is reproducible. The same rule set visits the same states in the same order every
time: no randomness, no seed. Everything else — that number of states, and how hard the
runtime is asked to look in each one — is a setting, and it is recorded alongside the test
design.

## Using it

From a shell, it is [Ruledger.Cli](https://github.com/reny-develop/Ruledger.Cli): `ruledger
derive`, `ruledger diff` and `ruledger observe`, and its
[tutorial](https://github.com/reny-develop/Ruledger.Cli/blob/main/doc/tutorial.md) is the half
hour that shows what this is like — reversi changed one line at a time, then a shift roster,
ending in a count of what did not happen.

From a program, it is this package, which is what the tool calls:

```sh
dotnet add package Ruledger
```

```csharp
using Rulealize;
using Ruledger;

RuleRuntime runtime = new RuleRuntime().LoadPluginsFrom("plugin");

// Walk it, and write the test design down.
TestDesign design = TestDesign.Derive(runtime, File.ReadAllText("reversi.json"));
File.WriteAllText("reversi.test-design.json", design.ToJson());

// Apply that design to a later version of the rules, which is the diff.
TestDesignDiff diff = TestDesignDiff.Of(
    runtime, File.ReadAllText("reversi.test-design.json"), File.ReadAllText("reversi.json"));

foreach (StateChange change in diff.Changed)
{
    // change.Before and change.After are the state as each design has it; Lost, Gained,
    // Moved, Refusing, NotRefusing and ProjectionsMoved are what the rules decide
    // differently there.
}
```

A host that shows a diff its own way asks this for it, and gets what changed as the values the
tool prints its lines from — never a second account of it worked out from two designs.
`diff.After` is the design the new rules give, with the choices of the one applied carried
across; what could not be carried is in its `Edits`.

A rule set whose initial state is only the shape of a case — who may sign and what may be
requested both empty until somebody says — offers nothing from there, and is walked from a
case instead:

```csharp
TestDesign design = TestDesign.Derive(
    runtime, File.ReadAllText("signed-request.json"), components,
    File.ReadAllText("case.state.json"), settings: null, edits: null);
```

The state is read against the rule set's schema, a composite's components included, like any
other. It travels in the design as `design.Start`, the document itself rather than where it
was read from, so whatever replays the design begins where the walk began; and a diff walks
the new version from it. Choices handed in beside a start are carried one by one, as they are
across a change to the rules.

A rule set whose `uses` holds something `many` keeps each instance — a claim of a month, say
— as a document of its own, in a store its `requires` names. The walk does not open that store.
Evaluation is pure, so where the documents are kept changes no answer, and it gives the runtime
a `MemoryStateStore` in its place; the store's plugin is still loaded, because the rule set is
built against it. The documents a `many` begins its instances in are handed over by name:

```csharp
TestDesign design = TestDesign.Derive(
    runtime, File.ReadAllText("claim-month.json"), components, starts,
    start: null, settings: null, edits: null);
```

A position is then every document, and a state carries them as `Documents`; a move is asked
of each document in turn and says which instance it is at. A step reads only the rule set's own
document and the instance it is at — Rulealize refuses a guard that reads anything its
transition does not write — so one already followed from the same documents is written down
and not followed again, which is what lets a walk end where another instance can always be
created. A collection whose keys are written as a schema is walked at every key the schema
names, and otherwise only at the keys a person wrote in a choice.

[doc/test-design.md](doc/test-design.md) is the form of the document both of them write.

## The numbers

Every claim about Ruledger is a number, and every one of them comes out of a run:

```sh
dotnet test verify/Ruledger.Verify.csproj --logger "console;verbosity=detailed"
```

67 measurements, about four minutes. They walk the rule sets in `verify/ruleset/`, print what
they found, and fail if any of it has moved. Nothing about this project is a figure somebody
wrote down once: the numbers in these documents are the numbers that command prints, and the
way to check one is to run it.

`test/` holds the unit tests, which are a different question and take two seconds.

## What v1 does

| | |
|---|---|
| Derive the test design, filling in the concrete values and the expected results | **In** |
| Apply the previous test design to a new version of the rule set and report what changed | **In** |
| Carry a human's edits across a change to the rule set | **In** |
| Walk from a state of a case rather than where the rule set begins | **In**. For a rule set whose initial state is the shape of a case and offers nothing until one is supplied. The state travels in the test design |
| Write down what each projection says about every state | **In**. In the text form the runtime writes, and where the runtime faults on one, the fault; a projection moves nothing, so the walk goes on. A field only a projection reads keeps positions apart as a guard's would. A projection added or taken away is reported once, not per state |
| Walk a parameter the rule set leaves open | **In**. Every value its schema admits where those can be named — an enumeration, a boolean, a bounded whole number — and otherwise only the values a person wrote as choices. It never makes one up. The values refused are written down with what refused them, and what each parameter admits once |
| Walk a rule set that keeps instances as documents of their own | **In**. In memory, in place of the store it names. A step already followed from the documents it reads is not followed again, and that this loses no step is measured against a walk to the end of every position of up to four claims. A collection keyed by a schema is walked at every key it names, otherwise at the keys a person wrote, and never at one made up |
| Verify anything outside the rule set — screens, persistence, integrations | Out. Not a limit in principle: what Ruledger reaches is what the rule set expresses, and that is extended by adding vocabulary, not by changing Ruledger |
| Visualize or edit the rule set | Out. A rule set is still JSON, which is hard to hand-write even for an engineer |
| Record approvals | Out. Approval is committing the test design; expiry is the diff coming back non-empty. Git already does both |
| A line-oriented rendering of the test design | Out of v1. The document carries what one needs, and that it does is measured |
| A number standing in for coverage | Out, and it stays out. The only quantity Ruledger reports is how many places the walk stopped in front of for want of states to visit |

Every "out" above is a decision rather than a gap, and the reason is in the row. Two of
them are worth saying twice. A line-oriented view of a test design is left out because the
document already carries what one needs — measured, not asserted. A coverage number is left
out and stays out, because a number standing in for quality is the thing this method was
written against; how many places the walk stopped in front of is a count of places, and it
is the only quantity reported.

## Status

v1 is done and published. The library walks a rule set, works out by itself which state two
positions are compared on, writes the test design down, applies one to a later version and
carries the developer's edits across. The measurements all run. `Ruledger` and `Ruledger.Cli`
are on nuget.org.

Since then it runs on the Rulealize that leaves a parameter open and lets an input `validate`
its arguments — and name, with `invalid`, the refusal of a value the schema does not admit — and the test design it writes became `ruledger/test-design/v2` for the one shape that
added — a move waiting for a value. It is `ruledger/test-design/v3` since a walk can begin in a
state of a case, which the design carries as `start`, and `ruledger/test-design/v4` since it
writes down what a rule set's projections say: a host shows them, so a change to one is a
change the rules made. It is `ruledger/test-design/v5` since it walks a rule set that keeps
instances as documents of their own, which needed a position of several documents, a move at an
instance and a move waiting for a key. A v4, v3, v2 or v1 test design is still read; a v3 one
said nothing about projections, so nothing about them is held against it.

Until `Ruledger.Cli` 1.3.0 the library shipped only inside the tool. It is a package of its own
from 1.0.0, and the tool has a repository of its own, because a host that draws a diff on a screen
has to be handed what changed rather than read it back out of lines meant for a person.

It is C#: it needs the Rulealize runtime, and reading the rule set statically is part of the
same job, not a separate program in another language.

## License

Apache-2.0. See [LICENSE](LICENSE).
