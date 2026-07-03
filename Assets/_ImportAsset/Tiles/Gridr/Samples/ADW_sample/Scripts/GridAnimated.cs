//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{
    public class GridAnimated : MonoBehaviour
    {
        [SerializeField] private GridEntity entity;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationBank animationBank;


        private void Start()
        {
            Animate();
        }

        public void Animate()
        {
            if (entity)
            {
                var team = PropertyUtil.GetProperty<GridTeamProperty>(entity);
                if (team != null)
                {
                    animator.Play(animationBank.GetIdleClip(team.team).name);
                }
            }
            else
            {
                var team = GetComponent<GridTeamProperty>();
                if (team != null)
                {
                    animator.Play(animationBank.GetIdleClip(team.team).name);
                }
            }
        }
    }
}