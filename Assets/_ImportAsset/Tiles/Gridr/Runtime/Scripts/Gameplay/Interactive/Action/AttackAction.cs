//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using Gridr.Extensions;
using Gridr.Utils;
using Scripts.Gameplay;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Stores data and methods for making attack action
    ///</summary>
    [RequireComponent(typeof(GridEntity))]
    public class AttackAction : GridAction
    {
        public AttackData attackData;
        public AttackSequence attackSequence;
        public AttackValidation validation;
        public InputModule inputModule;

        public readonly Dictionary<Cell, bool> canAction = new();

        public override State Get()
        {
            return Active ? inputModule.Get(this) : null;
        }

        protected virtual void HandleActionTaken()
        {
            onActionTaken.InvokeAll(Entity, action: this, onCompleted: InvokeAll);
        }

        public override void StartAction()
        {
            Entity.GameGrid.RestoreGrid();

            canAction.Clear();
            Entity.GameGrid.cells.ForEach(cell => canAction.Add(cell, validation.Validate(cell, this)));
        }

        public IAttackTarget TryGetAttackTarget(GridEntity entityToAttack)
        {
            return canAction[entityToAttack.Cell] ? PropertyUtil.GetFirstPropertyOfType<IAttackTarget>(entityToAttack) : null;
        }
        
        public void Attack(IAttackTarget attackTarget)
        {
            StartCoroutine(attackSequence.Run(Entity, attackTarget, null, HandleActionTaken));
        }
        
        public override void Close() { }
    }
}