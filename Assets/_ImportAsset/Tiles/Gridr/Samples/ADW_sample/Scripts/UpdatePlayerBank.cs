//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using Gridr.Utils;
using TMPro;
using UnityEngine;

namespace Gridr.Adw
{
    public class UpdatePlayerBank : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI ui;
        
        public void UpdateText(GridProperty property)
        {
            if(!(property is BankProperty bank))
                return;
            
            if(bank != null)
                ui.text = bank.Balance.ToString();
        }
        
        public void UpdateText(Player player)
        {
            var bank = PropertyUtil.GetProperty<BankProperty>(player);
            if(bank == null)
                return;

            ui.text = bank.Balance.ToString();
        }
    }
}