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
      if (__instance == null)
      {
        return;
      }

      var pawn = PawnField?.GetValue(__instance) as Pawn;
      if (pawn == null)
      {
        return;
      }

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

      GratefulRefugeesUtility.TryApplyTakenIn(pawn, guestStatus, hostFaction, "SetGuestStatus");
    }
  }
}
