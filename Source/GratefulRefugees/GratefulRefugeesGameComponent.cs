using System.Collections.Generic;
using System.Linq;
using Verse;

namespace GratefulRefugees
{
  public sealed class GratefulRefugeesGameComponent : GameComponent
  {
    private List<string> appliedKeys = new List<string>();
    private HashSet<string> appliedKeySet = new HashSet<string>();
    private List<string> pendingKeys = new List<string>();
    private HashSet<string> pendingKeySet = new HashSet<string>();
    private List<int> decayPawnIds = new List<int>();
    private List<int> decayTicks = new List<int>();

    public GratefulRefugeesGameComponent(Game game)
    {
    }

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Collections.Look(ref appliedKeys, "appliedKeys", LookMode.Value);
      Scribe_Collections.Look(ref pendingKeys, "pendingKeys", LookMode.Value);
      Scribe_Collections.Look(ref decayPawnIds, "decayPawnIds", LookMode.Value);
      Scribe_Collections.Look(ref decayTicks, "decayTicks", LookMode.Value);

      if (Scribe.mode == LoadSaveMode.PostLoadInit)
      {
        appliedKeySet = appliedKeys != null ? new HashSet<string>(appliedKeys) : new HashSet<string>();
        GratefulRefugeesDebug.LogVerbose("Loaded appliedKeys count=" + appliedKeySet.Count);
        pendingKeySet = pendingKeys != null ? new HashSet<string>(pendingKeys) : new HashSet<string>();
        GratefulRefugeesDebug.LogVerbose("Loaded pendingKeys count=" + pendingKeySet.Count);
        decayPawnIds ??= new List<int>();
        decayTicks ??= new List<int>();
      }
      else if (Scribe.mode == LoadSaveMode.Saving)
      {
        var count = appliedKeys != null ? appliedKeys.Count : 0;
        GratefulRefugeesDebug.LogVerbose("Saving appliedKeys count=" + count);
        var pendingCount = pendingKeys != null ? pendingKeys.Count : 0;
        GratefulRefugeesDebug.LogVerbose("Saving pendingKeys count=" + pendingCount);
      }
    }

    public override void FinalizeInit()
    {
      base.FinalizeInit();
      appliedKeySet = appliedKeys != null ? new HashSet<string>(appliedKeys) : new HashSet<string>();
      GratefulRefugeesDebug.LogVerbose("FinalizeInit appliedKeys count=" + appliedKeySet.Count);
      pendingKeySet = pendingKeys != null ? new HashSet<string>(pendingKeys) : new HashSet<string>();
      GratefulRefugeesDebug.LogVerbose("FinalizeInit pendingKeys count=" + pendingKeySet.Count);
      decayPawnIds ??= new List<int>();
      decayTicks ??= new List<int>();
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
          if (IsPending(pawns[i].thingIDNumber))
          {
            GratefulRefugeesUtility.TryApplyPendingIfReady(pawns[i], "PeriodicScan");
          }
        }
      }

      GratefulRefugeesUtility.TryHandleDecayQueue(this);
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
      GratefulRefugeesDebug.LogVerbose("MarkApplied pawnId=" + pawnId + " questId=" + questId);
      return true;
    }

    public void AddPending(int pawnId, int questId)
    {
      var key = pawnId + ":" + questId;
      if (pendingKeySet.Contains(key))
      {
        GratefulRefugeesDebug.LogVerbose("Pending already set pawnId=" + pawnId + " questId=" + questId);
        return;
      }

      pendingKeySet.Add(key);
      pendingKeys ??= new List<string>();
      pendingKeys.Add(key);
      GratefulRefugeesDebug.LogVerbose("Pending set pawnId=" + pawnId + " questId=" + questId);
    }

    public bool IsPending(int pawnId)
    {
      if (pendingKeySet == null)
      {
        return false;
      }

      return pendingKeySet.Any(key => key.StartsWith(pawnId + ":", System.StringComparison.Ordinal));
    }

    public void RemovePending(int pawnId)
    {
      if (pendingKeySet == null || pendingKeys == null)
      {
        return;
      }

      var prefix = pawnId + ":";
      var toRemove = pendingKeySet.Where(key => key.StartsWith(prefix, System.StringComparison.Ordinal)).ToList();
      if (toRemove.Count == 0)
      {
        return;
      }

      foreach (var key in toRemove)
      {
        pendingKeySet.Remove(key);
        pendingKeys.Remove(key);
      }
    }

    public void SetDecay(int pawnId, int tickToApply)
    {
      if (decayPawnIds == null || decayTicks == null)
      {
        decayPawnIds = new List<int>();
        decayTicks = new List<int>();
      }

      var existingIndex = decayPawnIds.IndexOf(pawnId);
      if (existingIndex >= 0)
      {
        decayTicks[existingIndex] = tickToApply;
        return;
      }

      decayPawnIds.Add(pawnId);
      decayTicks.Add(tickToApply);
    }

    public void ClearDecay(int pawnId)
    {
      if (decayPawnIds == null || decayTicks == null)
      {
        return;
      }

      var index = decayPawnIds.IndexOf(pawnId);
      if (index < 0)
      {
        return;
      }

      decayPawnIds.RemoveAt(index);
      decayTicks.RemoveAt(index);
    }

    public void GetDecayEntries(List<int> pawnIds, List<int> ticks)
    {
      pawnIds.Clear();
      ticks.Clear();

      if (decayPawnIds == null || decayTicks == null)
      {
        return;
      }

      pawnIds.AddRange(decayPawnIds);
      ticks.AddRange(decayTicks);
    }
  }
}
