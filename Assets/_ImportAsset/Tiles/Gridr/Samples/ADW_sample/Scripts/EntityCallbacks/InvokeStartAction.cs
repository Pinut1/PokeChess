//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/ADW/Start Action")]

    public class InvokeStartAction : EntityCallback
    {
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            if(!action)
                return;
            
            action.StartAction();
        }
    }
}