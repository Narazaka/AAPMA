using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Narazaka.Unity.AAPMA.Editor
{
    [CustomPropertyDrawer(typeof(AAPSetting))]
    class AAPMASettingDrawer : UnityEditor.PropertyDrawer
    {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var drawer = new Drawer(position, property, label);
            drawer.OnGUI();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var drawer = new DrawerHeight(property);
            return drawer.GetHeight();
        }

        class AAPParameterProperty
        {
            public readonly SerializedProperty Root;
            public readonly SerializedProperty Property;
            public readonly SerializedProperty Min;
            public readonly SerializedProperty Max;

            public AAPParameterProperty(SerializedProperty property)
            {
                Root = property;
                Property = property.FindPropertyRelative(nameof(AAPParameter.Parameter));
                Min = property.FindPropertyRelative(nameof(AAPParameter.Min));
                Max = property.FindPropertyRelative(nameof(AAPParameter.Max));
            }
        }

        class DrawerBase
        {
            protected readonly SerializedProperty _property;
            protected readonly SerializedProperty _type;
            protected readonly SerializedProperty _use1D;
            protected readonly AAPParameterProperty _input1;
            protected readonly AAPParameterProperty _input2;
            protected readonly AAPParameterProperty _output;

            public DrawerBase(SerializedProperty property)
            {
                _property = property;
                _type = property.FindPropertyRelative(nameof(AAPSetting.Type));
                _use1D = property.FindPropertyRelative(nameof(AAPSetting.Use1D));
                _input1 = new AAPParameterProperty(property.FindPropertyRelative(nameof(AAPSetting.Input1)));
                _input2 = new AAPParameterProperty(property.FindPropertyRelative(nameof(AAPSetting.Input2)));
                _output = new AAPParameterProperty(property.FindPropertyRelative(nameof(AAPSetting.Output)));
            }

            protected bool CanUse1DTypes => AAPSetting.CanUse1DTypes.Contains((LogicType)_type.enumValueIndex);
            protected bool Use1D => _use1D.boolValue;

            protected string Input1Name => string.IsNullOrEmpty(_input1.Property.stringValue) ? T.Input1 : _input1.Property.stringValue;
            protected string Input2Name => string.IsNullOrEmpty(_input2.Property.stringValue) ? T.Input2 : _input2.Property.stringValue;
            protected string OutputName => string.IsNullOrEmpty(_output.Property.stringValue) ? T.Output : _output.Property.stringValue;
        }

        class Drawer : DrawerBase
        {
            readonly Rect position;
            readonly GUIContent label;
            Rect line;
            float defaultLabelWidth;

            public Drawer(Rect position, SerializedProperty property, GUIContent label) : base(property)
            {
                this.position = position;
                this.label = label;
                line = position;
                line.height = EditorGUIUtility.singleLineHeight;
                defaultLabelWidth = EditorGUIUtility.labelWidth;
            }

            float LineHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            void NextLine(int lines = 1)
            {
                line.y += LineHeight * lines;
            }

            Rect line2 => new Rect(line.x, line.y, line.width, EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing);

            public void OnGUI()
            {
                EditorGUI.BeginProperty(line, label, _type);
                EditorGUI.BeginChangeCheck();
                var newType = EditorGUI.Popup(line, label, _type.enumValueIndex, LogicTypeUtil.Labels.Select(l => l.GUIContent).ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    _type.enumValueIndex = newType;
                    AssignCoefficientDefault((LogicType)newType);
                }
                EditorGUI.EndProperty();
                NextLine();

                switch ((LogicType)_type.intValue)
                {
                    case LogicType.Remap:
                        Remap();
                        break;
                    case LogicType.Addition:
                        Addition();
                        break;
                    case LogicType.Subtraction:
                        Subtraction();
                        break;
                    case LogicType.Multiplication:
                        Multiplication();
                        break;
                    case LogicType.Division:
                        Division();
                        break;
                    case LogicType.ExponentialSmoothing:
                        ExponentialSmoothing();
                        break;
                    case LogicType.LinearSmoothing:
                        LinearSmoothing();
                        break;
                    case LogicType.Arbitrary2Bit:
                        Arbitrary2BitGate();
                        break;
                    case LogicType.And:
                        AndGate();
                        break;
                    case LogicType.Or:
                        OrGate();
                        break;
                    case LogicType.Not:
                        NotGate();
                        break;
                }
            }

            void Remap()
            {
                DrawUse1D();
                if (Use1D)
                {
                    MinMax(_input1);
                    MinMax(_output);
                }
                else
                {
                    ZeroMaxPositive(_input1);
                    ZeroMax(_output);
                }
            }

            void Addition()
            {
                DrawExpression($"{OutputName} := {Input1Name} + {Input2Name}");
                DrawUse1D();
                if (Use1D)
                {
                    MinMax(_input1);
                    MinMax(_input2);
                    Param(_output);
                }
                else
                {
                    ParamPositive(_input1);
                    ParamPositive(_input2);
                    Param(_output);
                }
            }

            void Subtraction()
            {
                DrawExpression($"{OutputName} := {Input1Name} - {Input2Name}");
                DrawUse1D();
                if (Use1D)
                {
                    MinMax(_input1);
                    MinMax(_input2);
                    Param(_output);
                }
                else
                {
                    ParamPositive(_input1);
                    ParamPositive(_input2);
                    Param(_output);
                }
            }

            void Multiplication()
            {
                DrawExpression($"{OutputName} := {Input1Name} * {Input2Name}");
                DrawUse1D();
                if (Use1D)
                {
                    ZeroMaxPositive(_input1);
                    ZeroMaxPositive(_input2);
                }
                else
                {
                    ParamPositive(_input1);
                    ParamPositive(_input2);
                }
                Param(_output);
            }

            void Division()
            {
                DrawExpression($"{OutputName} := {_output.Max.floatValue} / (1 + {Input1Name})");
                ParamPositive(_input1);
                ZeroMax(_output);
            }

            void DrawUse1D()
            {
                EditorGUI.PropertyField(line, _use1D, new GUIContent(T.Use1D, T.Use1DDescription));
                NextLine();
            }

            void Param(AAPParameterProperty property)
            {
                EditorGUI.PropertyField(line, property.Property, LabelForPropertyRoot(property));
                NextLine();
            }

            void ParamPositive(AAPParameterProperty property)
            {
                Param(property);
                var rect = line;
                rect.y -= LineHeight;
                DrawPositive(rect);
            }

            void MinMax(AAPParameterProperty property)
            {
                EditorGUI.PropertyField(line, property.Property, LabelForPropertyRoot(property));
                NextLine();
                EditorGUIUtility.labelWidth = 30;
                var rect = line;
                rect.xMin += EditorGUIUtility.singleLineHeight;
                rect.width = rect.width / 2 - 2;
                EditorGUI.PropertyField(rect, property.Min, T.Min.GUIContent);
                rect.x += rect.width + 2;
                EditorGUI.PropertyField(rect, property.Max, T.Max.GUIContent);
                EditorGUIUtility.labelWidth = defaultLabelWidth;
                NextLine();
            }

            void ZeroMax(AAPParameterProperty property)
            {
                EditorGUI.PropertyField(line, property.Property, LabelForPropertyRoot(property));
                NextLine();
                EditorGUIUtility.labelWidth = 30;
                var rect = line;
                rect.xMin += EditorGUIUtility.singleLineHeight;
                rect.width = rect.width / 2 - 2;
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.FloatField(rect, T.Min.GUIContent, 0);
                EditorGUI.EndDisabledGroup();
                rect.x += rect.width + 2;
                EditorGUI.PropertyField(rect, property.Max, T.Max.GUIContent);
                EditorGUIUtility.labelWidth = defaultLabelWidth;
                NextLine();
            }

            void ZeroMaxPositive(AAPParameterProperty property)
            {
                ZeroMax(property);

                var rect = line;
                rect.y -= LineHeight * 2;
                DrawPositive(rect);

                if (property.Max.floatValue < 0)
                {
                    property.Max.floatValue = 0;
                }
            }

            void DrawPositive(Rect rect)
            {
                rect.width = 25;
                rect.x += EditorGUIUtility.labelWidth - rect.width;
                var color = GUI.color;
                GUI.color = Color.cyan;
                EditorGUI.LabelField(rect, ">=0", EditorStyles.miniLabel);
                GUI.color = color;
            }

            void ExponentialSmoothing()
            {
                DrawExpression($"{OutputName} := lerp({Input1Name}, {OutputName}, {(string)T.SmoothAmount})");
                MinMax(_input1);
                Param(_output);
                DrawCoefficient(T.SmoothAmount, nameof(AAPSetting.ExpSmoothAmount), withMaxField: false);
                DrawSmoothingTarget();
            }

            void LinearSmoothing()
            {
                DrawExpression($"{OutputName} += clamp({Input1Name} - {OutputName}, -{(string)T.StepSize}, +{(string)T.StepSize})");
                MinMax(_input1);
                Param(_output);
                DrawCoefficient(T.StepSize, nameof(AAPSetting.LinStepSize), withMaxField: true);
                DrawSmoothingTarget();
            }

            void AndGate()
            {
                DrawExpression($"{OutputName} := {Input1Name} && {Input2Name}");
                Param(_input1);
                Param(_input2);
                Param(_output);
            }

            void OrGate()
            {
                DrawExpression($"{OutputName} := {Input1Name} || {Input2Name}");
                Param(_input1);
                Param(_input2);
                Param(_output);
            }

            void NotGate()
            {
                DrawExpression($"{OutputName} := !{Input1Name}");
                Param(_input1);
                Param(_output);
            }

            void Arbitrary2BitGate()
            {
                DrawExpression($"{OutputName} := TruthTable[{Input1Name}][{Input2Name}]");
                Param(_input1);
                Param(_input2);
                Param(_output);
                DrawTruthValue(nameof(AAPSetting.LogicTruth00), T.TruthA0B0);
                DrawTruthValue(nameof(AAPSetting.LogicTruth01), T.TruthA0B1);
                DrawTruthValue(nameof(AAPSetting.LogicTruth10), T.TruthA1B0);
                DrawTruthValue(nameof(AAPSetting.LogicTruth11), T.TruthA1B1);
                DrawPresetButtons();
            }

            void DrawTruthValue(string propertyName, istring label)
            {
                var prop = _property.FindPropertyRelative(propertyName);
                EditorGUI.PropertyField(line, prop, label.GUIContent);
                NextLine();
            }

            void DrawPresetButtons()
            {
                var rect = line;
                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUI.LabelField(new Rect(rect.x, rect.y, labelWidth, rect.height), T.Presets);

                var buttonsArea = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);
                var buttonWidth = buttonsArea.width / 4f;
                DrawPresetButton(buttonsArea, 0, buttonWidth, "XOR", 0f, 1f, 1f, 0f);
                DrawPresetButton(buttonsArea, 1, buttonWidth, "NAND", 1f, 1f, 1f, 0f);
                DrawPresetButton(buttonsArea, 2, buttonWidth, "NOR", 1f, 0f, 0f, 0f);
                DrawPresetButton(buttonsArea, 3, buttonWidth, "XNOR", 1f, 0f, 0f, 1f);
                NextLine();
            }

            void DrawPresetButton(Rect area, int index, float width, string label,
                float t00, float t01, float t10, float t11)
            {
                var rect = new Rect(area.x + width * index, area.y, width, area.height);
                if (GUI.Button(rect, label))
                {
                    _property.FindPropertyRelative(nameof(AAPSetting.LogicTruth00)).floatValue = t00;
                    _property.FindPropertyRelative(nameof(AAPSetting.LogicTruth01)).floatValue = t01;
                    _property.FindPropertyRelative(nameof(AAPSetting.LogicTruth10)).floatValue = t10;
                    _property.FindPropertyRelative(nameof(AAPSetting.LogicTruth11)).floatValue = t11;
                }
            }

            void AssignCoefficientDefault(LogicType type)
            {
                switch (type)
                {
                    case LogicType.ExponentialSmoothing:
                        var exp = _property.FindPropertyRelative(nameof(AAPSetting.ExpSmoothAmount));
                        if (exp.floatValue == 0f) exp.floatValue = 0.9f;
                        break;
                    case LogicType.LinearSmoothing:
                        var lin = _property.FindPropertyRelative(nameof(AAPSetting.LinStepSize));
                        if (lin.floatValue == 0f) lin.floatValue = 0.05f;
                        break;
                    case LogicType.Arbitrary2Bit:
                        var t00 = _property.FindPropertyRelative(nameof(AAPSetting.LogicTruth00));
                        var t01 = _property.FindPropertyRelative(nameof(AAPSetting.LogicTruth01));
                        var t10 = _property.FindPropertyRelative(nameof(AAPSetting.LogicTruth10));
                        var t11 = _property.FindPropertyRelative(nameof(AAPSetting.LogicTruth11));
                        if (t00.floatValue == 0f && t01.floatValue == 0f && t10.floatValue == 0f && t11.floatValue == 0f)
                        {
                            // 全 0 状態（zero-init）なら XOR を初期値として書き込む
                            t00.floatValue = 0f;
                            t01.floatValue = 1f;
                            t10.floatValue = 1f;
                            t11.floatValue = 0f;
                        }
                        break;
                }
            }

            void DrawCoefficient(istring label, string valuePropName, bool withMaxField)
            {
                var useParam = _property.FindPropertyRelative(nameof(AAPSetting.CoefficientUseParameter));
                var value = _property.FindPropertyRelative(valuePropName);
                var paramName = _property.FindPropertyRelative(nameof(AAPSetting.CoefficientParameter));

                EditorGUI.PropertyField(line, useParam, new GUIContent($"{(string)label}: {(string)T.AsParameter}", label.Tooltip));
                NextLine();

                if (useParam.boolValue)
                {
                    EditorGUI.PropertyField(line, paramName, new GUIContent(T.Parameter, label.Tooltip));
                    NextLine();
                    if (withMaxField)
                    {
                        EditorGUI.PropertyField(line, value, new GUIContent(T.Max, label.Tooltip));
                        NextLine();
                    }
                }
                else
                {
                    EditorGUI.PropertyField(line, value, new GUIContent(T.Value, label.Tooltip));
                    NextLine();
                }
            }

            void DrawSmoothingTarget()
            {
                var prop = _property.FindPropertyRelative(nameof(AAPSetting.SmoothingTarget));
                EditorGUI.BeginChangeCheck();
                var newIdx = EditorGUI.Popup(line, T.SmoothingTarget.GUIContent,
                    prop.enumValueIndex,
                    SmoothingTargetUtil.Labels.Select(l => l.GUIContent).ToArray());
                if (EditorGUI.EndChangeCheck()) prop.enumValueIndex = newIdx;
                NextLine();
            }

            void DrawExpression(string expression)
            {
                var color = GUI.color;
                GUI.color = Color.cyan;
                EditorGUI.LabelField(line, expression);
                GUI.color = color;
                NextLine();
            }

            GUIContent LabelForPropertyRoot(AAPParameterProperty property)
            {
                var path = property.Root.propertyPath;
                if (path.EndsWith(".Input1")) return T.Input1.GUIContent;
                if (path.EndsWith(".Input2")) return T.Input2.GUIContent;
                if (path.EndsWith(".Output")) return T.Output.GUIContent;
                return new GUIContent(property.Root.displayName);
            }
        }

        static class T
        {
            public static istring Use1D = new istring("Use 1D BlendTree", "1D BlendTreeを使用");
            public static istring Use1DDescription = new istring("1D BlendTree is slightly less precise, but allows more flexible value range specification.", "1D BlendTree は少し精度が低くなるがより柔軟な値域指定が可能です");
            public static istring Min = new istring("Min", "最小");
            public static istring Max = new istring("Max", "最大");
            public static istring Input1 = new istring("Input1", "入力1");
            public static istring Input2 = new istring("Input2", "入力2");
            public static istring Output = new istring("Output", "出力");
            public static istring SmoothAmount = new istring("SmoothAmount", "スムージング強度",
                "Smoothing strength (0-1). Higher = smoother but slower tracking. 0 = instant, 1 = no movement.",
                "スムージングの強さ (0〜1)。大きいほど滑らかになるが追従が遅くなる。0=即時追従、1=動かない");
            public static istring StepSize = new istring("StepSize", "ステップ幅",
                "Maximum amount Output moves toward Input per frame. Larger = faster catch-up. In parameter mode, the Max field is the upper bound.",
                "1 フレームに Output が Input に向かって動く最大量。大きいほど速く追従する。パラメータモードでは Max フィールドが上限");
            public static istring AsParameter = new istring("as Parameter", "パラメータで指定");
            public static istring Value = new istring("Value", "値");
            public static istring Parameter = new istring("Parameter", "パラメータ");
            public static istring TruthA0B0 = new istring("A=0, B=0", "A=0, B=0");
            public static istring TruthA0B1 = new istring("A=0, B=1", "A=0, B=1");
            public static istring TruthA1B0 = new istring("A=1, B=0", "A=1, B=0");
            public static istring TruthA1B1 = new istring("A=1, B=1", "A=1, B=1");
            public static istring Presets = new istring("Presets", "プリセット");
            public static istring SmoothingTarget = new istring("Target", "対象",
                "Whether smoothing applies on local avatar, remote avatars, or both.",
                "ローカル / リモート / 両方のどれにスムージングを適用するか");
        }

        class DrawerHeight : DrawerBase
        {
            public DrawerHeight(SerializedProperty property) : base(property) { }

            public float GetHeight()
            {
                var height = 0f;
                // type
                height += LineHeight;
                if (CanUse1DTypes)
                {
                    height += LineHeight;
                }

                switch ((LogicType)_type.intValue)
                {
                    case LogicType.Remap:
                        height += ParamValueHeight * 2;
                        break;
                    case LogicType.Addition:
                        height += LineHeight + (Use1D ? ParamValueHeight : ParamHeight) * 2 + ParamHeight;
                        break;
                    case LogicType.Subtraction:
                        height += LineHeight + (Use1D ? ParamValueHeight : ParamHeight) * 2 + ParamHeight;
                        break;
                    case LogicType.Multiplication:
                        height += LineHeight + (Use1D ? ParamValueHeight : ParamHeight) * 2 + ParamHeight;
                        break;
                    case LogicType.Division:
                        height += LineHeight + ParamHeight + ParamValueHeight;
                        break;
                    case LogicType.ExponentialSmoothing:
                        height += LineHeight;        // expression
                        height += ParamValueHeight;  // input + Min/Max
                        height += ParamHeight;       // output
                        height += LineHeight;        // checkbox
                        height += LineHeight;        // value or paramName
                        height += LineHeight;        // smoothing target popup
                        break;
                    case LogicType.LinearSmoothing:
                        height += LineHeight;        // expression
                        height += ParamValueHeight;  // input + Min/Max
                        height += ParamHeight;       // output
                        height += LineHeight;        // checkbox
                        height += LineHeight;        // value or paramName
                        if (_property.FindPropertyRelative(nameof(AAPSetting.CoefficientUseParameter)).boolValue)
                        {
                            height += LineHeight;    // Max field (parametric mode only)
                        }
                        height += LineHeight;        // smoothing target popup
                        break;
                    case LogicType.And:
                    case LogicType.Or:
                        height += LineHeight;        // expression
                        height += ParamHeight * 3;   // input1, input2, output
                        break;
                    case LogicType.Not:
                        height += LineHeight;        // expression
                        height += ParamHeight * 2;   // input1, output
                        break;
                    case LogicType.Arbitrary2Bit:
                        height += LineHeight;        // expression
                        height += ParamHeight * 3;   // input1, input2, output
                        height += LineHeight * 4;    // 4 truth values
                        height += LineHeight;        // presets
                        break;
                }

                return height;
            }

            float LineHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            float ParamValueHeight => LineHeight * 2;
            float ParamHeight => LineHeight;
        }
    }
}
