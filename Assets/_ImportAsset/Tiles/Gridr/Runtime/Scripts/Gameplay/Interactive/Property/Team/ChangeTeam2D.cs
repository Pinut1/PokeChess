//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    public class ChangeTeam2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite neutralSprite;
        [SerializeField] private Sprite redSprite;
        [SerializeField] private Sprite blueSprite;

        public void HandleChangeAll(GridTeamProperty gridTeamProperty)
        {
            HandleChangeSprite(gridTeamProperty);
            HandleChangeDirection(gridTeamProperty);
        }

        public void HandleChangeSprite(GridTeamProperty gridTeamProperty)
        {
            _renderer.sprite = gridTeamProperty.team.teamName switch
            {
                "neutral" => neutralSprite,
                "red" => redSprite,
                "blue" => blueSprite,
                _ => _renderer.sprite
            };
        }
        
        public void HandleChangeDirection(GridTeamProperty gridTeamProperty)
        {
            _renderer.flipX = gridTeamProperty.team.teamDirection switch
            {
                TeamDirection.Left => true,
                TeamDirection.Right => false,
                _ => false
            };
        }
    }
}