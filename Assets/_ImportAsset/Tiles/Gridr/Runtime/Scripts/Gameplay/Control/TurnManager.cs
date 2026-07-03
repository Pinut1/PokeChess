//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay.Events;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Responsible for the turns of the game.
    ///</summary>
    public class TurnManager : MonoBehaviour
    {

        [HideInInspector] public Player currentPlayer;

        public PlayerGameEvent startTurn;
        public DefaultGameEvent endTurn;
        
        private Player[] _players;

        public void Init(Player[] players)
        {
            _players = players;
            
            if(_players.Length == 0)
                return;
            
            HandleNextPlayer();
        }
        
        
        private void HandleNextPlayer()
        {
            if (currentPlayer == null)
            {
                currentPlayer = _players[0];
                return;
            }

            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == currentPlayer)
                {
                    if (i + 1 > _players.Length-1)
                    {
                        currentPlayer = _players[0];
                        break;
                    }
                    else
                    {
                        currentPlayer = _players[i + 1];
                        break;
                    }
                }
            }
        }
        private void HandlePreviousPlayer()
        {
            if (currentPlayer == null)
            {
                currentPlayer = _players[0];
                return;
            }

            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == currentPlayer)
                {
                    if (i - 1 < 0)
                    {
                        currentPlayer = _players[_players.Length-1];
                        break;
                    }
                    else
                    {
                        currentPlayer = _players[i - 1];
                        break;
                    }
                }
            }
        }

        public void StartTurn()
        {
            startTurn.Raise(currentPlayer);
        }

        public void EndTurn()
        {
            HandleNextPlayer();
            endTurn.Raise(null);
        }
        
        public void DecrementTurn()
        {
            HandlePreviousPlayer();
            StartTurn();
        }

    }
}