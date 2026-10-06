# Ruledger

Ruledger implements
[Rule-Derived Test Design](https://github.com/reny-develop/rule-derived-test-design) on top of
[Rulealize](https://github.com/reny-develop/Rulealize).

It walks a rule set and writes down, for every state it visits, everything that can be observed
there: which inputs are legal, whether the state is terminal and with what `Result`, and where
each legal input leads. That enumeration is the test design. It is mechanical, so it does not
forget. It is also tautological, so it does not know what is correct: it states what the
rule set decides, never what it should decide. Supplying that judgement is the developer's job,
and it is the only part of the work Ruledger refuses to guess at.

Change the rule set, run it again, and the diff is the blast radius — which states gained or
lost a legal input, which outcomes moved. Edits the developer made to the previous test
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
    // Moved, Refusing and NotRefusing are what the rules decide differently there.
}
```

A host that shows a diff its own way asks this for it, and gets what changed as the values the
tool prints its lines from — never a second account of it worked out from two designs.
`diff.After` is the design the new rules give, with the choices of the one applied carried
across; what could not be carried is in its `Edits`.

[doc/test-design.md](doc/test-design.md) is the form of the document both of them write.

## The numbers

Every claim about Ruledger is a number, and every one of them comes out of a run:

```sh
dotnet test verify/Ruledger.Verify.csproj --logger "console;verbosity=detailed"
```

56 measurements, about four minutes. They walk the rule sets in `verify/ruleset/`, print what
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
| Walk a parameter the rule set leaves open | **In**. Every value its schema admits where those can be named — an enumeration, a boolean, a bounded whole number — and otherwise only the values a person wrote as choices. It never makes one up. The values refused are written down with what refused them, and what each parameter admits once |
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
its arguments — and name, with `invalid`, the refusal of a value the schema does not admit — and the test design it writes is `ruledger/test-design/v2` for the one shape that
added — a move waiting for a value. A v1 test design is still read.

Until `Ruledger.Cli` 1.3.0 the library shipped only inside the tool. It is a package of its own
from 1.0.0, and the tool has a repository of its own, because a host that draws a diff on a screen
has to be handed what changed rather than read it back out of lines meant for a person.

It is C#: it needs the Rulealize runtime, and reading the rule set statically is part of the
same job, not a separate program in another language.

## License

Apache-2.0. See [LICENSE](LICENSE).
