using UnityEngine;
using System;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    // Rebalanced: Starting gold set to 60
    public int Gold { get; private set; } = 60;
    public int Materials { get; private set; } = 50;
    public int SpecialMaterials { get; private set; } = 0;

    public event Action OnResourceChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ResetStartingResources()
    {
        Gold = 60;
        Materials = 50;
        SpecialMaterials = 0;
        OnResourceChanged?.Invoke();
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        OnResourceChanged?.Invoke();
        return true;
    }

    public bool SpendMaterials(int amount)
    {
        if (Materials < amount) return false;
        Materials -= amount;
        OnResourceChanged?.Invoke();
        return true;
    }

    public void AddReward(int day)
    {
        // Further reduced rewards — tight economy forces players to farm zombie kills too
        int goldGain = 8 + (day * 5);  // Day 1: 11G, Day 2: 14G, Day 3: 17G, Day 5: 23G, Day 10: 38G
        int matGain = 6 + (day * 3);   // Day 1: 8M,  Day 2: 10M, Day 3: 12M, Day 5: 16M, Day 10: 26M

        Gold += goldGain;
        Materials += matGain;
        OnResourceChanged?.Invoke();
        Debug.Log($"[Resource] Stage {day} Reward: +{goldGain} Gold, +{matGain} Materials");
    }

    public void AddSpecialReward()
    {
        SpecialMaterials += UnityEngine.Random.Range(1, 4);
        Gold += 15;
        OnResourceChanged?.Invoke();
        Debug.Log($"[Resource] Special event reward! SpecialMats: {SpecialMaterials}");
    }
}
