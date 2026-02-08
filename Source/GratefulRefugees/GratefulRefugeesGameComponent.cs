using System.Collections.Generic;
using Verse;

namespace GratefulRefugees
{
  public sealed class GratefulRefugeesGameComponent : GameComponent
  {
    private List<string> appliedKeys = new List<string>();
    private HashSet<string> appliedKeySet = new HashSet<string>();

    public GratefulRefugeesGameComponent(Game game)
    {
    }

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Collections.Look(ref appliedKeys, "appliedKeys", LookMode.Value);

      if (Scribe.mode == LoadSaveMode.PostLoadInit)
      {
        appliedKeySet = appliedKeys != null ? new HashSet<string>(appliedKeys) : new HashSet<string>();
      }
    }

    public override void FinalizeInit()
    {
      base.FinalizeInit();
      appliedKeySet = appliedKeys != null ? new HashSet<string>(appliedKeys) : new HashSet<string>();
    }

    public override void GameComponentTick()
    {
      base.GameComponentTick();

      if (Find.TickManager == null || Find.TickManager.TicksGame % 250 != 0)
      {
        return;
      }

      if (Find.Maps == null)
      {
        return;
      }

      foreach (var map in Find.Maps)
      {
        if (map?.mapPawns == null)
        {
          continue;
        }

        var pawns = map.mapPawns.AllPawnsSpawned;
        for (var i = 0; i < pawns.Count; i++)
        {
          GratefulRefugeesUtility.TryApplyTakenIn(pawns[i], null, null, "PeriodicScan");
        }
      }
    }

    public bool TryMarkApplied(int pawnId, int questId)
    {
      var key = pawnId + ":" + questId;
      if (appliedKeySet.Contains(key))
      {
        return false;
      }

      appliedKeySet.Add(key);
      appliedKeys ??= new List<string>();
      appliedKeys.Add(key);
      return true;
    }
  }
}
