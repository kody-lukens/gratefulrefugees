using RimWorld;
using Verse;

namespace GratefulRefugees
{
  public static class GratefulRefugeesSettingsApplier
  {
    public static void Apply()
    {
      var settings = GratefulRefugeesMod.Settings;
      if (settings == null)
      {
        return;
      }

      var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("GratefulRefugees_TakenIn");
      if (thoughtDef == null)
      {
        return;
      }

      var moodBonus = settings.moodBonus;
      var durationDays = settings.durationDays;
      var decayRateDays = settings.decayRateDays;

      if (durationDays < 1)
      {
        durationDays = 1;
      }

      if (decayRateDays < 1)
      {
        decayRateDays = 1;
      }

      thoughtDef.durationDays = settings.decayTakenIn ? decayRateDays : durationDays;
      if (thoughtDef.stages != null && thoughtDef.stages.Count > 0)
      {
        thoughtDef.stages[0].baseMoodEffect = moodBonus;
      }

      var settlingDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("GratefulRefugees_SettlingIn");
      if (settlingDef != null)
      {
        settlingDef.durationDays = decayRateDays;
        if (settlingDef.stages != null && settlingDef.stages.Count > 0)
        {
          settlingDef.stages[0].baseMoodEffect = moodBonus * 0.5f;
        }
      }
    }
  }
}
