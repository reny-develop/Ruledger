## Project
Ruledger implements [Rule-Derived Test Design](https://github.com/reny-develop/rule-derived-test-design) on top of [Rulealize](https://github.com/reny-develop/Rulealize). It walks a rule set and writes down, for every state it visits, everything that can be observed there.

One C# class library (`src/Ruledger/`, `net10.0`, nullable + implicit usings, a missing XML comment is an error), an xUnit suite in `test/`, and `verify/` for the material the measurements run against. The command line tool is not written yet.

`doc/v1.md` decides what is in scope, what the acceptance conditions are and what order things are built in. Read it before changing anything, and check a change against it afterwards. **It is written in Japanese and is deleted when v1 lands**, along with the thesis and the verification record in the sibling repository — readers get proof and experience by running Ruledger, so the two READMEs have to carry whatever must survive. Publication happens at v1 and not before, and this file will need rewriting when that day comes.

The `doc-sync` machinery in the workspace covers folders named `Rulealize*` and does not reach here. The discipline still applies; nothing runs it for you.

## Fixtures
`verify/ruleset/` holds the documents the measurements are about. Some were copied from `Rulealize/ruleset/` and the rest were written here.

**Do not change what is in there.** Adding a document for a new measurement is ordinary work; editing one that is already there is not. A measurement is a claim about a document, and a document that moves underneath it turns the claim into a report about whatever happened to be in that file that day.

Nothing reads the sibling repositories at run time either. Rule-Derived Test Design goes public, and a stranger with one clone has to be able to run the measurement — a test that reaches across the workspace can only be run by somebody who has the whole of it.

Where a measurement does not bite on the material that is here, write material where it does, and say in the document why it exists. `process` has seven states and never returns to one, so nothing about collapsing shows there; `seats` was written because of that.

A measurement about a change to a rule set needs the rule set both ways, so some of these come in pairs and the pair is the fixture: `reversi-swapped`, `reversi-strict` and `reversi-counted` are each one line away from `reversi`, and `approval-results` and `roster-seniors` from theirs. A later version raises `version` and says in its comment which line moved and why that line.

## Measure before writing it down
Reasoning from the code gives a hypothesis. Build the fixture, run it, and only then write the claim into `doc/v1.md` or the sibling repository. When a measurement contradicts something already written, say so plainly and correct it — the project exists because unmeasured judgement is unreliable, and an unmeasured claim in its own documents gives that away.

## What is public
v1 ships a `dotnet tool` and nothing else. The library is internal in every sense that matters: keep the public surface to what the tool and the tests need, prefer `internal`, and do not add a public member to make a test easier — `InternalsVisibleTo` is already there.

## Words
No new terms. Proper nouns are Ruledger and Rulealize; everything else is ordinary language.

In Japanese, never write 文書 for a rule set. Japanese uses the same word for the JSON and for the prose about it, so a reader has to work out which one is meant every time. A rule set is ルールセット, a state is 状態, an input is 入力. 状態パス is a place inside a state (`assigned`); 状態の名前 is how the walk arrived (`#2 = #1 + release(月)`). English keeps "document", which is what Rulealize calls the four it reads.

## What a test design may say
Three things are observable about a state and only those are written down: which inputs are legal, whether it is final and with what result, and where each legal input leads. A legal input is its name, its arguments and whose it is.

The one place a person writes is a choice — from this state, take this input first. Everything downstream of it is worked out again. An observation written by hand would be the only thing in a test design that can be wrong, so there is nowhere to write one.

No number that stands in for quality. Ruledger reports what a budget did not reach and nothing else of the kind; a percentage would invite exactly the misreading the method was written against.

Nothing is hidden when it fails. A choice that could not be carried is reported and never dropped back to what the machine would have picked, and a place the walk did not reach is reported as that rather than as nothing being there. There should be no code path that quietly reverts to a default.

## The walk
Deterministic: the same rule set and the same settings visit the same states in the same order. No randomness, no seed, and nothing that depends on the enumeration order of a hash table. Everything that is not derived from the rule set is a setting and travels with the test design.

Written on an explicit stack. The depth of a walk is a budget away from unbounded — a rule set holding a history never returns to a state it has been in — and recursion breaks at three thousand frames.

Splitting a rule set into a composite and the parts it holds is a way of writing it, not a different set of rules, so the composite is walked as the one document it stands for would be. `verify/ruleset/` keeps both forms of two processes for that reason.

## Reading a rule set
The runtime answers everything about behaviour; the document is read only to work out which state a position is compared on. That is the one place Ruledger knows a vocabulary by name — `state.get`, `state.update`, `def.ref`, `def.call`, `rec.at`, and the `$` and `#` shorthands. Adding a sixth is a decision, not a detail: say in the code why the alternative did not work.

Where the shape of the document cannot settle a question, keep the field rather than collapse it. Keeping too much costs rejoining; collapsing too much hides something observable.

## Building and testing
`dotnet test test/Ruledger.Tests.csproj`. The vocabularies come in as NuGet packages and land beside the test assembly, which is how the runtime finds them; the plugin repositories are never built from source for this. The suite takes three and a half to four minutes: Reversi and blackjack walking three thousand states, and the roster measurement deriving its test design twenty times over to stack twenty choices the way a person makes them.
