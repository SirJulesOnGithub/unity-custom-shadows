using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering.Universal;

namespace Rendering.CustomShadows
{
    [CustomEditor(typeof(CustomShadowRendererData), true)]
    public class CustomShadowRendererDataEditor : ScriptableRendererDataEditor
    {
        private SerializedProperty m_settings;
        
        private void OnEnable()
        {
            m_settings = serializedObject.FindProperty("m_settings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Custom Settings", EditorStyles.whiteLargeLabel);
            EditorGUILayout.PropertyField(m_settings);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Base Settings", EditorStyles.whiteLargeLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Basic features (ie. Render Features) not supported at this moment");
            //base.OnInspectorGUI();
        }
    }
}
