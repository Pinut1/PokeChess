//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Action Validation/Attack Validation")]
    public class AttackValidation : ScriptableObject
    {
        public List<ActionCondition<GridAction>> conditions;

        public bool Validate(Cell cell, AttackAction attackAction)
        {
            if (cell == null || attackAction == null)
                return false;
            
            return conditions.All(attackCondition => attackCondition.Validate(cell, attackAction));
        }
    }
}