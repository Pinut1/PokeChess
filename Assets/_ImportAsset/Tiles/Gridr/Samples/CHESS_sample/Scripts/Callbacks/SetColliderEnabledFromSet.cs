//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/Chess/Set Colliders")]
    public class SetColliderEnabledFromSet : EntityCallback
    {
        [SerializeField] private bool enable;
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            var entities = FindObjectsByType<GridEntity>(FindObjectsSortMode.None);
            var entitesWithCollider = entities.Where(e => e.GetComponent<Collider>() != null);
            foreach (var entityWithCollider in entitesWithCollider)
            {
                entityWithCollider.GetComponent<Collider>().enabled = enable;
            }
        }
    }
}