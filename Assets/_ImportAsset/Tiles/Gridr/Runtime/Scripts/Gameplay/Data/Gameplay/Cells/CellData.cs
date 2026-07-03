//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Datastructures;
using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Cell/Cell Data")]
    public class CellData : ScriptableObject
    {
        public Directions connections;
        public string cellName;
        public float cost;
        public bool connected;
    }
}
