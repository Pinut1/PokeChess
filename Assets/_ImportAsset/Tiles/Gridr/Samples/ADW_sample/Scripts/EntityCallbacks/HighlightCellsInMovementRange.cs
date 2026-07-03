//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Extensions;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/ADW/Highlight Cells In Movement Range")]
    public class HighlightCellsInMovementRange : EntityCallback
    {
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null,
            Cell cell = null)
        {
            if (entity == null)
                return;

            var movementAction = (MovementAction)entity.FindAction(typeof(MovementAction));
            if(movementAction == null)
                return;
            
            movementAction.Entity.GameGrid.cells.ForEach(gridCell => gridCell.DeactivateAllHighlight());
            
            foreach (var highlightCell in movementAction.Entity.GameGrid.cells)
            {
                if(movementAction.CanMove(highlightCell))
                    highlightCell.SetAsHighlighted();
            }
        }
    }
}