//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using TMPro;
using UnityEngine;

namespace Samples.CHESS_sample.Scripts
{
    public class ChangeText : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;

        public void ChangeTextToaTeam(Player player)
        {
            text.text = player.Name;
        }
    }
}
