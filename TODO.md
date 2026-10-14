# TODO

Known gaps, in rough priority order. Everything here is a deliberate omission rather than an unknown, so each
entry says what is missing and what it would take.

## 1. Nothing automatically compiles the MySQL, Oracle or PostgreSQL generated output

`Tester.Integration.EFCore8/9/10` and `Tester.Integration.Ef6` compile the SQL Server output, which is what turns a
bad mapping into a build failure. MySQL, Oracle and PostgreSQL have goldens
(`Generator.Tests.Integration/TestComparison/*_MySql_*`, `*_Oracle_*`, `*_PostgreSQL_*`) but nothing compiles them,
so a method the provider does not define only shows up if somebody reads the diff.

That is how `Settings.ColumnIdentity` came to emit `.UseIdentityColumn()` for MySQL, which Pomelo spells
`UseMySqlIdentityColumn`. It is also how, until v4.0.53, MySQL, Oracle and PostgreSQL output carried SQL Server's
stored procedure syntax and `IsClustered()` and did not compile at all, with every test green.

As of v4.0.53 each of those goldens compiles against its provider (EF6 at C# 7.3) and builds its model with every
table and view readable against the test databases. That was checked by hand in scratch projects, so nothing keeps
it true.

Fix: a tester project per dialect, referencing `Pomelo.EntityFrameworkCore.MySql`, `Oracle.EntityFrameworkCore`
and `Npgsql.EntityFrameworkCore.PostgreSQL`, that compiles the goldens and builds each model. This is the largest
remaining hole.

## 2. No stored procedure return models for MySQL or Oracle

Neither reader implements `ReadStoredProcReturnObjects`, so procedures generate without result classes.
PostgreSQL builds them from the catalogue; SQL Server executes with `SET FMTONLY`. Oracle can be done from
`ALL_ARGUMENTS`; MySQL has no catalogue for it, so it would need a prepared-statement round trip.

Until then an Oracle `SYS_REFCURSOR` OUT parameter comes back as an `out object` holding the `OracleRefCursor`,
which can only be read while the connection is still open, so the caller has to open it first.

## 3. Oracle reads exactly one schema per run

`OracleDatabaseReader` scopes every query to `SYS_CONTEXT('USERENV','CURRENT_SCHEMA')`. SQL Server and
PostgreSQL span schemas in a single context; Oracle cannot. The change is contained - take a schema list from a
setting and replace the `CURRENT_SCHEMA` predicate with an `IN` list - but nobody has asked for it.

## 4. MySQL cannot see across databases

MySQL treats a database as a schema, so connecting to `EfrpgTest` makes the `EfrpgTestOther` tables invisible.
`TestDatabases/MySQL/EfrpgTest.sql` creates that second database for the cross-schema foreign key case, so the
script contains the scenario but the test does not actually exercise it. Fixing it means teaching the MySQL
reader to read across databases.

## 5. Oracle synonyms: what the first pass leaves out

Private synonyms in the current schema are read (#886): tables, views, standalone routines and packages, with
foreign keys remapped to the synonym names on both sides. Deliberately not covered yet:

- **Database link synonyms** (`ALL_SYNONYMS.DB_LINK` set). Their target lives in a remote dictionary, the analogue
  of SQL Server's cross-database case. Filtered out rather than guessed at.
- **PUBLIC synonyms.** A schema can see thousands; they would swamp the model. Needs an opt-in setting, which means
  a request flag and a `SchemaVersion` bump.
- **Synonyms for synonyms.** Only one level is resolved; a chain finds no columns and drops out.
- **Indexes and comments behind a synonym.** `IndexSQL` and `ExtendedPropertySQL` still read the current schema
  only, so a synonym's entity has its target's keys but no `HasIndex` and no comments.
- **Triggers behind a cross-schema synonym.** `ALL_TRIGGERS` shows only triggers on the user's own tables (or all of
  them with CREATE ANY TRIGGER), so `SynonymTriggerName` is filled for same-schema synonyms only. That is Oracle's
  visibility rule, not a reader gap.
- **One relationship per constraint.** When a constraint is reachable under more than one pair of names - a table
  with two synonyms, or a synonym for a table in the current schema - the reader keeps one pair (real table first,
  then alphabetical), because the generator groups foreign key rows on schema plus constraint name.
- **Constraint names that collide across schemas.** For the same reason, a constraint behind a synonym that shares
  its name with one the current schema owns is dropped in favour of the current schema's own. Both entities are
  still generated; only the dropped relationship's navigation properties are missing.

## 6. `MySqlToCSharp` maps `tinyint` to `SByte`

The BCL name, where every other entry in every mapping uses the C# keyword. It compiles and
`DatabaseReader.MapType` accepts both spellings, so this is cosmetic. Left alone deliberately: changing it churns
both copies of the mapping, `DatabaseReaderSqlTests` and a golden, for no behavioural gain.

## 7. Language mapping keys are no longer compared between the two repositories

`WireContractTests` replaced `ParallelSourceTests` when the tool moved to its own repo, and it deliberately does
not compare mapping keys: an unknown key legitimately falls through to each mapping's default - that is how every
SQL Server character type is typed - so a missing key cannot be asserted mechanically. Only the integration
goldens catch it, and only for a dialect they cover. Item 1 would close most of this.
