//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Adw;
using Gridr.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridr.Gameplay
{
    public class InventoryItemButton : GridButton
    {
        public delegate void Confirm();

        [SerializeField] private Image image;
        [SerializeField] private TextMeshProUGUI text;

        public bool Active => _active;

        private UnitInventoryItem _item;
        private Player _player;
        private Confirm _confirm;
        private bool _active;

        public void Load(UnitInventoryItem item, Player player, bool active, Confirm confirm = null)
        {
            _item = item;
            _player = player;
            _active = active;
            _confirm = confirm;
            UpdateButton();
        }

        public (GridEntity, Player) GetItem()
        {
            var bank = PropertyUtil.GetProperty<BankProperty>(_player);

            if (bank == null)
                return (null, _player);
            if (!bank.Withdraw(_item.cost)) 
                return (null, _player);
            
            _confirm?.Invoke();
            return (_item.entity, _player);
        }
        
        private void UpdateButton()
        {
            image.sprite = _item.unitIcon;
            text.text = _item.cost.ToString();
        }

        public override State Select() => null;
        public override void Deselect() { }
        public override int GetPriority() => selectionPriority;
    }
}