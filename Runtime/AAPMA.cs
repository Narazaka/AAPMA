using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using VRC.SDKBase;

[assembly: InternalsVisibleTo("Narazaka.Unity.AAPMA.Editor")]

namespace Narazaka.Unity.AAPMA
{
    public class AAPMA : MonoBehaviour, IEditorOnly
    {
        [SerializeField] public VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType LayerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX;
        [SerializeField] public AAPSetting[] Settings;
    }
}
