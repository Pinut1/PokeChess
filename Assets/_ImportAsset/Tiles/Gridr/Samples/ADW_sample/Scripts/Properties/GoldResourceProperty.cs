//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    public class GoldResourceProperty : GridProperty
    {
        [SerializeField] private int amount;
        public int Amount => amount;
    }
}