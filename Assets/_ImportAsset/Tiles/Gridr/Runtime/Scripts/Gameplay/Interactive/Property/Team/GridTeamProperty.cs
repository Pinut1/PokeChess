//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using UnityEngine.Events;

namespace Gridr.Gameplay
{
    public enum TeamDirection
    {
        Right,
        Left
    }
    
    [Serializable]
    public class GridTeamProperty : GridProperty
    {
        public Team team;
        public UnityEvent<GridTeamProperty> onChangedTeam;
        
        public bool IsSameTeam(Team team)
        {
            return this.team == team;
        }
        
        public void ChangeTeam(Team newTeam)
        {
            team = newTeam;
            HandleChange();
        }
        
        private void OnValidate()
        {
            HandleChange();
        }

        void HandleChange()
        {
            onChangedTeam?.Invoke(this);
        }

        public void SetTeamDirection(TeamDirection direction)
        {
            team.teamDirection = direction;
            HandleChange();
        }
        
    }

}