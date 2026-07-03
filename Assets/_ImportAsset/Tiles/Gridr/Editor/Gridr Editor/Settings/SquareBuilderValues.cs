//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using ScriptableObjects.Editor;
using UnityEngine;

namespace Gridr.Editor
{
    //Add "Create Asset Menu" Attribute to create new data...
    public class SquareBuilderValues : ScriptableObject
    {
        [Tooltip("If true grid cell size will try and adjust to currently selected brush")]
        public bool autoSize = false;

        [Tooltip("If true the already placed cells will move with the origin")]
        public bool moveWithOrigin = false;

        [Tooltip("If true graph will rebuild each time an object is added or removed. Intense!")]
        public bool buildImmediate;
        
        [Tooltip("To paint with")]
        public Brushes brushes;

        [Tooltip("Manually select prefabs to paint with")]
        public List<GameObject> manualSpawnPrefabs;
        
        [Tooltip("Origin of the grid")]
        public Vector3 origin = new Vector3(0, 0, 0);

        [Tooltip("Width of the grid")]
        [Min(1)] public int gridWidth = 1;
        
        [Tooltip("Height of the grid")]
        [Min(1)] public int gridHeight = 1;
        
        [Tooltip("Size of each cell")]
        [Min(1)] public float cellSize = 1f;
        

        private static SquareBuilderValues _instance;
        
        public static SquareBuilderValues Instance {
            get {
                if( _instance == null )
                    _instance = UnityEngine.Resources.Load<SquareBuilderValues>( "SquareBuilderValues" );
                return _instance;
            }
        }
        
    }
}