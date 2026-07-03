//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    
    [CreateAssetMenu(menuName = "Gridr/Action Condition/X Shape")]

    public class XShape  : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            var pos = cell.gridPosition - action.Entity.GridPosition;
            var nonZero = pos.position.x != 0 && pos.position.y != 0;
            var identity = Mathf.Abs(pos.position.x) == Mathf.Abs(pos.position.y);
            return nonZero && identity;
        }
    }
}