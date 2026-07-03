//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay.Events;
using UnityEngine;

namespace Gridr.Gameplay
{
    public class BankProperty : GridProperty
    {
        [SerializeField] private int balance;
        [SerializeField] private PropertyGameEvent onBankChanged;
        
        public int Balance => balance;
        
        public void Deposit(int amount)
        {
            balance += amount;
            onBankChanged.Raise(this);
        }

        public bool Withdraw(int amount)
        {
            if (!CanWithdraw(amount)) 
                return false;

            balance -= amount;
            onBankChanged.Raise(this);
            return true;
        }
        
        public bool CanWithdraw(int amount)
        {
            return balance >= amount;
        }
        
    }
}