//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;


namespace Gridr.Gameplay
{
    public class GameObjectGridDisplay : GridDisplay
    {
        [SerializeField] private GameObject markAsPath;
        [SerializeField] private GameObject markAsHighlighted;
        [SerializeField] private GameObject markAsInRange;


        public override void DeactivateAll()
        {
            DeactivatePath();
            DeactivateHighlight();
            DeactivateInRange();
        }

        public override void ActivateInRange()
        {
            DeactivatePath();
            DeactivateHighlight();

            if (markAsInRange)
                markAsInRange.SetActive(true);
        }

        public override void DeactivateInRange()
        {
            if (markAsInRange)
                markAsInRange.SetActive(false);
        }

        public override void ActivatePath()
        {
            DeactivateHighlight();
            DeactivateInRange();

            if (markAsPath)
                markAsPath.SetActive(true);
        }

        public override void DeactivatePath() => markAsPath.SetActive(false);

        public override void ActivateHighlight()
        {
            DeactivatePath();
            DeactivateInRange();

            if (markAsHighlighted)
                markAsHighlighted.SetActive(true);
        }

        public override void DeactivateHighlight() => markAsHighlighted.SetActive(false);
    }
}