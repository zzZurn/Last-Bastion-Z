using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EventChoice
{
    public string buttonText;
    public string description;
    public int goldCost;
    public int materialCost;
    public int specialRewardMin;
    public int specialRewardMax;
    public int goldReward;
    public int materialReward;
    public Building.BuildingType? freeBuildingReward;
    public string outcomeMessage;
}

[System.Serializable]
public class WastelandEvent
{
    public string title;
    public string description;
    public EventChoice[] choices;
}

public class EventDilemmaSystem : MonoBehaviour
{
    public static EventDilemmaSystem Instance { get; private set; }

    public WastelandEvent currentEvent;
    public string lastOutcomeMessage = "";
    public bool isShowingOutcome = false;

    private List<WastelandEvent> allEvents = new List<WastelandEvent>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitializeEvents();
    }

    private void InitializeEvents()
    {
        allEvents = new List<WastelandEvent>
        {
            new WastelandEvent
            {
                title = "🚚 DERELICT MILITARY CONVOY",
                description = "Your scouts discovered an abandoned military convoy half-buried in radioactive dunes. The armored cargo containers appear intact, but rust makes opening them difficult.",
                choices = new EventChoice[]
                {
                    new EventChoice
                    {
                        buttonText = "Salvage Military Tech (-15 Gold)",
                        description = "Spend gold for torches & cutting tools to breach the tech crates.",
                        goldCost = 15,
                        materialCost = 0,
                        specialRewardMin = 2,
                        specialRewardMax = 3,
                        goldReward = 0,
                        materialReward = 5,
                        outcomeMessage = "Success! You extracted pristine combat electronics and 2-3 Exclusive Tech Parts!"
                    },
                    new EventChoice
                    {
                        buttonText = "Strip Armored Plates (Free)",
                        description = "Dismantle exterior steel plating for fortress fortifications.",
                        goldCost = 0,
                        materialCost = 0,
                        specialRewardMin = 0,
                        specialRewardMax = 0,
                        goldReward = 0,
                        materialReward = 28,
                        outcomeMessage = "The crew hauled back 28 units of heavy structural materials!"
                    },
                    new EventChoice
                    {
                        buttonText = "Secure Perimeter (+10 Gold)",
                        description = "Scout safely and mark the hazard zone on the wasteland map.",
                        goldCost = 0,
                        materialCost = 0,
                        specialRewardMin = 0,
                        specialRewardMax = 0,
                        goldReward = 10,
                        materialReward = 0,
                        outcomeMessage = "Area secured with no risks taken. Earned +10 Gold bounty."
                    }
                }
            },
            new WastelandEvent
            {
                title = "📡 DISTRESS SIGNAL FROM BUNKER B-7",
                description = "A faint automated radio transmission loops: 'Bunker B-7 life support failing... survivors sealed inside with military reserves...'.",
                choices = new EventChoice[]
                {
                    new EventChoice
                    {
                        buttonText = "Breach & Rescue (Free)",
                        description = "Send a rapid strike team to rescue the trapped engineers.",
                        goldCost = 0,
                        materialCost = 0,
                        specialRewardMin = 2,
                        specialRewardMax = 2,
                        goldReward = 0,
                        materialReward = 10,
                        freeBuildingReward = Building.BuildingType.SMGTent,
                        outcomeMessage = "The rescued engineer squad brought complete blueprints and joined your base! (1 Free SMG Tent added to Stock + 2 Tech Parts!)"
                    },
                    new EventChoice
                    {
                        buttonText = "Download Blueprints (-10 Mats)",
                        description = "Tap into the bunker antenna and splice data cables.",
                        goldCost = 0,
                        materialCost = 10,
                        specialRewardMin = 1,
                        specialRewardMax = 2,
                        goldReward = 12,
                        materialReward = 0,
                        outcomeMessage = "Recovered valuable classified schematics and 1-2 Tech Parts!"
                    },
                    new EventChoice
                    {
                        buttonText = "Ignore Signal (Safe)",
                        description = "Conserve fuel and resources. Avoid potential trap.",
                        goldCost = 0,
                        materialCost = 0,
                        specialRewardMin = 0,
                        specialRewardMax = 0,
                        goldReward = 5,
                        materialReward = 0,
                        outcomeMessage = "Kept soldiers rested and salvaged minor scrap nearby (+5 Gold)."
                    }
                }
            },
            new WastelandEvent
            {
                title = "🛒 WANDERING TECH SCAVENGER",
                description = "A heavily armored nomad caravan hoists a white flag, offering rare military electronics salvaged from the fallen capitol.",
                choices = new EventChoice[]
                {
                    new EventChoice
                    {
                        buttonText = "Buy Tech Parts (-35 Gold)",
                        description = "Trade currency for 3 guaranteed Exclusive Tech parts.",
                        goldCost = 35,
                        materialCost = 0,
                        specialRewardMin = 3,
                        specialRewardMax = 3,
                        goldReward = 0,
                        materialReward = 0,
                        outcomeMessage = "The nomad nodded in respect and delivered 3 pristine Exclusive Tech parts!"
                    },
                    new EventChoice
                    {
                        buttonText = "Barter Materials (-25 Mats)",
                        description = "Trade building lumber & scrap for gold and tech.",
                        goldCost = 0,
                        materialCost = 25,
                        specialRewardMin = 1,
                        specialRewardMax = 1,
                        goldReward = 20,
                        materialReward = 0,
                        outcomeMessage = "Barter completed! Received 1 Tech part and 20 Gold."
                    },
                    new EventChoice
                    {
                        buttonText = "Decline Trade (Free)",
                        description = "Politely bid farewell to the convoy.",
                        goldCost = 0,
                        materialCost = 0,
                        specialRewardMin = 0,
                        specialRewardMax = 0,
                        goldReward = 5,
                        materialReward = 0,
                        outcomeMessage = "The merchant shared news of safe routes ahead (+5 Gold)."
                    }
                }
            },
            new WastelandEvent
            {
                title = "☣️ BIOHAZARD RESEARCH OUTPOST",
                description = "An underground quarantine lab was breached. High-energy centrifuge cores are still humming behind shattered blast glass.",
                choices = new EventChoice[]
                {
                    new EventChoice
                    {
                        buttonText = "Extract Centrifuges (-15 Mats)",
                        description = "Use construction beams to support the ceiling and haul the cores.",
                        goldCost = 0,
                        materialCost = 15,
                        specialRewardMin = 2,
                        specialRewardMax = 4,
                        goldReward = 0,
                        materialReward = 0,
                        outcomeMessage = "Incredible find! Secured 2-4 rare Exclusive Tech power cells!"
                    },
                    new EventChoice
                    {
                        buttonText = "Strip Lab Machinery (Free)",
                        description = "Carefully take external copper wiring and steel framing.",
                        goldCost = 0,
                        materialCost = 0,
                        specialRewardMin = 0,
                        specialRewardMax = 0,
                        goldReward = 10,
                        materialReward = 20,
                        outcomeMessage = "Recovered 20 Building Materials and 10 Gold from the laboratory."
                    },
                    new EventChoice
                    {
                        buttonText = "Purge with Incendiary (-10 Gold)",
                        description = "Sterilize the biohazard site to protect the fortress water table.",
                        goldCost = 10,
                        materialCost = 0,
                        specialRewardMin = 1,
                        specialRewardMax = 1,
                        goldReward = 0,
                        materialReward = 15,
                        outcomeMessage = "Hazard eliminated safely. Salvaged 1 Tech part from heat shields!"
                    }
                }
            },
            new WastelandEvent
            {
                title = "🎯 VETERAN SNIPER ENCLAVE",
                description = "A squad of retired marksmen guarding a water tower offers their heavy ballistics equipment to your defense coalition.",
                choices = new EventChoice[]
                {
                    new EventChoice
                    {
                        buttonText = "Recruit Sniper Squad (-40 Gold)",
                        description = "Sign contracts and acquire an elite Sniper's Nest directly.",
                        goldCost = 40,
                        materialCost = 0,
                        specialRewardMin = 1,
                        specialRewardMax = 1,
                        goldReward = 0,
                        materialReward = 0,
                        freeBuildingReward = Building.BuildingType.SniperTower,
                        outcomeMessage = "The snipers assembled their gear! 1 FREE Sniper's Nest added to inventory stock + 1 Tech Part!"
                    },
                    new EventChoice
                    {
                        buttonText = "Buy Munitions Surplus (-15 Gold)",
                        description = "Purchase their excess ammunition crates and powder.",
                        goldCost = 15,
                        materialCost = 0,
                        specialRewardMin = 0,
                        specialRewardMax = 0,
                        goldReward = 0,
                        materialReward = 25,
                        outcomeMessage = "Supplied the garrison with 25 defensive building materials."
                    },
                    new EventChoice
                    {
                        buttonText = "Exchange Intel (Free)",
                        description = "Share tactical coordinates of the infected horde.",
                        goldCost = 0,
                        materialCost = 0,
                        specialRewardMin = 0,
                        specialRewardMax = 0,
                        goldReward = 12,
                        materialReward = 0,
                        outcomeMessage = "The snipers rewarded your warning with 12 Gold."
                    }
                }
            }
        };
    }

    public void TriggerRandomEvent()
    {
        if (allEvents == null || allEvents.Count == 0) InitializeEvents();

        int idx = UnityEngine.Random.Range(0, allEvents.Count);
        currentEvent = allEvents[idx];
        isShowingOutcome = false;
        lastOutcomeMessage = "";
        AudioHelper.PlaySound(SoundEffect.EventAlert, 0.8f);
    }

    public bool ExecuteChoice(int choiceIndex)
    {
        if (currentEvent == null || currentEvent.choices == null || choiceIndex < 0 || choiceIndex >= currentEvent.choices.Length)
        {
            return false;
        }

        EventChoice choice = currentEvent.choices[choiceIndex];

        // Resource check
        if (ResourceManager.Instance != null)
        {
            if (ResourceManager.Instance.Gold < choice.goldCost)
            {
                Debug.LogWarning("[Event] Not enough Gold for this choice!");
                return false;
            }
            if (ResourceManager.Instance.Materials < choice.materialCost)
            {
                Debug.LogWarning("[Event] Not enough Materials for this choice!");
                return false;
            }

            // Deduct
            if (choice.goldCost > 0) ResourceManager.Instance.SpendGold(choice.goldCost);
            if (choice.materialCost > 0) ResourceManager.Instance.SpendMaterials(choice.materialCost);

            // Reward
            int specialGain = UnityEngine.Random.Range(choice.specialRewardMin, choice.specialRewardMax + 1);
            if (specialGain > 0)
            {
                for (int i = 0; i < specialGain; i++)
                {
                    ResourceManager.Instance.AddSpecialReward();
                }
            }

            if (choice.goldReward > 0)
            {
                ResourceManager.Instance.SpendGold(-choice.goldReward);
            }
            if (choice.materialReward > 0)
            {
                ResourceManager.Instance.SpendMaterials(-choice.materialReward);
            }

            // Free building reward
            if (choice.freeBuildingReward.HasValue && PlacementManager.Instance != null)
            {
                PlacementManager.Instance.AddStoredCount(choice.freeBuildingReward.Value, 1);
            }
        }

        lastOutcomeMessage = choice.outcomeMessage;
        isShowingOutcome = true;
        AudioHelper.PlaySound(SoundEffect.ResourceGain, 0.8f);
        return true;
    }
}
