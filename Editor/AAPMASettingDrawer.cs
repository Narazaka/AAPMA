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
                DrawExpression($"{OutputName} = {Input1Name} + {Input2Name}");
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
                DrawExpression($"{OutputName} = {Input1Name} - {Input2Name}");
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
                DrawExpression($"{OutputName} = {Input1Name} * {Input2Name}");
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
                DrawExpression($"{OutputName} = {_output.Max.floatValue} / (1 + {Input1Name})");
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
                }

                return height;
            }

            float LineHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            float ParamValueHeight => LineHeight * 2;
            float ParamHeight => LineHeight;
        }
    }
}
