//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/ADW/Filter Non Valid Cells In Movement Path")]

    public class FilterPathsContainOnlyValidEndCells : EntityCallback
    {
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            if (!(action is MovementAction movementAction))
                return;

            var validCells = movementAction.canAction.Keys.ToList();
            foreach (var validCell in validCells)
            {
                movementAction.canAction[validCell] = movementAction.GetPath(validCell).All(c => movementAction.CanMove(c) || entity.Cell == c);
            }
        }
    }
}