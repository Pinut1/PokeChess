//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Extensions;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/ADW/Deactivate All Actions")]
    public class DeactivateActions : EntityCallback
    {
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            if(entity)
                entity.Actions.ForEach(c => c.Activate());
        }
    }
}