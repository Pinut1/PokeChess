//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{
    public class BuildInventoryUI : MonoBehaviour
    {
        
        [SerializeField] private GameObject buildUiGroup;
        [SerializeField] private InventoryItemButton inventoryItemButton;
        
        private List<GridButton> _activeButtons = new();
        private TurnManager _turnManager;

        private void Start()
        {
            _turnManager = FindFirstObjectByType<TurnManager>();
        }

        public void Show(GridAction action)
        {
            if(!(action is BuildAction buildAction))
                return;
            
            var player = _turnManager.currentPlayer;
            var bank = PropertyUtil.GetProperty<BankProperty>(player);
            var inventory = PropertyUtil.GetProperty<BuildInventory>(buildAction.Entity);
            
            buildUiGroup.gameObject.SetActive(true);
            LoadButtons(inventory.items, bank,  _turnManager.currentPlayer);
        }

        public void Hide()
        {
            UnloadButtons();
            buildUiGroup.gameObject.SetActive(false);
        }

        public void LoadButtons(IEnumerable<UnitInventoryItem> items, BankProperty bankProperty, Player player)
        {
            foreach (var item in items)
            {
                var button = Instantiate(inventoryItemButton, buildUiGroup.transform);
                var active = (bankProperty == null || bankProperty.CanWithdraw(item.cost));
                button.Load(item, player, active, Hide);
                _activeButtons.Add(button);
            }
        }

        public void UnloadButtons()
        {
            for (int i = _activeButtons.Count-1; i > 0; i--)
            {
                CleanUpUtil.DestroyContent(ref _activeButtons);
            }
        }
    }
}