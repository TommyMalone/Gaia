# Gaia

Gaia is a C# implementation of the game AI architecture from Kevin Dill and Christopher Dragert's "Modular AI" (Game AI
Pro 3, CRC Press, 2017, chapter 8). It is engine agnostic. Brains are written in XML files that the game
loads, and a game adds its own actions and considerations by registering them with the loader.

A brain is a tree. Reasoners pick one of their options each cycle. An option bundles the considerations that score it
with the action that runs when the option is chosen. A consideration reads something the game knows (a flag on a
blackboard, a distance, a list of contacts) and turns it into a weight.

## A brain

This is the guard from `Gaia.Core.Tests/Integration/guard.xml`, trimmed to two of its four options:

```xml
<Brain>
    <Reasoner Type="DualUtility">
        <Option Type="ConsiderationAndAction" Name="Fight">
            <Consideration Type="EntityExists" Variable="Threats">
                <WeightFunction Type="Boolean">
                    <True Rank="10"/>
                    <False Veto="true"/>
                </WeightFunction>
            </Consideration>
            <Action Type="Record" Name="Fight" Done="false"/>
        </Option>
        <Option Type="ConsiderationAndAction" Name="Patrol">
            <Consideration Type="Tuning" Addend="1" Rank="0"/>
            <Action Type="Record" Name="Patrol" Done="false"/>
        </Option>
    </Reasoner>
</Brain>
```

The guard fights while the game's `Threats` list has anything in it and patrols otherwise. A dual utility reasoner first
narrows to the options with the highest rank, then picks among those by weight. While a threat is known, Fight has rank
10 and is chosen. When the list is empty, Fight is vetoed and Patrol (rank 0) is the only option left. The full file has
two more options: Investigate, which follows a recent noise, and Alarm, which reacts to a new one.

`sniper.xml` in the same folder is the bigger example. The sniper hides until a target enters its kill zone, then runs
a sequence of steps (set pose, raise weapon, aim, fire) under a subreasoner. If it comes under fire it retreats when the
line of retreat is clear, and fights back when it isn't.

## How a game uses it

A game writes what it senses onto a blackboard under the names the brain file reads, such as `IsUnderFire` or the
`Threats` list. The game loads the brain through `GaiaLoader`, assigns it to an actor, and calls `Actor.Update()` once
per cycle. The reasoners decide and the actions run.

The code/data split is deliberate. The structural types (brains, reasoners, options) are public classes a game can
assemble in code. The leaves (considerations, actions, weight functions, regions, targets) are built by
factories from the configuration files. Anything the library doesn't ship, like the `Record` action in the files
above, is registered on a `FactorySet` by name before loading, and the loader builds it from the same files.

## Building and testing

The library targets .NET Standard 2.1. The test projects target .NET 10.

```
dotnet build Gaia.sln
dotnet test Gaia.sln
```

There are three test projects:

- `Gaia.Core.Tests` (1,065 tests): the library's specifications.
- `Gaia.Core.HostSpecs` (61 tests): the library driven the way a game drives it, through the public surface only.
- `Gaia.Authoring.Tests` (103 tests): the editing layer.

`coverage.sh` runs everything with coverage and opens an HTML report.

## Where to start reading

To read the code, start with these files, in this order:

1. `Gaia.Core/Brains/Brain.cs`. The root of the tree. Enabled once, updated every cycle.
2. `Gaia.Core/Reasoners/DualUtilityReasoner.cs`. How an option gets picked, by rank and then weighted chance.
   `RuleBasedReasoner.cs` and `SequenceReasoner.cs` next to it are the two simpler policies.
3. `Gaia.Core/Options/OptionBase.cs`. What an option holds, and how its considerations combine into a rank and a
   weight.
4. `Gaia.Core/Considerations/BooleanVariableConsideration.cs` and
   `Gaia.Core/WeightFunctions/BooleanWeightFunction.cs`. One blackboard read, and the function that turns it into
   weight values.
5. `Gaia.Core/Factories/GaiaLoader.cs` and `FactorySet.cs`. How a file becomes a tree, and how a game registers its
   own components.
6. `Gaia.Core.Tests/Integration/LoadedSniperSpec.cs`. The sniper loaded from its file and run through its
   situations.

Doc comments cite "Modular AI" by section number in a `seealso` element on each type that implements one of its
concepts. Sources for individual techniques are cited next to the code that uses them.

## Design choices

- A steady-state decision cycle doesn't allocate, so hundreds of brains can run without any work for the GC
  (`Gaia.Core.Tests/Integration/AllocationSpec.cs`).
- Decisions are reproducible from a seed, a clock and a file. Two brains loaded from the same file don't share a
  random stream (`Gaia.Core.Tests/Integration/DeterminismSpec.cs`).
- A variable the game hasn't written yet reads as whatever the file says it should (the `Missing` element in
  `sniper.xml`), neutral by default. It is never a guessed false or zero.
- When a component refuses a value from a file, the loader reports it with the line number and continues, so one load
  reports every problem rather than the first.
- `Vector3`, `Rotation` and `Transform` (in `Gaia.Core/Spatial`) are Gaia's own types. A game converts at the engine
  boundary, and nothing inside the library depends on an engine.

## Status

The core library covers the architecture in chapter 8: brains, the three reasoners, options, the shipped
considerations and weight functions, blackboards, regions, targets, the loader and the factories, plus diagnostics
that record each cycle's decision for a tool to read.

`Gaia.Authoring` is the beginning of an editor for brain files. The document model (with undo) and the resolver (which
works out what each element in a file is and what it can contain) are done. A graphical tool on top of those is
next.
