//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/AnimationBank")]
    public class AnimationBank : ScriptableObject
    {
        [Header("Team IDs")]
        [SerializeField] private Team redTeam;
        [SerializeField] private Team blueTeam;

        
        [Header("Red Team Animations")] 
        public AnimationClip redTeamIdle;
        public AnimationClip redTeamAttack;
        public AnimationClip redTeamMovement;
        
        [Header("Blue Team Animations")]
        public AnimationClip blueTeamIdle;
        public AnimationClip blueTeamAttack;
        public AnimationClip blueTeamMovement;
        


        public AnimationClip GetAttackClip(Team team)
        {
            return team == redTeam ? redTeamAttack : blueTeamAttack;
        }
        public AnimationClip GetMovementClip(Team team)
        {
            return team == redTeam ? redTeamMovement : blueTeamMovement;
        }

        public AnimationClip GetIdleClip(Team team)
        {
            return team == redTeam ? redTeamIdle : blueTeamIdle;
        }
    }
}