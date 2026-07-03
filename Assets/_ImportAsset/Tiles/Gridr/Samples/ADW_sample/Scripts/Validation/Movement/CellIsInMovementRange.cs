//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/ADW/Cell is In Movement Range")]

    public class CellIsInMovementRange : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is MovementAction movementAction))
                return false;
            if (!movementAction.costToCell.ContainsKey(cell))
                return true;
            
            return movementAction.costToCell[cell] <= movementAction.data.range;
        }
    }
}