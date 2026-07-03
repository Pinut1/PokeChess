//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Input Module/Confirm Attack ")]
    public class AttackInputModule : InputModule 
    {
        public override State Get(GridAction action)
        {
            return Initialized ? new ConfirmAttackState(action as AttackAction, inputReader, listener, actionInputSettings, onStartInputState) : null;
        }

    }
}