//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using Gridr.Extensions;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{
    [RequireComponent(typeof(GridTeamProperty))]
    public class CaptureAction : GridAction
    {
        
        [SerializeField] private CaptureValidation validation;
        [SerializeField] private int captureStrength;
        [SerializeField] private CaptureButton captureButton;
        [SerializeField] private InputModule inputModule;

        public readonly Dictionary<Cell, bool> canAction = new();
        
        public override State Get()
        {
            return Active ? inputModule.Get(this) : null;
        }

        public override void StartAction()
        {
            Entity.GameGrid.RestoreGrid();
            
            canAction.Clear();
            Entity.GameGrid.cells.ForEach(cell => canAction.Add(cell, validation.Validate(cell, this)));
        }

        public void Capture(ICaptureTarget target)
        {
            target.Capture(PropertyUtil.GetProperty<GridTeamProperty>(Entity), captureStrength);
            HandleActionTaken();
        }
        
        protected virtual void HandleActionTaken()
        {
            onActionTaken.InvokeAll(Entity, action: this, onCompleted: InvokeAll);
        }
        
        public override void Close() { }

        public bool CanCapture(Cell cell)
        {
            return cell != null && canAction[cell];
        }

        public void ActivateCaptureButton()
        {
            if(captureButton)
                captureButton.gameObject.SetActive(true);
        }

        public void DeactivateCaptureButton()
        {
            if(captureButton)
                captureButton.gameObject.SetActive(false);
        }
    }
}