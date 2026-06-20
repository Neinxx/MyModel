namespace Mainboard.Runtime
{
    public readonly struct WorldStartUIRequestedSignal
    {
        public WorldStartUIRequestedSignal(string viewID)
        {
            ViewID = viewID;
        }

        public string ViewID { get; }
    }
}
