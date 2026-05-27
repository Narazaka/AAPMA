using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using nadena.dev.ndmf;
using System.Linq;
using UnityEditor.Animations;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

[assembly: ExportsPlugin(typeof(Narazaka.Unity.AAPMA.Editor.AAPMAPlugin))]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Narazaka.Unity.AAPMA.Editor.Tests")]

namespace Narazaka.Unity.AAPMA.Editor
{
    public class AAPMAPlugin : Plugin<AAPMAPlugin>
    {
        public override string DisplayName => "AAPMA";
        public override string QualifiedName => "net.narazaka.vrchat.aapma";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating).BeforePlugin("nadena.dev.modular-avatar").Run(DisplayName, Pass);
        }

        void Pass(BuildContext ctx)
        {
            var aapmas = ctx.AvatarRootObject.GetComponentsInChildren<AAPMA>();
            
            foreach (var aapma in aapmas)
            {
                var animator = new LayerPass(aapma.LayerType).Build(aapma.Settings);
                if (animator == null) continue;
                var mergeAnimator = aapma.gameObject.AddComponent<ModularAvatarMergeAnimator>();
                mergeAnimator.animator = animator;
                mergeAnimator.layerType = aapma.LayerType;
                mergeAnimator.matchAvatarWriteDefaults = true;
            }

            foreach (var aapma in aapmas)
            {
                Object.DestroyImmediate(aapma);
            }
        }

        internal class LayerPass
        {
            static string OneParameter = "__AAPMA__OneParameter__";
            // Float で宣言する（Bool ではない）: Unity の 1D BlendTree blendParameter は Float 必須。
            // avatar 既存の Bool IsLocal は MA/NDMF が build 時に Float に Force-promote し、
            // 関連 transition (If/IfNot) も自動的に Greater(0.5)/Less(0.5) に書き換えるので整合する。
            static string IsLocalParameter = "IsLocal";

            readonly VRCAvatarDescriptor.AnimLayerType _layerType;

            public LayerPass(VRCAvatarDescriptor.AnimLayerType layerType = VRCAvatarDescriptor.AnimLayerType.FX)
            {
                _layerType = layerType;
            }

            List<string> _parameters = new List<string>();

            void EnsureIsLocalParameter()
            {
                if (_parameters.Contains(IsLocalParameter)) return;
                _parameters.Add(IsLocalParameter);
            }
            List<AnimatorControllerLayer> _layers = new List<AnimatorControllerLayer>();
            List<AnimatorControllerLayer> _localOnlySmoothers = new List<AnimatorControllerLayer>();
            List<AnimatorControllerLayer> _remoteOnlySmoothers = new List<AnimatorControllerLayer>();
            List<(VRCAnimatorLayerControl behaviour, AnimatorControllerLayer target)> _layerControlsToFixup = new List<(VRCAnimatorLayerControl, AnimatorControllerLayer)>();
            List<ChildMotion> _childMotions = new List<ChildMotion>();
            Dictionary<float, string> _hiddenConsts = new Dictionary<float, string>();
            int _linDeltaCounter = 0;

            string NewLinDeltaName() => $"__AAPMA__LinDelta_{_linDeltaCounter++}__";

            string HiddenConst(float value)
            {
                if (!_hiddenConsts.TryGetValue(value, out var name))
                {
                    name = $"__AAPMA__Const_{value:G9}__"
                        .Replace('.', '_').Replace('-', 'm').Replace('+', 'p').Replace('E', 'e');
                    _hiddenConsts.Add(value, name);
                }
                return name;
            }

            public AnimatorController Build(AAPSetting[] settings)
            {
                if (settings.Length == 0) return null;

                foreach (var setting in settings)
                {
                    ProcessSetting(setting);
                }

                if (_childMotions.Count > 0)
                {
                    _layers.Insert(0, MakeLayerForMotion(MakeRootDirect()));
                }
                // control layer は smoother layer よりも前 (低 index) に配置する。
                // Unity は layer 0 の weight を 1 に固定するが control layer は常時 1 で動くため都合が良く、
                // smoother (defaultWeight=0) を index 0 に置かないためのプレースホルダも兼ねる。
                if (_localOnlySmoothers.Count > 0)
                {
                    _layers.Insert(0, MakeControlLayer(SmoothingTarget.LocalOnly, _localOnlySmoothers));
                }
                if (_remoteOnlySmoothers.Count > 0)
                {
                    _layers.Insert(0, MakeControlLayer(SmoothingTarget.RemoteOnly, _remoteOnlySmoothers));
                }
                // 全ての layer 挿入完了後に VRCAnimatorLayerControl.layer に最終 index を書き込む
                foreach (var (behaviour, target) in _layerControlsToFixup)
                {
                    behaviour.layer = _layers.IndexOf(target);
                }
                var animator = new AnimatorController
                {
                    name = "AAPMA",
                    layers = _layers.ToArray(),
                    parameters = _parameters.Select(p => new AnimatorControllerParameter
                    {
                        name = p,
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = 0,
                    })
                    .Concat(new AnimatorControllerParameter[]
                    {
                    new AnimatorControllerParameter
                    {
                        name = OneParameter,
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = 1,
                    },
                    })
                    .Concat(_hiddenConsts.Select(kv => new AnimatorControllerParameter
                    {
                        name = kv.Value,
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = kv.Key,
                    }))
                    .ToArray(),
                };
                return animator;
            }

            void AddSmootherLayer(AAPSetting setting, Motion smoother, bool writeDefaultValues)
            {
                var target = setting.SmoothingTarget;
                // 非対応 LayerType + 非 Both → Both にフォールバック（Drawer 側で防止しているが build 時保険）
                if (target != SmoothingTarget.Both && !IsLayerTypeSupportedForControl(_layerType))
                {
                    target = SmoothingTarget.Both;
                }

                if (target == SmoothingTarget.Both)
                {
                    AddLayerForMotion(smoother, writeDefaultValues);
                    return;
                }

                EnsureIsLocalParameter();
                var layer = MakeLayerForMotion(smoother, writeDefaultValues);
                layer.defaultWeight = 0;
                _layers.Add(layer);

                if (target == SmoothingTarget.LocalOnly) _localOnlySmoothers.Add(layer);
                else _remoteOnlySmoothers.Add(layer);
            }

            AnimatorControllerLayer MakeControlLayer(SmoothingTarget target, List<AnimatorControllerLayer> smootherLayers)
            {
                var blendableLayer = MapToBlendableLayer(_layerType);
                var name = $"AAPMA {target} Control";

                var onState = new AnimatorState
                {
                    name = "On",
                    hideFlags = HideFlags.HideInHierarchy,
                    writeDefaultValues = true,
                };
                var offState = new AnimatorState
                {
                    name = "Off",
                    hideFlags = HideFlags.HideInHierarchy,
                    writeDefaultValues = true,
                };

                foreach (var smoother in smootherLayers)
                {
                    var onBehaviour = onState.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    onBehaviour.playable = blendableLayer;
                    onBehaviour.goalWeight = 1f;
                    onBehaviour.blendDuration = 0f;
                    _layerControlsToFixup.Add((onBehaviour, smoother));

                    var offBehaviour = offState.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    offBehaviour.playable = blendableLayer;
                    offBehaviour.goalWeight = 0f;
                    offBehaviour.blendDuration = 0f;
                    _layerControlsToFixup.Add((offBehaviour, smoother));
                }

                // LocalOnly: On when IsLocal > 0.5, Off when IsLocal < 0.5
                // RemoteOnly: On when IsLocal < 0.5, Off when IsLocal > 0.5
                var toOnMode = target == SmoothingTarget.LocalOnly
                    ? AnimatorConditionMode.Greater
                    : AnimatorConditionMode.Less;
                var toOffMode = target == SmoothingTarget.LocalOnly
                    ? AnimatorConditionMode.Less
                    : AnimatorConditionMode.Greater;

                offState.transitions = new[]
                {
                    new AnimatorStateTransition
                    {
                        name = "ToOn",
                        destinationState = onState,
                        hasExitTime = false,
                        duration = 0f,
                        hideFlags = HideFlags.HideInHierarchy,
                        conditions = new[]
                        {
                            new AnimatorCondition { mode = toOnMode, parameter = IsLocalParameter, threshold = 0.5f }
                        },
                    }
                };
                onState.transitions = new[]
                {
                    new AnimatorStateTransition
                    {
                        name = "ToOff",
                        destinationState = offState,
                        hasExitTime = false,
                        duration = 0f,
                        hideFlags = HideFlags.HideInHierarchy,
                        conditions = new[]
                        {
                            new AnimatorCondition { mode = toOffMode, parameter = IsLocalParameter, threshold = 0.5f }
                        },
                    }
                };

                var stateMachine = new AnimatorStateMachine
                {
                    name = name,
                    hideFlags = HideFlags.HideInHierarchy,
                    entryPosition = new Vector3(0, 0),
                    anyStatePosition = new Vector3(0, 100),
                    exitPosition = new Vector3(0, 200),
                    states = new[]
                    {
                        new ChildAnimatorState { state = onState, position = new Vector3(300, 0) },
                        new ChildAnimatorState { state = offState, position = new Vector3(300, 100) },
                    },
                    defaultState = offState,
                };

                return new AnimatorControllerLayer
                {
                    name = name,
                    stateMachine = stateMachine,
                    defaultWeight = 1,
                };
            }

            internal static bool IsLayerTypeSupportedForControl(VRCAvatarDescriptor.AnimLayerType type)
            {
                return type == VRCAvatarDescriptor.AnimLayerType.FX
                    || type == VRCAvatarDescriptor.AnimLayerType.Action
                    || type == VRCAvatarDescriptor.AnimLayerType.Additive
                    || type == VRCAvatarDescriptor.AnimLayerType.Gesture;
            }

            static VRC_AnimatorLayerControl.BlendableLayer MapToBlendableLayer(VRCAvatarDescriptor.AnimLayerType type)
            {
                switch (type)
                {
                    case VRCAvatarDescriptor.AnimLayerType.FX: return VRC_AnimatorLayerControl.BlendableLayer.FX;
                    case VRCAvatarDescriptor.AnimLayerType.Action: return VRC_AnimatorLayerControl.BlendableLayer.Action;
                    case VRCAvatarDescriptor.AnimLayerType.Additive: return VRC_AnimatorLayerControl.BlendableLayer.Additive;
                    case VRCAvatarDescriptor.AnimLayerType.Gesture: return VRC_AnimatorLayerControl.BlendableLayer.Gesture;
                    default: throw new System.InvalidOperationException($"LayerType {type} cannot be mapped to BlendableLayer");
                }
            }

            void ProcessSetting(AAPSetting setting)
            {
                switch (setting.Type)
                {
                    case LogicType.Remap:
                        Remap(setting);
                        break;
                    case LogicType.Addition:
                        Addition(setting);
                        break;
                    case LogicType.Subtraction:
                        Subtraction(setting);
                        break;
                    case LogicType.Multiplication:
                        Multiplication(setting);
                        break;
                    case LogicType.Division:
                        Division(setting);
                        break;
                    case LogicType.ExponentialSmoothing:
                        ExponentialSmoothing(setting);
                        break;
                    case LogicType.LinearSmoothing:
                        LinearSmoothing(setting);
                        break;
                    case LogicType.And:
                        AndGate(setting);
                        break;
                    case LogicType.Or:
                        OrGate(setting);
                        break;
                    case LogicType.Not:
                        NotGate(setting);
                        break;
                    case LogicType.Arbitrary2Bit:
                        Arbitrary2BitGate(setting);
                        break;
                }
            }

            void Remap(AAPSetting setting)
            {
                var name = $"Remap {setting.Input1} => {setting.Output}";
                _parameters.Add(setting.Input1.Parameter);
                _parameters.Add(setting.Output.Parameter);
                if (setting.Use1DEffective)
                {
                    AddLayerForMotion(New1D(name, setting.Input1.Parameter, setting.Input1.Min, MinClip(setting.Output), setting.Input1.Max, MaxClip(setting.Output)));
                }
                else
                {
                    AddChildMotion(setting.Input1.Parameter, MaxClip(setting.Output));
                }
            }

            void Addition(AAPSetting setting)
            {
                _parameters.Add(setting.Input1.Parameter);
                _parameters.Add(setting.Input2.Parameter);
                _parameters.Add(setting.Output.Parameter);
                if (setting.Use1DEffective)
                {
                    AddChildMotion(OneParameter, New1D($"{setting.Output} += {setting.Input1}", setting.Input1, setting.Input1.Min, NewClip(setting.Output, setting.Input1.Min), setting.Input1.Max, NewClip(setting.Output, setting.Input1.Max)));
                    AddChildMotion(OneParameter, New1D($"{setting.Output} += {setting.Input2}", setting.Input2, setting.Input2.Min, NewClip(setting.Output, setting.Input2.Min), setting.Input2.Max, NewClip(setting.Output, setting.Input2.Max)));
                }
                else
                {
                    AddChildMotion(setting.Input1.Parameter, NewClip(setting.Output, 1));
                    AddChildMotion(setting.Input2.Parameter, NewClip(setting.Output, 1));
                }
            }

            void Subtraction(AAPSetting setting)
            {
                _parameters.Add(setting.Input1.Parameter);
                _parameters.Add(setting.Input2.Parameter);
                _parameters.Add(setting.Output.Parameter);
                if (setting.Use1DEffective)
                {
                    AddChildMotion(OneParameter, New1D($"{setting.Output} += {setting.Input1}", setting.Input1, setting.Input1.Min, NewClip(setting.Output, setting.Input1.Min), setting.Input1.Max, NewClip(setting.Output, setting.Input1.Max)));
                    AddChildMotion(OneParameter, New1D($"{setting.Output} -= {setting.Input2}", setting.Input2, setting.Input2.Min, NewClip(setting.Output, -setting.Input2.Min), setting.Input2.Max, NewClip(setting.Output, -setting.Input2.Max)));
                }
                else
                {
                    AddChildMotion(setting.Input1.Parameter, NewClip(setting.Output, 1));
                    AddChildMotion(setting.Input2.Parameter, NewClip(setting.Output, -1));
                }
            }

            void Multiplication(AAPSetting setting)
            {
                var name = $"{setting.Output} = {setting.Input1} * {setting.Input2}";
                _parameters.Add(setting.Input1.Parameter);
                _parameters.Add(setting.Input2.Parameter);
                _parameters.Add(setting.Output.Parameter);
                if (setting.Use1DEffective)
                {
                    AddLayerForMotion(New1D(name, setting.Input1, 0, NewClip(setting.Output, 0), setting.Input1.Max, New1D("Multiply", setting.Input2, 0, NewClip(setting.Output, 0), setting.Input2.Max, NewClip(setting.Output, setting.Input1.Max * setting.Input2.Max))));
                }
                else
                {
                    AddChildMotion(setting.Input1.Parameter, NewDirect(name, addChildMotion => addChildMotion(setting.Input2.Parameter, NewClip(setting.Output, 1))));
                }
            }

            void Division(AAPSetting setting)
            {
                _parameters.Add(setting.Input1.Parameter);
                _parameters.Add(setting.Output.Parameter);
                var direct = NewDirect($"{setting.Output} = {setting.Output.Max} / (1 + {setting.Input1})", addChildMotion =>
                {
                    addChildMotion(setting.Input1.Parameter, EmptyClip());
                    addChildMotion(OneParameter, NewClip(setting.Output, setting.Output.Max));
                });
                var so = new SerializedObject(direct);
                so.FindProperty("m_NormalizedBlendValues").boolValue = true;
                so.ApplyModifiedProperties();
                AddLayerForMotion(direct);
            }

            void ExponentialSmoothing(AAPSetting setting)
            {
                var inputName = setting.Input1.Parameter;
                var outputName = setting.Output.Parameter;
                var min = setting.Input1.Min;
                var max = setting.Input1.Max;

                _parameters.Add(inputName);
                _parameters.Add(outputName);

                var minClip = NewClip(outputName, min);
                var maxClip = NewClip(outputName, max);

                var innerA = New1D($"{outputName} := {inputName}", inputName, min, minClip, max, maxClip);
                var innerB = New1D($"{outputName} := {outputName}", outputName, min, minClip, max, maxClip);

                string smoothAmountSource;
                if (setting.CoefficientUseParameter)
                {
                    smoothAmountSource = setting.CoefficientParameter;
                    _parameters.Add(smoothAmountSource);
                }
                else
                {
                    smoothAmountSource = HiddenConst(setting.ExpSmoothAmount);
                }

                var outer = New1D($"ExpSmooth {outputName}", smoothAmountSource, 0, innerA, 1, innerB);
                AddSmootherLayer(setting, outer, writeDefaultValues: false);
            }

            void LinearSmoothing(AAPSetting setting)
            {
                var inputName = setting.Input1.Parameter;
                var outputName = setting.Output.Parameter;
                var min = setting.Input1.Min;
                var max = setting.Input1.Max;
                var coef = setting.LinStepSize;

                _parameters.Add(inputName);
                _parameters.Add(outputName);

                var deltaName = NewLinDeltaName();
                _parameters.Add(deltaName);

                var deltaInput = New1D($"Delta := {inputName}", inputName,
                    min, NewClip(deltaName, min),
                    max, NewClip(deltaName, max));

                var deltaMinusOutput = New1D($"Delta := -{outputName}", outputName,
                    min, NewClip(deltaName, -min),
                    max, NewClip(deltaName, -max));

                var outputSelf = New1D($"{outputName} := {outputName}", outputName,
                    min, NewClip(outputName, min),
                    max, NewClip(outputName, max));

                var linearBlend = New1DTriple($"clamp({inputName}-{outputName}, ±{coef})",
                    deltaName,
                    -coef, NewClip(outputName, -1f),
                    0f,    NewClip(outputName, 0f),
                    +coef, NewClip(outputName, +1f));

                string stepSizeSource;
                if (setting.CoefficientUseParameter)
                {
                    stepSizeSource = setting.CoefficientParameter;
                    _parameters.Add(stepSizeSource);
                }
                else
                {
                    stepSizeSource = HiddenConst(setting.LinStepSize);
                }

                var root = NewDirect($"LinSmooth {outputName}", add =>
                {
                    add(OneParameter, deltaInput);
                    add(OneParameter, deltaMinusOutput);
                    add(OneParameter, outputSelf);
                    add(stepSizeSource, linearBlend);
                });
                AddSmootherLayer(setting, root, writeDefaultValues: true);
            }

            void AndGate(AAPSetting setting)
            {
                var inputAName = setting.Input1.Parameter;
                var inputBName = setting.Input2.Parameter;
                var outputName = setting.Output.Parameter;

                _parameters.Add(inputAName);
                _parameters.Add(inputBName);
                _parameters.Add(outputName);

                var clip0 = NewClip(outputName, 0f);
                var clip1 = NewClip(outputName, 1f);

                var inner = New1D($"AND inner {outputName}", inputBName, 0f, clip0, 1f, clip1);
                var outer = New1D($"AND {outputName}", inputAName, 0f, clip0, 1f, inner);
                AddLayerForMotion(outer, writeDefaultValues: false);
            }

            void OrGate(AAPSetting setting)
            {
                var inputAName = setting.Input1.Parameter;
                var inputBName = setting.Input2.Parameter;
                var outputName = setting.Output.Parameter;

                _parameters.Add(inputAName);
                _parameters.Add(inputBName);
                _parameters.Add(outputName);

                var clip0 = NewClip(outputName, 0f);
                var clip1 = NewClip(outputName, 1f);

                var inner = New1D($"OR inner {outputName}", inputBName, 0f, clip0, 1f, clip1);
                var outer = New1D($"OR {outputName}", inputAName, 0f, inner, 1f, clip1);
                AddLayerForMotion(outer, writeDefaultValues: false);
            }

            void NotGate(AAPSetting setting)
            {
                var inputName = setting.Input1.Parameter;
                var outputName = setting.Output.Parameter;

                _parameters.Add(inputName);
                _parameters.Add(outputName);

                var clip0 = NewClip(outputName, 0f);
                var clip1 = NewClip(outputName, 1f);

                var bt = New1D($"NOT {outputName}", inputName, 0f, clip1, 1f, clip0);
                AddLayerForMotion(bt, writeDefaultValues: false);
            }

            void Arbitrary2BitGate(AAPSetting setting)
            {
                var inputAName = setting.Input1.Parameter;
                var inputBName = setting.Input2.Parameter;
                var outputName = setting.Output.Parameter;

                _parameters.Add(inputAName);
                _parameters.Add(inputBName);
                _parameters.Add(outputName);

                var clip00 = NewClip(outputName, setting.LogicTruth00);
                var clip01 = NewClip(outputName, setting.LogicTruth01);
                var clip10 = NewClip(outputName, setting.LogicTruth10);
                var clip11 = NewClip(outputName, setting.LogicTruth11);

                var innerA0 = New1D($"Arb2Bit A=0 {outputName}", inputBName, 0f, clip00, 1f, clip01);
                var innerA1 = New1D($"Arb2Bit A=1 {outputName}", inputBName, 0f, clip10, 1f, clip11);
                var outer = New1D($"Arb2Bit {outputName}", inputAName, 0f, innerA0, 1f, innerA1);
                AddLayerForMotion(outer, writeDefaultValues: false);
            }

            void AddChildMotion(string parameter, Motion motion)
            {
                _childMotions.Add(new ChildMotion { motion = motion, directBlendParameter = parameter });
            }

            void AddLayerForMotion(Motion motion, bool writeDefaultValues = true)
            {
                _layers.Add(MakeLayerForMotion(motion, writeDefaultValues));
            }

            AnimatorControllerLayer MakeLayerForMotion(Motion motion, bool writeDefaultValues = true)
            {
                var state = new AnimatorState
                {
                    name = motion.name,
                    hideFlags = HideFlags.HideInHierarchy,
                    motion = motion,
                    writeDefaultValues = writeDefaultValues,
                };
                var stateMachine = new AnimatorStateMachine
                {
                    name = motion.name,
                    hideFlags = HideFlags.HideInHierarchy,
                    entryPosition = new Vector3(0, 0),
                    anyStatePosition = new Vector3(0, 100),
                    exitPosition = new Vector3(0, 200),
                    states = new ChildAnimatorState[]
                    {
                    new ChildAnimatorState { state = state, position = new Vector3(300, 0) }
                    },
                };
                var layer = new AnimatorControllerLayer
                {
                    name = motion.name,
                    stateMachine = stateMachine,
                    defaultWeight = 1,
                };
                return layer;
            }

            BlendTree New1D(string name, string parameter)
            {
                return new BlendTree
                {
                    name = $"AAPMA {name}",
                    hideFlags = HideFlags.HideInHierarchy,
                    blendType = BlendTreeType.Simple1D,
                    useAutomaticThresholds = false,
                    blendParameter = parameter,
                };
            }

            BlendTree New1D(string name, string parameter, float startThreshold, Motion start, float endThreshold, Motion end)
            {
                var blendTree = New1D(name, parameter);
                blendTree.children = new ChildMotion[]
                {
                new ChildMotion { motion = start, threshold = startThreshold },
                new ChildMotion { motion = end, threshold = endThreshold },
                };
                return blendTree;
            }

            BlendTree New1D(string name, string parameter, Motion start, Motion end)
            {
                return New1D(name, parameter, 0, start, 1, end);
            }

            BlendTree New1DTriple(string name, string parameter,
                float t0, Motion m0, float t1, Motion m1, float t2, Motion m2)
            {
                var bt = New1D(name, parameter);
                bt.children = new[]
                {
                    new ChildMotion { motion = m0, threshold = t0 },
                    new ChildMotion { motion = m1, threshold = t1 },
                    new ChildMotion { motion = m2, threshold = t2 },
                };
                return bt;
            }

            BlendTree NewDirect(string name)
            {
                return new BlendTree
                {
                    name = $"AAPMA {name}",
                    hideFlags = HideFlags.HideInHierarchy,
                    blendType = BlendTreeType.Direct,
                    useAutomaticThresholds = false,
                };
            }

            BlendTree NewDirect(string name, System.Action<System.Action<string, Motion>> action)
            {
                var blendTree = NewDirect(name);
                var childMotions = new List<ChildMotion>();
                action((parameter, motion) => childMotions.Add(new ChildMotion { motion = motion, directBlendParameter = parameter }));
                blendTree.children = childMotions.ToArray();
                return blendTree;
            }

            BlendTree MakeRootDirect()
            {
                var blendTree = NewDirect("AAPMA");
                blendTree.children = _childMotions.ToArray();
                return blendTree;
            }

            Dictionary<(string, float), AnimationClip> _clips = new Dictionary<(string, float), AnimationClip>();

            AnimationClip NewClip(string parameter, float value)
            {
                if (!_clips.TryGetValue((parameter, value), out var clip))
                {
                    clip = new AnimationClip
                    {
                        name = $"AAPMA {parameter} -> {value}",
                        hideFlags = HideFlags.HideInHierarchy,
                    };
                    clip.SetCurve("", typeof(Animator), parameter, new AnimationCurve { keys = new Keyframe[] { new Keyframe { time = 0, value = value } } });
                    _clips.Add((parameter, value), clip);
                }
                return clip;
            }

            AnimationClip MinClip(AAPParameter parameter)
            {
                return NewClip(parameter.Parameter, parameter.Min);
            }

            AnimationClip MaxClip(AAPParameter parameter)
            {
                return NewClip(parameter.Parameter, parameter.Max);
            }

            AnimationClip _emptyClip = null;

            AnimationClip EmptyClip()
            {
                if (_emptyClip == null)
                {
                    _emptyClip = new AnimationClip
                    {
                        name = "AAPMA Empty",
                        hideFlags = HideFlags.HideInHierarchy,
                    };
                }
                return _emptyClip;
            }
        }
    }
}
