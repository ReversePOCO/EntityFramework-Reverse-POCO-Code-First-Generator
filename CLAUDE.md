# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Does

This is the **EntityFramework Reverse POCO Code First Generator** (EFRPG) — a Visual Studio extension and T4 template system that reverse-engineers an existing database and generates EF Code First POCO classes, DbContext, interface, configuration mappings, enumerations, fake DbContext (for unit testing), and stored procedure/TVF callers.

The generator is distributed as a VSIX (Visual Studio Extension) containing a T4 item template. Users add a `Database.tt` file to their project; saving it triggers the generator.

## Solution Structure

- **`Generator/`** — Core library (`Efrpg` namespace, `net48`). All generation logic lives here.
- **`BuildTT/`** — Console app that compiles the `Generator/` C# project into a single `.ttinclude` file (`EF.Reverse.POCO.v4.ttinclude`) that ships with the extension.
- **`EntityFramework.Reverse.POCO.Generator/`** — The `.ttinclude` file output from BuildTT, plus the `Database.tt` template that users add to their projects.
- **`EntityFramework Reverse POCO Generator/`** — VSIX packaging project.
- **`Generator.Tests.Unit/`** — Unit tests using NUnit (targets `net48`, requires SQL Server LocalDB for some tests).
- **`Generator.Tests.Unit.EFCore/`** — EF Core-specific unit tests (targets `net8.0`).
- **`Generator.Tests.Integration/`** — Integration tests that actually connect to SQL Server and write generated files to `~/Documents`.
- **`Generator.Tests.Common/`** — Shared test constants and helpers (`netstandard2.0`).
- **`Tester.Integration.EFCore8/`, `EFCore9/`, `Ef6/`** — Projects that consume the generated output and verify it compiles/runs correctly.
- **`Tester.Repository/`**, **`Tester.BusinessLogic.EfCore/`** — Support projects for integration testing.

## Key Architecture

### Generation Pipeline

1. **Settings** (`Generator/Settings.cs`) — static class holding all configuration. The `.tt` file sets these before running.
2. **DatabaseReader** (`Generator/Readers/`) — reads schema from the database. `DatabaseReaderFactory` selects the reader based on `Settings.DatabaseType` (SqlServer, PostgreSQL, SQLite, MySql or Oracle).
3. **Generator** (`Generator/Generators/`) — abstract base class with `GeneratorEf6` and `GeneratorEfCore` implementations. Selected by `GeneratorFactory` from `Settings.TemplateType`: `Ef6` gets the EF6 generator, everything else EF Core.
4. **Template** (`Generator/Templates/`) — abstract base class with `TemplateEf6` and `TemplateEfCore8` implementations. Mustache-based string templates. Selected by `TemplateFactory` based on `Settings.TemplateType`.
5. **Filtering** (`Generator/Filtering/`) — `FilterSettings` and `DbContextFilter` control which schemas/tables/columns/stored procs are included.
6. **FileManagement** (`Generator/FileManagement/`) — handles writing output files; different implementations for EF Core projects, VS4.x projects, and null (test mode).

### The `.ttinclude` Build Process

`BuildTT` concatenates all C# files from `Generator/` into one large `EF.Reverse.POCO.v4.ttinclude` file. **Never edit the `.ttinclude` directly** — edit the source files in `Generator/` and run `BuildTT` to regenerate.

**Always run BuildTT before committing.** Any change under `Generator/` leaves the checked-in `.ttinclude` stale until BuildTT is run, and the stale copy is what ships to users — the generator's own tests all run against `Generator/` source and will stay green while it rots. Run it and commit the regenerated `.ttinclude` in the same commit as the source change. Re-running BuildTT on an already-current tree rewrites the file byte for byte, so `git status` staying clean is the proof it was up to date.

Version is controlled by `BuildTT/version.txt`. That version covers the VSIX, the item template and the `.ttinclude` **only** - it does not version the `efrpg` dotnet tool, which releases on its own cadence (see below).

> **Gotcha when editing anything under `Generator/`:** `BuildTT/Application/BaseWriterStrategy.cs` strips the literal string `Efrpg.` from every code line on its way into the `.ttinclude`, including inside string literals. So a message ending `"... install -g Efrpg."` silently becomes `"... install -g "`. Never let `Efrpg` be immediately followed by a full stop in source under `Generator/`.

> **Second gotcha:** the concatenated source sits inside a T4 class-feature block, so the T4 parser reads a literal
> `<#` or `#>` anywhere in it - a string, a comment, anything - as a nested block and every template fails with
> "An unexpected start or end tag was found within a block". The C# project compiles fine, and the generator's
> tests stay green, because only the `.ttinclude` sees it. Spell such a string in two pieces (`"<" + "#"`).

### Other T4 hosts

The include has no dependency on Visual Studio beyond `hostspecific="true"` and three calls: `Host.TemplateFile`,
`Host.ResolvePath` and `Host.ResolveParameterValue` for the namespace hint. Only Visual Studio supplies that hint;
`TextTransform.exe` and Rider return nothing, so the prologue falls back to `ProjectNamespace.Resolve`, which
derives the same answer from the nearest `.csproj`.

Rider (the ForTea plugin) compiles the template from its editor document, which uses LF, so every verbatim string
in the include arrives with LF endings. Anything that splits generated text on `Environment.NewLine` alone
therefore silently fails to split there: `CodeOutput.AddCode` and the Mustache newline stripping split on both
endings for that reason. Keep it that way for any new code that splits multi-line literals.

Rider also writes the line break that follows `<#@ include #>` in the user's `.tt` (its collector drops the one
after every other directive and statement block, and says so in `T4InfoCollectorSateSeenSpecialBlock.cs`). It is
emitted in document order, after the include's prologue has run, so nothing in the `.ttinclude` can remove it;
Rider output starts with one blank line. Accepted for now.

### Wire format contract

The `efrpg` dotnet tool reads the database and writes XML to stdout; the T4 template parses it back. `EfrpgResultXmlWriter.cs` (in the separate **Efrpg** repository) and `Generator/Readers/EfrpgResultXmlReader.cs` are the two halves of that contract.

**The tool source is not in this repository.** It lives in its own repo, which is also where the NuGet package is published from. They can never share a binary in any case: the reader must remain plain source under `Generator/` because BuildTT concatenates it into the `.ttinclude`, and the whole point of the tool split is that the template needs no installed assemblies. Treat the XML itself as the interface.

**How drift is caught now.** Each side hand-maintains a copy of every wire DTO, and no build sees both, so a member added to one copy alone is never populated on the other - silent wrong output, not a compiler error. `Generator.Tests.Unit/WireContractTests.cs` guards this using the captured payloads in `Generator.Tests.Unit/WireContract/`: it reflects over the DTO type graph and fails when a member has no matching name in the payload. The Efrpg repository holds the same fixtures and the mirror test, which fails when its writer stops emitting something the fixtures contain.

So when you bump `SchemaVersion`: regenerate the fixtures (the command is in the comment at the top of each one) and copy them to **both** repositories. That is the one manual step, and it happens exactly when you are already changing the wire format. This replaced `ParallelSourceTests`, which diffed the two copies of the C# and needed both repos on disk.

**The compatibility direction is asymmetric.** The tool is installed globally and shared by every project on the machine; the template is pinned inside each project and rarely upgraded. So *newer tool + older template* is the normal case and must keep working. Only *older tool + newer template* is an error.

That is why the check is a floor, not a match:

- `WireFormat.SchemaVersion` (tool) - the version of the XML this tool emits.
- `EfrpgResultXmlReader.RequiredSchemaVersion` (template) - the minimum the template can work with.
- The reader fails loudly when `schemaVersion < RequiredSchemaVersion`. A missing attribute reads as `0`, which correctly rejects any tool built before the handshake existed.

**Three rules keep the floor check sound. Breaking rule 2 causes silent data corruption, not an error:**

1. **Additive only.** New attributes and elements may be added at any time. Never remove or rename an existing one - the reader ignores attributes it does not know about, which is exactly what makes forward compatibility free.
2. **Semantics frozen.** Never change the meaning or encoding of an existing attribute. An older reader will parse the new encoding without complaint and produce wrong output. If the meaning must change, add a *new* attribute, keep emitting the old one with its old meaning, and retire it only on a major release.
3. **Bump `SchemaVersion`** whenever you add something a template could come to depend on. Never bump it for template-side-only changes.

Worked example of rule 2: `StoredProcedureParameter.DefaultValue` distinguishes null (the DB default is `NULL`) from empty string, because null is what makes an `AllowNullStrings` parameter generate as `string?`. The writer therefore *omits* the attribute for null rather than writing `defaultValue=""`. Changing that encoding now would silently break every template already in the field.

**One removal happened, before v4 shipped.** Multi-context generation was taken out of both halves in one go:
the template stopped sending `--multi-context` and reading `<MultiContextSettings>`, and the tool stopped
offering the flag and writing the element. `SchemaVersion` stayed at 1, because a newer template with an older
tool works (the template no longer asks) and the only templates that ever read the element were the
pre-release v4 ones in this repository. That is the one moment rule 1 does not bind; after release it does.

The enum exchange (`--enums-base64`, `<EnumData>`) carries no version stamp by design: it is a second invocation of the same binary within one run, and the first call has already passed the floor check.

`SchemaVersion` governs the **whole protocol, in both directions**, not just the XML the tool returns. If the template ever starts *requiring* a tool capability on the request side, bump `SchemaVersion` too - the floor check is the only thing that can reject a tool too old to understand the request, and it fires before a confusing downstream failure.

**Illegal XML characters.** Databases store the whole C0 control range and, because `nvarchar` is UCS-2 with no pairing validation, lone surrogates too. XML 1.0 permits none of them. Both writers therefore pass the finished tree through `XmlSanitiser.Sanitise` immediately before `ToString()` - one chokepoint, not one call per attribute, so a new attribute added later cannot forget to opt in. Do not move sanitising back to the individual `XAttribute` calls. This is not a wire-format change: no attribute changes meaning or encoding and no legal value is altered, so it needs no `SchemaVersion` bump.

The failure it prevents is unusually nasty: every read succeeds, `XElement.ToString()` then throws, the complete payload is discarded, and the template reports only "efrpg tool returned no output". `Program.cs` builds the XML string before writing a single byte to stdout, so stdout is always either a whole document or empty, never truncated.

### Secrets

Connection strings are passed to the tool over **stdin** (`--secrets-stdin`, see `SecretsXml` on both sides), never on the command line. Command-line arguments are captured by process listings and, more importantly, by command-line audit logging - Sysmon event 1, EDR telemetry, ETW - which forwards them to a SIEM and to anyone with access to it.

**Do not "fix" this with encryption.** It was considered and rejected. Both processes run as the same user with no shared secret and no way to establish one, so any key must ship inside `EF.Reverse.POCO.v4.ttinclude`, which is a plaintext file distributed to every user. Anyone who can read the command line can read the key off their own disk. That applies to XOR, AES and everything in between - the cipher is not the weakness, handing over the key is. Keeping the value off the command line is the fix.

`--connection` and `--connection-base64` remain for direct CLI and CI use and are documented in `--help` as visible in process listings. Base64 is transport encoding to survive shell quoting, **not** protection. The T4 templates must always use stdin.

Note the connection string is also sitting in plaintext in the user's `Database.tt`, usually in source control. Stdin does not make it a secret; it stops it spreading from the repo into security logging infrastructure.

`EfrpgToolRunner.Execute` is the single place any of this happens - the T4 templates, the enum pass and the integration tests all route through it. Do not hand-roll another `ProcessStartInfo`: stdin must be written and closed before the stdout/stderr drain threads are joined, or the tool blocks forever on `ReadToEnd`.

**Versioning.** The tool now lives in its own repository, and its `Efrpg.csproj` `<Version>` is ordinary SemVer for the NuGet package, deliberately **independent** of `BuildTT/version.txt`. Do not re-couple them - two repos on separate release cadences would otherwise be forced into lockstep releases forever. `SchemaVersion` is a separate monotonic integer and is the only thing any code branches on; the tool version travels in the payload as `toolVersion` purely so error messages can name it.

### Licensing

The licence is enforced by the `efrpg` tool, not the template. The tool reads and verifies `Documents\ReversePOCO.txt`
and, without a valid licence, cuts the payload down to 10 keyed tables and 10 routines before writing a byte
(`Licensing/TrialLimits.cs` in the tool repo). The outcome arrives as the `<Licence>` element (`RawLicence`), and the
template only reports it: the header, the trial banner and the "Licence file ... not found" messages come from
`Generator.ReportLicence`, in the words the template used when it read the file itself.

Never put a licence check or a trial trim back into `Generator/`. The `.ttinclude` ships as editable source, so
anything there can be deleted - which is why it moved. A schema version 1 tool enforces nothing, which is why
`RequiredSchemaVersion` is 2.

The tool chooses the trial's 10 tables before the template's filters run, because filters are C# in the user's `.tt`
and cannot reach the tool. Filters therefore narrow within those 10 rather than choosing them.

### Template Types

- `TemplateType.EfCore9` / `EfCore8` → uses `TemplateEfCore8` class with Mustache templates inline in C#
- `TemplateType.Ef6` → uses `TemplateEf6` class

## Build Commands

**`dotnet test` and `dotnet run` silently do nothing on the old-style `net48` projects** - `BuildTT`,
`Generator.Tests.Unit` and `Generator.Tests.Integration`. There is no test host, so `dotnet test` restores,
exits 0 and runs zero tests, which reads as a pass. Use `dotnet vstest` against the built assembly instead,
and run `BuildTT.exe` directly.

```bash
# Build the solution
dotnet build EF.Reverse.POCO.GeneratorV4.sln

# Regenerate EF.Reverse.POCO.v4.ttinclude, Database.tt and settings-metadata.v4.json
dotnet build BuildTT/BuildTT.csproj
(cd BuildTT/bin/Debug && ./BuildTT.exe)

# Run unit tests (no DB required for most).
# The 6.2.0 adapter is required: 4.x throws "Unknown framework version 10.0" when the .NET 10 SDK is present.
dotnet vstest Generator.Tests.Unit/bin/Debug/Generator.Tests.Unit.dll --TestAdapterPath:packages/NUnit3TestAdapter.6.2.0/build/net462

# Run a single test fixture
dotnet vstest Generator.Tests.Unit/bin/Debug/Generator.Tests.Unit.dll --TestAdapterPath:packages/NUnit3TestAdapter.6.2.0/build/net462 --TestCaseFilter:"FullyQualifiedName~PluralisationTests"

# Run EF Core unit tests - SDK-style net8.0, so dotnet test works here
dotnet test Generator.Tests.Unit.EFCore/Generator.Tests.Unit.EFCore.csproj

# Run integration tests (requires SQL Server with EfrpgTest and Northwind databases, and the *.tt files
# must have been executed first - see Testing Patterns). The trait name is TestCategory, not Category.
dotnet vstest Generator.Tests.Integration/bin/Debug/Generator.Tests.Integration.dll --TestAdapterPath:packages/NUnit3TestAdapter.6.2.0/build/net462 --TestCaseFilter:"TestCategory=Integration"
```

## Packaging

The VSIX item template zip (`efrpoco.zip`) is built by `VersionSetter.BuildEfrpocoZip`, which runs only from
`SetVersions()` - and that call is normally **commented out** in `BuildTT/Program.cs`, so an ordinary BuildTT
run does not rebuild it. Uncomment those two lines when cutting a release, run BuildTT, then comment them back
out. Leaving them enabled during development churns the four `efrpoco.zip` copies on every run, because
`ZipArchive` stamps each entry with the current time.

`SetVersions()` also stamps `source.extension.vsixmanifest` and `MyTemplate.vstemplate` from
`BuildTT/version.txt`, and it rewrites the manifest **wholesale**. Never hand-edit that manifest and expect it
to survive: put the change in `VersionSetter.UpdateVsixManifest` as well, or it is deleted the next time a
release is cut. `AssemblyInfo.cs` is the one copy of the version `VersionSetter` does not own - bump it by hand.

`pack.bat` used to build the zip with 7-Zip and was deleted: two mechanisms for one artefact, and the 7-Zip one
needed a tool at a hard-coded path.

## Wiki documentation examples

The code examples on the wiki's `Settings.*` pages are **generated, not written**. `Generator.Tests.Unit/DocSamples/`
runs the generator over a hand-written schema fixture, and `WikiSnippetDriftTests` regenerates every block the
wiki marks with `<!-- docsample: Key -->` and fails when a page has fallen behind.

Read `Generator.Tests.Unit/DocSamples/README.md` before touching the wiki's generated code blocks or adding a
new one. It covers the authoring loop, the two fixture schemas, and why `StaticStateSnapshot` is mandatory for
any fixture that generates a sample.

This exists because prose describing code drifts from the code. An audit found a documented setting that never
existed, a helper method that never existed, three wrong defaults, and an example that does not compile on
MySQL - all of it written by reading the source. Do not go back to hand-written examples.

## Releasing

Order matters at three points; the rest is ordinary.

1. **Bump `BuildTT/version.txt`.** This is the source of truth.
2. **Bump by hand the three copies `VersionSetter` does not own:**
   - `Generator/Properties/AssemblyInfo.cs` - `AssemblyVersion` and `AssemblyFileVersion`
   - `EntityFramework Reverse POCO Generator/Properties/AssemblyInfo.cs` - the same two
   - `EntityFramework.Reverse.POCO.Generator/Northwind.tt` - its `// v` header. BuildTT writes `Database.tt`
     but not this one.
3. **Uncomment `SetVersions()`** in `BuildTT/Program.cs`.
4. **Build BuildTT.** Not optional: `version.txt` is `CopyToOutputDirectory=Always`, so `BuildTT.exe` reads the
   copy beside itself. Skip this and it stamps everything with the *previous* version and nothing complains.
5. **Run `BuildTT/bin/Debug/BuildTT.exe`.** Regenerates `EF.Reverse.POCO.v4.ttinclude`, `Database.tt`,
   `settings-metadata.v4.json` and `EfrpgVersion.cs`; stamps
   `source.extension.vsixmanifest` and `MyTemplate.vstemplate`; rebuilds all four `efrpoco.zip` copies.
6. **Re-comment `SetVersions()`** so day-to-day builds stop churning the zips.
7. **Rebuild the solution.** Step 5 rewrote `EfrpgVersion.cs`, so until now `EF.Reverse.POCO.Generator.dll`
   still carries the old version. The `.ttinclude` is already correct - BuildTT writes `EfrpgVersion.cs` before
   it concatenates - but the compiled assembly is not.
8. **Run the `*.tt` files, then the tests** (see Testing Patterns). Generated output feeds the tests, so the
   order is not negotiable.
9. **Rebuild the VSIX in Release - `-t:Rebuild`, never an incremental build.** Output is
   `EntityFramework Reverse POCO Generator\bin\Release\EntityFramework Reverse POCO Generator.vsix`.
   `dotnet build` cannot build this project at all - it needs the VSSDK targets - so use the VS MSBuild at
   `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, first with
   `-t:Restore`, then with `-t:Rebuild -p:Configuration=Release`.
10. **Verify the version inside the `.vsix`**, which is a zip: `extension.vsixmanifest` must carry the new
    version in **both** `<Identity Version="...">` and the `Microsoft.VisualStudio.Assembly` asset's
    `AssemblyName`. See below for why this is a separate step.
11. **Commit and tag**, then upload the `.vsix` to the marketplace.

**An incremental Release build ships a stale manifest.** The VSSDK does not always regenerate
`extension.vsixmanifest` when only `source.extension.vsixmanifest` has changed, so the `.vsix` gets the
*previous* version stamped into the `Microsoft.VisualStudio.Assembly` asset while containing the *new*
assembly. `MyTemplate.vstemplate` asks for the new one by full name, nothing registers it, and adding a `.tt`
fails with *"this template attempted to load component assembly ... Version=x.y.z"* - naming the version that
is correct, which sends you looking in the wrong place entirely. The extension installs fine and the menu
command still works, because only the `IWizard` lookup goes through that asset. `-t:Rebuild` and the check
above are the whole fix.

**Never change the extension identity.** `source.extension.vsixmanifest` carries
`Id="EntityFramework_Reverse_POCO_Generator..d542a934-8bd6-4136-b490-5f0049d62033"` and the project's
`<AssemblyName>` produces the `.vsix` filename. That identity is what makes an install *upgrade* rather than
sit beside the extension users already have. Only the version changes.

**A version already handed out is spent.** Visual Studio refuses to install a VSIX whose version matches one
already installed - "this extension is already installed to all applicable products" - so a build shared with
anyone, even informally, needs the next number rather than a rebuild.

## Testing Patterns

- Unit tests use `FakeDatabaseReader` to avoid real DB connections.
- Integration tests connect to `(local)` SQL Server using `Integrated Security=True`. Test databases are `EfrpgTest` and `Northwind` (SQL scripts in `TestDatabases/`, one folder per dialect).
- Integration tests write generated `.cs` files to `~/Documents` sub-folders like `.V3TestE8`.
- The `Tester.Integration.*` projects compile the generated output to verify it.
- Test categories: `Constants.DbType.SqlServer`, `Constants.DbType.PostgreSQL`, `Constants.Integration`.
