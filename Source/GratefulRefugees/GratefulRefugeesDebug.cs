using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GratefulRefugees
{
  public static class GratefulRefugeesDebug
  {
    public static bool VerboseLogging = true;
    private const string Prefix = "[GratefulRefugees] ";

    public static void Log(string message)
    {
      if (!VerboseLogging)
      {
        return;
      }

      LogMessage(message);
    }

    public static void LogMessage(string message)
    {
      Verse.Log.Message(Prefix + message);
    }

    public static void LogQuestDefDiscovery()
    {
      if (!VerboseLogging)
      {
        return;
      }

      var candidatesByName = DefDatabase<QuestScriptDef>.AllDefs
        .Where(def => def.defName != null && def.defName.IndexOf("Refugee", StringComparison.OrdinalIgnoreCase) >= 0)
        .ToList();

      foreach (var def in candidatesByName)
      {
        Log("Quest def candidate by name: " + def.defName + " label=" + (def.label ?? "<null>"));
      }

      var candidatesByLabel = DefDatabase<QuestScriptDef>.AllDefs
        .Where(def => def.label != null && def.label.IndexOf("Refugees Seek Shelter", StringComparison.OrdinalIgnoreCase) >= 0)
        .ToList();

      foreach (var def in candidatesByLabel)
      {
        Log("Quest def candidate by label match: label='" + def.label + "' defName=" + def.defName);
      }

      var candidatesByRoot = DefDatabase<QuestScriptDef>.AllDefs
        .Where(def => def.root != null && def.root.GetType().Name.IndexOf("Refugee", StringComparison.OrdinalIgnoreCase) >= 0)
        .ToList();

      foreach (var def in candidatesByRoot)
      {
        Log("Quest def candidate by root type: defName=" + def.defName + " rootType=" + def.root.GetType().FullName);
      }

      if (candidatesByName.Count == 0 && candidatesByLabel.Count == 0 && candidatesByRoot.Count == 0)
      {
        Log("No matching Refugees Seek Shelter quest defs found at startup.");
      }
    }

    public static void LogPawnDetails(Pawn pawn)
    {
      if (!VerboseLogging)
      {
        return;
      }

      if (pawn == null)
      {
        Log("Pawn details: <null>");
        return;
      }

      var guestStatus = pawn.guest != null ? pawn.guest.GuestStatus.ToString() : "<no guest tracker>";
      var hostFaction = pawn.guest?.HostFaction != null ? pawn.guest.HostFaction.Name : "<null>";
      var faction = pawn.Faction != null ? pawn.Faction.Name : "<null>";
      var mapIndex = pawn.Map != null ? pawn.Map.Index.ToString() : "<no map>";
      var mapParent = pawn.Map?.Parent != null ? pawn.Map.Parent.LabelCap : "<no map parent>";
      var mapParentType = pawn.Map?.Parent != null ? pawn.Map.Parent.GetType().Name : "<no map parent type>";
      var mapHeld = pawn.MapHeld != null ? pawn.MapHeld.Index.ToString() : "<no map held>";
      var isCaravanMember = TryGetBool(pawn, "IsCaravanMember");
      var foodRestriction = GetFoodRestrictionLabel(pawn) ?? "<none>";
      var moodPresent = pawn.needs?.mood != null;
      var moodLevel = moodPresent ? pawn.needs.mood.CurLevel.ToString("0.##") : "<no mood>";

      Log("Pawn detected: name=" + pawn.LabelShortCap
          + " id=" + pawn.thingIDNumber
          + " faction=" + faction
          + " guestStatus=" + guestStatus
          + " hostFaction=" + hostFaction
          + " isColonist=" + pawn.IsColonist
          + " isPrisoner=" + TryGetBool(pawn, "IsPrisoner")
          + " isSlave=" + TryGetBool(pawn, "IsSlave")
          + " downed=" + pawn.Downed
          + " dead=" + pawn.Dead
          + " spawned=" + pawn.Spawned
          + " humanlike=" + pawn.RaceProps?.Humanlike
          + " mapIndex=" + mapIndex
          + " mapParent=" + mapParent
          + " mapParentType=" + mapParentType
          + " mapHeld=" + mapHeld
          + " caravanMember=" + isCaravanMember
          + " foodRestriction=" + foodRestriction
          + " moodPresent=" + moodPresent
          + " moodLevel=" + moodLevel);
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

    private static string GetFoodRestrictionLabel(Pawn pawn)
    {
      if (pawn?.foodRestriction == null)
      {
        return null;
      }

      var property = AccessTools.Property(pawn.foodRestriction.GetType(), "CurrentFoodRestriction");
      if (property != null)
      {
        var restriction = property.GetValue(pawn.foodRestriction);
        if (restriction != null)
        {
          var restrictionLabelProperty = AccessTools.Property(restriction.GetType(), "Label");
          if (restrictionLabelProperty != null)
          {
            return restrictionLabelProperty.GetValue(restriction) as string;
          }
        }
      }

      var labelProperty = AccessTools.Property(pawn.foodRestriction.GetType(), "CurrentFoodRestrictionLabel");
      if (labelProperty != null)
      {
        return labelProperty.GetValue(pawn.foodRestriction) as string;
      }

      return null;
    }
  }
}
