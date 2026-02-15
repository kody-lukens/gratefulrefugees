using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
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
      GratefulRefugeesDebug.LogVerbose("Patched method fired: Pawn_GuestTracker.SetGuestStatus");

      if (__instance == null)
      {
        GratefulRefugeesDebug.LogVerbose("Event fired but 0 pawns detected — likely wrong hook.");
        return;
      }

      var pawn = PawnField?.GetValue(__instance) as Pawn;
      if (pawn == null)
      {
        GratefulRefugeesDebug.LogVerbose("Event fired but 0 pawns detected — likely wrong hook.");
        return;
      }

      var mapIndex = pawn.Map != null ? pawn.Map.Index.ToString() : "<no map>";
      var mapParentLabel = pawn.Map?.Parent != null ? pawn.Map.Parent.LabelCap : "<no map parent>";
      var mapParentType = pawn.Map?.Parent != null ? pawn.Map.Parent.GetType().Name : "<no map parent type>";
      GratefulRefugeesDebug.LogVerbose("Event map: index=" + mapIndex + " parentLabel=" + mapParentLabel + " parentType=" + mapParentType);

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

      GratefulRefugeesDebug.LogVerbose("Event detected pawn count=1");
      GratefulRefugeesDebug.LogPawnDetails(pawn);

      var quest = GratefulRefugeesUtility.TryGetQuestFromPawnPublic(pawn);
      var guestStatusText = pawn.guest != null ? pawn.guest.GuestStatus.ToString() : "<no guest tracker>";
      var hostFactionText = pawn.guest?.HostFaction != null ? pawn.guest.HostFaction.Name : "<null>";
      var pawnFactionText = pawn.Faction != null ? pawn.Faction.Name : "<null>";
      var eligible = GratefulRefugeesUtility.IsGratefulRefugeeCandidate(pawn, quest, guestStatus, hostFaction, out var eligibleReason);
      GratefulRefugeesDebug.LogMessage("SetGuestStatus: pawn=" + pawn.LabelShortCap + " id=" + pawn.thingIDNumber
        + " guestStatus=" + guestStatusText
        + " hostFaction=" + hostFactionText
        + " pawnFaction=" + pawnFactionText
        + " eligible=" + eligible
        + (eligible ? "" : " reason=" + eligibleReason));

      if (guestStatus == GuestStatus.Guest)
      {
        GratefulRefugeesUtility.AddPending(pawn, quest, "SetGuestStatus");
      }
      else
      {
        GratefulRefugeesDebug.LogNotCandidate(pawn, "guest status not Guest", "SetGuestStatus");
      }
      if (!pawn.Spawned || pawn.Map == null)
      {
        GratefulRefugeesDebug.LogVerbose("Queued pending apply: pawn=" + pawn.LabelShortCap + " id=" + pawn.thingIDNumber + " reason=not spawned");
      }
      GratefulRefugeesUtility.TryApplyPendingIfReady(pawn, "SetGuestStatus");
    }
  }

  [HarmonyPatch(typeof(QuestPart_PawnsArrive), "Notify_QuestSignalReceived")]
  public static class QuestPart_PawnsArrive_Notify_Patch
  {
    private static readonly FieldInfo QuestField = AccessTools.Field(typeof(QuestPart), "quest");
    private static readonly FieldInfo MapParentField = AccessTools.Field(typeof(QuestPart_PawnsArrive), "mapParent");

    public static void Postfix(QuestPart_PawnsArrive __instance, Signal signal)
    {
      GratefulRefugeesDebug.LogVerbose("Patched method fired: QuestPart_PawnsArrive.Notify_QuestSignalReceived");

      if (__instance == null)
      {
        GratefulRefugeesDebug.LogVerbose("Event fired but 0 pawns detected — likely wrong hook.");
        return;
      }

      var quest = QuestField?.GetValue(__instance) as Quest;
      if (quest != null)
      {
        var questDefField = AccessTools.Field(quest.GetType(), "questScriptDef");
        var questDef = questDefField?.GetValue(quest) as QuestScriptDef;
        GratefulRefugeesDebug.LogVerbose("QuestPart quest def=" + (questDef != null ? questDef.defName : "<null>"));
      }
      else
      {
        GratefulRefugeesDebug.LogVerbose("QuestPart quest is null");
      }

      GratefulRefugeesDebug.LogVerbose("Signal received: " + signal.tag);

      var mapParent = MapParentField?.GetValue(__instance) as MapParent;
      var map = mapParent?.Map;
      var mapIndex = map != null ? map.Index.ToString() : "<no map>";
      var mapParentLabel = mapParent != null ? mapParent.LabelCap : "<no map parent>";
      var mapParentType = mapParent != null ? mapParent.GetType().Name : "<no map parent type>";
      GratefulRefugeesDebug.LogVerbose("Event map: index=" + mapIndex + " parentLabel=" + mapParentLabel + " parentType=" + mapParentType);

      var pawns = GetPawnsFromQuestPart(__instance);
      if (pawns == null || pawns.Count == 0)
      {
        GratefulRefugeesDebug.LogVerbose("Event fired but 0 pawns detected — likely wrong hook.");
        return;
      }

      GratefulRefugeesDebug.LogVerbose("Event detected pawn count=" + pawns.Count);
      for (var i = 0; i < pawns.Count; i++)
      {
        var pawn = pawns[i];
        GratefulRefugeesDebug.LogPawnDetails(pawn);
        GratefulRefugeesUtility.AddPending(pawn, quest, "QuestPart_PawnsArrive");
        GratefulRefugeesUtility.TryApplyPendingIfReady(pawn, "QuestPart_PawnsArrive");
      }
    }

    private static List<Pawn> GetPawnsFromQuestPart(QuestPart_PawnsArrive questPart)
    {
      var list = new List<Pawn>();
      if (questPart == null)
      {
        return list;
      }

      var fields = questPart.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      foreach (var field in fields)
      {
        var fieldType = field.FieldType;
        if (typeof(IEnumerable<Pawn>).IsAssignableFrom(fieldType))
        {
          var value = field.GetValue(questPart) as IEnumerable<Pawn>;
          if (value == null)
          {
            continue;
          }

          GratefulRefugeesDebug.Log("QuestPart pawn field: " + field.Name + " type=" + fieldType.FullName);
          list.AddRange(value);
        }
      }

      return list.Distinct().ToList();
    }
  }

  [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
  public static class Pawn_SpawnSetup_Patch
  {
    public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
    {
      GratefulRefugeesDebug.LogVerbose("Patched method fired: Pawn.SpawnSetup");
      if (__instance == null)
      {
        GratefulRefugeesDebug.LogVerbose("Spawn hook: pawn is null");
        return;
      }

      GratefulRefugeesDebug.LogVerbose("Spawn hook: pawn=" + __instance.LabelShortCap + " id=" + __instance.thingIDNumber
        + " spawned=" + __instance.Spawned
        + " map=" + (map != null ? map.Index.ToString() : "<no map>")
        + " respawning=" + respawningAfterLoad);

      GratefulRefugeesUtility.TryApplyPendingIfReady(__instance, "SpawnSetup");
    }
  }

  [HarmonyPatch(typeof(Map), "FinalizeInit")]
  public static class Map_FinalizeInit_Patch
  {
    public static void Postfix(Map __instance)
    {
      if (!GratefulRefugeesDebug.DevSummaryOnMapLoad || __instance == null)
      {
        return;
      }

      GratefulRefugeesUtility.LogMapCandidateSummary(__instance);
    }
  }
}
