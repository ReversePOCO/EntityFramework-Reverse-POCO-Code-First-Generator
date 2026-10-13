using System.Collections.Generic;

namespace Efrpg.LanguageMapping
{
    public class OracleToCSharp : IDatabaseToPropertyType
    {
        // [Database type] = Language type
        public Dictionary<string, string> GetMapping()
        {
            return new Dictionary<string, string>
            {
                { string.Empty,                     "string" }, // default
                { "binary_double",                  "double" },
                { "binary_float",                   "float" },
                { "binary_integer",                 "long" },
                { "blob",                           "byte[]" },
                { "char",                           "string" },
                { "clob",                           "string" },
                { "date",                           "DateTime" },
                { "float",                          "double" },
                { "interval day to second",         "decimal" },
                { "interval year to month",         "decimal" },
                { "long raw",                       "byte[]" },
                { "long",                           "string" },
                { "nchar",                          "string" },
                { "nclob",                          "string" },
                { "number(1)",                      "bool" },
                { "number(3)",                      "byte" },
                { "number(5)",                      "short" },
                { "number(10)",                     "int" },
                { "number(19)",                     "long" },
                { "number",                         "decimal" },
                { "nvarchar2",                      "string" },
                { "pls_integer",                    "long" },
                { "raw",                            "byte[]" },
                { "real",                           "float" },
                { "rowid",                          "string" },
                { "timestamp with local time zone", "DateTime" },
                { "timestamp with time zone",       "DateTimeOffset" }, // Oracle.EntityFrameworkCore refuses DateTime here
                { "timestamp",                      "DateTime" },
                { "urowid",                         "string" },
                { "varchar2",                       "string" },
                { "xmltype",                        "string" }
            };
        }

        public List<string> SpatialTypes()
        {
            return new List<string> { "sdo_geometry" };
        }

        public List<string> PrecisionTypes()
        {
            return new List<string> { "float", "timestamp", "timestamp with time zone", "timestamp with local time zone" };
        }

        public List<string> PrecisionAndScaleTypes()
        {
            return new List<string> { "number" };
        }
    }
}