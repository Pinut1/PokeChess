//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Chess/Path Contains No Occupied Cells")]
    public class PathNotContainsOccupied : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is MovementAction movementAction))
                return false;
            
            return !movementAction.GetPath(cell).Where(checkCell => checkCell != cell && checkCell != action.Entity.Cell).Any(gridCell => gridCell.Occupied);
        }
    }
}