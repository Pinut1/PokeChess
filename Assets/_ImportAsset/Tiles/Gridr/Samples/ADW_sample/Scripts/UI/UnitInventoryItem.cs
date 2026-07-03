//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/ADW/Unit Inventory Item")]
    public class UnitInventoryItem : ScriptableObject
    {
        public string itemName;
        public GridEntity entity;
        public Sprite unitIcon;
        public InventoryCategory category;
        public int cost;
    
        [ContextMenu("Load Entity Sprite")]
        public void LoadSprite()
        {
            var spriteRenderer = entity.GetComponent<SpriteRenderer>();
            if(spriteRenderer == null)
                return;
    
            unitIcon = spriteRenderer.sprite;
        }
    }
}

