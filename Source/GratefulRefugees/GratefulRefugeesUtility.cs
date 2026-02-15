using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GratefulRefugees
{
  public static class GratefulRefugeesUtility
  {
    private const string RefugeeQuestDefName = "Hospitality_Refugee";
    private const string TakenInThoughtDefName = "GratefulRefugees_TakenIn";
    private const string InitialOptimismDefName = "NewColonyOptimism";

    private static MethodInfo questFromThingMethod;
    private static MethodInfo questFromTagMethod;
    private static MethodInfo isQuestLodgerMethod;
    private static FieldInfo questTagsField;
    private static PropertyInfo questTagsProperty;
    private static FieldInfo questScriptDefField;
    private static PropertyInfo questScriptDefProperty;
    private static FieldInfo questIdField;
    private static PropertyInfo questIdProperty;

    public static void TryApplyTakenIn(Pawn pawn, GuestStatus? guestStatusFromPatch, Faction hostFactionFromPatch, string source)
    {
      GratefulRefugeesDebug.LogApplyAttempt(pawn, source);

      if (!PassesBasicPawnChecks(pawn, out var basicFailure))
      {
        GratefulRefugeesDebug.LogNotCandidate(pawn, basicFailure, source);
        return;
      }

      var quest = TryGetQuestFromPawn(pawn);
      TryApplyTakenInWithQuest(pawn, quest, guestStatusFromPatch, hostFactionFromPatch, source);
    }

    public static void TryApplyTakenInWithQuest(Pawn pawn, Quest quest, GuestStatus? guestStatusFromPatch, Faction hostFactionFromPatch, string source)
    {
      if (pawn == null)
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "pawn is null");
        return;
      }

      if (!PassesBasicPawnChecks(pawn, out var basicFailure))
      {
        GratefulRefugeesDebug.LogNotCandidate(pawn, basicFailure, source);
        return;
      }

      if (!PassesLodgerChecks(pawn, guestStatusFromPatch, hostFactionFromPatch, out var lodgerFailure))
      {
        GratefulRefugeesDebug.LogNotCandidate(pawn, lodgerFailure, source);
        return;
      }

      if (!PassesQuestAssociationChecks(pawn, quest, out var questAssocFailure))
      {
        GratefulRefugeesDebug.LogNotCandidate(pawn, questAssocFailure, source);
        return;
      }

      var component = Current.Game?.GetComponent<GratefulRefugeesGameComponent>();
      if (component == null)
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "game component missing");
        return;
      }

      var thoughtDef = GetConfiguredThoughtDef();
      if (thoughtDef == null)
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "missing thought def");
        return;
      }

      var memories = pawn.needs?.mood?.thoughts?.memories;
      if (memories == null)
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "no mood need or memories");
        return;
      }

      LogCandidateIfNeeded(pawn, quest, guestStatusFromPatch, hostFactionFromPatch, source);

      var questId = GetQuestId(quest);
      if (!component.TryMarkApplied(pawn.thingIDNumber, questId))
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "already applied");
        return;
      }

      if (memories.GetFirstMemoryOfDef(thoughtDef) != null)
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "thought already present");
        return;
      }

      memories.TryGainMemory(thoughtDef);
      QueueSettlingInIfNeeded(pawn, thoughtDef);
      GratefulRefugeesDebug.LogApplySuccess(pawn, thoughtDef);
    }

    private static bool PassesBasicPawnChecks(Pawn pawn, out string failureReason)
    {
      failureReason = null;
      if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
      {
        if (pawn == null)
        {
          failureReason = "pawn is null";
        }
        else if (pawn.Dead)
        {
          failureReason = "pawn is dead";
        }
        else if (!pawn.Spawned || pawn.Map == null)
        {
          failureReason = "not spawned / not on map";
        }
        return false;
      }

      if (!pawn.RaceProps.Humanlike)
      {
        failureReason = "not humanlike";
        return false;
      }

      if (IsPrisonerOrSlave(pawn))
      {
        failureReason = "prisoner/slave";
        return false;
      }

      return true;
    }

    private static bool PassesLodgerChecks(Pawn pawn, GuestStatus? guestStatusFromPatch, Faction hostFactionFromPatch, out string failureReason)
    {
      failureReason = null;
      if (!IsTemporaryQuestLodger(pawn, guestStatusFromPatch, hostFactionFromPatch, out var lodgerReason))
      {
        failureReason = lodgerReason ?? "not temporary quest lodger/guest";
        return false;
      }
      return true;
    }

    private static bool IsTemporaryQuestLodger(Pawn pawn, GuestStatus? guestStatusFromPatch, Faction hostFactionFromPatch, out string failureReason)
    {
      failureReason = null;
      if (pawn == null)
      {
        failureReason = "pawn is null";
        return false;
      }

      if (TryGetBool(pawn, "IsQuestLodger"))
      {
        GratefulRefugeesDebug.LogVerbose("Lodger check: pawn.IsQuestLodger=true");
        return true;
      }

      if (TryGetBool(pawn.guest, "IsQuestLodger"))
      {
        GratefulRefugeesDebug.LogVerbose("Lodger check: pawn.guest.IsQuestLodger=true");
        return true;
      }

      var guestStatus = guestStatusFromPatch ?? pawn.guest?.GuestStatus;
      if (guestStatus == GuestStatus.Guest)
      {
        GratefulRefugeesDebug.LogVerbose("Lodger check: GuestStatus=Guest");
        return true;
      }

      var hostFaction = hostFactionFromPatch ?? pawn.guest?.HostFaction;
      if (hostFaction == Faction.OfPlayer)
      {
        GratefulRefugeesDebug.LogVerbose("Lodger check: hostFaction=player");
        return true;
      }

      var questUtilityType = AccessTools.TypeByName("RimWorld.QuestUtility");
      if (questUtilityType != null)
      {
        isQuestLodgerMethod ??= questUtilityType
          .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
          .FirstOrDefault(m =>
            m.ReturnType == typeof(bool) &&
            m.Name.IndexOf("IsQuestLodger", StringComparison.OrdinalIgnoreCase) >= 0 &&
            m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType == typeof(Pawn));

        if (isQuestLodgerMethod != null)
        {
          try
          {
            var result = (bool)isQuestLodgerMethod.Invoke(null, new object[] { pawn });
            GratefulRefugeesDebug.LogVerbose("Lodger check: QuestUtility." + isQuestLodgerMethod.Name + "=" + result);
            return result;
          }
          catch
          {
            GratefulRefugeesDebug.LogVerbose("Lodger check: QuestUtility.IsQuestLodger invocation failed");
          }
        }
      }

      failureReason = "lodger check failed (guestStatus=" + (guestStatus?.ToString() ?? "<null>") + " hostFaction=" + (hostFaction != null ? hostFaction.Name : "<null>") + ")";
      return false;
    }

    public static void AddPending(Pawn pawn, Quest quest, string source)
    {
      if (pawn == null)
      {
        GratefulRefugeesDebug.LogVerbose("Pending add skipped: pawn is null");
        return;
      }

      var component = Current.Game?.GetComponent<GratefulRefugeesGameComponent>();
      if (component == null)
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "game component missing");
        return;
      }

      var questId = GetQuestId(quest);
      component.AddPending(pawn.thingIDNumber, questId);
      GratefulRefugeesDebug.LogVerbose("Pending added: pawn=" + pawn.LabelShortCap + " id=" + pawn.thingIDNumber + " questId=" + questId + " source=" + source);
    }

    public static void TryApplyPendingIfReady(Pawn pawn, string source)
    {
      if (pawn == null)
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "pawn is null");
        return;
      }

      var component = Current.Game?.GetComponent<GratefulRefugeesGameComponent>();
      if (component == null)
      {
        GratefulRefugeesDebug.LogApplyFailure(pawn, "game component missing");
        return;
      }

      if (!component.IsPending(pawn.thingIDNumber))
      {
        return;
      }

      if (!pawn.Spawned || pawn.Map == null)
      {
        GratefulRefugeesDebug.LogNotCandidate(pawn, "not spawned / not on map", source);
        return;
      }

      var quest = TryGetQuestFromPawn(pawn);
      TryApplyTakenInWithQuest(pawn, quest, null, null, source);

      var thoughtDef = GetConfiguredThoughtDef();
      if (thoughtDef != null)
      {
        var memories = pawn.needs?.mood?.thoughts?.memories;
        var hasMemory = memories != null && memories.GetFirstMemoryOfDef(thoughtDef) != null;
        if (hasMemory)
        {
          component.RemovePending(pawn.thingIDNumber);
        }
      }
    }

    public static bool IsGratefulRefugeeCandidate(Pawn pawn, Quest quest, GuestStatus? guestStatusFromPatch, Faction hostFactionFromPatch, out string reason)
    {
      reason = null;
      if (!PassesBasicPawnChecks(pawn, out var basicFailure))
      {
        reason = basicFailure;
        return false;
      }

      if (!PassesLodgerChecks(pawn, guestStatusFromPatch, hostFactionFromPatch, out var lodgerFailure))
      {
        reason = lodgerFailure;
        return false;
      }

      if (!PassesQuestAssociationChecks(pawn, quest, out var questAssocFailure))
      {
        reason = questAssocFailure;
        return false;
      }

      if (pawn.needs?.mood?.thoughts?.memories == null)
      {
        reason = "no mood need";
        return false;
      }

      return true;
    }

    private static bool PassesQuestAssociationChecks(Pawn pawn, Quest quest, out string failureReason)
    {
      failureReason = null;
      if (pawn == null)
      {
        failureReason = "pawn is null";
        return false;
      }

      var hasQuest = quest != null;
      var tags = GetQuestTags(pawn);
      var hasTags = tags != null && tags.Count > 0;
      var isQuestLodger = TryGetBool(pawn, "IsQuestLodger") || TryGetBool(pawn.guest, "IsQuestLodger");

      if (hasQuest || hasTags || isQuestLodger)
      {
        return true;
      }

      failureReason = "not quest associated";
      return false;
    }

    public static void LogMapCandidateSummary(Map map)
    {
      if (map == null)
      {
        return;
      }

      var key = Gen.HashCombineInt(map.Index, "candidateSummary".GetHashCode());
      var pawns = map.mapPawns?.AllPawnsSpawned ?? new List<Pawn>();
      var lines = new List<string>();
      foreach (var pawn in pawns)
      {
        var quest = TryGetQuestFromPawn(pawn);
        if (IsGratefulRefugeeCandidate(pawn, quest, null, null, out var reason))
        {
          var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(TakenInThoughtDefName);
          var memories = pawn.needs?.mood?.thoughts?.memories;
          var hasMemory = thoughtDef != null && memories != null && memories.GetFirstMemoryOfDef(thoughtDef) != null;
          lines.Add("candidate pawn=" + pawn.LabelShortCap + " id=" + pawn.thingIDNumber + " hasMemory=" + hasMemory);
        }
      }

      var mapLabel = map.Parent != null ? map.Parent.LabelCap : "Map " + map.Index;
      var summary = "[Summary] " + mapLabel + " candidates=" + lines.Count;
      if (lines.Count > 0)
      {
        summary += " | " + string.Join("; ", lines);
      }

      GratefulRefugeesDebug.LogOnce(summary, key);
    }

    public static void TryHandleDecayQueue(GratefulRefugeesGameComponent component)
    {
      var settings = GratefulRefugeesMod.Settings;
      if (settings == null || settings.useInitialOptimism || !settings.decayTakenIn)
      {
        return;
      }

      if (component == null)
      {
        return;
      }

      var settlingDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("GratefulRefugees_SettlingIn");
      if (settlingDef == null)
      {
        return;
      }

      var pawnIds = new List<int>();
      var ticks = new List<int>();
      component.GetDecayEntries(pawnIds, ticks);
      if (pawnIds.Count == 0)
      {
        return;
      }

      var currentTick = Find.TickManager?.TicksGame ?? 0;
      var pawnsById = new Dictionary<int, Pawn>();
      foreach (var map in Find.Maps)
      {
        if (map?.mapPawns == null)
        {
          continue;
        }

        foreach (var pawn in map.mapPawns.AllPawnsSpawned)
        {
          pawnsById[pawn.thingIDNumber] = pawn;
        }
      }

      for (var i = pawnIds.Count - 1; i >= 0; i--)
      {
        if (ticks[i] > currentTick)
        {
          continue;
        }

        var pawnId = pawnIds[i];
        if (!pawnsById.TryGetValue(pawnId, out var pawn))
        {
          component.ClearDecay(pawnId);
          continue;
        }

        var memories = pawn.needs?.mood?.thoughts?.memories;
        if (memories == null)
        {
          component.ClearDecay(pawnId);
          continue;
        }

        if (memories.GetFirstMemoryOfDef(settlingDef) == null)
        {
          memories.TryGainMemory(settlingDef);
        }

        component.ClearDecay(pawnId);
      }
    }

    private static void QueueSettlingInIfNeeded(Pawn pawn, ThoughtDef appliedDef)
    {
      var settings = GratefulRefugeesMod.Settings;
      if (settings == null || settings.useInitialOptimism || !settings.decayTakenIn)
      {
        return;
      }

      if (appliedDef == null || appliedDef.defName != TakenInThoughtDefName)
      {
        return;
      }

      var component = Current.Game?.GetComponent<GratefulRefugeesGameComponent>();
      if (component == null)
      {
        return;
      }

      var tickNow = Find.TickManager?.TicksGame ?? 0;
      var durationTicks = appliedDef.DurationTicks;
      if (durationTicks <= 0)
      {
        return;
      }

      component.SetDecay(pawn.thingIDNumber, tickNow + durationTicks);
    }

    private static QuestScriptDef GetQuestScriptDef(Quest quest)
    {
      if (quest == null)
      {
        return null;
      }

      questScriptDefField ??= AccessTools.Field(quest.GetType(), "questScriptDef");
      if (questScriptDefField != null)
      {
        return questScriptDefField.GetValue(quest) as QuestScriptDef;
      }

      questScriptDefProperty ??= AccessTools.Property(quest.GetType(), "QuestScriptDef")
        ?? AccessTools.Property(quest.GetType(), "questScriptDef");

      return questScriptDefProperty?.GetValue(quest) as QuestScriptDef;
    }

    private static int GetQuestId(Quest quest)
    {
      if (quest == null)
      {
        return -1;
      }

      questIdField ??= AccessTools.Field(quest.GetType(), "id");
      if (questIdField != null && questIdField.FieldType == typeof(int))
      {
        return (int)questIdField.GetValue(quest);
      }

      questIdProperty ??= AccessTools.Property(quest.GetType(), "id")
        ?? AccessTools.Property(quest.GetType(), "Id");

      if (questIdProperty != null && questIdProperty.PropertyType == typeof(int))
      {
        return (int)questIdProperty.GetValue(quest);
      }

      return quest.GetHashCode();
    }

    public static Quest TryGetQuestFromPawnPublic(Pawn pawn)
    {
      return TryGetQuestFromPawn(pawn);
    }

    private static Quest TryGetQuestFromPawn(Pawn pawn)
    {
      var questUtilityType = AccessTools.TypeByName("RimWorld.QuestUtility");
      if (questUtilityType == null)
      {
        GratefulRefugeesDebug.LogVerbose("QuestUtility type not found");
        return null;
      }

      questFromThingMethod ??= questUtilityType
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .FirstOrDefault(m =>
          typeof(Quest).IsAssignableFrom(m.ReturnType) &&
          m.GetParameters().Length == 1 &&
          (m.GetParameters()[0].ParameterType == typeof(Thing) ||
           m.GetParameters()[0].ParameterType == typeof(Pawn)));

      if (questFromThingMethod != null)
      {
        try
        {
          GratefulRefugeesDebug.LogVerbose("Quest lookup via method: " + questFromThingMethod.Name);
          return questFromThingMethod.Invoke(null, new object[] { pawn }) as Quest;
        }
        catch
        {
          // Fall through to tag-based resolution.
        }
      }

      var tags = GetQuestTags(pawn);
      if (tags == null || tags.Count == 0)
      {
        GratefulRefugeesDebug.LogVerbose("Quest tags not found on pawn");
        return null;
      }

      GratefulRefugeesDebug.LogVerbose("Quest tags found: " + string.Join(", ", tags));

      questFromTagMethod ??= questUtilityType
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .FirstOrDefault(m =>
          typeof(Quest).IsAssignableFrom(m.ReturnType) &&
          m.GetParameters().Length == 1 &&
          m.GetParameters()[0].ParameterType == typeof(string));

      if (questFromTagMethod == null)
      {
        return null;
      }

      for (var i = 0; i < tags.Count; i++)
      {
        try
        {
          GratefulRefugeesDebug.LogVerbose("Quest lookup via tag: " + tags[i]);
          var quest = questFromTagMethod.Invoke(null, new object[] { tags[i] }) as Quest;
          if (quest != null)
          {
            return quest;
          }
        }
        catch
        {
          // Ignore bad quest tags.
        }
      }

      return null;
    }

    private static List<string> GetQuestTags(Thing thing)
    {
      if (thing == null)
      {
        return null;
      }

      questTagsField ??= AccessTools.Field(thing.GetType(), "questTags")
        ?? AccessTools.Field(typeof(Thing), "questTags");
      if (questTagsField != null)
      {
        return questTagsField.GetValue(thing) as List<string>;
      }

      questTagsProperty ??= AccessTools.Property(thing.GetType(), "QuestTags")
        ?? AccessTools.Property(typeof(Thing), "QuestTags");
      return questTagsProperty?.GetValue(thing) as List<string>;
    }

    private static bool IsPrisonerOrSlave(Pawn pawn)
    {
      if (pawn?.guest == null)
      {
        return false;
      }

      return TryGetBool(pawn, "IsPrisonerOfColony")
        || TryGetBool(pawn, "IsPrisoner")
        || TryGetBool(pawn, "IsSlaveOfColony")
        || TryGetBool(pawn, "IsSlave")
        || TryGetBool(pawn.guest, "IsPrisoner")
        || TryGetBool(pawn.guest, "IsSlave");
    }

    private static bool TryGetBool(object target, string propertyName)
    {
      if (target == null)
      {
        return false;
      }

      var property = AccessTools.Property(target.GetType(), propertyName);
      if (property != null && property.PropertyType == typeof(bool))
      {
        return (bool)property.GetValue(target);
      }

      return false;
    }

    private static void LogCandidateIfNeeded(Pawn pawn, Quest quest, GuestStatus? guestStatusFromPatch, Faction hostFactionFromPatch, string source)
    {
      if (pawn == null)
      {
        return;
      }

      var mapLabel = pawn.Map?.Parent != null ? pawn.Map.Parent.LabelCap : (pawn.Map != null ? "Map " + pawn.Map.Index : "<no map>");
      var faction = pawn.Faction != null ? pawn.Faction.Name : "<null>";
      var guestFlags = GetGuestFlags(pawn, guestStatusFromPatch, hostFactionFromPatch);
      var questInfo = GetQuestInfo(pawn, quest);
      GratefulRefugeesDebug.LogCandidate(pawn, mapLabel, faction, guestFlags, questInfo, source);
    }

    private static string GetQuestInfo(Pawn pawn, Quest quest)
    {
      var questDef = GetQuestScriptDef(quest);
      var questId = GetQuestId(quest);
      var tags = GetQuestTags(pawn);
      var tagsText = tags != null && tags.Count > 0 ? string.Join("|", tags) : "<no tags>";
      return (questDef != null ? questDef.defName : "<null>") + " id=" + questId + " tags=" + tagsText;
    }

    private static string GetGuestFlags(Pawn pawn, GuestStatus? guestStatusFromPatch, Faction hostFactionFromPatch)
    {
      var guestStatus = guestStatusFromPatch ?? pawn.guest?.GuestStatus;
      var isQuestLodger = TryGetBool(pawn, "IsQuestLodger") || TryGetBool(pawn.guest, "IsQuestLodger");
      var hostFaction = hostFactionFromPatch ?? pawn.guest?.HostFaction;
      var hostFactionName = hostFaction != null ? hostFaction.Name : "<null>";
      return "status=" + (guestStatus?.ToString() ?? "<null>")
        + " questLodger=" + isQuestLodger
        + " hostFaction=" + hostFactionName;
    }

    private static ThoughtDef GetConfiguredThoughtDef()
    {
      var settings = GratefulRefugeesMod.Settings;
      if (settings != null && settings.useInitialOptimism)
      {
        return DefDatabase<ThoughtDef>.GetNamedSilentFail(InitialOptimismDefName);
      }

      return DefDatabase<ThoughtDef>.GetNamedSilentFail(TakenInThoughtDefName);
    }
  }
}
