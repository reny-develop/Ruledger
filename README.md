# Ruledger

Ruledger implements
[Rule-Derived Test Design](https://github.com/reny-develop/rule-derived-test-design) on top of
[Rulealize](https://github.com/reny-develop/Rulealize).

It walks a ruleset and writes down, for every state it visits, everything that can be observed
there: which inputs are legal, whether the state is terminal and with what `Result`, and where
each legal input leads. That enumeration is the test design. It is mechanical, so it does not
forget. It is also tautological, so it does not know what is correct: it states what the
ruleset decides, never what it should decide. Supplying that judgement is the developer's job,
and it is the only part of the work Ruledger refuses to guess at.

Change the ruleset, run it again, and the diff is the blast radius — which states gained or
lost a legal input, which outcomes moved. Edits the developer made to the previous design are
carried over; whatever could not be carried is reported instead of silently reset. How far the
walk goes is a budget, and what the budget did not reach is reported as not reached, never as
not there.

The walk is reproducible. The same ruleset visits the same states in the same order every
time: no randomness, no seed. Everything else — the budget, how far the walk backtracks — is
configuration, and it is recorded alongside the design.

## What v1 does

| | |
|---|---|
| Derive the test design, filling in the concrete values and the expected results | **In** |
| Apply the previous design to a new version of the ruleset and report what changed | **In** |
| Carry a human's edits across a change to the ruleset | **In** |
| Verify anything outside the ruleset — screens, persistence, integrations | Out. Not a limit in principle: what Ruledger reaches is what the ruleset expresses, and that is extended by adding vocabulary, not by changing Ruledger |
| Visualize or edit the ruleset | Out. A ruleset is still JSON, which is hard to hand-write even for an engineer |
| Record approvals | Out. Approval is committing the design; expiry is the diff coming back non-empty. Git already does both |

The reasoning behind each line is in the thesis — see §6.5 and §6.6.

## Status

Pre-implementation. This repository holds nothing but this README yet. The tool is C#: it
needs the Rulealize runtime, and reading the ruleset statically is part of the same job, not a
separate program in another language.

## License

Apache-2.0. See [LICENSE](LICENSE).
