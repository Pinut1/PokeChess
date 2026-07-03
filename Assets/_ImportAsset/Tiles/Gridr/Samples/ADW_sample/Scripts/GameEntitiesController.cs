//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using Gridr.Extensions;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{
    ///<summary>
    /// Responsible for controlling the state of the GridEntities on the grid.
    ///</summary>
    public class GameEntitiesController : MonoBehaviour
    {

        private GridSet<GridEntity> _entitySet;
        

        public void Init(GridSet<GridEntity> entitySet)
        {
            _entitySet = entitySet;
        }
        
        private void ActivateEntities(IEnumerable<GridEntity> entities)
        {
            entities?.ForEach(entity => entity.Activate());
        }

        private void DeactivateEntities(IEnumerable<GridEntity> entities)
        {
            entities?.ForEach(entity => entity.Deactivate());
        }

        private void DeactivateAllEntities()
        {
            if (_entitySet == null)
                FindObjectsByType<GridEntity>(FindObjectsSortMode.None).ForEach(entity => entity.Deactivate());
            else
                _entitySet.set.ForEach(entity => entity.Deactivate());
        }

        private void ActivateAllEntities()
        {
            if (_entitySet == null)
                FindObjectsByType<GridEntity>(FindObjectsSortMode.None).ForEach(entity => entity.Activate());
            else
                _entitySet.set.ForEach(entity => entity.Activate());
        }

        private void ActivateActions(IEnumerable<GridEntity> entities)
        {
            entities.ForEach(entity => entity.ActivateActions());
        }

        public void OnEndTurn()
        {
            DeactivateAllEntities();
        }

        public void OnStartTurn(Player player)
        {
            if (player == null)
            {
                ActivateAndRestore(_entitySet == null ? FindObjectsByType<GridEntity>(FindObjectsSortMode.None).ToArray() : _entitySet.set.ToArray());
                return;
            }
            
            HandlePlayerResources(player);
            ActivateAndRestore(GetEntitiesOfSameTeamAsPlayer().ToArray());

            void ActivateAndRestore(GridEntity[] entities)
            {
                ActivateEntities(entities);
                ActivateActions(entities);
            }

            IEnumerable<GridEntity> GetEntitiesOfSameTeamAsPlayer()
            {
                if (_entitySet == null)
                    return FindObjectsByType<GridEntity>(FindObjectsSortMode.None).Where(entity => PropertyUtil.IsSameTeam(player, entity));
                else
                    return _entitySet.set.Where(entity => PropertyUtil.IsSameTeam(player, entity));
            }

            void HandlePlayerResources(Player player)
            {
                var bank = PropertyUtil.GetProperty<BankProperty>(player);
                if(bank == null)
                    return;
                var entities = FindObjectsByType<GridEntity>(FindObjectsSortMode.None).Where(entity => PropertyUtil.IsSameTeam(player, entity));
                
                foreach (var gridEntity in entities)
                {
                    if(!PropertyUtil.IsSameTeam(player,gridEntity))
                        continue;
                    var resource = PropertyUtil.GetProperty<GoldResourceProperty>(gridEntity);
                    if(resource == null)
                        continue;
                    
                    bank.Deposit(resource.Amount);
                }
            }
        }
    }
}