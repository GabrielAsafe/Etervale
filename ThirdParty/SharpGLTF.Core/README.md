# SharpGLTF.Core

Source from [SharpGLTF v1.0.7](https://github.com/vpenades/SharpGLTF/tree/acbc151e70d65ceb8bec24fa95f0067d0e5ed933),
MIT licensed (see LICENSE). The Core C# sources and the two shared source files are included, with LF line endings.
The SDK project targets .NET 10 and uses the upstream public strong-name key to keep the assembly identity.
The original signing private key is not included. The separate Toolkit NuGet dependency remains available to tools.

The `IO/UnknownNode.cs` reader uses `JsonNode.Parse` and writes the same JSON DOM. Its six broad `DynamicDependency(All)`
attributes are unnecessary and would root reflection-only generic JSON APIs in a trimmed app. The local source removes
only those attributes; unknown extensions and their values are still preserved. Trimming warnings remain errors.

KTX2 header fields are decoded explicitly as little-endian integers rather than assigned through a memory cast.
Validation diagnostics read logical indices directly from the built-in indexed schema objects; unindexed or custom
objects retain their type name without reflection. The validation conditions and glTF format handling are unchanged.
