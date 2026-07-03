//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections;
using UnityEngine;


namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/Shared/Invoke Callback After Seconds")]
    public class EntityCallbackAfterTime : EntityCallback
    {
        [SerializeField] private float timeInSeconds;
        [SerializeField] private EntityCallback callback;
        
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null,
            Cell cell = null)
        {
            entity.StartCoroutine(InvokeAtEndOfFrame(entity, player, action, property, cell));
        }

        IEnumerator InvokeAtEndOfFrame(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null,
            Cell cell = null)
        {
            yield return new WaitForSeconds(timeInSeconds);
            callback.Invoke(entity, player, action, property, cell);
        }
    }
}