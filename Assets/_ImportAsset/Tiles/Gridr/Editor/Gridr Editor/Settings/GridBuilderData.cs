//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Editor
{
    //Add "Create Asset Menu" Attribute to create new data...
    public class GridBuilderData : ScriptableObject
    {
        [Header("Grid icons")] public Texture2D gridIcon;

        public Texture2D squareGridIcon2d;
        public Texture2D squareGridIcon3d;
        public Texture2D hexagonalGridIcon;
        public Texture2D hexagonalGridIcon2d;
        public Texture2D hexagonalGridIcon3d;
        public Texture2D hexagonIcon;
        public Texture2D squareIcon;
        public Texture2D lockIcon;
        public Texture2D gridIsBuildingIcon;

        [Header("Entity Icons")] public Texture2D gridEntityIcon;
        public Texture2D gridActionIcon;

        [Header("Component Icons")] public Texture2D gameControllerIcon;
        public Texture2D gridControllerIcon;
        public Texture2D snapToSquareIcon;
        public Texture2D snapToHexIcon;
        public Texture2D eventListenerIcon;
        public Texture2D eventIcon;

        [Header("Data icons")] public Texture2D graphIcon;
        public Texture2D rootIcon;
        public Texture2D brushesIcon;
        public Texture2D widthIcon;
        public Texture2D heightIcon;
    
        static GridBuilderData instance;
        public static GridBuilderData Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<GridBuilderData>("GridBuilderData");
                return instance;
            }
        }
    }
}