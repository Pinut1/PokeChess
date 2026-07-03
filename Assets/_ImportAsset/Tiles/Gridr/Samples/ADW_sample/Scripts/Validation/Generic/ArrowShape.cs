//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using Gridr.Datastructures;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Shared/Arrow")]
    public class ArrowShape : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is MovementAction movementAction))
                return false;
            
            var pos = cell.gridPosition - action.Entity.GridPosition;
            var zero = pos.position.x == 0 && pos.position.y == 0;
            var xLess = Mathf.Abs(pos.position.x) <= 2;
            var yLess = Mathf.Abs(pos.position.y) <= 2;
            var xyLess = Mathf.Abs(pos.position.y) + MathF.Abs(pos.position.x) <= 2;

            var isDiagonalDownRight = pos == new GridPosition(1, -1, 0, 0);
            var isDiagonalDownLeft = pos == new GridPosition(-1, -1, 0, 0);

            return !zero && xLess && yLess && xyLess && !isDiagonalDownLeft && !isDiagonalDownRight;

        }
    }
}