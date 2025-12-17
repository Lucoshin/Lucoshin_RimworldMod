using Verse;
using RimWorld;
using UnityEngine;
 
namespace MoreStoragePriority
{
    public class MSP_SessionSettings : GameComponent
    {
        public static MSP_SessionSettings Instance;
        public bool enableMorePriority = true;
 
        public MSP_SessionSettings(Game game)
        {
            Instance = this;
        }
 
        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableMorePriority, "mspEnableMorePriority", true);
        }
 
        public static bool Enabled => Instance?.enableMorePriority ?? true;
        public static void Toggle() { if (Instance != null) Instance.enableMorePriority = !Instance.enableMorePriority; }
    }
}
