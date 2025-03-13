using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Narazaka.Unity.AAPMA
{
    [System.Serializable]
    public class AAPSetting
    {
        internal static HashSet<LogicType> CanUse1DTypes = new HashSet<LogicType>
        {
            LogicType.Remap,
            LogicType.Addition,
            LogicType.Subtraction,
            LogicType.Multiplication,
        };

        public LogicType Type;
        public bool Use1D;
        public bool Use1DEffective => Use1D && CanUse1DTypes.Contains(Type);
        public AAPParameter Input1;
        public AAPParameter Input2;
        public AAPParameter Output;
    }
}
