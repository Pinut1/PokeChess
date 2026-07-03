//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using ScriptableObjects.Editor;
using UnityEngine;


namespace Gridr.Editor
{
    [CreateAssetMenu]
    public class HexBuilderValues : ScriptableObject
    {
        [Tooltip("Size of each cell measured as outer radius")]
        [Min(1)] public float cellSize = 1f;
        
        [Tooltip("If true grid will adjust to the currently selected prefab. Recommended!")]
        public bool autoSize = false;

        [Tooltip("If true the already placed cells will move with the origin")]
        public bool moveWithOrigin = false;
        
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
        
        private static HexBuilderValues _instance;
        
        public static HexBuilderValues Instance {
            get {
                if( _instance == null )
                    _instance = UnityEngine.Resources.Load<HexBuilderValues>( "HexBuilderValues" );
                return _instance;
            }
        }
    }
}