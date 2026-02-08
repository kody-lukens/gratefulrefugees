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

    private static MethodInfo questFromThingMethod;
    private static MethodInfo questFromTagMethod;
    private static FieldInfo questTagsField;
    private static PropertyInfo questTagsProperty;
    private static FieldInfo questScriptDefField;
    private static PropertyInfo questScriptDefProperty;
    private static FieldInfo questIdField;
    private static PropertyInfo questIdProperty;

    public static void TryApplyTakenIn(Pawn pawn, GuestStatus? guestStatusFromPatch, Faction hostFactionFromPatch, string source)
    {
      GratefulRefugeesDebug.Log("Apply attempt: " + (pawn != null ? pawn.LabelShortCap : "<null>") + " (" + (pawn != null ? pawn.thingIDNumber.ToString() : "null") + ") source=" + source);

      if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
      {
        if (pawn == null)
        {
          GratefulRefugeesDebug.Log("Skip: pawn is null");
        }
        else if (pawn.Dead)
        {
          GratefulRefugeesDebug.Log("Skip: pawn is dead");
        }
        else if (!pawn.Spawned || pawn.Map == null)
        {
          GratefulRefugeesDebug.Log("Skip: not spawned / not on map");
        }
        return;
      }

      if (!pawn.RaceProps.Humanlike)
      {
        GratefulRefugeesDebug.Log("Skip: not humanlike");
        return;
      }

      if (pawn.IsColonist || pawn.IsColonistPlayerControlled)
      {
        GratefulRefugeesDebug.Log("Skip: pawn is colonist");
        return;
      }

      if (IsPrisonerOrSlave(pawn))
      {
        GratefulRefugeesDebug.Log("Skip: pawn is prisoner / slave");
        return;
      }

      var guestStatus = guestStatusFromPatch ?? pawn.guest?.GuestStatus;
      if (guestStatus != GuestStatus.Guest)
      {
        GratefulRefugeesDebug.Log("Skip: pawn is not guest / not temporarily joined (status=" + (guestStatus?.ToString() ?? "<null>") + ")");
        return;
      }

      var hostFaction = hostFactionFromPatch ?? pawn.guest?.HostFaction;
      if (hostFaction != Faction.OfPlayer)
      {
        GratefulRefugeesDebug.Log("Skip: pawn is hostile / faction not appropriate (hostFaction=" + (hostFaction != null ? hostFaction.Name : "<null>") + ")");
        return;
      }

      var quest = TryGetQuestFromPawn(pawn);
      if (!IsHospitalityRefugeeQuest(quest))
      {
        GratefulRefugeesDebug.Log("Skip: quest mismatch (expected=" + RefugeeQuestDefName + " actual=" + (GetQuestScriptDef(quest)?.defName ?? "<null>") + ")");
        return;
      }

      var component = Current.Game?.GetComponent<GratefulRefugeesGameComponent>();
      if (component == null)
      {
        GratefulRefugeesDebug.Log("Failed: game component missing");
        return;
      }

      var questId = GetQuestId(quest);
      if (!component.TryMarkApplied(pawn.thingIDNumber, questId))
      {
        GratefulRefugeesDebug.Log("Skip: already applied via our tracking dictionary");
        return;
      }

      var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(TakenInThoughtDefName);
      if (thoughtDef == null)
      {
        GratefulRefugeesDebug.Log("Failed: missing thought def " + TakenInThoughtDefName);
        return;
      }

      var memories = pawn.needs?.mood?.thoughts?.memories;
      if (memories == null)
      {
        GratefulRefugeesDebug.Log("Failed: pawn has no needs/mood or cannot receive memories");
        return;
      }

      if (memories.GetFirstMemoryOfDef(thoughtDef) != null)
      {
        GratefulRefugeesDebug.Log("Skip: thought already present");
        return;
      }

      memories.TryGainMemory(thoughtDef);
      GratefulRefugeesDebug.Log("Applied TakenIn thought to " + pawn.LabelShortCap + " (" + pawn.thingIDNumber + ") moodOffset=30 durationTicks=" + thoughtDef.DurationTicks);
    }

    private static bool IsHospitalityRefugeeQuest(Quest quest)
    {
      var questDef = GetQuestScriptDef(quest);
      if (questDef == null)
      {
        GratefulRefugeesDebug.Log("Quest identification: questDef is null");
      }
      else
      {
        GratefulRefugeesDebug.Log("Quest identification: questDef=" + questDef.defName + " label=" + (questDef.label ?? "<null>") + " rootType=" + questDef.root?.GetType().FullName);
      }
      return questDef != null && questDef.defName == RefugeeQuestDefName;
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

    private static Quest TryGetQuestFromPawn(Pawn pawn)
    {
      var questUtilityType = AccessTools.TypeByName("RimWorld.QuestUtility");
      if (questUtilityType == null)
      {
        GratefulRefugeesDebug.Log("QuestUtility type not found");
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
          GratefulRefugeesDebug.Log("Quest lookup via method: " + questFromThingMethod.Name);
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
        GratefulRefugeesDebug.Log("Quest tags not found on pawn");
        return null;
      }

      GratefulRefugeesDebug.Log("Quest tags found: " + string.Join(", ", tags));

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
          GratefulRefugeesDebug.Log("Quest lookup via tag: " + tags[i]);
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
  }
}
