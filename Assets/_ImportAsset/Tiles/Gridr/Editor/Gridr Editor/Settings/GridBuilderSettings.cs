//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Editor
{
    public class GridBuilderSettings : ScriptableObject
    {
        [Min(1)]public int brushColumns = 3;
        public float brushVerticalStart = 0;
        public float brushHorizontalStart = 0;
        public float brushButtonWidth = 0;
        public float brushButtonHeight = 0;

        public float verticalStart = 15;
        public float horizontalStart = 15;
        public float verticalSpacing = 35;
        public float horizontalSpacing = 135;
        public float textButtonWidth = 55;
        public float textButtonHeight = 15;
    
        public float commandVerticalStart = 95;
        public float commandHorizontalStart = 15;
        public float commandVerticalSpacing = 55;
        public float commandHorizontalSpacing = 15;
        public float commandButtonWidth = 55;
        public float commandButtonHeight = 25;

        public float silderSensitivity = 500;
        public float gridLineWeight = 1;

        public bool showGridPosition;
        public bool showGridPositionIndex;
        public bool showGrid;
        
        public bool showGraphEdges;
        public Vector3 graphEdgesOffset;
        public float edgeLineWeight;

        private static GridBuilderSettings _instance;
        
        public static GridBuilderSettings Instance {
            get {
                if( _instance == null )
                    _instance = Resources.Load<GridBuilderSettings>( "GridBuilderSettings" );
                return _instance;
            }
        }
    }
}
