//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using UnityEngine;


namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Chess/Path Is Orthogonal")]
    public class PathIsOrthogonal : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is MovementAction movementAction))
                return false;
            
            var actionPosition = action.Entity.GridPosition.position;
            var allPathOnSameX = movementAction.GetPath(cell).All(pathCell => pathCell.gridPosition.position.x == actionPosition.x);
            var allPathOnSameY = movementAction.GetPath(cell).All(pathCell => pathCell.gridPosition.position.y == actionPosition.y);
            
            return  allPathOnSameX || allPathOnSameY;
        }
    }
} 