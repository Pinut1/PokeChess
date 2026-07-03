//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using Gridr.Gameplay.Events;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/ADW/Raise Action Selected")]
    public class RaiseActionSelected : EntityCallback
    {
        [SerializeField] private ActionGameEvent onActionSelected;
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            if (action == null)
            {
                Debug.LogWarning("No action passed to callback, will not raise event");
                return;
            }
            
            if(onActionSelected != null)
                onActionSelected.Raise(action);
        }
    }
}