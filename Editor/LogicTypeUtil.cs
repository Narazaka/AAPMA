using System.Linq;
using System.Reflection;

namespace Narazaka.Unity.AAPMA.Editor
{
    class LogicTypeUtil
    {
        public static istring[] Labels = typeof(LogicType).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Select(item =>
        {
            var istr = item.GetCustomAttribute<IStringAttribute>();
            if (istr != null) return istr.Data;
            return new istring(item.Name, item.Name);
        }).ToArray();
    }
}
