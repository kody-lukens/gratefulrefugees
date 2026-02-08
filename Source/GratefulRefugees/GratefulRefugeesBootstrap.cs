using HarmonyLib;
using Verse;

namespace GratefulRefugees
{
  [StaticConstructorOnStartup]
  public static class GratefulRefugeesBootstrap
  {
    static GratefulRefugeesBootstrap()
    {
      var harmony = new Harmony("cocoapebbles.gratefulrefugees");
      harmony.PatchAll();
    }
  }
}
