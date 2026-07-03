//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;

namespace Gridr.Adw
{
    public interface ICaptureTarget
    {
        void Capture(GridTeamProperty gridTeamProperty, int captureStrength);
    }
}