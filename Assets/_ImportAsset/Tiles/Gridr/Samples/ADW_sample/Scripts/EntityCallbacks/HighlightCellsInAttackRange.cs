//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Extensions;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/ADW/Highlight All Cells In Attack Range")]

    public class HighlightCellsInAttackRange : EntityCallback
    {
        [SerializeField] private AttackValidation validation;
        
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            if(!action || !(action is AttackAction attackAction))
                return;
            
            attackAction.Entity.GameGrid.cells.ForEach(gridCell => gridCell.DeactivateAllHighlight());
            attackAction.Entity.GameGrid.cells.Where(gridCell => validation.Validate(gridCell, attackAction)).ForEach(gridCell => gridCell.SetAsInRange());
        }
    }
}