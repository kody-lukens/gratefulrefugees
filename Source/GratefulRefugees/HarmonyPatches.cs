using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GratefulRefugees
{
  [HarmonyPatch]
  public static class PawnGuestTracker_SetGuestStatus_Patch
  {
    private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_GuestTracker), "pawn");

    public static IEnumerable<MethodBase> TargetMethods()
    {
      return AccessTools.GetDeclaredMethods(typeof(Pawn_GuestTracker))
        .Where(method => method.Name == "SetGuestStatus");
    }

    public static void Postfix(Pawn_GuestTracker __instance, object[] __args)
    {
      GratefulRefugeesDebug.Log("Patched method fired: Pawn_GuestTracker.SetGuestStatus");

      if (__instance == null)
      {
        GratefulRefugeesDebug.Log("Event fired but 0 pawns detected — likely wrong hook.");
        return;
      }

      var pawn = PawnField?.GetValue(__instance) as Pawn;
      if (pawn == null)
      {
        GratefulRefugeesDebug.Log("Event fired but 0 pawns detected — likely wrong hook.");
        return;
      }

      var mapIndex = pawn.Map != null ? pawn.Map.Index.ToString() : "<no map>";
      var mapParentLabel = pawn.Map?.Parent != null ? pawn.Map.Parent.LabelCap : "<no map parent>";
      var mapParentType = pawn.Map?.Parent != null ? pawn.Map.Parent.GetType().Name : "<no map parent type>";
      GratefulRefugeesDebug.Log("Event map: index=" + mapIndex + " parentLabel=" + mapParentLabel + " parentType=" + mapParentType);

      GuestStatus? guestStatus = null;
      Faction hostFaction = null;

      if (__args != null)
      {
        for (var i = 0; i < __args.Length; i++)
        {
          if (__args[i] is GuestStatus status)
          {
            guestStatus = status;
          }
          else if (__args[i] is Faction faction)
          {
            hostFaction = faction;
          }
        }
      }

      GratefulRefugeesDebug.Log("Event detected pawn count=1");
      GratefulRefugeesDebug.LogPawnDetails(pawn);
      GratefulRefugeesUtility.TryApplyTakenIn(pawn, guestStatus, hostFaction, "SetGuestStatus");
    }
  }
}
