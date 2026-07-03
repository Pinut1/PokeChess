//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using Gridr.Extensions;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Base class of an Action or Ability of an Entity.
    ///</summary>
    [RequireComponent(typeof(GridEntity))]
    public abstract class GridAction : MonoBehaviour, IInvokeCallbacks
    {
        [SerializeField] protected GridEntity entity;
        [SerializeField] protected EntityCallbacks onActionTaken;

        private Queue<Action> _transient;
        private List<Action> _persistent;

        public GridEntity Entity => entity == null ? GetComponent<GridEntity>() : entity;
        public bool Active { get; protected set; } = true;

        public void Activate() => Active = true;
        public void Deactivate() => Active = false;

        
        public abstract void StartAction();
        public abstract void Close();
        public abstract State Get();
        

        public void AddTransient(Action action)
        {
            _transient ??= new Queue<Action>();
            _transient.Enqueue(action);
        }
        public void AddPersistent(Action action)
        {
            _persistent ??= new List<Action>();
            _persistent.Add(action);
        }
        public void InvokeTransient() => _transient?.StepAll(c => c.Invoke());
        public void InvokePersistent() => _persistent?.ForEach(c => c.Invoke());
        public void InvokeAll() { InvokeTransient(); InvokePersistent(); }
    }
}