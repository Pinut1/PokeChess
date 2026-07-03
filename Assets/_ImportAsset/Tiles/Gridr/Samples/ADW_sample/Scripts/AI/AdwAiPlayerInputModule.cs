//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    
    [CreateAssetMenu(menuName = "Gridr/Input Module/ADW/AI Player Input Module")]
    public class AdwAiPlayerInputModule : PlayerInputModule
    {
        public override State Get(Player pLayer)
        {
            return new AdwAiPlayerState(pLayer);
        }
    }
}