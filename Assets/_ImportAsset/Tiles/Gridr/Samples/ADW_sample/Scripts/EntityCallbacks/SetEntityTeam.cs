//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/ADW/Set Team Of Entity")]
    public class SetEntityTeam : EntityCallback
    {
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            if (entity == null)
                return;

            var entityTeam = PropertyUtil.GetProperty<GridTeamProperty>(entity);
            var playerTeam = PropertyUtil.GetProperty<GridTeamProperty>(player);
            
            if(entityTeam != null && playerTeam != null)
                entityTeam.ChangeTeam(playerTeam.team);
        }
    }
}