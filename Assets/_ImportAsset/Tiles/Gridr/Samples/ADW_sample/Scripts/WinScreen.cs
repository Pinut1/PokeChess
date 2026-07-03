//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gridr.Adw
{
    public class WinScreen : MonoBehaviour
    {
        [SerializeField] private Image winnerIcon;
        [SerializeField] private GameObject winScreenGroup;

        public void Display(Player player)
        {
            winScreenGroup.SetActive(true);
            var winningTeam = PropertyUtil.GetProperty<GridTeamProperty>(player);
            
            if(winningTeam)
                winnerIcon.sprite = winningTeam.team.teamEmblem;
        }
        
        public void OnReplayMatch()
        {
            SceneManager.LoadScene("ADW_Scene1", LoadSceneMode.Single);
        }
    }
}