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
How far the walk goes is a budget, and what the budget did not reach is reported as not
reached, never as not there.

The walk is reproducible. The same rule set visits the same states in the same order every
time: no randomness, no seed. Everything else — the budget, and how hard the runtime is asked
to look — is a setting, and it is recorded alongside the test design.

## Using it

```sh
dotnet tool install -g Rulealize.Cli
dotnet tool install -g Ruledger.Cli
```

```sh
rulealize restore reversi.json     # fetch the vocabularies the rule set draws on
ruledger derive reversi.json       # walk it, and write the test design down
ruledger diff reversi.test-design.json reversi.json   # apply that design to a later version
ruledger observe reversi.json      # what a position is compared on, and what is dropped
```

`diff` is meant for a build server: it exits 0 when there is nothing to report, 1 when it
could not be done, 2 when the command line was not understood, and 3 when the rules decided
differently, a choice could not be carried, or the two versions are not compared on the same
state.

**[doc/tutorial.md](doc/tutorial.md) is the half hour that shows what this is like** — reversi
changed one line at a time, then a shift roster, ending in a count of what did not happen.
[doc/test-design.md](doc/test-design.md) is the form of the document the two of them produce.

## The numbers

Every claim about Ruledger is a number, and every one of them comes out of a run:

```sh
dotnet test verify/Ruledger.Verify.csproj --logger "console;verbosity=detailed"
```

55 measurements, about four minutes. They walk the rule sets in `verify/ruleset/`, print what
they found, and fail if any of it has moved. They are wired to run on every commit. Nothing
about this project is a figure somebody wrote down once.

`test/` holds the unit tests, which are a different question and take two seconds.

## What v1 does

| | |
|---|---|
| Derive the test design, filling in the concrete values and the expected results | **In** |
| Apply the previous test design to a new version of the rule set and report what changed | **In** |
| Carry a human's edits across a change to the rule set | **In** |
| Verify anything outside the rule set — screens, persistence, integrations | Out. Not a limit in principle: what Ruledger reaches is what the rule set expresses, and that is extended by adding vocabulary, not by changing Ruledger |
| Visualize or edit the rule set | Out. A rule set is still JSON, which is hard to hand-write even for an engineer |
| Record approvals | Out. Approval is committing the test design; expiry is the diff coming back non-empty. Git already does both |
| A line-oriented rendering of the test design | Out of v1. The document carries what one needs, and that it does is measured |
| A number standing in for coverage | Out, and it stays out. The only quantity Ruledger reports is how many places the budget stopped in front of |

The reasoning behind each line is in the thesis — see §6.5 and §6.6. What has to exist
before v1 is finished — to prove the method, and to let someone experience it — is
[doc/v1.md](doc/v1.md).

## Status

Being written, and close. The library walks a rule set, works out by itself which state two
positions are compared on, writes the test design down, applies one to a later version and
carries the developer's edits across. The command line tool does all three of those from a
shell. The measurements all run.

Not done: the build server has not run yet, and nothing is published, so `dotnet tool install`
above is what will work rather than what works today. Publication happens when v1 is finished
and not before. The scope is settled and written down in [doc/v1.md](doc/v1.md).

The tool is C#: it needs the Rulealize runtime, and reading the rule set statically is part of
the same job, not a separate program in another language.

## License

Apache-2.0. See [LICENSE](LICENSE).
