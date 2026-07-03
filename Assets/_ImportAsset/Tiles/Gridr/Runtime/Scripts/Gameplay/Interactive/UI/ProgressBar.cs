//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;


namespace Gridr.Gameplay
{
    public class ProgressBar : MonoBehaviour
    {
        
        public float maxValue;
        public float minValue;

        [SerializeField] private SpriteRenderer spriteRenderer;

        public void SetProgress(float percent)
        {
            var progress = (maxValue/100) * Mathf.Max(Mathf.Min(percent, 100), minValue);
            var height = spriteRenderer.size.y;
            
            spriteRenderer.size = new Vector2(progress, height);
        }
    }
}