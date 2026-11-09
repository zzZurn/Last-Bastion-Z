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

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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

    public static int GetStageGoldReward(int day)
    {
        // Early game bonus: +20% and now +20% more on the first 4 days (day <= 4) -> 1.44x total bonus
        float earlyBonus = (day <= 4) ? 1.44f : 1.0f;
        int baseGold = Mathf.RoundToInt((8 + (day * 5)) * 1.2f * earlyBonus);
        bool isElite = (GameManager.Instance != null && GameManager.Instance.isEliteBattleRoute);
        int finalGold = isElite ? (baseGold * 2) : baseGold;
        if (day == 15)
        {
            finalGold += 200; // Wave 15 Milestone Clear Bonus (+200 Gold)
        }
        return finalGold;
    }

    public static int GetStageMaterialReward(int day)
    {
        // Early game bonus: +20% and now +20% more on the first 4 days (day <= 4) -> 1.44x total bonus
        float earlyBonus = (day <= 4) ? 1.44f : 1.0f;
        int baseMat = Mathf.RoundToInt((6 + (day * 3)) * 1.2f * earlyBonus);
        bool isElite = (GameManager.Instance != null && GameManager.Instance.isEliteBattleRoute);
        return isElite ? (baseMat * 2) : baseMat;
    }

    public void AddReward(int day)
    {
        int goldGain = GetStageGoldReward(day);
        int matGain = GetStageMaterialReward(day);

        Gold += goldGain;
        Materials += matGain;
        OnResourceChanged?.Invoke();
        bool isElite = (GameManager.Instance != null && GameManager.Instance.isEliteBattleRoute);
        string earlyTag = (day <= 4) ? " (+44% Early Day 1-4 Bonus)" : "";
        string wave15Tag = (day == 15) ? " (+200G Wave 15 Milestone Bonus!)" : "";
        Debug.Log($"[Resource] Stage {day} Reward{(isElite ? " [ELITE 2x BONUS]" : "")}{earlyTag}{wave15Tag}: +{goldGain} Gold, +{matGain} Materials");
    }

    public void AddSpecialReward()
    {
        SpecialMaterials += UnityEngine.Random.Range(1, 4);
        Gold += 15;
        OnResourceChanged?.Invoke();
        Debug.Log($"[Resource] Special event reward! SpecialMats: {SpecialMaterials}");
    }
}
