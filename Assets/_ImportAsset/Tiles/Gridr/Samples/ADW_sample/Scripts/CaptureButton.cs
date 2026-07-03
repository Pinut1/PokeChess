//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;

namespace Gridr.Adw
{
    public class CaptureButton : GridButton
    {

        public override State Select() => null;
        public override void Deselect() { }
        public override int GetPriority() => selectionPriority;
    }
}