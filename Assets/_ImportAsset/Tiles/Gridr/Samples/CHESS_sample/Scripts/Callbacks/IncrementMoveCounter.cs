//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/Chess/Increment Move Counter")]

    public class IncrementMoveCounter : EntityCallback
    {
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null,
            Cell cell = null)
        {
            var moveCounter = (MoveCounter)entity.FindProperty(typeof(MoveCounter));
            if(moveCounter)
                moveCounter.Increment();
        }
    }
}