//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Gridr.Gameplay
{
    [Serializable]
    public class HealthProperty : GridProperty, IAttackTarget
    {
        public float Amount => amount;
        
        [SerializeField][Min(0)] private float amount;
        [SerializeField][Min(0)] private float cap;

        public UnityEvent<float> onHealthChanged;

        public void Damage(float damageAmount)
        {
            var difference = amount - damageAmount;
            
            if (difference <= 0)
                amount = 0;
            else
                amount -= damageAmount;

            var percentageRemaining = (amount / cap) * 100;
            onHealthChanged.Invoke(percentageRemaining);
            
            if(IsDepleted())
                Destroy(gameObject);
            
            
            bool IsDepleted() => amount <= 0;
        }

        public void Restore(float restorationAmount)
        {
            amount = Mathf.Min(amount+restorationAmount, cap);
        }

        public Cell GetCell()
        {
            return GetComponent<GridEntity>().Cell;
        }
    }
}