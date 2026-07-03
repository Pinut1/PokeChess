//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Input Module/Player/Human Player Input Module")]
    
    public class HumanPlayerInputModule : PlayerInputModule
    {
        public override State Get(Player player)
        {
            return null;
        }
    }
}