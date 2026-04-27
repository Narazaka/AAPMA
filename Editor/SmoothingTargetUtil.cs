using System.Linq;
using System.Reflection;

namespace Narazaka.Unity.AAPMA.Editor
{
    class SmoothingTargetUtil
    {
        public static istring[] Labels = typeof(SmoothingTarget)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(item =>
            {
                var istr = item.GetCustomAttribute<IStringAttribute>();
                if (istr != null) return istr.Data;
                return new istring(item.Name, item.Name);
            }).ToArray();
    }
}
