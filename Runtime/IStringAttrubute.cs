using System;

namespace Narazaka.Unity.AAPMA
{
    [AttributeUsage(AttributeTargets.Field)]
    public class IStringAttribute : Attribute
    {
        public istring Data;

        public IStringAttribute(string en, string ja)
        {
            Data = new istring(en, ja);
        }
    }
}
