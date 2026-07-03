//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Gridr.Adw
{
    public class EmblemCard : MonoBehaviour
    {
        [SerializeField] private Image emblem;

        public void OnChangePlayer(Player player)
        {
            var playerTeam = PropertyUtil.GetProperty<GridTeamProperty>(player);
            
            if(playerTeam)
                emblem.sprite = playerTeam.team.teamEmblem;
        }
    }
}