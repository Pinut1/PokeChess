//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using UnityEngine;

namespace Gridr.Gameplay
{
    public class BuildAction : GridAction
    {
        public InputModule inputModule;
        
        public override State Get()
        {
            return !Active ? null : inputModule.Get(this);
        }

        public override void StartAction() {  }
        
        
        public void Build((GridEntity, Player) deploy)
        {
            var (createdEntity, player) = deploy;

            if (createdEntity == null || player == null)
                return;
            
            var entityObject = Instantiate(createdEntity.gameObject, transform.position + createdEntity.PositionOffset, Quaternion.identity);
            var entityComponent = entityObject.GetComponent<GridEntity>();
            player.SetupEntity(entityComponent);

            HandleActionTaken();
        }

        public bool CanBuild(Cell cell)
        {
            return cell.Occupants.All(e => e == Entity);
        }
        
        private void HandleActionTaken()
        {
            onActionTaken.InvokeAll(Entity, action: this, cell: Entity.Cell, onCompleted: InvokeAll);
        }

        public override void Close() { }

    }
}