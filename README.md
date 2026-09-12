# Gaia
A C# implementation of the Game AI Architecture (GAIA) described by Kevin Dill in Game AI Pro.

The primary source is Kevin Dill and Christopher Dragert, "Modular AI", Game AI Pro 3, CRC Press,
2017, chapter 8. Doc comments cite it as "Modular AI" with a section number, in a seealso element
on each type that implements one of its concepts. Sources for individual techniques are cited
beside the code that uses them.

The public surface follows one rule. Structure is public, shipped leaves load from configuration.
A host loads a brain and the whole tree beneath it through GaiaLoader, or assembles brains,
reasoners and options in code, while the shipped considerations, actions, weight functions,
regions and targets are built by the factories from configuration files, and a game's own
components join them by registering on the factory set.
