namespace Mainboard.Runtime
{
    internal interface IMainboardStateSink
    {
        void SetPhase(MainboardPhase phase, string step, float progress);
    }
}
