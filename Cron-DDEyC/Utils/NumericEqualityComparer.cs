namespace Cron_DDEyC.Utils
{
    public class NumericEqualityComparer : IEqualityComparer<long>
    {
        public bool Equals(long x, long y)
        {
            // Check if the two long values are equal
            return x == y;
        }

        public int GetHashCode(long obj)
        {
            // Return the hash code for the long value
            return obj.GetHashCode();
        }
    }

    public static class LongListExtensions
    {
        public static List<long> GetNewElements(this List<long> source, List<long> comparisonList)
        {
            // Use LongEqualityComparer to find elements in 'source' that are not in 'comparisonList'
            return source.Where(x => !comparisonList.Contains(x, new NumericEqualityComparer())).ToList();
        }
    }
}
