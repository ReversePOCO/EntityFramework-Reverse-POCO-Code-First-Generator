using System.Collections.Generic;

namespace Efrpg.LanguageMapping
{
    public class PostgresToCSharp : IDatabaseToPropertyType
    {
        // [Database type] = Language type
        public Dictionary<string, string> GetMapping()
        {
            return new Dictionary<string, string>
            {
                { string.Empty,                  "string" }, // default
                { "bigint",                      "long" },
                { "bigserial",                   "long" },
                { "bit varying",                 "BitArray" },
                { "bit",                         "BitArray" },
                { "bool",                        "bool" },
                { "boolean",                     "bool" },
                { "box",                         "NpgsqlBox" },
                { "bytea",                       "byte[]" },
                { "char",                        "char" },
                { "character varying",           "string" },
                { "character",                   "string" },
                { "cid",                         "uint" },
                { "cidr",                        "NpgsqlCidr" }, // Npgsql 8; NpgsqlInet is inet's, and EF refuses it on cidr
                { "circle",                      "NpgsqlCircle" },
                { "citext",                      "string" },
                { "date",                        "DateTime" },
                { "decimal",                     "decimal" },
                { "double precision",            "double" },
                { "float4",                      "float" },
                { "float8",                      "double" },
                { "geometry",                    Settings.DisableGeographyTypes ? string.Empty : "PostgisGeometry" },
                { "hstore",                      "Dictionary<string, string>" },
                { "inet",                        "NpgsqlInet" },
                { "int",                         "int" },
                { "int2",                        "short" },
                { "int4",                        "int" },
                { "int8",                        "long" },
                { "integer",                     "int" },
                { "interval",                    "TimeSpan" },
                { "json",                        "string" },
                { "jsonb",                       "string" },
                { "line",                        "NpgsqlLine" },
                { "lseg",                        "NpgsqlLSeg" },
                { "macaddr",                     "PhysicalAddress" },
                { "money",                       "decimal" },
                { "name",                        "string" },
                { "numeric",                     "decimal" },
                { "oid",                         "uint" },
                { "oidvector",                   "uint[]" },
                { "path",                        "NpgsqlPath" },
                { "point",                       "NpgsqlPoint" },
                { "polygon",                     "NpgsqlPolygon" },
                { "real",                        "float" },
                { "record",                      "object[]" },
                { "serial",                      "int" },
                { "serial4",                     "int" },
                { "serial8",                     "long" },
                { "smallint",                    "short" },
                { "text",                        "string" },
                { "time",                        "TimeSpan" },
                { "time with time zone",         "DateTimeOffset" },
                { "time without time zone",      "TimeSpan" },
                { "timetz",                      "TimeSpan" },
                { "timestamp",                   "DateTime" },
                { "timestamp with time zone",    "DateTime" },
                { "timestamp without time zone", "DateTime" },
                { "timestamptz",                 "DateTime" },
                { "tsquery",                     "NpgsqlTsQuery" },
                { "tsvector",                    "NpgsqlTsVector" },
                { "uuid",                        "Guid" },
                { "varbit",                      "BitArray" },
                { "xid",                         "uint" },
                { "xml",                         "string" },

                // Arrays. The reader reports the element type with [] appended, using PostgreSQL's own internal
                // spelling (int4, bpchar, timestamptz), because information_schema collapses every array to the
                // single type name ARRAY, which nothing can key on. array[] is the fallback when even the element
                // type could not be resolved.
                { "array[]",                     "string[]" },
                { "bool[]",                      "bool[]" },
                { "bpchar[]",                    "string[]" },
                { "bytea[]",                     "byte[][]" },
                { "date[]",                      "DateTime[]" },
                { "float4[]",                    "float[]" },
                { "float8[]",                    "double[]" },
                { "int2[]",                      "short[]" },
                { "int4[]",                      "int[]" },
                { "int8[]",                      "long[]" },
                { "numeric[]",                   "decimal[]" },
                { "oid[]",                       "uint[]" },
                { "text[]",                      "string[]" },
                { "timestamp[]",                 "DateTime[]" },
                { "timestamptz[]",               "DateTime[]" },
                { "uuid[]",                      "Guid[]" },
                { "varchar[]",                   "string[]" },

                // Range types. Npgsql models these as NpgsqlRange<T>, and the PostgreSQL templates already add the
                // NpgsqlTypes using.
                { "daterange",                   "NpgsqlRange<DateTime>" },
                { "int4range",                   "NpgsqlRange<int>" },
                { "int8range",                   "NpgsqlRange<long>" },
                { "numrange",                    "NpgsqlRange<decimal>" },
                { "tsrange",                     "NpgsqlRange<DateTime>" },
                { "tstzrange",                   "NpgsqlRange<DateTime>" },

                { "macaddr8",                    "PhysicalAddress" },

                // Enum types and composite types both arrive as USER-DEFINED and there is nothing better to do with
                // them: Npgsql reads an unmapped enum as its label. Listed so the mapping states it rather than
                // arriving here through the empty-string default.
                { "user-defined",                "string" }
            };
        }

        public List<string> SpatialTypes()
        {
            return new List<string> { "geometry", "point", "line", "lseg", "box", "path", "polygon", "circle" };
        }

        public List<string> PrecisionTypes()
        {
            return new List<string> { "float" };
        }

        public List<string> PrecisionAndScaleTypes()
        {
            return new List<string> { "decimal", "numeric" };
        }
    }
}