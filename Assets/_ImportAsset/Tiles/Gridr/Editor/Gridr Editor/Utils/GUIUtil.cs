//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Datastructures;
using UnityEngine;

namespace Gridr.Editor.Utils
{
    public static class GUIUtil
    {
        public static string graphDescription = "The graph";
        public static string parentObjectDescription = "Parent object of cells in the scene hierarchy";
        public static string brushDescription = "Collection of prefabs to be instantiated on the grid";
        
        
        public static Texture2D GetGameGridIconTexture(ViewType viewType)
        {
            if (viewType == ViewType.View2D)
                return GridBuilderData.Instance.squareGridIcon2d;
            else
                return GridBuilderData.Instance.squareGridIcon3d;
        }
        public static Texture2D GetHexGameGridIconTexture(ViewType viewType)
        {
            if (viewType == ViewType.View2D)
                return GridBuilderData.Instance.hexagonalGridIcon2d;
            else
                return GridBuilderData.Instance.hexagonalGridIcon3d;
        }
        public static Texture2D GetGraphIconTexture() =>  GridBuilderData.Instance.graphIcon;
        public static Texture2D GetGridIconTexture() => GridBuilderData.Instance.gridIcon;
        public static Texture2D GetRootIconTexture() => GridBuilderData.Instance.rootIcon;
        public static Texture2D GetBrushesIconTexture() => GridBuilderData.Instance.brushesIcon;
        public static Texture2D GetWidthIconTexture() => GridBuilderData.Instance.widthIcon;
        public static Texture2D GetHeightIconTexture() => GridBuilderData.Instance.heightIcon;
        public static Texture2D GetGridIsBuildingTexture() => GridBuilderData.Instance.gridIsBuildingIcon;
        
        public static GUIContent GetGraphFieldContent()
        {
            GUIContent content = new GUIContent();
            content.image = GetGridIconTexture();
            content.text = "Graph";
            content.tooltip = graphDescription;
            return content;
        }
        public static GUIContent GetGridBuilderFieldContent()
        {
            GUIContent content = new GUIContent();
            content.image = GetGridIconTexture();
            content.text = "Game Grid";
            content.tooltip = graphDescription;
            return content;
        }
        public static GUIContent GetRootFieldContent()
        {
            GUIContent content = new GUIContent();
            content.image = GetRootIconTexture();
            content.text = "Parent Object";
            content.tooltip = parentObjectDescription;
            return content;
        }
        public static GUIContent GetBrushesFieldContent()
        {
            GUIContent content = new GUIContent();
            content.image = GetBrushesIconTexture();
            content.text = "Brush";
            content.tooltip = brushDescription;
            return content;
        }
        public static GUIContent GetWidthFieldContent()
        {
            GUIContent content = new GUIContent();
            content.image = GetWidthIconTexture();
            content.text = "Grid Width";
            content.tooltip = "";
            return content;
        }
        public static GUIContent GetHeightFieldContent()
        {
            GUIContent content = new GUIContent();
            content.image = GetHeightIconTexture();
            content.text = "Grid Height";
            content.tooltip = "";
            return content;
        }

        
        public static GUIStyle GetStandardButtonStyle()
        {
            GUIStyle style = GUI.skin.button;
            style.hover = GetHoverStyle();
            return style;
        }
        public static GUIStyleState GetHoverStyle()
        {
            var styleState = new GUIStyleState();
            styleState.textColor = Color.yellow;
            return styleState;
        }
        
        public static GUIStyle GetDefaultTextStyle()
        {
            var style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.blue;
            return style;
        }
        public static void SetColorToSelected() => GUI.color = Color.green;
        public static void SetColorToDefault() => GUI.color = Color.white;
        
    }
}