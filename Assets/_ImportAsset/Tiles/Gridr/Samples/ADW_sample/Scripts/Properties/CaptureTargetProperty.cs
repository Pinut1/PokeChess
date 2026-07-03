//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using Gridr.Gameplay;
using UnityEngine;
using UnityEngine.Events;

namespace Gridr.Adw
{
    public class CaptureTargetProperty : GridProperty, ICaptureTarget
    {
        [Header("Capture")]
        [SerializeField] private int resistanceStrength;
        [SerializeField] private int resistanceStrengthCap;
        
        public UnityEvent<Team> onCapture;

        public int Resistance => resistanceStrength;
        
        public void Capture(GridTeamProperty gridTeamProperty, int captureStrength)
        {
            resistanceStrength = Math.Max(0, resistanceStrength - captureStrength);
            if (resistanceStrength <= 0)
            {
                onCapture?.Invoke(gridTeamProperty.team);
            }
        }

        public void Restore()
        {
            resistanceStrength = resistanceStrengthCap;
        }

        public int GetResistance() => resistanceStrength;
        public int GetResistanceCap() => resistanceStrengthCap;
    }
}