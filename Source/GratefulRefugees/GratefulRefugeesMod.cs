using UnityEngine;
using Verse;

namespace GratefulRefugees
{
  public class GratefulRefugeesMod : Mod
  {
    public static GratefulRefugeesSettings Settings;

    public GratefulRefugeesMod(ModContentPack content) : base(content)
    {
      Settings = GetSettings<GratefulRefugeesSettings>();
      GratefulRefugeesSettingsApplier.Apply();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
      var listing = new Listing_Standard();
      listing.Begin(inRect);

      listing.Label("Mood bonus");
      listing.IntEntry(ref Settings.moodBonus, ref _moodBonusBuffer);
      listing.Label("Duration (days)");
      listing.IntEntry(ref Settings.durationDays, ref _durationDaysBuffer);
      listing.CheckboxLabeled("Use vanilla \"Initial optimism\" moodlet instead", ref Settings.useInitialOptimism);
      listing.CheckboxLabeled("Decay \"Taken in!\" value over time", ref Settings.decayTakenIn);
      listing.Label("Decay rate (days)");
      listing.IntEntry(ref Settings.decayRateDays, ref _decayRateBuffer);

      listing.End();

      GratefulRefugeesSettingsApplier.Apply();
    }

    public override string SettingsCategory()
    {
      return "Grateful Refugees";
    }

    public override void WriteSettings()
    {
      base.WriteSettings();
      GratefulRefugeesSettingsApplier.Apply();
    }

    private string _moodBonusBuffer = "30";
    private string _durationDaysBuffer = "30";
    private string _decayRateBuffer = "5";
  }
}
