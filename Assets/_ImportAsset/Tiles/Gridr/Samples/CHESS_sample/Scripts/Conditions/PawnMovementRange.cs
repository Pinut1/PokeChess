//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Chess/Pawn Movement Range")]

    public class PawnMovementRange : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is MovementAction movementAction))
                return false;
            if (!movementAction.costToCell.ContainsKey(cell))
                return true;
            
            var moveCounter = (MoveCounter)action.Entity.FindProperty(typeof(MoveCounter));
            if (moveCounter.GetCount() <= 0)
            {
                return movementAction.costToCell[cell] <= movementAction.data.range + .7;
            }
            
            return movementAction.costToCell[cell] <= movementAction.data.range;
        }
    }
}