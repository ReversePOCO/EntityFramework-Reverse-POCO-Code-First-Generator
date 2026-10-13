using System.Linq;

namespace Efrpg.Filtering
{
    public class PeriodFilter : IFilterType<EntityName>
    {
        public bool IsExcluded(EntityName item)
        {
            return item.DbName.Contains('.') && !IsOraclePackageMember(item);
        }

        // The efrpg tool reports an Oracle package member as PACKAGE.MEMBER. The period is not part of either name, and
        // the generated call and [DbFunction] quote the two parts separately.
        private static bool IsOraclePackageMember(EntityName item)
        {
            return Settings.DatabaseType == DatabaseType.Oracle && item is StoredProcedure;
        }
    }
}