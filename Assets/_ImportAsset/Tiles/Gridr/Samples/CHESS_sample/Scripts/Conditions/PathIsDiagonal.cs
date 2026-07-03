//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using UnityEngine;


namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Chess/Path Is Diagonal")]
    public class PathIsDiagonal : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is MovementAction movementAction))
                return false;
            
            var actionPosition = movementAction.Entity.GridPosition.position;
            
            var allPathDiagonal = movementAction.GetPath(cell).All(pathCell => 
                Mathf.Abs(pathCell.gridPosition.position.x - actionPosition.x) == 
                Mathf.Abs(pathCell.gridPosition.position.y - actionPosition.y));

            return allPathDiagonal;
        }
    }
}