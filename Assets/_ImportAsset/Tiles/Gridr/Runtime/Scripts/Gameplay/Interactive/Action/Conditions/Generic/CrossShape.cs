//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Cross Shape")]
    public class CrossShape : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            var pos = cell.gridPosition - action.Entity.GridPosition;
            return pos.position.x == 0 || pos.position.y == 0;
        }
    }
}