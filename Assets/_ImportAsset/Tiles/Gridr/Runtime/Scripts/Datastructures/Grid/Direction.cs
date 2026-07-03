using UnityEngine;

//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Datastructures
{
    [CreateAssetMenu(menuName = "Gridr/Cell/Direction")]
    public class Direction : ScriptableObject
    {
        public Vector3Int direction;
        public float costMultiplier;
    }
}