//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Team")]
    public class Team : ScriptableObject
    {
        public string teamName;
        public TeamDirection teamDirection;

        public Color teamColor;
        public Sprite teamEmblem;
    }
}