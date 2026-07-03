//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using Gridr.Gameplay;
using Gridr.Gameplay.Events;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Chess
{
    public class MoveController : MonoBehaviour
    {
        [SerializeField] private GameGrid gameGrid;
        [SerializeField] private TypeID king;
        [SerializeField] private EntityGameEvent onRetractMove;
        [SerializeField] private GameObject checkUi;
        
        private Dictionary<GridEntity, Stack<Cell>> _moves = new();

        private void Start()
        {
            foreach (var entity in gameGrid.Entities)
            {
                var cells = new Stack<Cell>();
                cells.Push(entity.Cell);
                _moves.Add(entity, cells);
            }
        }

        public void RecordMove(GridAction action)
        {
            _moves[action.Entity].Push(action.Entity.Cell);
            gameGrid.RestoreGrid();

            if(!Validate(action))
                RetractMove(action as MovementAction);
        }

        public void RetractMove(MovementAction action)
        {
            _moves[action.Entity].Pop();
            action.Entity.transform.position = _moves[action.Entity].Peek().transform.position + action.Entity.PositionOffset;            
            gameGrid.RestoreGrid();
            onRetractMove.Raise(action.Entity);
        }

        public void OnStartTurn(Player currentPlayer)
        {
            if(IsCheck(currentPlayer))
                checkUi.SetActive(true);
            else
                checkUi.SetActive(false);
        }
        
        public bool IsCheck(Player currentPlayer)
        {
            var myKing = gameGrid.Entities.FirstOrDefault(entity => entity.ID == king && PropertyUtil.IsSameTeam(currentPlayer, entity));
            if (myKing != null)
            {
                foreach (var entity in gameGrid.Entities)
                {
                    if(PropertyUtil.IsSameTeam(currentPlayer, entity))
                        continue;
                
                    var movement = (MovementAction)entity.FindAction(typeof(MovementAction));
                    if (movement == null)
                        return false;
                    
                    movement.StartAction();
                    if (movement.CanMove(myKing.Cell))
                        return true;
                }
            }

            return false;
        }
        
        public bool Validate(GridAction action)
        {
            var myKing = gameGrid.Entities.FirstOrDefault(entity => entity.ID == king && PropertyUtil.IsSameTeam(action.Entity, entity));
            if (myKing != null)
            {
                foreach (var entity in gameGrid.Entities)
                {
                    if(PropertyUtil.IsSameTeam(action.Entity, entity))
                        continue;
                
                    var movement = (MovementAction)entity.FindAction(typeof(MovementAction));
                    if (movement == null)
                        return true;
                    movement.StartAction();

                    if (movement.CanMove(myKing.Cell))
                        return false;
                }
            }
            

            return true;
        }
    }
}