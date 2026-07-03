//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEditor;

namespace Gridr.Editor
{
    public class GUISettings
    {
        public SerializedObject settings;
        
        public SerializedProperty brushColumns;
        public SerializedProperty verticalStart;
        public SerializedProperty horizontalStart;
        public SerializedProperty verticalSpacing;
        public SerializedProperty horizontalSpacing;
        public SerializedProperty textButtonWidth;
        public SerializedProperty textButtonHeight;

        public SerializedProperty commandVerticalStart;
        public SerializedProperty commandHorizontalStart;
        public SerializedProperty commandVerticalSpacing;
        public SerializedProperty commandHorizontalspacing;
        public SerializedProperty commandButtonWidth;
        public SerializedProperty commandButtonHeight;

        public SerializedProperty brushVerticalStart;
        public SerializedProperty brushHorizontalStart;
        public SerializedProperty brushButtonWidth;
        public SerializedProperty brushButtonHeight;
        
        public SerializedProperty sliderSensitivity;
        public SerializedProperty showPositions;
        public SerializedProperty showIndex;
        public SerializedProperty showEdges;
        public SerializedProperty showGrid;
        public SerializedProperty defaultBrushSquare;
        public SerializedProperty defaultBrushHex;
        public SerializedProperty edgesOffset;
        public SerializedProperty edgesLineWeight;
        public SerializedProperty gridLineWeight;

        
        public GUISettings(SerializedObject settings)
        {
            this.settings = settings;
            Setup();
        }

        public void Update()
        {
            settings.Update();
        }

        public bool ApplyModifiedProperties()
        {
           return settings.ApplyModifiedProperties();
        }

        private void Setup()
        {
            brushColumns = settings.FindProperty("brushColumns");
            verticalStart = settings.FindProperty("verticalStart");
            horizontalStart = settings.FindProperty("horizontalStart");
            verticalSpacing = settings.FindProperty("verticalSpacing");
            horizontalSpacing = settings.FindProperty("horizontalSpacing");
            textButtonWidth = settings.FindProperty("textButtonWidth");
            textButtonHeight = settings.FindProperty("textButtonHeight");

            commandVerticalStart = settings.FindProperty("commandVerticalStart");
            commandHorizontalStart = settings.FindProperty("commandHorizontalStart");
            commandVerticalSpacing = settings.FindProperty("commandVerticalSpacing");
            commandHorizontalspacing = settings.FindProperty("commandHorizontalSpacing");
            commandButtonWidth = settings.FindProperty("commandButtonWidth");
            commandButtonHeight = settings.FindProperty("commandButtonHeight");

            brushVerticalStart = settings.FindProperty("brushVerticalStart");
            brushHorizontalStart = settings.FindProperty("brushHorizontalStart");
            brushButtonWidth = settings.FindProperty("brushButtonWidth");
            brushButtonHeight = settings.FindProperty("brushButtonHeight");
            sliderSensitivity = settings.FindProperty("silderSensitivity");
            showPositions = settings.FindProperty("showGridPosition");
            showIndex = settings.FindProperty("showGridPositionIndex");
            showEdges = settings.FindProperty("showGraphEdges");
            showGrid = settings.FindProperty("showGrid");
            defaultBrushSquare = settings.FindProperty("defaultBrushSquare");
            defaultBrushHex = settings.FindProperty("defaultBrushHex");
            edgesOffset = settings.FindProperty("graphEdgesOffset");
            edgesLineWeight = settings.FindProperty("edgeLineWeight");
            gridLineWeight = settings.FindProperty("gridLineWeight");
        }
    }
}