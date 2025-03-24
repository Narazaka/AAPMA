using UnityEditor;
using nadena.dev.ndmf.ui;

namespace Narazaka.Unity.AAPMA.Editor
{
    [CustomEditor(typeof(AAPMA))]
    class AAPMAEditor : UnityEditor.Editor
    {
        SerializedProperty _layerType;
        SerializedProperty _settings;

        void OnEnable()
        {
            _layerType = serializedObject.FindProperty(nameof(AAPMA.LayerType));
            _settings = serializedObject.FindProperty(nameof(AAPMA.Settings));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.PropertyField(_layerType, T.LayerType.GUIContent);
            EditorGUILayout.PropertyField(_settings, T.Settings.GUIContent, true);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Separator();
            LanguageSwitcher.DrawImmediate();
        }

        static class T
        {
            public static istring LayerType = new istring("Layer Type", "レイヤー");
            public static istring Settings = new istring("Settings", "設定");
        }
    }
}
