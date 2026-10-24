# `ruledger/test-design/v5`

Written for: someone reading or writing a test design by hand, or writing something that
reads one.

A test design is what Ruledger produces, what a person edits, and what gets committed. This
is its form. It is a fourth document beside the three Rulealize already reads, and it is
made of them: a state travels as a `rulealize/state/v1`, an input as the name and arguments
a `rulealize/input/v1` carries, a draw as what a `rulealize/outcome/v1` carries. Nothing is
transcribed into a shape of its own, so a person stepping through a design by hand and
Ruledger applying it again are reading the same thing.

Ruledger produces one by walking the rule set: from the state the rule set starts in, or the
state of a case it was handed instead, take each legal input, carry on from where it lands, and write down every state not arrived at
before, until the number of states it was given have been written down. **The walk** below
means that, and `settings` holds that number.

## The whole of it

```json
{
  "$schema": "ruledger/test-design/v5",
  "ruleSet": "reversi@1.0.0",
  "settings": { "states": 3000, "candidates": 10000, "outcomes": 64 },
  "observed": ["board", "turn", "passes"],
  "collapsed": [],
  "unreached": 252,
  "edits": [],
  "states": [ ... ]
}
```

| key | |
|---|---|
| `$schema` | `ruledger/test-design/v5`, and a document without it is refused rather than guessed at. A `ruledger/test-design/v4` document is read as a v5 one whose rule set keeps no instances; v5 adds what one that does needs, below. A `ruledger/test-design/v3` document is read as one that said nothing about projections; see below. A `ruledger/test-design/v2` document is read as a v3 one: v3 adds `start`, and a v2 document began where the rule set begins. A `ruledger/test-design/v1` document is read too: v2 adds a move that waits for a value, and a v1 document has none |
| `ruleSet` | what the rule set calls itself, as `id@version` |
| `settings` | how the walk was told to go, each a count of the thing it limits: `states` to visit before stopping, `candidates` inputs to try in one state before giving up on finding more legal ones there, `outcomes` of one random draw to follow for an input. Recorded because none of it comes from the rule set, and two designs are only comparable when they agree. Each of the three leaves a mark where it bit: `"to": null`, `truncated`, `followed` |
| `start` | the state the walk began in, as a `rulealize/state/v1` document, where it was handed one — or, for a rule set that keeps instances, the documents of that position, as `documents` has them. Absent where it began in the rule set's own initial state; see below |
| `observed` | the state paths two positions have to agree on to be the same position, in the order the rule set's `state.schema` declares them |
| `collapsed` | the state paths left out of that comparison because nothing observable reads them, in the same order |
| `admits` | what each parameter the rule set leaves open admits, by input and parameter: the op of its schema and the bounds beside it, as the runtime answered them. Absent where nothing is left open; see below |
| `projections` | the names of the projections the rule set declares, in the order it declares them. Absent where it declares none; see below |
| `keys` | what the keys of each collection written as a schema admit, by collection. Absent where there is none; see [Instances kept as documents of their own](#instances-kept-as-documents-of-their-own) |
| `unreached` | how many landings the walk had no states left for: one per `"to": null` below |
| `edits` | the choices a person made, and what became of each |
| `states` | the states, in the order the walk first arrived at them |

`observed` and `collapsed` together are every field the state declares. They are worked out
from the rule set, not declared in it. The walk compares positions on `observed` alone, so
that it recognises a position it has already been in and writes it down once; comparing whole
states instead would make an audit trail enough to tell two identical positions apart, and a
rule set that keeps one would never be walked back into a position it had visited. Both lists
are written down because a diff has to know whether two designs were compared the same way.
Which fields is what it asks, not their order: the same fields declared in another order are
the same comparison, since one walk agrees with itself on the order whatever it is.

`start` is there for a rule set whose initial state is the shape of a case rather than one —
who may sign and what may be requested, empty until somebody says — and which offers nothing
from there. Which case was walked is not derived from the rule set, so it travels in the design
as the settings do, and it is the document itself rather than a path to one: whatever replays
the design begins where the walk began, even after the file it came from has changed. A
composite's component states may leave out the `ruleSet` they belong to; the runtime reads
such a state as the one that names it, and so does the walk. `#0` is wherever the walk began,
and a state is named by how the walk arrived from there, so two designs that began in
different states are not compared with each other. A v2 reader would pass over `start` and
take the walk for one from the rule set's own initial state, which is why the version moved.

`unreached` is the only quantity of its kind. It counts the times an input arrived somewhere
the walk had not been with no states left to spend, and there is no percentage, no coverage
figure and no number standing in for how much of a rule set a design covers.

## A state

```json
{
  "name": "#1",
  "from": "#0",
  "by": "place(at: e6)",
  "state": { "$schema": "rulealize/state/v1", "ruleSet": "reversi@1.0.0", "data": { ... } },
  "evaluated": 65,
  "moves": [ ... ]
}
```

| key | |
|---|---|
| `name` | `#0` for the state the rule set starts in, and the rest numbered as the walk first arrives. **A number names a state inside one design only** |
| `from` | the state this one was first reached from. Absent on `#0` |
| `by` | the input that first reached it, written `place(at: e6)`. Absent on `#0` |
| `state` | the position itself, as a `rulealize/state/v1` document |
| `documents` | in place of `state`, for a rule set that keeps instances: every document of the position, by address; see [Instances kept as documents of their own](#instances-kept-as-documents-of-their-own) |
| `evaluated` | how many candidates had their guard evaluated to find `moves`, and how many values were tried for a parameter left open |
| `truncated` | present and `true` only when the search for legal inputs stopped at `candidates` |
| `terminal` | present only when the rules call this state final. It holds `result`, which may be `null` |
| `moves` | every input that is legal here, in the order the runtime offered them |
| `refused` | the values for a parameter left open that were tried here and refused, each with the codes that refused it. Absent where none were |
| `projections` | what each projection the rule set declares says about this state, by name. Absent where it declares none; see below |

`from` and `by` are what make a state's real name: following them back to `#0` spells out
how the walk arrived, and that is the name that still means this state after the rule set
changes. Adding a field to every state changes every state and no route to one.

Four things are observable about a state and all four are here: which inputs are legal
(`moves`), whether it is final and with what result (`terminal`), where each legal input
leads (below), and what each of the rule set's projections says about it (`projections`).
They are what the runtime answers about a position, and nothing else is written down,
because nothing else can be checked.

### What a projection says

A projection is a rule set saying something about a position for a host to read — what to
put on a screen, or what an HTTP endpoint returns beside the state. For a rule set with no
screen it can be all of what the rules decide that anybody sees, so each state has every
declared projection's answer, in the text form the runtime writes an argument in:

```json
"projections": { "atLeastTen": "false" }
```

A scalar reads as it would in `args`; a sequence or a record is its JSON on one line, in the
order the runtime wrote it:

```json
"projections": { "booking": "{\"who\":\"\",\"stage\":\"empty\",\"seat\":null,\"party\":{\"size\":1,\"wants\":null},\"ready\":false}" }
```

Where the runtime faults, the fault is the answer, and it is an object, which no answer can be
taken for:

```json
"projections": { "beatBest": { "fault": "cmp.gte: Ordering is not defined for null. Test with cmp.isNull or supply a default with cmp.coalesce." } }
```

**A projection that faults stops nothing.** It is asked about a position and moves nothing,
so the walk does not need its answer to go on. A guard or `terminal` that faults stops the
walk instead: without its answer there is no next state to go to, and nothing past it could
be written down.

A field only a projection reads is among the `observed`, as a field only `actor` reads is.
Collapsed, two positions that answer a projection differently would be written down as one
state with one answer, and the other position's answer nowhere.

The names are written once at the top as well, for the reason `admits` is: which projections
there are is read off the rule set, and one added or taken away is one thing that happened,
not one per state.

## A move

```json
{ "input": "place", "args": { "at": "e6" }, "actor": "black", "to": "#1" }
```

| key | |
|---|---|
| `input` | the name of the input. Absent only on a move waiting for a key |
| `at` | which instance it is at, one key per collection on the way down, as an input document's `at` has it. Absent at the rule set's own document |
| `args` | what it was called with, by parameter name, each in the text form the runtime writes — a value for a parameter left open included. Absent when the input takes none |
| `actor` | whose move it is. Absent where the rule set does not say |
| `open` | the parameters it is still waiting for, in declared order. Present only on a move the walk had no value for; see below |
| `to` / `lands` / `as` | where applying it goes, in one of the shapes below |
| `followed` | how much of the draw `lands` is. Present only when `outcomes` cut it short; absent means all of it |

A legal input is its name, its arguments and whose it is, which is why `actor` is here and
why a field only `actor` reads is still among the `observed`.

### Where it goes

Five shapes, and they mean five different things; a sixth, a move waiting for a value, is
below with the parameter it waits for.

| | |
|---|---|
| `"to": "#7"` | applying it settles the next state, and that state is `#7` |
| `"to": null` | the walk had no states left for it. **Not "it leads nowhere"** |
| `"lands": [ ... ]` | the rules draw, so one input arrives in more than one place |
| `"as": "#5"` | only for a rule set that keeps instances: the same step was followed from `#5`, so it is not followed again here. **Not "the walk ran out"**; see below |
| none of these | a move in a state the rules call final: offered, and not followed |

The last is the walk's decision and not the rule set's. What the rules still allow in a
finished position is theirs to say, and leaving the moves out would make stopping look like
something they decided.

### A parameter left open

A parameter with a `domain` arrives with its values enumerated, and each is a move of its own.
A parameter left `open` does not: its value comes from outside, and the runtime offers the move
with the value still to come. The walk does one of two things with it, and never guesses.

Where the schema it is open to admits few enough values to name — an enumeration, a boolean, a
whole number between two bounds — every one of them is tried, exactly as a domain's would be,
and each the rules take is a move with its value in `args`. What an input's `validate` refuses
is not legal, so it is not there.

Where the schema does not, as with text, the move is written down as legal and waiting, and not
followed:

```json
{ "input": "setName", "open": ["to"] }
```

It is followed only with a value a person wrote as a choice (below), and then it is there twice:
waiting, because the rules still take any other value, and with the value, going where that
value leads.

A value tried and refused is not a move, and it is not left out either. It goes beside the
moves, with what refused it:

```json
"refused": [ { "input": "setParty", "args": { "size": "1" }, "codes": ["party.unchanged"] } ]
```

A refusal is as much an answer as a move, and the one a later version can take back without
the list of moves showing it: relax the clause, and a value that was turned away is taken.

A value the schema itself does not admit — thirteen letters where the field holds twelve — is
refused before any clause is asked. Where the parameter names that refusal with `invalid`, its
code is written like a clause's; where it does not, the rule set has given it no name, and the
refusal is written with no codes:

```json
"refused": [ { "input": "setName", "args": { "to": "Christabellas" }, "codes": ["name.malformed"] } ]
```

What each open parameter admits is written once, at the top, rather than in every state that
offers it — it is read off the schema, which the position does not change:

```json
"admits": { "setParty": { "size": { "op": "type.int", "min": 1, "max": 6 } } }
```

A move waiting for a value is one move whatever its schema allows, so this is where a bound
widened, a choice added or a text limit raised shows. The keys are the runtime's and are
carried, not read.

```json
{
  "input": "dealSeat",
  "lands": [
    {
      "probability": 0.07692307692307693,
      "drew": {
        "$schema": "rulealize/outcome/v1",
        "ruleSet": "blackjack@1.0.0",
        "input": "dealSeat",
        "draws": [
          "A"
        ]
      },
      "to": "#1"
    },
    {
      "probability": 0.07692307692307693,
      "drew": {
        "$schema": "rulealize/outcome/v1",
        "ruleSet": "blackjack@1.0.0",
        "input": "dealSeat",
        "draws": [
          "2"
        ]
      },
      "to": null
    }
  ]
}
```

One entry per branch, with what was drawn and how likely it was. `drew` is a
`rulealize/outcome/v1` document, and it is there because a draw is observable: two branches
of one input are told apart by nothing else, and a design that left it out would be naming
two different states the same.

```json
{ "input": "dealSeat", "followed": 0.23076923076923078, "lands": [ ... ] }
```

`followed` is the third thing a limit can do, beside `"to": null` for `states` and
`truncated` for `candidates`. Cutting a list of legal inputs short leaves a list of legal
inputs; cutting a draw short leaves a distribution that no longer sums to one, and the
branches that survive cannot say so between them. **Adding them up is not a way to find out
either**: thirteen thirteenths come to 0.9999999999999996, so a sum below one is as much what
floating point looks like as what a cut looks like. The runtime answers the question and the
answer is carried here, on the moves the runtime says were cut short and on no others.

**It is carried and not reported.** A share of something is the one shape Ruledger does not
print, because a number that says how much of a thing was got is the number this method was
written against, and the only quantity it prints is `unreached`, which is a count of places. A
diff between two versions that followed different amounts of a draw says so by their branches
differing, which is what moved the share in the first place.

## Instances kept as documents of their own

A rule set whose `uses` holds something `many` — a month holding any number of claims — keeps
each instance as a document of its own, in a store its `requires` names. Its position is not one
document, so a state says `documents` where it would say `state`:

```json
{
  "name": "#3",
  "from": "#2",
  "by": "claims.enterAmount@1(amount: 2)",
  "documents": [
    { "at": "/",        "state": { "$schema": "rulealize/state/v1", "ruleSet": "claim-month@1.0.0", "data": { ... } } },
    { "at": "claims/1", "state": { "$schema": "rulealize/state/v1", "ruleSet": "claim@1.0.0", "data": { ... } } }
  ],
  ...
}
```

The rule set's own document is at `/` and first, even before anything has written it; then each
instance at its address, nested ones under the instance holding them
(`months/2026-10/claims/1`), in the order they were created, because that order is part of the
position — a projection reads the instances in it. Two positions are the same position when
the rule set's own document agrees on `observed`, and every instance is at the same address,
in the same order, with the same document.

The walk keeps the documents in memory, in place of the store the rule set names: evaluation is
pure, so where they are kept changes no answer. A key the store assigns is `1`, `2`, … — one
more than the collection holds — so it follows from the position and not from the route.

**A move says where it is.** The runtime finds moves one place at a time, so they are asked of
the rule set's own document and then of each instance, and a move at an instance carries the
`at` it was found at and writes it after the name:

```json
{ "input": "submit", "at": { "claims": "1" }, "to": "#7" }
```

That move is `submit@1`, and nested keys go outermost first, `months.claims.enterAmount@2026-10/1(amount: 2)`.
A refusal at an instance carries `at` the same way.

**A step already followed is not followed again.** A month can always create another claim, so
a walk that followed every move from every position would go on creating them for as long as it
was allowed to and reach nothing else. It need not: a step reads the rule set's own document
and the instances on the way to where it is, and nothing else — Rulealize refuses a guard that
reads a document its transition does not write — so the same input from the same documents is
legal or not, and writes what it writes, whatever other instances the position holds. A step
already followed from those documents is written down as legal and not followed, with where it
was:

```json
{ "input": "create", "as": "#0" }
```

That settles what the step decides, not what follows it. That nothing is lost is measured
(`verify/Keeping.cs`): every position of up to four claims of `claim-month`, walked to the end
breadth first straight through the runtime, takes 112 different steps — a month and a claim
before, an input, a month and a claim after — and the walk takes all 112 in 66 states, and
ends. Where the claim's approver is drawn (`claim-drawn`), each thing an input can draw is a
step of its own: up to three claims, 299 steps, and the walk takes all 299 in 113 states. What
it does not write down is a position it did not visit: what a projection reading
every instance would say about a month holding four drafts is not in the design, because no
step needed that position.

**Where the rules draw, each branch is followed.** What may happen is asked of the documents the
input reads, through the store, and each branch is applied and committed on its own, so a move
at an instance `lands` in more than one place as any other does. A step already followed is
written `as` once, whatever it draws: its branches are the ones it had where it was followed.

**A collection keyed by a schema has an instance at every key the schema admits.** Where the
schema names its keys — an enumeration — every one is walked. Where it does not, the walk goes
only to the keys a person wrote in a choice's `at`, and writes down in every position that it
is waiting for one:

```json
{ "at": { "months": null }, "open": ["at.months"] }
```

That is `months@?`. It has no `input`, because what is offered at a month is not known until
somebody says which month, and it is there so that a position with nothing else on offer is not
read as a final one. What each such collection admits is written once at the top, as `admits` is
for a parameter left open, so that a bound that moved shows somewhere:

```json
"keys": { "months": { "op": "type.string", "minLength": 7, "maxLength": 7 } }
```

## An edit

The one place a person writes.

```json
{ "state": "#0", "input": "assign", "args": { "who": "cy", "shift": "fri-am" }, "carried": "yes" }
```

| key | |
|---|---|
| `state` | the state to choose from, written either as how the walk arrives there — `#0 + submit` — or as the number that state has in the design being read |
| `input` | the name of the input to take first |
| `at` | which instance it is at, by alias, as a move's `at` has it. Absent at the rule set's own document. A key a collection keyed by a schema could not list is how a person says which instance to walk |
| `args` | what to call it with. Absent when the input takes none. For a parameter left open, the value to follow it with |
| `carried` | `yes`, `not legal here`, or `not reached`. Written by Ruledger; absent means `yes` |

Writing one by hand needs `state` and `input`, and nothing else. Ruledger writes `state`
back out the long way, because the number is a name inside one design and a choice has to
survive the rule set changing. A value for a parameter left open goes back out as the move
is written, for the same reason that it is compared that way: where the parameter's schema is
a number, `3.0` and `03` name the move written `3`, and a boolean written `True` names `true`.

**An edit says which input to take first, and never what was found there.** Everything
downstream of a choice is worked out again from the rules. There is nowhere in this document
to write an observation, because an observation written by hand would be the only thing in a
test design that could be wrong.

**A choice is also how a value nobody could enumerate gets walked.** An edit that gives a
value for every parameter a move leaves open — `{ "state": "#0", "input": "setName", "args":
{ "to": "alice" } }` — is followed with that value, and first. It is still a choice and not an
observation: whether `alice` is a legal name is worked out from the rules, and a value the
rules refuse is `not legal here`.

**A choice that could not be taken stays in the file, with `carried` saying which of two
ways it went missing.** It is never dropped, and it never falls back to the input the
machine would have picked. The two ways are different enough to be worth telling apart: the
state is still there and the input is not legal in it any more, or the walk stopped before it
reached a state of that name.

## Reading it as lines

A test design is the document of record; a line-oriented rendering of it is a view. v1 does
not build one. What the form guarantees is that one is a fold over `states` and nothing else:

```
#0
    legal   place(at: e6) -> #1
            place(at: f5) -> (not reached)
            place(at: c4) -> (not reached)
            place(at: d3) -> (not reached)
    final   no
```

Every line above comes from one state object, in the order `states` already has them, with
no lookup outside it but resolving `to` to another state's `name`. Nothing has to be
recomputed and nothing has to be walked again.

That is the whole of what "the form does not block a rendering" means, and it is measured
rather than asserted: `verify/Rendering.cs` is the smallest such renderer, written against
the JSON rather than against Ruledger's own types, and it folds approval, reversi,
blackjack, roster and tally with nothing else to hand, tally's projections among them as
`says` lines. The block above is its output.

## Two designs

Ruledger compares two designs by route — `from` and `by` followed back — and never by what
is in `state`. Adding a field to every state moves every state and no route, so a diff by
content would report a change the rules did not make and would lose every choice a person
had made. This is measured, on a version of reversi that counts its own moves: all 3,000
states differ as documents, and all twenty choices survive.

Two designs walked with different `settings` are not compared at all. They visited different
states because they were told to, and none of that is the rule set deciding differently.

A state both designs have is **decided differently** when any of these moved: the four
observables, what was refused there, and the limit that can cut the first of them short:

| | |
|---|---|
| it lost a legal input | |
| it gained one | |
| a legal input lands somewhere else | where the rules draw, this is asked of each branch: one appearing or going, a different value drawn, the same branch at a different chance, or a different `followed` |
| its ending moved | `terminal` gained, lost, or a different `result` |
| its `truncated` moved | it started or stopped hitting `candidates`, so the two say different amounts about it |
| a refusal came, went, or changed its codes | a value that moved between refused and legal is already a legal input lost or gained, and is said once |
| a projection says something else | for a projection both versions declare; a fault that came or went is one |

What an open parameter admits is compared as well, once per parameter rather than per state,
where both designs record it; one that moved is a difference like any of these. What the keys of
a collection admit is compared the same way. So is a
projection only one version declares, said once by name: a host reads a projection by its
name, so a rename is a field one client stops getting and another starts to. A v3 design
recorded no projections, and none of this is asked of it.

A state only one of the two designs has is not in that count. It is reported on its own line
either way, never left as a number: the walk stopped before reaching it this time, or it
reached somewhere the earlier walk had not.

**Two designs can also disagree about `observed` itself**, and then the walks rejoined in
different places and some of what the diff says is that rather than the rules. Adding a field
that something observable reads is enough: the rules decide exactly what they decided, and
positions that used to be the same position are not any more. There is no telling the two
apart from inside the diff, so it says which it cannot tell apart, and the exit code is the
same as the rules moving — a build that went green on it would be a build that passed because
nobody was told.
