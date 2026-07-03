//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Editor.Builder;
using Gridr.Editor.Utils;
using UnityEditor;
using UnityEngine;


namespace Gridr.Editor
{
    public class DrawSquareBuilderValuesUtil
    {
        private readonly SquareBuilderProperties _properties;
        private readonly SquareBuilderEditorWindow _editorWindow;
        
        public DrawSquareBuilderValuesUtil(SquareBuilderEditorWindow editorWindow)
        {
            _editorWindow = editorWindow;
            _properties = new SquareBuilderProperties(new SerializedObject(SquareBuilderValues.Instance));
        }
        public void Update()
        {
            _properties.Update();
        }
        public bool ApplyModifiedProperties()
        {
            return _properties.ApplyModifiedProperties();
        }
        
        public void DrawGridBuilder()
        {
            GUIVisUtil.DrawHeader();
            GUIVisUtil.DrawStyleOptions(_editorWindow.ViewType, _editorWindow.SetTo2D, _editorWindow.SetTo3D, new[] { _properties.AutoSize, _properties.MoveWithOrigin});
            GUIVisUtil.DrawInfo(new []
            {
                ("Cells:", _editorWindow.Count),
                ("Cell Size: ", _editorWindow.CellSize),
                ("Width: ", _editorWindow.Width),
                ("Height: ", _editorWindow.Height),
            }, _editorWindow.position.width / 2 - 4);


            GUIVisUtil.DrawTwoButtons(false, false, ("Load Grid", "Set Grid Positions"), (_editorWindow.LoadSceneGrid, _editorWindow.LoadGridPositions));
            GUILayout.Space(10);

            GUIVisUtil.DrawGridProperties(new[] { _properties.Origin, _properties.CellSize, _properties.GridWidth, _properties.GridHeight });
            GUILayout.Space(10);

            GUIVisUtil.DrawProperty(
                _editorWindow.Grid == null,
                "Brush",
                _properties.Brush,
                GUIUtil.GetBrushesFieldContent());
            GUILayout.Space(10);

            GUIVisUtil.DrawGraphOptions(_editorWindow.PropGrid, false,(_editorWindow.Grid == null),_editorWindow.BuildGraph, new[]{_properties.BuildImmediate});
            GUIVisUtil.DrawGraphProperties((_editorWindow.Grid == null), new[]
            {
                ("Cells: ", _editorWindow.Grid == null ? 0f : _editorWindow.Grid.AmountOfNodes),
                ("Connections: ", _editorWindow.Grid == null ? 0f : _editorWindow.Grid.AmountOfEdges)
            }, _editorWindow.position.width / 2);
            GUILayout.Space(10);
        }
    }
}