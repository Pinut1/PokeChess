//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/ADW/Change Sprite Color")]
    public class ChangeSpriteColor : EntityCallback
    {
        public Color color;
        
        public override void Invoke(GridEntity entity, Player player = null, GridAction gridAction = null, GridProperty property = null, Cell cell = null)
        {
            if(!entity)
                return;
            
            var spriteRenderer = entity.GetComponent<SpriteRenderer>();
            if (spriteRenderer)
                spriteRenderer.color = color;
        }
    }
}
