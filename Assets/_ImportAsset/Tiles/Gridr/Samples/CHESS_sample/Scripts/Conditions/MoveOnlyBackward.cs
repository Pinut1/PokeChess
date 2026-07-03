//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Chess/Move Only Backwards")]
    
    public class MoveOnlyBackward : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            var actionPosition = action.Entity.GridPosition.position;
            var cellPosition = cell.gridPosition.position;
            
            var difference = actionPosition.y - cellPosition.y;
            return difference > 0 && actionPosition.x == cellPosition.x;
        }
    }

}
