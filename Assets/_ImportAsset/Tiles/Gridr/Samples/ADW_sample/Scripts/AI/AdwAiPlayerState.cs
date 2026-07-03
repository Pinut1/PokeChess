//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gridr.Extensions;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{

    public class AdwAiPlayerState : State
    {
        private readonly Player _player;
        private readonly Queue<GridEntity> _entities;
        
        private GridEntity _currentEntity;
        private Stopwatch _timer;
        private TurnManager _turnManager;

        private State GetAiState() => new AdwAiPlayerState(_player);
        
        public AdwAiPlayerState(Player player)
        {
            _player = player;
            _entities = new Queue<GridEntity>();
        }  

        protected override void Enter()
        {
            EntityUtil.GetActiveEntities(_player).ForEach(entity => _entities.Enqueue(entity));
            nextState =  new EndingTurnState(0,  Object.FindFirstObjectByType<TurnManager>());
        }
        
        protected override void Update()
        {
            if (HandleNextEntity())
            {
                Exit();
                return;
            }
           
            while (_entities.Any())
                return;


            Exit();
        }
        
        
        private bool HandleNextEntity()
        {
            if (!_entities.Any())
                return false;
            
            _currentEntity = _entities.Dequeue();

            if (!_currentEntity.Active)
                return false;
            
            //First priority is to capture resources
            if (HandleCaptureCurrentCell())
                return true;
            //Second priority is to attack enemies already in range
            if (HandleAttackEnemiesInRange())
                return true;
            //Third priority is to move
            if (HandleMovement())
                return true;
            if (HandlePurchase())
                return true;
            
            return false;
        }
        
        
        private bool HandleAttackEnemiesInRange()
        {
            var attacking = GetAttackAction();
            //Check to see if there is an attack action, and that it is marked as active
            if (attacking == null || !attacking.Active)
                return false;
            
            //Start action to store valid targets
            attacking.StartAction();
            
            //Find all valid targets and order them by health
            var entities = _player.GetEntities()
                .Where(entity => attacking.TryGetAttackTarget(entity) != null)
                .OrderBy(PropertyUtil.GetFirstPropertyOfType<IAttackTarget>)
                .ToArray();

            //Search all target alternatives recursively 
            var damageable = SelectTargetRecursive(attacking, entities);
            if (damageable == null) 
                return false;
            
            //Handle next state and take action
            nextState = new WaitForCallbackState(attacking, GetAiState);
            attacking.Attack(damageable);

            return true;
        }
        
        
        private IAttackTarget SelectTargetRecursive(AttackAction attacking, GridEntity[] entities, int counter = 0)
        {
            if (counter > entities.Length-1)
                return null;

            var damageable = attacking.TryGetAttackTarget(entities[counter]);
            
            return damageable ?? SelectTargetRecursive(attacking, entities, ++counter);
        }

        private bool HandleCaptureCurrentCell()
        {
            var capture = GetCaptureAction();
            if (capture == null || !capture.Active )
                return false;
            
            //Start action to store valid targets
            capture.StartAction();

            //Check if current cell is valid target
            if (!capture.CanCapture(_currentEntity.Cell))
                return false;
            
            //Handle next state and take action
            nextState = new WaitForCallbackState(capture, GetAiState);
            var captureTarget = _currentEntity.Cell.Occupants.FirstOrDefault(e => PropertyUtil.GetProperty<CaptureTargetProperty>(e) != null);
            if(captureTarget == null)
                return false;
                
            capture.Capture(PropertyUtil.GetProperty<CaptureTargetProperty>(captureTarget));
            
            return true;
        }
        private bool HandleMovement()
        {
            var movement = GetMovementAction();
            if (movement == null || !movement.Active)
                return false;

            movement.StartAction();
            
            return TryApproachResource() || TryApproachEnemyUnit();


            bool TryApproachResource()
            {
                var capture = GetCaptureAction();
                if (capture == null || !capture.Active) 
                    return false;

                var otherPlayerResources = EntityUtil.GetOthersEntitiesCaptureTarget(_player);

                if (otherPlayerResources == null  || !otherPlayerResources.Any())
                    return false;
                
                //Get the closest vacant resource that can be captured
                var targetCell = 
                    otherPlayerResources
                    ?.Where(resource => resource.Cell.Occupants.Count <= 1)
                    ?.Select(e => e.Cell)
                    ?.OrderBy(c => Vector3.Distance(c.transform.position, _currentEntity.transform.position))
                    ?.FirstOrDefault(e => movement.CanMove(e));

                //If there is a direct target
                if (targetCell != null)
                {
                    //Handle next state and take action
                    nextState = new WaitForCallbackState(movement, GetAiState);
                    movement.Move(targetCell);
                    return true;
                }

                //If there is no direct target, move towards random capturable resource
                var alternativeCell = movement.canAction.Keys?
                    .OrderBy(cell => Vector3.Distance(cell.transform.position, otherPlayerResources.FirstOrDefault().transform.position))
                    .FirstOrDefault(cell => movement.CanMove(cell));
                
                //If there is an alternative
                if (alternativeCell != null)
                {
                    //Handle next state and take action
                    nextState = new WaitForCallbackState(movement, GetAiState);
                    movement.Move(alternativeCell);
                    return true;
                }

                return false;
            }
            bool TryApproachEnemyUnit()
            {
                //Get enemy entity closest to current entity
                var otherEntity = EntityUtil.GetClosestEnemyEntity(_player, _currentEntity);
                if (otherEntity == null)
                {

                    
                    var otherPlayerResources = EntityUtil.GetOthersEntitiesWithResource(_player);;
                    if (otherPlayerResources == null || !otherPlayerResources.Any())
                        return false;
                    

                    //If there is no direct target, move towards random capturable resource
                    var alternativeCell = movement.canAction.Keys?
                        .OrderBy(cell => Vector3.Distance(cell.transform.position, otherPlayerResources.FirstOrDefault().transform.position))
                        .FirstOrDefault(cell => movement.CanMove(cell));
                    
                    //Handle next state and take action
                    nextState = new WaitForCallbackState(movement, GetAiState);
                    movement.Move(alternativeCell);
                    return true;
                }
                
                //Get all the cells that the entity can move to
                var targetCell = movement.canAction.Keys?
                    .OrderBy(cell => Vector3.Distance(cell.transform.position, otherEntity.Cell.transform.position))
                    .FirstOrDefault(cell => movement.CanMove(cell));

                //If there is not target exit
                if (targetCell == null)
                    return false;

                //Handle next state and take action
                nextState = new WaitForCallbackState(movement, GetAiState);
                movement.Move(targetCell);
                return true;
            }
        }

        private bool HandlePurchase()
        {
            var buildAction = GetBuildAction();
            if (buildAction == null || !buildAction.Active || !buildAction.CanBuild(buildAction.Entity.Cell)) 
                return false;

            var inventory = PropertyUtil.GetProperty<BuildInventory>(_currentEntity);
            if (inventory == null)
                return false;

            var playerBank = PropertyUtil.GetProperty<BankProperty>(_player);

            var buildEntity = inventory.items.OrderByDescending(i => i.cost).FirstOrDefault(i => playerBank.CanWithdraw(i.cost));
            if (buildEntity != null && playerBank.Withdraw(buildEntity.cost))
            {
                buildAction.Build((buildEntity.entity, _player));
                nextState = new WaitUntilCompleteState(() => !_currentEntity.Active, GetAiState);
                return true;
            }
            
            foreach (var inventoryItem in inventory.items)
            {
                if(!playerBank.Withdraw(inventoryItem.cost))
                    continue;
                
                buildAction.Build((inventoryItem.entity, _player));
                
                nextState = new WaitUntilCompleteState(() => !_currentEntity.Active, GetAiState);
                return true;
            }
            return false;
        }
        
        private BuildAction GetBuildAction()
        {
            return (BuildAction) _currentEntity.FindAction(typeof(BuildAction));
        }
        
        private CaptureAction GetCaptureAction()
        {
            return (CaptureAction) _currentEntity.FindAction(typeof(CaptureAction));
        }

        private MovementAction GetMovementAction()
        {
            return (MovementAction) _currentEntity.FindAction(typeof(MovementAction));
        }

        private AttackAction GetAttackAction()
        {
            return (AttackAction) _currentEntity.FindAction(typeof(AttackAction));
        }
        
    }
}