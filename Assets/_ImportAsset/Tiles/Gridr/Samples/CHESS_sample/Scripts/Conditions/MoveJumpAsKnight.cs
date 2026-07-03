//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;


namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Chess/Valid Knight Positions")]
    public class MoveJumpAsKnight : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            var position = (action.Entity.Cell.gridPosition - cell.gridPosition).position;
            var condition1 = position.x == 1 && position.y == 2;
            var condition2 = position.x == 2 && position.y == 1;
            
            var condition3 = position.x == 2 && position.y == -1;
            var condition4 = position.x == 1 && position.y == -2;

            var condition5 = position.x == -1 && position.y == -2;
            var condition6 = position.x == -2 && position.y == -1;

            var condition7 = position.x == -2 && position.y == 1;
            var condition8 = position.x == -1 && position.y == 2;

            return condition1 ||
                   condition2 ||
                   condition3 ||
                   condition4 ||
                   condition5 ||
                   condition6 ||
                   condition7 ||
                   condition8;

        }
    }
}