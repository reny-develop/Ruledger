## Project
Ruledger implements [Rule-Derived Test Design](https://github.com/reny-develop/rule-derived-test-design) on top of [Rulealize](https://github.com/reny-develop/Rulealize). It walks a rule set and writes down, for every state it visits, everything that can be observed there.

One C# class library (`src/Ruledger/`, `net10.0`, nullable + implicit usings, a missing XML comment is an error), the `dotnet tool` in `src/Ruledger.Cli/`, an xUnit suite in `test/` for the units, and `verify/` for the measurements and the material they run against.

**v1 has landed and its scaffolding is gone.** `doc/v1.md` here, and the thesis and the verification record in the sibling repository, were working documents written in Japanese for the author; they were deleted on purpose, because a reader gets proof and experience by running Ruledger rather than by reading a claim that it works. Three documents are what must now stay true, and each is read on its own: the README says what is in and what is out, `doc/tutorial.md` is the half hour that shows what the tool is like, `doc/test-design.md` is the form of the document it writes. Every console transcript in them was captured from a run and has to stay a true prefix of what the command prints today.

`Ruledger.Cli` 1.0.0 is on nuget.org, which is the whole of what is published — the library ships inside the tool and has no package of its own. A release raises `<Version>` in `src/Ruledger.Cli/Ruledger.Cli.csproj` and nothing else carries the number; nuget.org never lets a version be reused, so it is decided before the push and not after.

The `doc-sync` machinery in the workspace covers folders named `Rulealize*` and does not reach here. The discipline still applies; nothing runs it for you.

## Fixtures
`verify/ruleset/` holds the documents the measurements are about. Some were copied from `Rulealize/ruleset/` and the rest were written here.

**Do not change what is in there.** Adding a document for a new measurement is ordinary work; editing one that is already there is not. A measurement is a claim about a document, and a document that moves underneath it turns the claim into a report about whatever happened to be in that file that day.

Nothing reads the sibling repositories at run time either. Rule-Derived Test Design goes public, and a stranger with one clone has to be able to run the measurement — a test that reaches across the workspace can only be run by somebody who has the whole of it.

Where a measurement does not bite on the material that is here, write material where it does, and say in the document why it exists. `process` has seven states and never returns to one, so nothing about collapsing shows there; `seats` was written because of that.

A measurement about a change to a rule set needs the rule set both ways, so some of these come in pairs and the pair is the fixture: `reversi-swapped`, `reversi-strict` and `reversi-counted` are each one line away from `reversi`, and `approval-results` and `roster-seniors` from theirs. A later version raises `version` and says in its comment which line moved and why that line.

## Measure before writing it down
Reasoning from the code gives a hypothesis. Build the fixture, run it, and only then write the claim into the README, `doc/tutorial.md`, `doc/test-design.md` or the sibling repository. When a measurement contradicts something already written, say so plainly and correct it — the project exists because unmeasured judgement is unreliable, and an unmeasured claim in its own documents gives that away.

## What is public
v1 ships a `dotnet tool` and nothing else. The library is internal in every sense that matters: keep the public surface to what the tool and the tests need, prefer `internal`, and do not add a public member to make a test easier — `InternalsVisibleTo` is already there.

## Words
No new terms. Proper nouns are Ruledger and Rulealize; everything else is ordinary language.

English keeps "document", which is what Rulealize calls the four kinds it reads. Nothing in the repository is written in any other language now, so the rest of this paragraph is for talking about the project rather than for anything in it.

**In Japanese, never write 文書 for a rule set.** Japanese uses the same word for the JSON and for the prose about it, so a listener has to work out which one is meant every time. A rule set is ルールセット, a state is 状態, an input is 入力. 状態パス is a place inside a state (`assigned`); 状態の名前 is how the walk arrived (`#2 = #1 + release(月)`).

## What a test design may say
Three things are observable about a state and only those are written down: which inputs are legal, whether it is final and with what result, and where each legal input leads. A legal input is its name, its arguments and whose it is.

The one place a person writes is a choice — from this state, take this input first. Everything downstream of it is worked out again. An observation written by hand would be the only thing in a test design that can be wrong, so there is nowhere to write one.

No number that stands in for quality. Ruledger reports where a walk stopped for want of states to visit and nothing else of the kind; a percentage would invite exactly the misreading the method was written against.

Nothing is hidden when it fails. A choice that could not be carried is reported and never dropped back to what the machine would have picked, and a place the walk did not reach is reported as that rather than as nothing being there. There should be no code path that quietly reverts to a default.

The line-oriented form a person reads is a **view** of the test design and never a second copy to be kept in step. v1 does not build one; what it guarantees is that the document does not block one, and that guarantee is a measurement (`verify/Rendering.cs`, written against the JSON rather than against Ruledger's types) rather than a promise.

## The walk
Deterministic: the same rule set and the same settings visit the same states in the same order. No randomness, no seed, and nothing that depends on the enumeration order of a hash table. Everything that is not derived from the rule set and can be varied is a setting and travels with the test design.

Written on an explicit stack. Only the count of states a walk may visit keeps its depth bounded — a rule set holding a history never returns to a state it has been in — and recursion breaks at three thousand frames.

Splitting a rule set into a composite and the parts it holds is a way of writing it, not a different set of rules, so the composite is walked as the one document it stands for would be, and the two produce the same test design. `verify/ruleset/` keeps both forms of two processes for that reason. **If they ever disagree, Ruledger is the cause** — the runtime can only narrow what a component offers — and what the pair is really watching is how coarse the scan is.

A composite whose components were not supplied is refused rather than answered. Its guards run through theirs, so answering would return a set of legal inputs with some of them missing and nothing in the answer saying any were.

A person's choices are held by **how the walk arrives** at a state and never by what is in it. A field added to every state moves every state and no route; keying by contents would lose every choice on a change that altered no behaviour.

## Reading a rule set
The runtime answers everything about behaviour; the document is read only to work out which state a position is compared on. That is the one place Ruledger knows a vocabulary by name — `state.get`, `state.update`, `def.ref`, `def.call`, `rec.at`, and the `$` and `#` shorthands. Adding a sixth is a decision, not a detail: say in the code why the alternative did not work.

Where the shape of the document cannot settle a question, keep the field rather than collapse it. Keeping too much costs rejoining; collapsing too much hides something observable.

## Building and testing
Two suites, and they answer different questions.

- `dotnet test test/Ruledger.Tests.csproj` — the units, and the command line tool's output and exit codes. Two seconds. A change that breaks one of these broke a member.
- `dotnet test verify/Ruledger.Verify.csproj --logger "console;verbosity=detailed"` — the measurements. Fifty-six of them, about four minutes, and the log is the report: each one prints what it found and fails if the number has moved. A change that breaks one of these moved a number somebody else is relying on, and the fix is to measure again and write down what it now is.

The vocabularies come in as NuGet packages and land beside the test assemblies, which is how the runtime finds them; the plugin repositories are never built from source for this.

**There is no build server, on purpose.** CI never appeared in the method, and the one thing it would have bought — a number produced somewhere other than the author's machine — needs one run rather than one per commit; reproducibility comes from the measurement running anywhere with one command. So the measurements are what makes a number a fact, and nothing runs them for you: run them yourself before committing anything that could move one, and when one has moved, measure again and write down what it now is rather than restoring the old number.

The twenty choices the carry-over measurements are about are fixed in `verify/choice/`, not rebuilt each run. Stacking them — one at a time, each read off the design the ones before it produced — is how they were arrived at and it is written in the file; applying all twenty to one walk reaches the same design, which is why the file is enough.

## Not decided
- **How `ruledger/test-design/v1` gets a version.** Whether the walk becomes a recorded setting rides on this. There is one walk, so it is not recorded; add a second and an old test design can no longer say which one produced it — raise the schema version to tell them apart, or add a settings field. Decide it then, not now.
- **Registering both repositories in `docmap.json`.** The README's table of what v1 does and the sibling README restate each other in places. Deferred. The copies in `verify/ruleset/` are deliberately not registered: they are held still by not being edited, and a detector for documents nobody may change buys nothing.
