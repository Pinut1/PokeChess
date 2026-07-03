//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEditor;

namespace Gridr.Editor
{
    public class SquareBuilderProperties
    {
        public SerializedObject values;

        public SerializedProperty Count;
        public SerializedProperty Origin;
        public SerializedProperty GridWidth;
        public SerializedProperty GridHeight;
        public SerializedProperty CellSize;
        public SerializedProperty Brush;
        public SerializedProperty AutoSize;
        public SerializedProperty MoveWithOrigin;
        public SerializedProperty BuildImmediate;

        
        public SquareBuilderProperties(SerializedObject values)
        {
            this.values = values;
            Setup();
        }

        public void Update()
        {
            values.Update();
        }

        public bool ApplyModifiedProperties()
        {
           return values.ApplyModifiedProperties();
        }

        private void Setup()
        {
            Origin = values.FindProperty("origin");
            GridWidth = values.FindProperty("gridWidth");
            GridHeight = values.FindProperty("gridHeight");
            Count = values.FindProperty("cellCount");
            CellSize = values.FindProperty("cellSize");
            Brush = values.FindProperty("brushes");
            AutoSize = values.FindProperty("autoSize");
            MoveWithOrigin = values.FindProperty("moveWithOrigin");
            BuildImmediate = values.FindProperty("buildImmediate");
        }
    }
}