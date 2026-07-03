//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using UnityEngine;


namespace Gridr.Gameplay
{
    ///<summary>
    /// Base class of a player
    ///</summary>
    public class Player : MonoBehaviour, IPropertyHolder
    {
        
        [SerializeField] private string playerName;
        
        [Tooltip("Assign an input module to hand over state when the player starts turn")]
        [SerializeField] private PlayerInputModule playerInputModule;
        [SerializeField] private List<GridProperty> properties;
        
        [Tooltip("All callbacks that will be invoked on an entity requesting setup")]
        public EntityCallbacks setupEntityCallbacks;
        
        public State PlayerStartTurnState => playerInputModule.Get(this);


        private GridSet<GridEntity> _entitySet;
        public string Name => playerName;
        
        public void Init(GridSet<GridEntity> entitySet)
        {
            _entitySet = entitySet;
        }
        public void SetupEntity(GridEntity entity) => setupEntityCallbacks.InvokeAll(entity, this);
        public GridProperty FindProperty(Type type) => properties.FirstOrDefault(property => property.GetType() == type);
        public IEnumerable<GridProperty> FindProperties(Type type) => properties.Where(property => property.GetType() == type);
        public IEnumerable<GridProperty> GetAll()
        {
            return properties;
        }

        public IEnumerable<GridEntity> GetEntities() => _entitySet.set;


    }
}