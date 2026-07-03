//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Input Module/Player/Ai Player Input Module")]
    public class AiPlayerInputModule : PlayerInputModule
    {
        public override State Get(Player player)
        {
            return new DefaultAiState(player);
        }
    }
}