//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Action Validation/Movement Validation")]
    public class MovementValidation : ScriptableObject
    {
        public List<ActionCondition<GridAction>> conditions;
        
        public bool Validate(Cell cell, MovementAction movementAction)
        {
            if (cell == null || movementAction == null)
                return false;
            
            return conditions.All(movementCondition => movementCondition.Validate(cell, movementAction));
        }
    }
}