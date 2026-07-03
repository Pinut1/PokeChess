//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEditor;
using UnityEngine;
using GUISettings = Gridr.Editor.GUISettings;

namespace Gridr.Editor
{
    public class DrawSettingsUtil
    {
        private readonly GUISettings _guiSettings;
        
        public static EditorGUILayout.HorizontalScope Horizontal => new EditorGUILayout.HorizontalScope();
        public static EditorGUILayout.VerticalScope Vertical => new EditorGUILayout.VerticalScope();
        public static EditorGUILayout.HorizontalScope HorizontalHelpBox => new EditorGUILayout.HorizontalScope(EditorStyles.helpBox);
        public static EditorGUILayout.VerticalScope VerticalHelpBox => new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        public static void PropertyField(SerializedProperty prop) => EditorGUILayout.PropertyField(prop);
        
        public DrawSettingsUtil()
        {
            _guiSettings = new GUISettings(new SerializedObject(GridBuilderSettings.Instance));
        }

        public void Update()
        {
            _guiSettings.Update();
        }
        public bool ApplyModifiedProperties()
        {
            return _guiSettings.ApplyModifiedProperties();
        }
        public void DrawSettingsOptions()
        {
            using (VerticalHelpBox)
            {
                GUILayout.Label("Brush Settings:");
                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.brushColumns);
                    GUILayout.Space(5);
                }
                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.brushVerticalStart);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.brushHorizontalStart);
                    GUILayout.Space(5);
                }

                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.brushButtonWidth);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.brushButtonHeight);
                    GUILayout.Space(5);
                }

                GUILayout.Space(10);
                GUILayout.Label("Tools Settings:");
                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.verticalStart);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.horizontalStart);
                    GUILayout.Space(5);
                }

                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.verticalSpacing);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.horizontalSpacing);
                    GUILayout.Space(5);
                }

                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.textButtonWidth);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.textButtonHeight);
                    GUILayout.Space(5);
                }

                GUILayout.Space(10);
                GUILayout.Label("Drawing Settings:");
                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.commandVerticalStart);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.commandHorizontalStart);
                    GUILayout.Space(5);
                }

                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.commandVerticalSpacing);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.commandHorizontalspacing);
                    GUILayout.Space(5);
                }

                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.commandButtonWidth);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.commandButtonHeight);
                    GUILayout.Space(5);
                }

                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.commandButtonWidth);
                    GUILayout.Space(5);
                    PropertyField(_guiSettings.commandButtonHeight);
                    GUILayout.Space(5);
                }

                GUILayout.Space(10);

                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.showGrid);
                    GUILayout.Space(5);
                }
                
                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.showPositions);
                    PropertyField(_guiSettings.showIndex);
                    GUILayout.Space(5);
                }
                
                GUILayout.Space(10);
                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.showEdges);
                    GUILayout.Space(5);
                }
                using (HorizontalHelpBox)
                {
                    PropertyField(_guiSettings.gridLineWeight);
                    PropertyField(_guiSettings.edgesLineWeight);
                    GUILayout.Space(5);
                }
                EditorGUILayout.PropertyField(_guiSettings.edgesOffset);
                
            }
            GUILayout.Space(10);
        }
    }
}