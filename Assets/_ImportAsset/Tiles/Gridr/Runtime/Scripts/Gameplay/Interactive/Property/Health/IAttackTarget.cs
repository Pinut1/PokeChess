//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Gameplay
{
    public interface IAttackTarget
    {
        void Damage(float amount);
        void Restore(float amount);
        Cell GetCell();
    }
}