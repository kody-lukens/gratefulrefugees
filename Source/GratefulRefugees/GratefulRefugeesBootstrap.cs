using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GratefulRefugees
{
  [StaticConstructorOnStartup]
  public static class GratefulRefugeesBootstrap
  {
    static GratefulRefugeesBootstrap()
    {
      GratefulRefugeesDebug.Log("Mod startup");
      GratefulRefugeesDebug.Log("RimWorld version: " + VersionControl.CurrentVersionString);
      GratefulRefugeesDebug.LogQuestDefDiscovery();

      var harmony = new Harmony("cocoapebbles.gratefulrefugees");
      GratefulRefugeesDebug.Log("Harmony patching started");
      harmony.PatchAll();
      GratefulRefugeesDebug.Log("Harmony patching complete");

      foreach (var method in harmony.GetPatchedMethods().Where(m => Harmony.GetPatchInfo(m)?.Owners?.Contains(harmony.Id) == true))
      {
        GratefulRefugeesDebug.Log("Patched: " + method.DeclaringType?.FullName + "." + method.Name);
      }
    }
  }
}
