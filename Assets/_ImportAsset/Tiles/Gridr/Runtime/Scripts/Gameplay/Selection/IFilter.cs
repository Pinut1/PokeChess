//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


namespace Gridr.Gameplay
{
    public interface IFilter<in T>
    {
        void Filter(T method);
        void Filter(bool condition);
        void Filter();

    }
}