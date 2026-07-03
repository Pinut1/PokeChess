//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    
    [CreateAssetMenu(menuName = "Gridr/Action Condition/AND condition")]

    public class ActionConditionAND : ActionCondition<GridAction>
    {
        [SerializeField] private ActionCondition<GridAction> condition1;
        [SerializeField] private ActionCondition<GridAction> condition2;
        
        public override bool Validate(Cell cell, GridAction action)
        {
            return condition1.Validate(cell, action) && condition2.Validate(cell, action);
        }
    }
}