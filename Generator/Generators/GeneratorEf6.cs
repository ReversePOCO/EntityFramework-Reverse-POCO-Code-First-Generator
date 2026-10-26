using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Efrpg.FileManagement;
using Efrpg.Readers;

namespace Efrpg.Generators
{
    public class GeneratorEf6 : Generator
    {
        public GeneratorEf6(FileManagementService fileManagementService)
            : base(fileManagementService)
        {
        }

        protected override bool AllowFkToNonPrimaryKey()
        {
            return false; // Cannot have a FK to a non-primary key
        }

        protected override bool FkMustHaveSameNumberOfColumnsAsPrimaryKey()
        {
            return true;
        }

        protected override void SetupEntity(Column c)
        {
            if (c.PropertyType == "Hierarchy.HierarchyId")
                c.PropertyType = "System.Data.Entity.Hierarchy.HierarchyId";

            var comments = string.Empty;
            if (Settings.IncludeComments != CommentsStyle.None)
            {
                comments = c.DbName;
                if (c.IsPrimaryKey)
                {
                    if (c.IsUniqueConstraint)
                        comments += " (Primary key via unique index " + c.UniqueIndexName + ")";
                    else
                        comments += " (Primary key)";
                }

                if (c.MaxLength > 0)
                    comments += string.Format(" (length: {0})", c.MaxLength);
            }

            c.InlineComments = Settings.IncludeComments == CommentsStyle.AtEndOfField ? " // " + comments : string.Empty;

            c.SummaryComments = string.Empty;
            if (Settings.IncludeComments == CommentsStyle.InSummaryBlock && !string.IsNullOrEmpty(comments))
            {
                c.SummaryComments = comments;
            }
            if (Settings.IncludeExtendedPropertyComments == CommentsStyle.InSummaryBlock && !string.IsNullOrEmpty(c.ExtendedProperty))
            {
                if (string.IsNullOrEmpty(c.SummaryComments))
                    c.SummaryComments = c.ExtendedProperty;
                else
                    c.SummaryComments += ". " + c.ExtendedProperty;
            }

            if (Settings.IncludeExtendedPropertyComments == CommentsStyle.AtEndOfField && !string.IsNullOrEmpty(c.ExtendedProperty))
            {
                if (string.IsNullOrEmpty(c.InlineComments))
                    c.InlineComments = " // " + c.ExtendedProperty;
                else
                    c.InlineComments += ". " + c.ExtendedProperty;
            }
        }

        protected override void SetupConfig(Column c)
        {
            string databaseGeneratedOption = null;

            var isNewSequentialId = !string.IsNullOrEmpty(c.HasDefaultValueSql) && c.HasDefaultValueSql.ToLower().Contains("newsequentialid");
            var isTemporalColumn = c.GeneratedAlwaysType != ColumnGeneratedAlwaysType.NotApplicable;

            // Identity, instead of Computed, seems the best for Temporal `GENERATED ALWAYS` columns: https://stackoverflow.com/questions/40742142/entity-framework-not-working-with-temporal-table
            if (c.IsIdentity || isNewSequentialId || isTemporalColumn)
            {
                databaseGeneratedOption = ".HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity)";
            }
            else if (c.IsComputed)
            {
                databaseGeneratedOption = ".HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed)";
            }
            else if (c.IsPrimaryKey)
            {
                databaseGeneratedOption = ".HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)";
            }

            var sb = new StringBuilder(255);
            sb.AppendFormat(".HasColumnName(@\"{0}\")", c.DbName);

            var excludedHasColumnType = string.Empty;
            if (!Settings.UseDataAnnotations && !string.IsNullOrEmpty(c.SqlPropertyType))
            {
                if (Column.ExcludedHasColumnType.Contains(c.SqlPropertyType))
                    excludedHasColumnType = string.Format(" // .HasColumnType(\"{0}\") was excluded", c.SqlPropertyType);
                else
                    sb.AppendFormat(".HasColumnType(\"{0}\")", c.SqlPropertyType);
            }

            sb.Append(c.IsNullable ? ".IsOptional()" : ".IsRequired()");

            if (c.IsFixedLength || c.IsRowVersion)
                sb.Append(".IsFixedLength()");

            if (!c.IsUnicode)
                sb.Append(".IsUnicode(false)");

            if (!c.IsMaxLength && c.MaxLength > 0)
            {
                var doNotSpecifySize = (_result != null && _result.DoNotSpecifySizeForMaxLength && c.MaxLength > 4000); // Issue #179

                if (doNotSpecifySize)
                    sb.Append(".HasMaxLength(null)");
                else
                    sb.AppendFormat(".HasMaxLength({0})", c.MaxLength);
            }

            if (c.IsMaxLength)
                sb.Append(".IsMaxLength()");

            if ((c.Precision > 0 || c.Scale > 0) && IsPrecisionAndScaleType(c.SqlPropertyType))
                sb.AppendFormat(".HasPrecision({0},{1})", c.Precision, c.Scale);
            else if (c.Precision > 0 && IsPrecisionType(c.SqlPropertyType) && c.SqlPropertyType != "float")
                sb.AppendFormat(".HasPrecision({0})", c.Precision);

            if (c.IsRowVersion)
            {
                sb.Append(".IsRowVersion()");
                c.IsConcurrencyToken = true;
            }

            if (c.IsConcurrencyToken)
                sb.Append(".IsConcurrencyToken()");

            if (databaseGeneratedOption != null)
                sb.Append(databaseGeneratedOption);

            var config = sb.ToString();
            if (!string.IsNullOrEmpty(config))
                c.Config = string.Format("Property(x => x.{0}){1};{2}", c.NameHumanCase, config, excludedHasColumnType);
        }

        public override string PrimaryKeyModelBuilder(Table table)
        {
            return null;
        }

        public override List<string> IndexModelBuilder(Table t)
        {
            return null;
        }

        public override string IndexModelBuilder(Column c)
        {
            var sb = new StringBuilder(1024);
            var indexes = c.Indexes.Where(x => !x.IsPrimaryKey).OrderBy(x => x.IndexName).ThenBy(x => x.KeyOrdinal).ToList();
            var count = indexes.Count;
            var first = true;
            var cannotUseAdded = false;
            var closeBrackets = false;
            var n = count;
            foreach (var index in indexes)
            {
                --n;
                if (first)
                {
                    sb.AppendLine($"modelBuilder.Entity<{c.ParentTable.NameHumanCaseWithSuffix()}>()");
                    sb.AppendLine($"            .Property(e => e.{c.NameHumanCase})");
                    sb.AppendLine("            .HasColumnAnnotation(");
                    sb.AppendLine("                IndexAnnotation.AnnotationName,");
                }

                if (count == 1)
                    sb.AppendLine($"                new IndexAnnotation({AddIndexAttribute(index)})");
                else
                {
                    if (first)
                    {
                        sb.AppendLine("                new IndexAnnotation(new[]");
                        sb.AppendLine("                {");
                        closeBrackets = true;

                    }

                    sb.Append("                    ");
                    sb.Append(AddIndexAttribute(index));
                    if (n > 0)
                        sb.Append(",");

                    sb.AppendLine();
                }

                first = false;

                if (n == 0)
                {
                    sb.AppendLine(closeBrackets ? "                }));" : "            );");
                }
            }

            if (cannotUseAdded)
                sb.Append("        */");

            return sb.ToString();
        }

        private string AddIndexAttribute(RawIndex rawIndex)
        {
            var sb = new StringBuilder(255);
            var properties = new List<string>();

            sb.Append($"new IndexAttribute(\"{rawIndex.IndexName}\", {rawIndex.KeyOrdinal})");
            if (rawIndex.IsUnique)
                properties.Add("IsUnique = true");
            if (rawIndex.IsClustered)
                properties.Add("IsClustered = true");

            if (properties.Any())
            {
                sb.Append(" { ");
                sb.Append($"{string.Join(", ", properties)}");
                sb.Append(" }");
            }

            return sb.ToString();
        }

        // HasOptional
        // HasRequired
        // HasMany
        protected override string GetHasMethod(Relationship relationship, IList<Column> fkCols, IList<Column> pkCols, bool isNotEnforced, bool fkHasUniqueConstraint)
        {
            var withMany = relationship == Relationship.ManyToOne || relationship == Relationship.ManyToMany;
            var fkIsNullable = fkCols.Any(c => c.IsNullable);
            var pkIsUnique = pkCols.Any(c => c.IsUnique || c.IsUniqueConstraint || c.IsPrimaryKey);

            if (withMany || pkIsUnique || fkHasUniqueConstraint)
            {
                if (fkIsNullable || isNotEnforced)
                    return "HasOptional";

                return "HasRequired";
            }

            return "HasMany";
        }

        // WithOptional
        // WithRequired
        // WithMany
        // WithRequiredPrincipal
        // WithRequiredDependent
        protected override string GetWithMethod(Relationship relationship, IList<Column> fkCols, string fkPropName, string manyToManyMapping, string mapKey,
            bool includeReverseNavigation, string hasMethod, string pkTableNameHumanCase, string fkTableNameHumanCase, string primaryKeyColumns, bool fkHasUniqueConstraint)
        {
            var withParam = includeReverseNavigation ? string.Format("b => b.{0}", fkPropName) : string.Empty;

            switch (relationship)
            {
                case Relationship.OneToOne:
                    if (hasMethod == "HasOptional")
                        return string.Format(".WithOptionalPrincipal({0})", withParam);
                    return string.Format(".WithOptional({0})", withParam);

                case Relationship.OneToMany:
                    return string.Format(".WithRequiredDependent({0})", withParam);

                case Relationship.ManyToOne:
                    if (!fkCols.Any(c => c.Hidden))
                        return string.Format(".WithMany({0}).HasForeignKey({1})", withParam, manyToManyMapping);   // Foreign Key Association
                    return string.Format(".WithMany({0}).Map(c => c.MapKey({1}))", withParam, mapKey);  // Independent Association

                case Relationship.ManyToMany:
                    return string.Format(".WithMany({0}).HasForeignKey({1})", withParam, manyToManyMapping);

                default:
                    throw new ArgumentOutOfRangeException(nameof(relationship));
            }
        }

        protected override string GetCascadeOnDelete(bool cascadeOnDelete)
        {
            return cascadeOnDelete ? string.Empty : ".WillCascadeOnDelete(false)";
        }

        protected override string GetForeignKeyConstraintName(string foreignKeyConstraintName)
        {
            return string.Empty;
        }
    }
}