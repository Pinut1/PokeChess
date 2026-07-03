//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/Chess/Remove Piece On Capture")]
    public class RemovePieceOnCapture : EntityCallback
    {
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            var targetEntity = entity.GameGrid.Entities.FirstOrDefault(otherEntity => otherEntity != entity && otherEntity.Cell == entity.Cell);
            
            if(targetEntity)
                targetEntity.gameObject.SetActive(false);
        }
    }
}