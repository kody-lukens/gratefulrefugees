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
      if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
      {
        return;
      }

      if (!pawn.RaceProps.Humanlike)
      {
        return;
      }

      if (pawn.IsColonist || pawn.IsColonistPlayerControlled)
      {
        return;
      }

      if (IsPrisonerOrSlave(pawn))
      {
        return;
      }

      var guestStatus = guestStatusFromPatch ?? pawn.guest?.GuestStatus;
      if (guestStatus != GuestStatus.Guest)
      {
        return;
      }

      var hostFaction = hostFactionFromPatch ?? pawn.guest?.HostFaction;
      if (hostFaction != Faction.OfPlayer)
      {
        return;
      }

      var quest = TryGetQuestFromPawn(pawn);
      if (!IsHospitalityRefugeeQuest(quest))
      {
        return;
      }

      var component = Current.Game?.GetComponent<GratefulRefugeesGameComponent>();
      if (component == null)
      {
        return;
      }

      var questId = GetQuestId(quest);
      if (!component.TryMarkApplied(pawn.thingIDNumber, questId))
      {
        return;
      }

      var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(TakenInThoughtDefName);
      if (thoughtDef == null)
      {
        return;
      }

      var memories = pawn.needs?.mood?.thoughts?.memories;
      if (memories == null || memories.GetFirstMemoryOfDef(thoughtDef) != null)
      {
        return;
      }

      memories.TryGainMemory(thoughtDef);
    }

    private static bool IsHospitalityRefugeeQuest(Quest quest)
    {
      var questDef = GetQuestScriptDef(quest);
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
        return null;
      }

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
