//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Chess/Backward Cell Is Not Occupied")]

    public class BackwardCellNotOccupied : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            var cellPosition = cell.gridPosition.position;
            var actionPosition = action.Entity.GridPosition.position;
            var difference = actionPosition.y - cellPosition.y;
            
            if(difference > 0 && cellPosition.x == actionPosition.x)
                return !cell.Occupied;

            return true;
        }
    }
}