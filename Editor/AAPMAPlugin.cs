using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using nadena.dev.ndmf;
using System.Linq;
using UnityEditor.Animations;
using nadena.dev.modular_avatar.core;
using UnityEditor;

[assembly: ExportsPlugin(typeof(Narazaka.Unity.AAPMA.Editor.AAPMAPlugin))]

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
            var settingsByLayer = aapmas.GroupBy(aapma => aapma.LayerType).ToDictionary(group => group.Key, group => group.SelectMany(aapma => aapma.Settings).ToArray());

            foreach (var pair in settingsByLayer)
            {
                new LayerPass().PassLayer(ctx, pair.Key, pair.Value);
            }

            foreach (var aapma in aapmas)
            {
                Object.DestroyImmediate(aapma);
            }
        }

        class LayerPass
        {
            static string OneParameter = "__AAPMA__OneParameter__";
            List<string> _parameters = new List<string>();
            List<AnimatorControllerLayer> _layers = new List<AnimatorControllerLayer>();
            List<ChildMotion> _childMotions = new List<ChildMotion>();

            public void PassLayer(BuildContext ctx, VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType layerType, AAPSetting[] settings)
            {
                if (settings.Length == 0) return;

                foreach (var setting in settings)
                {
                    ProcessSetting(setting);
                }

                if (_childMotions.Count > 0)
                {
                    _layers.Insert(0, MakeLayerForMotion(MakeRootDirect()));
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
                    .ToArray(),
                };
                var mergeAnimator = ctx.AvatarRootObject.AddComponent<ModularAvatarMergeAnimator>();
                mergeAnimator.animator = animator;
                mergeAnimator.layerType = layerType;
                mergeAnimator.matchAvatarWriteDefaults = false;
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

            void AddChildMotion(string parameter, Motion motion)
            {
                _childMotions.Add(new ChildMotion { motion = motion, directBlendParameter = parameter });
            }

            void AddLayerForMotion(Motion motion)
            {
                _layers.Add(MakeLayerForMotion(motion));
            }

            AnimatorControllerLayer MakeLayerForMotion(Motion motion)
            {
                var state = new AnimatorState
                {
                    name = motion.name,
                    hideFlags = HideFlags.HideInHierarchy,
                    motion = motion,
                    writeDefaultValues = true,
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
