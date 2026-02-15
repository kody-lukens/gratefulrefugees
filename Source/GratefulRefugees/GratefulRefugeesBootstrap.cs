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
      GratefulRefugeesDebug.LogStartup("Mod startup");
      GratefulRefugeesDebug.LogVerbose("RimWorld version: " + VersionControl.CurrentVersionString);
      GratefulRefugeesDebug.LogQuestDefDiscovery();

      var harmony = new Harmony("cocoapebbles.gratefulrefugees");
      GratefulRefugeesDebug.LogVerbose("Harmony patching started");
      harmony.PatchAll();
      GratefulRefugeesDebug.LogVerbose("Harmony patching complete");

      foreach (var method in harmony.GetPatchedMethods().Where(m => Harmony.GetPatchInfo(m)?.Owners?.Contains(harmony.Id) == true))
      {
        GratefulRefugeesDebug.LogVerbose("Patched: " + method.DeclaringType?.FullName + "." + method.Name);
      }

      GratefulRefugeesSettingsApplier.Apply();
    }
  }
}
