using Verse;

namespace GratefulRefugees
{
  public class GratefulRefugeesSettings : ModSettings
  {
    public int moodBonus = 30;
    public int durationDays = 30;
    public bool useInitialOptimism = false;
    public bool decayTakenIn = false;
    public int decayRateDays = 5;

    public override void ExposeData()
    {
      Scribe_Values.Look(ref moodBonus, "moodBonus", 30);
      Scribe_Values.Look(ref durationDays, "durationDays", 30);
      Scribe_Values.Look(ref useInitialOptimism, "useInitialOptimism", false);
      Scribe_Values.Look(ref decayTakenIn, "decayTakenIn", false);
      Scribe_Values.Look(ref decayRateDays, "decayRateDays", 5);
      base.ExposeData();
    }
  }
}
